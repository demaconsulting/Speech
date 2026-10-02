using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Internal utility that runs a delegate on a dedicated, long-running background thread,
///     applying a cooperative-cancel-then-abandon policy for native calls that do not honor
///     cancellation promptly.
/// </summary>
/// <remarks>
///     Dedicated per subsystem (this is the synthesis subsystem's own copy; the recognition
///     subsystem carries an independently unit-tested, identically-shaped copy) rather than
///     factored into a new shared internal subsystem, since introducing a third subsystem (with
///     its own full companion-artifact set) for one ~100-line utility would be disproportionate.
///     <para>
///     Every call runs on its own <see cref="TaskCreationOptions.LongRunning"/> task (a dedicated
///     OS thread, not a pooled thread-pool thread) so a blocking native call never starves the
///     thread pool. On cancellation, the worker is given a
///     bounded grace period (the abandon timeout, injectable for testability,
///     defaulting to <see cref="DefaultAbandonTimeout"/>) to stop cooperatively; if it has not
///     finished by then, the returned task completes as cancelled, a
///     <see cref="SpeechDiagnosticLevel.Warning"/> is reported, and the worker thread is detached
///     (left to finish in the background; its eventual result or exception is discarded). An
///     async signature alone cannot make a blocking P/Invoke call interruptible, so this is a
///     bounded, honest compromise rather than a true cancellation guarantee.
///     </para>
/// </remarks>
internal static class DedicatedWorker
{
    /// <summary>
    ///     The default grace period given to a worker to stop cooperatively after cancellation is
    ///     requested, before it is abandoned. See the type-level remarks for the full policy.
    /// </summary>
    public static readonly TimeSpan DefaultAbandonTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     Runs <paramref name="work"/> on a dedicated long-running thread, applying the
    ///     cooperative-cancel-then-abandon policy.
    /// </summary>
    /// <typeparam name="T">The type of result <paramref name="work"/> produces.</typeparam>
    /// <param name="work">
    ///     The delegate to run, receiving a token it should check cooperatively to stop early.
    ///     Must not be <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">A token requesting cooperative cancellation of <paramref name="work"/>.</param>
    /// <param name="diagnostics">The sink to report an abandonment warning to, or <see langword="null"/> for the null sink.</param>
    /// <param name="diagnosticsCategory">The diagnostics category to report an abandonment warning under.</param>
    /// <param name="abandonTimeout">
    ///     The grace period to wait for cooperative completion after cancellation before
    ///     abandoning the worker, or <see langword="null"/> to use <see cref="DefaultAbandonTimeout"/>.
    ///     Exposed for deterministic testing.
    /// </param>
    /// <returns>The result of <paramref name="work"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="work"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">
    ///     Thrown when <paramref name="cancellationToken"/> is cancelled and <paramref name="work"/>
    ///     either observes it and stops, or does not stop within <paramref name="abandonTimeout"/>
    ///     and is abandoned.
    /// </exception>
    public static async Task<T> Run<T>(
        Func<CancellationToken, T> work,
        CancellationToken cancellationToken,
        ISpeechDiagnostics? diagnostics,
        string diagnosticsCategory,
        TimeSpan? abandonTimeout = null) =>
        await Start(work, cancellationToken, diagnostics, diagnosticsCategory, abandonTimeout).Task.ConfigureAwait(false);

    /// <summary>
    ///     Runs <paramref name="work"/> on a dedicated long-running thread, applying the
    ///     cooperative-cancel-then-abandon policy, and additionally exposes the dedicated
    ///     thread's own raw completion alongside the abandon-aware task <see cref="Run{T}"/> also
    ///     returns.
    /// </summary>
    /// <typeparam name="T">The type of result <paramref name="work"/> produces.</typeparam>
    /// <param name="work">
    ///     The delegate to run, receiving a token it should check cooperatively to stop early.
    ///     Must not be <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">A token requesting cooperative cancellation of <paramref name="work"/>.</param>
    /// <param name="diagnostics">The sink to report an abandonment warning to, or <see langword="null"/> for the null sink.</param>
    /// <param name="diagnosticsCategory">The diagnostics category to report an abandonment warning under.</param>
    /// <param name="abandonTimeout">
    ///     The grace period to wait for cooperative completion after cancellation before
    ///     abandoning the worker, or <see langword="null"/> to use <see cref="DefaultAbandonTimeout"/>.
    ///     Exposed for deterministic testing.
    /// </param>
    /// <returns>
    ///     The abandon-aware task (see <see cref="DedicatedWorkerRun{T}.Task"/>) together with the
    ///     dedicated thread's own completion (see <see cref="DedicatedWorkerRun{T}.Completion"/>),
    ///     which a caller that must not reuse or dispose a resource <paramref name="work"/>
    ///     shares with another session - even an abandoned one - should await instead of (or as
    ///     well as) the abandon-aware task.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="work"/> is <see langword="null"/>.</exception>
    internal static DedicatedWorkerRun<T> Start<T>(
        Func<CancellationToken, T> work,
        CancellationToken cancellationToken,
        ISpeechDiagnostics? diagnostics,
        string diagnosticsCategory,
        TimeSpan? abandonTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(work);

        var workerTask = Task.Factory.StartNew(
            () => work(cancellationToken),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        return new DedicatedWorkerRun<T>(workerTask, AwaitWithAbandonAsync(workerTask, cancellationToken, diagnostics, diagnosticsCategory, abandonTimeout));
    }

    /// <summary>
    ///     Applies the cooperative-cancel-then-abandon policy to <paramref name="workerTask"/>,
    ///     the shared implementation behind both <see cref="Run{T}"/> and <see cref="Start{T}"/>.
    /// </summary>
    private static async Task<T> AwaitWithAbandonAsync<T>(
        Task<T> workerTask,
        CancellationToken cancellationToken,
        ISpeechDiagnostics? diagnostics,
        string diagnosticsCategory,
        TimeSpan? abandonTimeout)
    {
        var sink = diagnostics ?? NullSpeechDiagnostics.Instance;
        var timeout = abandonTimeout ?? DefaultAbandonTimeout;

        if (!cancellationToken.CanBeCanceled)
        {
            return await workerTask.ConfigureAwait(false);
        }

        var cancelSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelSignal);

        var firstCompleted = await Task.WhenAny(workerTask, cancelSignal.Task).ConfigureAwait(false);
        if (firstCompleted == workerTask)
        {
            return await workerTask.ConfigureAwait(false);
        }

        // Cancellation was requested: give the worker a bounded grace period to stop cooperatively.
        var abandonDelay = Task.Delay(timeout, CancellationToken.None);
        var raceResult = await Task.WhenAny(workerTask, abandonDelay).ConfigureAwait(false);
        if (raceResult == workerTask)
        {
            return await workerTask.ConfigureAwait(false);
        }

        // Abandoned: the worker did not finish cooperatively within the grace period.
        sink.Report(
            SpeechDiagnosticLevel.Warning,
            diagnosticsCategory,
            $"A dedicated worker did not stop cooperatively within {timeout.TotalSeconds:F1}s of cancellation and was abandoned; its eventual result will be discarded.");

        // Observe the worker's eventual completion (success or fault) so it never becomes an
        // unobserved task exception, without ever awaiting it here.
        _ = workerTask.ContinueWith(static t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        throw new OperationCanceledException(cancellationToken);
    }
}

/// <summary>
///     The pair of tasks returned by <see cref="DedicatedWorker.Start{T}"/>: the abandon-aware
///     task ordinary callers await, and the dedicated thread's own raw completion that a caller
///     protecting a shared resource from an abandoned worker must await before touching that
///     resource again.
/// </summary>
/// <typeparam name="T">The type of result the dedicated work produces.</typeparam>
/// <param name="Completion">
///     The dedicated thread's own task, which completes only once the work genuinely returns -
///     even if <see cref="Task"/> itself already completed early as abandoned.
/// </param>
/// <param name="Task">
///     The abandon-aware task with the same semantics as <see cref="DedicatedWorker.Run{T}"/>'s
///     return value.
/// </param>
internal readonly record struct DedicatedWorkerRun<T>(Task<T> Completion, Task<T> Task);
