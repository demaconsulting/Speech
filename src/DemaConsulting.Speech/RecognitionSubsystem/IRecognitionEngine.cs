namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Internal, mockable seam over one loaded streaming recognition engine: accepts mono audio
///     samples and reports the recognition results decoded from them.
/// </summary>
/// <remarks>
///     This seam exists for the same reason as <c>IPortAudioApi</c> (Phase 1b) and
///     <c>IModelDownloadClient</c> (Phase 2a): it confines every sherpa-onnx interop call to a
///     single implementation (<see cref="SherpaOnnxRecognitionEngine"/>) so
///     <see cref="SherpaOnnxSpeechRecognizer"/>'s threading, resampling, and event-emission logic
///     is fully unit-testable with a pure managed fake. No native sherpa-onnx runtime binary and
///     no downloaded model are ever required to run the recognition subsystem's tests.
///     <para>
///     Implementations are not thread-safe: <see cref="SherpaOnnxSpeechRecognizer"/> calls them
///     from exactly one background consumer thread at a time and never concurrently, which
///     matches the single-stream ownership model of the underlying engine.
///     </para>
/// </remarks>
internal interface IRecognitionEngine : IDisposable
{
    /// <summary>
    ///     Feeds one block of mono audio into the engine's current utterance stream.
    /// </summary>
    /// <param name="monoSamples">
    ///     Normalized single-channel samples in the range <c>[-1.0, 1.0]</c>, already resampled to
    ///     the rate the model declared. May be empty, in which case the call is a no-op.
    /// </param>
    /// <remarks>
    ///     Accepting samples only buffers them; it never blocks on decoding, so the caller must
    ///     follow it with <see cref="TryDecode"/> to observe any resulting text.
    /// </remarks>
    void AcceptSamples(ReadOnlySpan<float> monoSamples);

    /// <summary>
    ///     Decodes as much buffered audio as the engine currently can and reports the next
    ///     recognition result, if any.
    /// </summary>
    /// <param name="result">
    ///     When this method returns <see langword="true"/>, the decoded provisional or final
    ///     result; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when a result is available; <see langword="false"/> when the
    ///     engine has nothing new to report (no buffered audio, no text yet, or text unchanged
    ///     since the previous call).
    /// </returns>
    /// <remarks>
    ///     Returning "nothing new" as <see langword="false"/> rather than an empty result keeps
    ///     the caller's event loop free of duplicate <see cref="SpeechRecognitionEvent"/>s while
    ///     silence is being streamed. Callers may invoke this repeatedly until it returns
    ///     <see langword="false"/> to drain every result an accepted block produced.
    /// </remarks>
    bool TryDecode(out SpeechRecognitionResult? result);

    /// <summary>
    ///     Finalizes and decodes any buffered audio the engine has accepted but not yet decoded,
    ///     reporting one last result if that produced or completed any text.
    /// </summary>
    /// <param name="result">
    ///     When this method returns <see langword="true"/>, the final result produced by
    ///     finalizing the trailing audio; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when finalizing produced non-empty text; <see langword="false"/>
    ///     when there was nothing buffered or finalizing it produced no text.
    /// </returns>
    /// <remarks>
    ///     A streaming engine sometimes cannot decode the tail of an utterance without more audio
    ///     that a caller who has just stopped will never supply - for example a push-to-talk
    ///     release with no trailing silence. Call this once, at session end, before
    ///     <see cref="Reset"/> discards the stream, so that trailing audio is finalized and
    ///     delivered rather than silently discarded along with it.
    /// </remarks>
    bool TryFlush(out SpeechRecognitionResult? result);

    /// <summary>
    ///     Discards any partially decoded utterance and returns the engine to its
    ///     start-of-utterance state.
    /// </summary>
    /// <remarks>
    ///     Used when a recognition session ends so a subsequent session does not inherit text
    ///     from audio the caller has already abandoned. Call <see cref="TryFlush"/> first to
    ///     finalize and deliver whatever trailing audio can still be recovered - by the time this
    ///     method runs, anything it discards was never delivered to a caller.
    /// </remarks>
    void Reset();
}
