using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Internal utility that runs a delegate on a dedicated, non-pooled thread and applies a
///     cooperative-cancel-then-abandon policy when the caller requests cancellation.
/// </summary>
/// <remarks>
///     An async signature does not make a blocking native call interruptible, so this utility
///     does not pretend otherwise: when <see cref="RunAsync(Action{CancellationToken}, CancellationToken)"/>'s <c>cancellationToken</c> is
///     cancelled, the delegate - which is expected to observe that same token cooperatively at
///     its own natural boundaries - is given <see cref="AbandonTimeout"/> to finish. If it does,
///     the returned task completes normally (or with whatever the delegate itself produced). If
///     it does not, the returned task completes as cancelled, the delegate's thread is detached
///     to finish in the background with its eventual result discarded, and the condition is
///     reported through the supplied <see cref="ISpeechDiagnostics"/> sink at
///     <see cref="SpeechDiagnosticLevel.Warning"/>. See Decision #4 in the engine/session async
///     redesign planning report for the full rationale, including why callers that abandon as
///     part of their own explicit stop/dispose treat that outcome as best-effort completion
///     rather than a fault.
///     <para>
///     The delegate always runs via <see cref="TaskCreationOptions.LongRunning"/>, which asks the
///     scheduler for a dedicated thread rather than a pooled one - appropriate here because the
///     delegate is expected to block for the life of a recognition session's pump loop, not
///     return quickly like ordinary thread-pool work.
///     </para>
/// </remarks>
internal sealed class DedicatedWorker
{
    /// <summary>The default abandon timeout used in production: 2 seconds.</summary>
    internal static readonly TimeSpan DefaultAbandonTimeout = TimeSpan.FromSeconds(2);

    /// <summary>The diagnostics sink this worker reports abandonment to.</summary>
    private readonly ISpeechDiagnostics _diagnostics;

    /// <summary>The diagnostics category used for every event this worker reports.</summary>
    private readonly string _diagnosticsCategory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DedicatedWorker"/> class.
    /// </summary>
    /// <param name="abandonTimeout">
    ///     The duration to wait for the delegate to honor a cancellation request before
    ///     abandoning it, or <see langword="null"/> to use <see cref="DefaultAbandonTimeout"/>.
    ///     Tests inject a near-zero value to keep abandonment tests fast.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report abandonment to, or <see langword="null"/> to use
    ///     <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <param name="diagnosticsCategory">The diagnostics category used for reported events.</param>
    internal DedicatedWorker(
        TimeSpan? abandonTimeout = null,
        ISpeechDiagnostics? diagnostics = null,
        string diagnosticsCategory = "RecognitionSubsystem")
    {
        AbandonTimeout = abandonTimeout ?? DefaultAbandonTimeout;
        _diagnostics = diagnostics ?? NullSpeechDiagnostics.Instance;
        _diagnosticsCategory = diagnosticsCategory;
    }

    /// <summary>
    ///     Gets the duration this worker waits for a cancelled delegate to finish before
    ///     abandoning it.
    /// </summary>
    internal TimeSpan AbandonTimeout { get; }

    /// <summary>
    ///     Runs <paramref name="action"/> to completion on a dedicated, long-running thread.
    /// </summary>
    /// <param name="action">
    ///     The delegate to run, given <paramref name="cancellationToken"/> so it can observe
    ///     cancellation cooperatively at its own natural boundaries. Must not be null.
    /// </param>
    /// <param name="cancellationToken">
    ///     A token whose cancellation requests the delegate stop. The delegate is given
    ///     <see cref="AbandonTimeout"/> to honor the request before being abandoned.
    /// </param>
    /// <returns>
    ///     A task that completes once the delegate finishes, or - if the delegate does not honor
    ///     a cancellation request within <see cref="AbandonTimeout"/> - completes as cancelled
    ///     while the delegate is abandoned to finish in the background.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    internal Task RunAsync(Action<CancellationToken> action, CancellationToken cancellationToken = default) =>
        RunAsync(action, cancellationToken, out _);

    /// <summary>
    ///     Runs <paramref name="action"/> to completion on a dedicated, long-running thread,
    ///     additionally exposing the raw, non-abandon-aware completion of that thread.
    /// </summary>
    /// <param name="action">
    ///     The delegate to run, given <paramref name="cancellationToken"/> so it can observe
    ///     cancellation cooperatively at its own natural boundaries. Must not be null.
    /// </param>
    /// <param name="cancellationToken">
    ///     A token whose cancellation requests the delegate stop. The delegate is given
    ///     <see cref="AbandonTimeout"/> to honor the request before being abandoned.
    /// </param>
    /// <param name="completion">
    ///     Set to the dedicated thread's own task, which completes only once
    ///     <paramref name="action"/> genuinely returns - even if it is abandoned. A caller that
    ///     must not touch a resource <paramref name="action"/> shares with another session (for
    ///     example, a "hot" backend reused across sessions) until that thread has truly exited -
    ///     regardless of whether the returned task completed early as abandoned - should await
    ///     this instead of (or as well as) the returned task.
    /// </param>
    /// <returns>
    ///     A task that completes once the delegate finishes, or - if the delegate does not honor
    ///     a cancellation request within <see cref="AbandonTimeout"/> - completes as cancelled
    ///     while the delegate is abandoned to finish in the background.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    internal Task RunAsync(Action<CancellationToken> action, CancellationToken cancellationToken, out Task completion)
    {
        ArgumentNullException.ThrowIfNull(action);

        var worker = Task.Factory.StartNew(
            () => action(cancellationToken),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        completion = worker;
        return AwaitWithAbandonAsync(worker, cancellationToken);
    }

    /// <summary>
    ///     Awaits <paramref name="worker"/>, applying the cooperative-cancel-then-abandon policy
    ///     once <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    private async Task AwaitWithAbandonAsync(Task worker, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            await worker.ConfigureAwait(false);
            return;
        }

        try
        {
            await worker.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller asked the delegate to stop; give it AbandonTimeout to actually do so
            // before giving up on waiting for it any further.
            var abandonDelay = Task.Delay(AbandonTimeout, CancellationToken.None);
            var finished = await Task.WhenAny(worker, abandonDelay).ConfigureAwait(false);
            if (finished != worker)
            {
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Warning,
                    _diagnosticsCategory,
                    "A dedicated worker did not honor a cooperative cancellation request within " +
                    $"the {AbandonTimeout.TotalSeconds:F1}s abandon timeout; it has been abandoned " +
                    "to finish in the background and its eventual result will be discarded.");

                // Observe and discard whatever the abandoned delegate eventually produces, so an
                // unobserved-task-exception does not later surface unrelated to this call.
                _ = worker.ContinueWith(
                    static completed => _ = completed.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
                throw;
            }

            // The delegate genuinely finished within the abandon bound; propagate whatever it
            // produced (including rethrowing a fault) rather than the cancellation.
            await worker.ConfigureAwait(false);
        }
    }
}
