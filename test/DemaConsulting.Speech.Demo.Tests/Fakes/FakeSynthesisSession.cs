using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.Fakes;

/// <summary>
///     Hand-written test double for <see cref="ISynthesisSession"/>, used instead of a mocking
///     library so a test can fully control the lifecycle transitions a real session would raise
///     around <see cref="SpeakAsync"/>, including cooperative cancellation driven by
///     <see cref="StopAsync"/> rather than only the caller's own token.
/// </summary>
public sealed class FakeSynthesisSession : ISynthesisSession
{
    /// <summary>The source canceled by <see cref="StopAsync"/> while a <see cref="SpeakAsync"/> call is in flight.</summary>
    private CancellationTokenSource? _speakCts;

    /// <inheritdoc/>
    public bool IsAvailable { get; set; } = true;

    /// <inheritdoc/>
    public SynthesisSessionState State { get; private set; } = SynthesisSessionState.Created;

    /// <inheritdoc/>
    public event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    /// <summary>Gets the texts passed to every <see cref="SpeakAsync"/> call, in order.</summary>
    public List<string> SpeakTexts { get; } = [];

    /// <summary>Gets the number of times <see cref="SpeakAsync"/> was called.</summary>
    public int SpeakCallCount => SpeakTexts.Count;

    /// <summary>Gets the number of times <see cref="StopAsync"/> was called.</summary>
    public int StopCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="DisposeAsync"/> was called.</summary>
    public int DisposeCallCount { get; private set; }

    /// <summary>
    ///     Gets or sets the exception <see cref="SpeakAsync"/> throws, if any, after its normal
    ///     Starting/Running transitions. A <see cref="SynthesisSessionFaultedException"/> also
    ///     raises <see cref="StateChanged"/> to <see cref="SynthesisSessionState.Faulted"/>
    ///     first, exactly like a real session reporting an unrecoverable fault.
    /// </summary>
    public Exception? SpeakException { get; set; }

    /// <summary>
    ///     Gets or sets the in-flight body <see cref="SpeakAsync"/> awaits after its Running
    ///     transition; defaults to an immediately-completing no-op. Set to a delegate awaiting
    ///     <see cref="Timeout.Infinite"/> on its token to simulate an indefinitely long utterance
    ///     that only unwinds when <see cref="StopAsync"/> cancels it.
    /// </summary>
    public Func<string, CancellationToken, Task>? SpeakImplementation { get; set; }

    /// <summary>
    ///     Gets or sets a callback invoked at the start of <see cref="StopAsync"/>, letting a
    ///     test observe exactly when a Stop was requested.
    /// </summary>
    public Action? OnStopRequested { get; set; }

    /// <summary>
    ///     Raises <see cref="StateChanged"/>, moving <see cref="State"/> to <paramref name="current"/>.
    /// </summary>
    /// <param name="current">The state to transition to.</param>
    public void RaiseStateChanged(SynthesisSessionState current)
    {
        var previous = State;
        State = current;
        StateChanged?.Invoke(this, new SessionStateChangedEventArgs(previous, current));
    }

    /// <inheritdoc/>
    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        SpeakTexts.Add(text);

        if (SpeakException is not null)
        {
            if (SpeakException is SynthesisSessionFaultedException)
            {
                RaiseStateChanged(SynthesisSessionState.Faulted);
            }

            throw SpeakException;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _speakCts = cts;

        RaiseStateChanged(SynthesisSessionState.Starting);
        RaiseStateChanged(SynthesisSessionState.Running);

        try
        {
            if (SpeakImplementation is not null)
            {
                await SpeakImplementation(text, cts.Token).ConfigureAwait(false);
            }
            else
            {
                await Task.Yield();
            }
        }
        catch (OperationCanceledException)
        {
            RaiseStateChanged(SynthesisSessionState.Stopping);
            RaiseStateChanged(SynthesisSessionState.Stopped);
            _speakCts = null;
            throw;
        }

        _speakCts = null;
        RaiseStateChanged(SynthesisSessionState.Stopped);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SynthesizedSpeech>> SynthesizeAsync(string text, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by SynthesisPanelViewModel, which only calls SpeakAsync.");

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopCallCount++;
        OnStopRequested?.Invoke();
        _speakCts?.Cancel();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        return ValueTask.CompletedTask;
    }
}
