namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Mockable chunked/streaming text-to-speech contract: synthesizes text (including inline
///     Natural Language Audio Tags) into ordered audio segments and plays them back, with
///     playback of an earlier segment beginning while later segments are still being
///     synthesized.
/// </summary>
/// <remarks>
///     Per architecture.md's "engine backend stays swappable at the public API surface"
///     decision, no member of this contract exposes a sherpa-onnx (or any other engine) type, so
///     a future non-sherpa-onnx backend can be added without a breaking change. Hosts obtain
///     implementations from <see cref="SpeechSynthesizerFactory"/> rather than constructing them,
///     and tests can substitute this interface directly to exercise host playback logic with no
///     model, no speakers, and no native runtime.
///     <para>
///     A synthesizer that cannot function honestly reports <see cref="IsAvailable"/> as
///     <see langword="false"/> instead of throwing at composition time; see
///     <see cref="UnavailableSpeechSynthesizer"/> for the canonical fallback. Implementations own
///     unmanaged inference resources, so callers must dispose them; disposal is idempotent and
///     implies <see cref="Stop"/>.
///     </para>
/// </remarks>
public interface ISpeechSynthesizer : IDisposable
{
    /// <summary>
    ///     Gets a value indicating whether this synthesizer is backed by a real, loaded synthesis
    ///     engine and a usable playback device. When <see langword="false"/>, every operational
    ///     member throws <see cref="SpeechSynthesizerUnavailableException"/> rather than silently
    ///     doing nothing, because a caller that ignores this flag has made a programming error
    ///     that should surface immediately rather than silently speak nothing.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    ///     Synthesizes text (which may contain inline Natural Language Audio Tags) into an
    ///     ordered, asynchronously produced stream of audio segments.
    /// </summary>
    /// <param name="text">The text to synthesize. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the synthesis session.</param>
    /// <returns>
    ///     An ordered asynchronous sequence of <see cref="SynthesizedSpeech"/> segments. Segments
    ///     later in the sequence may still be being synthesized while earlier ones are already
    ///     available, per architecture.md's chunked, low-latency streaming design.
    /// </returns>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    IAsyncEnumerable<SynthesizedSpeech> SynthesizeStreamAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Plays an ordered stream of audio segments through the configured playback device,
    ///     writing each segment's silence and samples in order as they become available, and
    ///     only returns once the playback device has genuinely finished rendering every sample
    ///     written - not merely once every segment has been handed off to it.
    /// </summary>
    /// <param name="stream">The ordered segment stream to play. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel playback.</param>
    /// <returns>
    ///     A task that completes once every segment in <paramref name="stream"/> has been played
    ///     and the playback device has drained everything written to it (or cancellation ends the
    ///     wait early).
    /// </returns>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <see langword="null"/>.</exception>
    Task PlayStreamAsync(IAsyncEnumerable<SynthesizedSpeech> stream, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Convenience method composing <see cref="SynthesizeStreamAsync"/> and
    ///     <see cref="PlayStreamAsync"/>: synthesizes and speaks <paramref name="text"/>, with
    ///     playback of earlier segments beginning while later segments are still being
    ///     synthesized.
    /// </summary>
    /// <param name="text">The text to speak. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the session.</param>
    /// <returns>A task that completes once the entire text has been played.</returns>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Cancels an in-flight <see cref="SpeakAsync"/> or <see cref="PlayStreamAsync"/> session
    ///     deterministically, stopping playback and ending the session's task. Calling this when
    ///     no session is in flight is a safe no-op.
    /// </summary>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>.
    /// </exception>
    void Stop();
}
