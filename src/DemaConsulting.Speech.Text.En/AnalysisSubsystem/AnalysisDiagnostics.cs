using System.Globalization;
using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.Text.En.AnalysisSubsystem;

/// <summary>
///     The aggregate numbers one analysis request reports to a diagnostics sink.
/// </summary>
/// <param name="Sentences">The number of sentences found.</param>
/// <param name="Tokens">The number of tokens found.</param>
/// <param name="Annotations">The number of annotations produced.</param>
/// <param name="UnknownWords">
///     The number of word tokens (not punctuation, symbols or numbers) the tagger's lexicon does
///     not know; 0 when the text was not tagged.
/// </param>
internal readonly record struct AnalysisCounts(int Sentences, int Tokens, int Annotations, int UnknownWords);

/// <summary>
///     Reports one aggregate-counts event per analyzed request to an optional host-supplied
///     <see cref="ISpeechDiagnostics"/> sink, and never anything else.
/// </summary>
/// <remarks>
///     The library's diagnostics contract forbids synthesized or recognized text content in any
///     report, so the message is built only from fixed words and numbers: a word, a token or an
///     annotation value can never reach the sink. Reporting is strictly best effort: a sink that
///     throws is contained here, because observing an analysis must never interrupt it. The type
///     keeps no mutable state and is thread-safe provided the sink is.
/// </remarks>
internal sealed class AnalysisDiagnostics
{
    /// <summary>The diagnostics category used for every event this package reports.</summary>
    internal const string Category = "TextEn.Analysis";

    /// <summary>The sink to report to, or <see langword="null"/> for no reporting.</summary>
    private readonly ISpeechDiagnostics? _sink;

    /// <summary>
    ///     Initializes a new instance.
    /// </summary>
    /// <param name="sink">The sink to report to, or <see langword="null"/> to report nothing.</param>
    internal AnalysisDiagnostics(ISpeechDiagnostics? sink)
    {
        _sink = sink;
    }

    /// <summary>
    ///     Builds the report message for a request: fixed text plus the four counts.
    /// </summary>
    /// <param name="counts">The aggregate counts of the request.</param>
    /// <returns>The message; contains digits and fixed words only.</returns>
    internal static string FormatMessage(AnalysisCounts counts) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Analyzed one text: {counts.Sentences} sentences, {counts.Tokens} tokens, {counts.Annotations} annotations, {counts.UnknownWords} unknown words.");

    /// <summary>
    ///     Reports the counts of one completed request, once.
    /// </summary>
    /// <param name="counts">The aggregate counts of the request.</param>
    internal void ReportRequest(AnalysisCounts counts)
    {
        if (_sink is null)
        {
            return;
        }

        try
        {
            _sink.Report(SpeechDiagnosticLevel.Info, Category, FormatMessage(counts));
        }
        catch (Exception)
        {
            // Intentionally broad: a diagnostics sink is an observer, and the contract of
            // ISpeechDiagnostics says it must not throw; if one does anyway, the analysis it
            // was observing must still complete.
        }
    }
}
