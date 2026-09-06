namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Mockable streaming speech-to-text contract: starts and stops consuming audio from a
///     capture device and raises progressive provisional and final recognition results while
///     running.
/// </summary>
/// <remarks>
///     Per this library's "engine backend stays swappable at the public API surface"
///     decision, no member of this contract exposes a sherpa-onnx (or any other engine) type, so
///     a future non-sherpa-onnx backend can be added without a breaking change. Hosts obtain
///     implementations from <see cref="SpeechRecognizerFactory"/> rather than constructing them,
///     and tests can substitute this interface directly to exercise host transcript logic with no
///     model, no microphone, and no native runtime.
///     <para>
///     A recognizer that cannot function honestly reports <see cref="IsAvailable"/> as
///     <see langword="false"/> instead of throwing at composition time; see
///     <see cref="UnavailableSpeechRecognizer"/> for the canonical fallback. Implementations own
///     unmanaged inference resources, so callers must dispose them; disposal is idempotent and
///     implies <see cref="Stop"/>.
///     </para>
///     <para>
///     <b>Thread safety</b>: <see cref="Start"/>, <see cref="Stop"/>, and
///     <see cref="IDisposable.Dispose"/> may each be called concurrently, from any thread,
///     without external synchronization - implementations are responsible for serializing
///     their own internal state transitions so overlapping calls compose safely (each is
///     individually idempotent, as documented on that member). This contract does not
///     promise any particular outcome for the relative ordering of unrelated, racing
///     Start/Stop calls issued from different threads at the same time - only that each call
///     completes without corrupting the recognizer's internal state. <see cref="ResultReceived"/>
///     is always raised from the recognizer's own background decoding thread and handlers are
///     invoked serially, never concurrently with each other; see that event's remarks.
///     </para>
/// </remarks>
public interface ISpeechRecognizer : IDisposable
{
    /// <summary>
    ///     Gets a value indicating whether this recognizer is backed by a real, loaded
    ///     recognition engine and a usable capture device. When <see langword="false"/>,
    ///     <see cref="Start"/> and <see cref="Stop"/> throw
    ///     <see cref="SpeechRecognizerUnavailableException"/> rather than silently doing nothing,
    ///     because a caller that ignores this flag has made a programming error that should
    ///     surface immediately rather than silently recognize nothing.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    ///     Raised once for each provisional or final recognition result produced while the
    ///     recognizer is running.
    /// </summary>
    /// <remarks>
    ///     Raised from the recognizer's own background decoding thread - never from the audio
    ///     capture callback thread - so a handler may do moderate work without risking audio
    ///     glitches. Handlers are still invoked serially, so a slow handler delays subsequent
    ///     results. An exception thrown by a handler is caught and reported through the
    ///     recognizer's diagnostics sink; it never propagates and never stops the recognizer.
    ///     Handlers must therefore not rely on exceptions escaping.
    /// </remarks>
    event EventHandler<SpeechRecognitionEvent>? ResultReceived;

    /// <summary>
    ///     Begins streaming audio from the configured capture device into the recognition engine,
    ///     after which <see cref="ResultReceived"/> is raised for each result until
    ///     <see cref="Stop"/> is called. Calling this on an already-running recognizer is a safe
    ///     no-op.
    /// </summary>
    /// <exception cref="SpeechRecognizerUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>, or when the
    ///     recognizer reported itself as available but its capture device failed to start.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    ///     Thrown when the recognizer has already been disposed.
    /// </exception>
    void Start();

    /// <summary>
    ///     Stops streaming audio and drains any already-captured audio through the engine, so
    ///     every result derived from audio accepted before this call is raised before it returns.
    ///     No further <see cref="ResultReceived"/> events are raised until <see cref="Start"/> is
    ///     called again. Calling this on a recognizer that is not running is a safe no-op.
    /// </summary>
    /// <exception cref="SpeechRecognizerUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>.
    /// </exception>
    void Stop();
}
