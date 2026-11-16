using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Text.En.AnalysisSubsystem;

/// <summary>
///     One token of an analyzed text, with its span in the source and, when the text was tagged,
///     its part of speech.
/// </summary>
/// <param name="Start">
///     The zero-based UTF-16 code unit offset of the token's first character in the analyzed
///     text.
/// </param>
/// <param name="Length">The number of UTF-16 code units of the token; always positive.</param>
/// <param name="Text">
///     The token's text, exactly as it appears in the analyzed text. This is the word itself, so
///     it is for local use by the caller and must not be sent to a diagnostics sink.
/// </param>
/// <param name="PartOfSpeech">
///     The Universal Dependencies tag name (for example <c>"NOUN"</c>), or <see langword="null"/>
///     when part-of-speech annotation was not selected so the text was not tagged. Punctuation
///     and symbol tokens are listed with <c>"PUNCT"</c> or <c>"SYM"</c> but carry no annotation.
/// </param>
/// <param name="Confidence">
///     The tag's confidence from 0 to 1 (the tagger's probability of the chosen tag among the
///     tags it allowed for the token), or 0 when the token was not tagged.
/// </param>
/// <param name="IsKnownWord">
///     Whether the tagger's lexicon has an entry for the token's spelling;
///     <see langword="false"/> when the token was not tagged.
/// </param>
/// <remarks>An immutable value, safe to share across threads.</remarks>
public sealed record AnalyzedToken(int Start, int Length, string Text, string? PartOfSpeech, double Confidence, bool IsKnownWord);

/// <summary>
///     The rich, local-only result of <see cref="EnglishTextAnalyzer.AnalyzeDetailed"/>: every
///     token with its tag and confidence, the sentence boundaries, and the annotations the
///     language-neutral <see cref="ITextAnalyzer"/> path would have produced.
/// </summary>
/// <remarks>
///     Unlike <see cref="TextAnalysisResult"/>, this result carries the words themselves
///     (<see cref="AnalyzedToken.Text"/>) and per-token confidence, so it is meant for the
///     caller's own use (tests, tooling, a model package's own pipeline) and must never be
///     forwarded to a diagnostics sink. All offsets index the exact text that was analyzed. The
///     class is immutable: it can be created only by the analyzer, and its lists are read-only
///     private copies, so it is safe to share across threads.
/// </remarks>
public sealed class EnglishTextAnalysis
{
    /// <summary>
    ///     Initializes a new instance from the analyzer's own freshly built lists; the lists are
    ///     wrapped read-only and never mutated afterwards.
    /// </summary>
    /// <param name="tokens">Every token, in text order.</param>
    /// <param name="sentenceBreaks">The sentence-break offsets, ascending.</param>
    /// <param name="annotations">The annotations, in token order.</param>
    internal EnglishTextAnalysis(
        IReadOnlyList<AnalyzedToken> tokens,
        IReadOnlyList<int> sentenceBreaks,
        IReadOnlyList<SegmentAnnotation> annotations)
    {
        Tokens = tokens;
        SentenceBreaks = sentenceBreaks;
        Annotations = annotations;
    }

    /// <summary>
    ///     Gets every token of the text in text order, including punctuation and symbols; empty
    ///     for a text without tokens.
    /// </summary>
    public IReadOnlyList<AnalyzedToken> Tokens { get; }

    /// <summary>
    ///     Gets the authoritative sentence boundaries in the sense of
    ///     <see cref="TextAnalysisResult.SentenceBreaks"/>: each value is the offset of the first
    ///     token of a sentence after the first. Never null; empty when the text has at most one
    ///     sentence.
    /// </summary>
    public IReadOnlyList<int> SentenceBreaks { get; }

    /// <summary>
    ///     Gets the annotations in token order; empty when nothing was selected or no token could
    ///     be annotated.
    /// </summary>
    public IReadOnlyList<SegmentAnnotation> Annotations { get; }
}
