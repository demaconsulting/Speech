namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Honest fallback <see cref="ISpeechRecognizer"/> used when no real recognition engine can
///     be composed, reporting <see cref="IsAvailable"/> as <see langword="false"/> rather than
///     letting a caller build against a recognizer that cannot function.
/// </summary>
/// <remarks>
///     Per architecture.md's "nothing throws at composition" decision, obtaining and holding this
///     instance never throws: a missing model, an absent native runtime, and a machine with no
///     microphone are ordinary machine states at application start-up, not programming errors.
///     Only the operational members (<see cref="Start"/>, <see cref="Stop"/>) throw
///     <see cref="SpeechRecognizerUnavailableException"/>, and only when actually invoked - a
///     caller that checks <see cref="IsAvailable"/> first, as documented, never triggers them.
///     The type is stateless and holds no resources, so the shared <see cref="Instance"/> is safe
///     for concurrent use by any number of callers and <see cref="Dispose"/> is a no-op that
///     never invalidates it. This mirrors <c>UnavailableAudioCaptureDevice</c> exactly.
/// </remarks>
public sealed class UnavailableSpeechRecognizer : ISpeechRecognizer
{
    /// <summary>
    ///     Prevents external construction; callers use the shared <see cref="Instance"/> instead
    ///     since the type carries no state and multiple instances would provide no value.
    /// </summary>
    private UnavailableSpeechRecognizer()
    {
    }

    /// <summary>
    ///     Gets the single shared unavailable speech recognizer.
    /// </summary>
    public static UnavailableSpeechRecognizer Instance { get; } = new();

    /// <summary>
    ///     Backing field for <see cref="ResultReceived"/>. Never invoked, because this recognizer
    ///     can never enter a running state; kept only so subscribe/unsubscribe are safe no-ops
    ///     rather than throwing.
    /// </summary>
    private EventHandler<SpeechRecognitionEvent>? _resultReceived;

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    /// <remarks>
    ///     Never raised, since this recognizer can never enter a running state; subscribing and
    ///     unsubscribing are still safe no-ops.
    /// </remarks>
    public event EventHandler<SpeechRecognitionEvent>? ResultReceived
    {
        add => _resultReceived += value;
        remove => _resultReceived -= value;
    }

    /// <inheritdoc/>
    /// <exception cref="SpeechRecognizerUnavailableException">
    ///     Always thrown; this recognizer has no real engine or capture device to start.
    /// </exception>
    public void Start()
    {
        throw new SpeechRecognizerUnavailableException(
            "Cannot start recognition: no speech recognizer is available.");
    }

    /// <inheritdoc/>
    /// <exception cref="SpeechRecognizerUnavailableException">
    ///     Always thrown; this recognizer has no real engine or capture device to stop.
    /// </exception>
    public void Stop()
    {
        throw new SpeechRecognizerUnavailableException(
            "Cannot stop recognition: no speech recognizer is available.");
    }

    /// <summary>Releases resources held by this recognizer.</summary>
    /// <remarks>
    ///     A no-op: this recognizer owns no engine, thread, or native resource. Disposal must not
    ///     throw or invalidate <see cref="Instance"/>, because a host that wraps its recognizer in
    ///     a <c>using</c> block gets the shared instance here and may dispose it many times.
    /// </remarks>
    public void Dispose()
    {
        // Intentionally empty - see the remarks above.
    }
}
