namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Optional, language-aware text analysis a synthesis model supplies through
///     <see cref="ModelManagementSubsystem.ISynthesisModel.TextAnalyzer"/>, run once per speech
///     request before the text is chunked.
/// </summary>
/// <remarks>
///     Phoneme-driven models need sentence-level context that no chunk or tag-delimited fragment
///     carries (for example a part of speech that selects a heteronym's pronunciation). The
///     library therefore hands the analyzer the complete request text at once and delivers the
///     result to the backend that synthesizes each piece. The library itself is language-neutral:
///     it never interprets annotation kinds or values.
///     <para>
///     Implementations must be thread-safe (one model instance may back several engines) or
///     stateless per call, must not assume thread affinity (no <c>ThreadStatic</c> state and no UI
///     or synchronization-context access), must honor the cancellation token, must not retain the
///     text, and must not perform I/O they cannot cancel. The analyzer runs on a thread-pool
///     thread (<c>Task.Run</c>), never on the thread that called
///     <c>SpeakAsync</c> or <c>SynthesizeAsync</c>, so a slow analyzer cannot freeze a UI thread;
///     it is not the dedicated native worker, because it is expected to be managed CPU work only.
///     The token is the operation's linked token: it is checked before the analyzer starts (an
///     already-canceled operation never invokes the analyzer) and must be honored during the
///     call. An analyzer that ignores its token delays stop and dispose until it returns.
///     Throwing is allowed: the session contains the failure and continues without annotations.
///     </para>
/// </remarks>
public interface ITextAnalyzer
{
    /// <summary>
    ///     Analyzes the complete text of one speech request.
    /// </summary>
    /// <param name="text">
    ///     The model-normalized text (<see cref="ModelManagementSubsystem.ISpeechModel.NormalizeText"/>)
    ///     in which every recognized audio tag is replaced by exactly one
    ///     <see cref="TextAnalysisConventions.TagPlaceholder"/>. Unrecognized or malformed
    ///     bracket content stays literal text, exactly as <see cref="AudioTagParser"/> keeps it.
    ///     Must not be retained after the call returns.
    /// </param>
    /// <param name="cancellationToken">Cancels the analysis cooperatively.</param>
    /// <returns>
    ///     The annotations and optional sentence boundaries, with offsets relative to the exact
    ///     <paramref name="text"/> string (UTF-16 code unit indices). May be
    ///     <see cref="TextAnalysisResult.Empty"/>.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    TextAnalysisResult Analyze(string text, CancellationToken cancellationToken);
}
