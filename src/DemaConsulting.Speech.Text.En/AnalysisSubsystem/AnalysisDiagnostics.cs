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
///     The aggregate numbers of the heteronym readings one analysis request assigned.
/// </summary>
/// <param name="Classified">The number of readings the model decided among several candidates.</param>
/// <param name="SingleCandidate">The number of readings of forms with exactly one candidate (confidence 1).</param>
/// <param name="ByOverride">The number of readings fixed by a <see cref="OverrideAction.Reading"/> override.</param>
/// <param name="LowConfidence">The number of classified readings below the confidence threshold.</param>
internal readonly record struct ReadingCounts(int Classified, int SingleCandidate, int ByOverride, int LowConfidence)
{
    /// <summary>Gets the total number of readings assigned.</summary>
    internal int Assigned => Classified + SingleCandidate + ByOverride;
}

/// <summary>
///     Reports counts-only events to an optional host-supplied
///     <see cref="ISpeechDiagnostics"/> sink, and never anything else.
/// </summary>
/// <remarks>
///     The library's diagnostics contract forbids synthesized or recognized text content in any
///     report, so every message is built only from fixed words and numbers: a word, a token, a
///     spoken replacement, a reading id or an annotation value can never reach the sink. Three events
///     exist: the per-request <c>TextEn.Analysis</c> counts, the optional per-request
///     <c>TextEn.Heteronym</c> counts of assigned readings, and the <c>TextEn.Model</c> warning that the
///     analyzer reports at most once per instance. Reporting is strictly best effort: a sink that
///     throws is contained here, because observing an analysis must never interrupt it. The type
///     keeps no mutable state and is thread-safe provided the sink is.
/// </remarks>
internal sealed class AnalysisDiagnostics
{
    /// <summary>The diagnostics category used for every request event this package reports.</summary>
    internal const string Category = "TextEn.Analysis";

    /// <summary>The category of the heteronym readings event.</summary>
    internal const string ReadingCategory = "TextEn.Heteronym";

    /// <summary>The category of the model warning.</summary>
    internal const string ModelCategory = "TextEn.Model";

    /// <summary>The fixed message of the model warning; it names neither the model file nor any text.</summary>
    internal const string ModelProblemMessage = "Heteronym model unusable (invalid or incompatible file); readings disabled.";

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
    internal void ReportRequest(AnalysisCounts counts) => Report(SpeechDiagnosticLevel.Info, Category, FormatMessage(counts));

    /// <summary>
    ///     Builds the heteronym report message: fixed text, four counts and the threshold.
    /// </summary>
    /// <param name="counts">The reading counts of the request.</param>
    /// <param name="threshold">The low-confidence threshold, from 0 to 1.</param>
    /// <returns>The message; contains digits and fixed words only, never a word or a reading id.</returns>
    internal static string FormatReadingMessage(ReadingCounts counts, double threshold) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Assigned {counts.Assigned} heteronym readings: {counts.Classified} classified, {counts.SingleCandidate} single-candidate, {counts.ByOverride} by override, {counts.LowConfidence} below confidence {threshold:0.00}.");

    /// <summary>
    ///     Reports the heteronym readings of one completed request, once, when any were assigned.
    /// </summary>
    /// <param name="counts">The reading counts of the request.</param>
    /// <param name="threshold">The low-confidence threshold in force.</param>
    internal void ReportReadings(ReadingCounts counts, double threshold)
    {
        if (counts.Assigned > 0)
        {
            Report(SpeechDiagnosticLevel.Info, ReadingCategory, FormatReadingMessage(counts, threshold));
        }
    }

    /// <summary>
    ///     Reports that a heteronym model exists but cannot be used.
    /// </summary>
    internal void ReportModelProblem() => Report(SpeechDiagnosticLevel.Warning, ModelCategory, ModelProblemMessage);

    /// <summary>
    ///     Sends one event to the sink, containing any failure of the sink.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <param name="category">The category.</param>
    /// <param name="message">The message, already free of text content.</param>
    private void Report(SpeechDiagnosticLevel level, string category, string message)
    {
        if (_sink is null)
        {
            return;
        }

        try
        {
            _sink.Report(level, category, message);
        }
        catch (Exception)
        {
            // Intentionally broad: a diagnostics sink is an observer, and the contract of
            // ISpeechDiagnostics says it must not throw; if one does anyway, the analysis it
            // was observing must still complete.
        }
    }
}
