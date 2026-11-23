namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Mockable streaming speech-to-text session: cheap, bound to exactly one capture device
///     instance for its entire life, and single-use.
/// </summary>
/// <remarks>
///     Obtained from <see cref="ISpeechRecognizerEngine.CreateSessionAsync"/> rather than
///     constructed directly. Per this library's single-use session decision, a session that has
///     reached <see cref="RecognitionSessionState.Stopped"/> can never run again -
///     <see cref="StartAsync"/> throws <see cref="InvalidOperationException"/> rather than
///     restarting; create a new session via <see cref="ISpeechRecognizerEngine.CreateSessionAsync"/>
///     for another run.
///     <para>
///     A session that cannot function honestly reports <see cref="IsAvailable"/> as
///     <see langword="false"/>; see <see cref="UnavailableRecognitionSession"/> for the canonical
///     fallback. Implementations own unmanaged inference resources reached through their owning
///     engine, so callers must dispose them; disposal is idempotent. Disposing a session that is
///     still running or stopping implicitly performs the same
///     <see cref="RecognitionSessionState.Stopping"/> -&gt;
///     <see cref="RecognitionSessionState.Stopped"/> drain as an explicit <see cref="StopAsync"/>
///     call before the engine's lease is released, so a caller never needs to call
///     <see cref="StopAsync"/> before disposing.
///     </para>
/// </remarks>
public interface IRecognitionSession : IAsyncDisposable
{
    /// <summary>
    ///     Gets a value indicating whether this session is backed by a real, loaded recognition
    ///     engine and a usable capture device. When <see langword="false"/>,
    ///     <see cref="StartAsync"/> and <see cref="GetResultsAsync"/> throw
    ///     <see cref="SpeechRecognizerUnavailableException"/>.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>Gets the session's current lifecycle state.</summary>
    RecognitionSessionState State { get; }

    /// <summary>
    ///     Raised once for every <see cref="RecognitionSessionState"/> transition this session
    ///     makes.
    /// </summary>
    /// <remarks>
    ///     Raised from the session's own background pump thread and handlers are invoked
    ///     serially, never concurrently with each other - the same threading and fault-isolation
    ///     convention this library used for synchronous result delivery before the Engine/Session
    ///     split.
    ///     An exception thrown by a handler is caught and reported through the session's
    ///     diagnostics sink; it never propagates and never faults the session.
    /// </remarks>
    event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    /// <summary>
    ///     Begins streaming audio from the bound capture device into the recognition backend,
    ///     after which results become available through <see cref="GetResultsAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation of this call.</param>
    /// <returns>A task that completes once the session is running.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <see cref="State"/> is not <see cref="RecognitionSessionState.Created"/> -
    ///     most notably when the session has already reached
    ///     <see cref="RecognitionSessionState.Stopped"/>, since a session is single-use.
    /// </exception>
    /// <exception cref="SpeechRecognizerUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>, or when the bound
    ///     capture device failed to start.
    /// </exception>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stops streaming audio and drains any already-captured audio through the backend, so
    ///     every result derived from audio accepted before this call - including the backend's
    ///     final flushed result for a trailing utterance - is enumerable via
    ///     <see cref="GetResultsAsync"/> before the returned task completes.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A token that, when canceled before or during this call, turns it into a discarding
    ///     stop: audio still queued is dropped rather than decoded, no final flush is performed,
    ///     and this call's own task completes with <see cref="OperationCanceledException"/>.
    ///     Leave it uncanceled (or pass <see cref="CancellationToken.None"/>) for a graceful stop
    ///     that keeps the tail of the utterance.
    /// </param>
    /// <returns>A task that completes once the session has stopped.</returns>
    /// <remarks>
    ///     Idempotent and safe to call concurrently or while overlapping a prior call still in
    ///     flight: <see cref="RecognitionSessionState.Running"/>,
    ///     <see cref="RecognitionSessionState.Starting"/>, and
    ///     <see cref="RecognitionSessionState.Stopping"/> all converge on
    ///     <see cref="RecognitionSessionState.Stopped"/>, and every caller's task completes once
    ///     that convergence happens. A fault while finalizing or resetting is reported through
    ///     diagnostics rather than thrown; this call still completes.
    ///     <para>
    ///     A graceful stop discards no captured audio. Cancellation is the way to discard it on
    ///     purpose; the only other case is abandonment, where something the stop waits on - a
    ///     recognizer call or a capture callback - is genuinely stuck for the implementation's
    ///     abandon interval, so the stop gives up on it rather than hang, and reports the
    ///     abandonment as a warning diagnostic. Cancellation applies to the one
    ///     shared stop operation, so a canceled caller also shortens the drain observed by any
    ///     concurrent caller (including <see cref="IAsyncDisposable.DisposeAsync"/>); the session
    ///     still releases its capture device and resets its recognizer, and still converges on
    ///     <see cref="RecognitionSessionState.Stopped"/>, for every caller. After any stop the
    ///     next session created from the same engine starts a clean utterance.
    ///     </para>
    /// </remarks>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Streams every provisional and final recognition result produced while the session
    ///     runs.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A token that ends only this enumeration when canceled - the session itself keeps
    ///     running.
    /// </param>
    /// <returns>An asynchronous sequence of recognition events.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when a second concurrent call is made while one enumeration of this session's
    ///     results is already active; this contract is single-consumer.
    /// </exception>
    /// <exception cref="SpeechRecognizerUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>.
    /// </exception>
    /// <exception cref="RecognitionSessionFaultedException">
    ///     Thrown from the enumerator when the session transitions to
    ///     <see cref="RecognitionSessionState.Faulted"/> while being enumerated.
    /// </exception>
    IAsyncEnumerable<SpeechRecognitionEvent> GetResultsAsync(CancellationToken cancellationToken = default);
}
