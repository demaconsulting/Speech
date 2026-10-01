namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Honest fallback <see cref="ISynthesisSession"/> used when no real synthesis engine could be
///     loaded, reporting <see cref="IsAvailable"/> as <see langword="false"/> rather than letting
///     a caller build against a session that cannot function.
/// </summary>
/// <remarks>
///     The type is stateless and holds no resources, so the shared <see cref="Instance"/> is safe
///     for concurrent use by any number of callers, and <see cref="DisposeAsync"/> is a no-op that
///     never invalidates it. This mirrors <see cref="UnavailableSpeechSynthesizerEngine"/> and
///     <c>RecognitionSubsystem.UnavailableRecognitionSession</c>.
/// </remarks>
public sealed class UnavailableSynthesisSession : ISynthesisSession
{
    /// <summary>
    ///     Prevents external construction; callers use the shared <see cref="Instance"/> instead
    ///     since the type carries no state and multiple instances would provide no value.
    /// </summary>
    private UnavailableSynthesisSession()
    {
    }

    /// <summary>Gets the single shared unavailable synthesis session.</summary>
    public static UnavailableSynthesisSession Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    /// <remarks>
    ///     Always <see cref="SynthesisSessionState.Created"/>: this session never performs an
    ///     operation and so never transitions, and is never itself disposed away (the shared
    ///     <see cref="Instance"/> remains reusable across many callers).
    /// </remarks>
    public SynthesisSessionState State => SynthesisSessionState.Created;

    /// <inheritdoc/>
    /// <remarks>Never raised: this session never transitions state.</remarks>
#pragma warning disable S108 // Intentionally empty: this session never transitions state, so no handler is ever invoked and none needs to be retained.
    public event EventHandler<SessionStateChangedEventArgs>? StateChanged
    {
        add { }
        remove { }
    }
#pragma warning restore S108

    /// <inheritdoc/>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Always thrown; this session has no real engine or playback device to speak with.
    /// </exception>
    public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        throw new SpeechSynthesizerUnavailableException(
            "Cannot speak: no synthesis session is available.");
    }

    /// <inheritdoc/>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Always thrown; this session has no real engine to synthesize with.
    /// </exception>
    public Task<IReadOnlyList<SynthesizedSpeech>> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        throw new SpeechSynthesizerUnavailableException(
            "Cannot synthesize speech: no synthesis session is available.");
    }

    /// <inheritdoc/>
    /// <remarks>A no-op: this session has no in-flight operation to stop.</remarks>
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    /// <remarks>
    ///     A no-op: this session owns no engine, thread, or native resource. Disposal must not
    ///     throw or invalidate <see cref="Instance"/>.
    /// </remarks>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
