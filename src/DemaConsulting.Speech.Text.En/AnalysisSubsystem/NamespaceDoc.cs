namespace DemaConsulting.Speech.Text.En.AnalysisSubsystem;

/// <summary>
///     English text analysis for DemaConsulting.Speech: an <c>ITextAnalyzer</c> that tags the
///     words of a speech request with their Universal Dependencies part of speech and supplies
///     authoritative sentence boundaries.
/// </summary>
/// <remarks>
///     <see cref="EnglishTextAnalyzer"/> is the entry point; a model package returns an instance
///     from <c>ISynthesisModel.TextAnalyzer</c>. <see cref="EnglishTextAnalyzerOptions"/> selects
///     which annotations are produced and optionally supplies a counts-only diagnostics sink.
///     <see cref="TextEnAnnotations"/> names the annotation kinds this package defines and reads
///     them back for a backend. <see cref="EnglishTextAnalysis"/> is the richer, local-only
///     result of <see cref="EnglishTextAnalyzer.AnalyzeDetailed"/>. The package is a preview: the
///     <c>DemaConsulting.PartOfSpeech</c> tagger it builds on is itself a pre-release package.
/// </remarks>
internal static class NamespaceDoc
{
}
