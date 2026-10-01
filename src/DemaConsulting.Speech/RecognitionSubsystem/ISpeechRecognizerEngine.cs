using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Mockable contract for one loaded, native-backed recognition model, with no capture device
///     bound yet.
/// </summary>
/// <remarks>
///     Obtained from <see cref="SpeechRecognizerFactory"/>. Loading the model into native memory
///     is the expensive step; an engine may be reused to create many sessions over its lifetime
///     (though only one at a time - see <see cref="CreateSessionAsync"/>), so a host should load
///     one engine per model/parameter combination and keep it for as long as it may recognize
///     speech, rather than reloading per turn.
///     <para>
///     An engine that cannot function honestly reports <see cref="IsAvailable"/> as
///     <see langword="false"/> instead of throwing at composition time; see
///     <see cref="UnavailableSpeechRecognizerEngine"/> for the canonical fallback.
///     </para>
/// </remarks>
public interface ISpeechRecognizerEngine : IAsyncDisposable
{
    /// <summary>
    ///     Gets a value indicating whether this engine is backed by a real, loaded recognition
    ///     backend. When <see langword="false"/>, <see cref="CreateSessionAsync"/> never throws
    ///     for ordinary unavailability - it returns <see cref="UnavailableRecognitionSession.Instance"/>
    ///     instead.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    ///     Binds this engine to exactly one capture device for the entire life of the returned
    ///     session.
    /// </summary>
    /// <param name="device">
    ///     The capture device the returned session streams audio from for its entire life. Must
    ///     not be null.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation of this call.</param>
    /// <returns>
    ///     A new <see cref="IRecognitionSession"/> bound to <paramref name="device"/>, or
    ///     <see cref="UnavailableRecognitionSession.Instance"/> when <see cref="IsAvailable"/> is
    ///     <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="device"/> is null.</exception>
    /// <exception cref="RecognitionEngineBusyException">
    ///     Thrown when a session created from this engine already holds this engine's
    ///     exclusivity lease - including while that prior session is still tearing down via its
    ///     own <see cref="IAsyncDisposable.DisposeAsync"/>. This call never queues or waits for
    ///     that release; it fails fast instead.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     Thrown when <paramref name="cancellationToken"/> is cancelled before the call
    ///     completes.
    /// </exception>
    /// <remarks>
    ///     Never faults for an ordinary unavailable-engine state; faults only for a null
    ///     argument, caller cancellation, or a concurrently leased session (see
    ///     <see cref="RecognitionEngineBusyException"/>).
    /// </remarks>
    Task<IRecognitionSession> CreateSessionAsync(
        IAudioCaptureDevice device,
        CancellationToken cancellationToken = default);
}
