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
/// <param name="Normalizations">The number of replacements (<c>en.norm</c> and <c>en.spell</c>) found; 0 when normalization was not selected.</param>
/// <param name="AmbiguousReadings">
///     The number of resolutions that fell back to a documented default or chose between
///     competing cues (abbreviations and numeric readings such as a year or a month-first
///     date); 0 when normalization was not selected.
/// </param>
/// <param name="NumericFallbacks">
///     The number of numeric-looking expressions left as written, or read digit by digit,
///     because of a documented size limit or an unsupported numeric shape; 0 when normalization
///     was not selected.
/// </param>
/// <param name="OverridesApplied">The number of user pronunciation overrides applied to a word; 0 without overrides.</param>
/// <param name="OverridesIgnored">
///     The number of user pronunciation overrides that matched a word but were not applied because
///     an earlier multi-token match had already consumed it; 0 without overrides.
/// </param>
internal readonly record struct AnalysisCounts(int Sentences, int Tokens, int Annotations, int UnknownWords, int Normalizations, int AmbiguousReadings, int NumericFallbacks, int OverridesApplied = 0, int OverridesIgnored = 0);

/// <summary>
///     Reports one aggregate-counts event per analyzed request to an optional host-supplied
///     <see cref="ISpeechDiagnostics"/> sink, and never anything else.
/// </summary>
/// <remarks>
///     The library's diagnostics contract forbids synthesized or recognized text content in any
///     report, so the message is built only from fixed words and numbers: a word, a token, a
///     spoken replacement or an annotation value can never reach the sink. Reporting is strictly best effort: a sink that
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
    ///     Builds the report message for a request: fixed text plus the nine counts.
    /// </summary>
    /// <param name="counts">The aggregate counts of the request.</param>
    /// <returns>The message; contains digits and fixed words only.</returns>
    internal static string FormatMessage(AnalysisCounts counts) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Analyzed one text: {counts.Sentences} sentences, {counts.Tokens} tokens, {counts.Annotations} annotations, {counts.UnknownWords} unknown words, {counts.Normalizations} normalizations, {counts.AmbiguousReadings} ambiguous readings, {counts.NumericFallbacks} numeric fallbacks, {counts.OverridesApplied} overrides applied, {counts.OverridesIgnored} overrides ignored.");

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
