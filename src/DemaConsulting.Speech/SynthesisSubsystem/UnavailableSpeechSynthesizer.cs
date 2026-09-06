namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Honest fallback <see cref="ISpeechSynthesizer"/> used when no real synthesis engine can be
///     composed, reporting <see cref="IsAvailable"/> as <see langword="false"/> rather than
///     letting a caller build against a synthesizer that cannot function.
/// </summary>
/// <remarks>
///     Per architecture.md's "nothing throws at composition" decision, obtaining and holding this
///     instance never throws: a missing model, an absent native runtime, and a machine with no
///     speakers are ordinary machine states at application start-up, not programming errors.
///     Only the operational members (<see cref="SynthesizeStreamAsync"/>,
///     <see cref="PlayStreamAsync"/>, <see cref="SpeakAsync"/>, <see cref="Stop"/>) throw
///     <see cref="SpeechSynthesizerUnavailableException"/>, and only when actually invoked - a
///     caller that checks <see cref="IsAvailable"/> first, as documented, never triggers them.
///     The type is stateless and holds no resources, so the shared <see cref="Instance"/> is safe
///     for concurrent use by any number of callers and <see cref="Dispose"/> is a no-op that
///     never invalidates it. This mirrors <see cref="RecognitionSubsystem.UnavailableSpeechRecognizer"/>
///     exactly.
/// </remarks>
public sealed class UnavailableSpeechSynthesizer : ISpeechSynthesizer
{
    /// <summary>
    ///     Prevents external construction; callers use the shared <see cref="Instance"/> instead
    ///     since the type carries no state and multiple instances would provide no value.
    /// </summary>
    private UnavailableSpeechSynthesizer()
    {
    }

    /// <summary>
    ///     Gets the single shared unavailable speech synthesizer.
    /// </summary>
    public static UnavailableSpeechSynthesizer Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Always thrown; this synthesizer has no real engine to synthesize with.
    /// </exception>
    public IAsyncEnumerable<SynthesizedSpeech> SynthesizeStreamAsync(string text, CancellationToken cancellationToken = default)
    {
        throw new SpeechSynthesizerUnavailableException(
            "Cannot synthesize speech: no speech synthesizer is available.");
    }

    /// <inheritdoc/>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Always thrown; this synthesizer has no real playback device to play through.
    /// </exception>
    public Task PlayStreamAsync(IAsyncEnumerable<SynthesizedSpeech> stream, CancellationToken cancellationToken = default)
    {
        throw new SpeechSynthesizerUnavailableException(
            "Cannot play synthesized speech: no speech synthesizer is available.");
    }

    /// <inheritdoc/>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Always thrown; this synthesizer has no real engine or playback device to speak with.
    /// </exception>
    public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        throw new SpeechSynthesizerUnavailableException(
            "Cannot speak: no speech synthesizer is available.");
    }

    /// <inheritdoc/>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Always thrown; this synthesizer has no in-flight session to stop.
    /// </exception>
    public void Stop()
    {
        throw new SpeechSynthesizerUnavailableException(
            "Cannot stop: no speech synthesizer is available.");
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     A no-op: this synthesizer owns no engine, thread, or native resource. Disposal must
    ///     not throw or invalidate <see cref="Instance"/>, because a host that wraps its
    ///     synthesizer in a <c>using</c> block gets the shared instance here and may dispose it
    ///     many times.
    /// </remarks>
    public void Dispose()
    {
        // Intentionally empty - see the remarks above.
    }
}
