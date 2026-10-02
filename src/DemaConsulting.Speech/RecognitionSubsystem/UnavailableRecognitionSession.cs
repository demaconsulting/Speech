namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Honest fallback <see cref="IRecognitionSession"/> used when no real recognition session
///     can be composed, reporting <see cref="IsAvailable"/> as <see langword="false"/> rather
///     than letting a caller operate a session that cannot function.
/// </summary>
/// <remarks>
///     Per this library's "nothing throws at composition" decision, only the operational members
///     (<see cref="StartAsync"/>, <see cref="GetResultsAsync"/>) throw
///     <see cref="SpeechRecognizerUnavailableException"/>, and only when actually invoked -
///     <see cref="StopAsync"/> and <see cref="DisposeAsync"/> are safe no-ops. The type is
///     stateless and holds no resources, so the shared <see cref="Instance"/> is safe for
///     concurrent use by any number of callers. This mirrors <see cref="UnavailableSpeechRecognizerEngine"/>
///     and the AudioSubsystem's <c>UnavailableAudioCaptureDevice</c> exactly.
/// </remarks>
public sealed class UnavailableRecognitionSession : IRecognitionSession
{
    /// <summary>
    ///     Prevents external construction; callers use the shared <see cref="Instance"/> instead
    ///     since the type carries no state and multiple instances would provide no value.
    /// </summary>
    private UnavailableRecognitionSession()
    {
    }

    /// <summary>Gets the single shared unavailable recognition session.</summary>
    public static UnavailableRecognitionSession Instance { get; } = new();

    /// <summary>
    ///     Backing field for <see cref="StateChanged"/>. Never invoked, because this session can
    ///     never enter a running state; kept only so subscribe/unsubscribe are safe no-ops
    ///     rather than throwing.
    /// </summary>
    private EventHandler<SessionStateChangedEventArgs>? _stateChanged;

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    /// <remarks>Always <see cref="RecognitionSessionState.Created"/>; this session never runs.</remarks>
    public RecognitionSessionState State => RecognitionSessionState.Created;

    /// <inheritdoc/>
    /// <remarks>Never raised, since this session can never enter a running state.</remarks>
    public event EventHandler<SessionStateChangedEventArgs>? StateChanged
    {
        add => _stateChanged += value;
        remove => _stateChanged -= value;
    }

    /// <inheritdoc/>
    /// <exception cref="SpeechRecognizerUnavailableException">
    ///     Always thrown; this session has no real engine or capture device to start.
    /// </exception>
    public Task StartAsync(CancellationToken cancellationToken = default) =>
        throw new SpeechRecognizerUnavailableException(
            "Cannot start recognition: no speech recognition session is available.");

    /// <inheritdoc/>
    /// <remarks>A safe no-op: this session was never running.</remarks>
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    /// <exception cref="SpeechRecognizerUnavailableException">
    ///     Always thrown; this session has no real engine or capture device to stream results
    ///     from.
    /// </exception>
    public IAsyncEnumerable<SpeechRecognitionEvent> GetResultsAsync(CancellationToken cancellationToken = default) =>
        throw new SpeechRecognizerUnavailableException(
            "Cannot stream recognition results: no speech recognition session is available.");

    /// <summary>Releases resources held by this session.</summary>
    /// <remarks>
    ///     A no-op: this session owns no engine, thread, or native resource. Disposal must not
    ///     throw or invalidate <see cref="Instance"/>.
    /// </remarks>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
