using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.NormalizationSubsystem;

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
public sealed record AnalyzedToken(int Start, int Length, string Text, string? PartOfSpeech, double Confidence, bool IsKnownWord)
{
    /// <summary>
    ///     Gets the grammatical role of this token when it is a clitic such as <c>'s</c> or
    ///     <c>n't</c>; <see cref="CliticKind.None"/> for every other token, and always
    ///     <see cref="CliticKind.None"/> when normalization was not selected.
    /// </summary>
    /// <remarks>
    ///     Derived from the neighboring words only (not from the part of speech), so it is a
    ///     heuristic: <c>the dog's training session</c> reads as a contraction.
    /// </remarks>
    public CliticKind Clitic { get; init; }

    /// <summary>
    ///     Gets the heteronym reading id assigned to this token (for example <c>record.noun</c>), or
    ///     <see langword="null"/> when none was: the token is not a heteronym form, the reading family was
    ///     not selected or has no model, or the token was a replacement or carried another override.
    /// </summary>
    /// <remarks>Equals the <c>Value</c> of the token's <see cref="TextEnAnnotations.Reading"/> annotation.</remarks>
    public string? Reading { get; init; }

    /// <summary>
    ///     Gets the confidence of <see cref="Reading"/> from 0 to 1 (1 for a single-candidate form or an
    ///     override), or 0 when the token has no reading.
    /// </summary>
    public double ReadingConfidence { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the token's classified reading is below
    ///     <see cref="EnglishTextAnalyzerOptions.LowConfidenceThreshold"/>; <see langword="false"/> when
    ///     the token has no reading and for a single-candidate or overridden reading.
    /// </summary>
    public bool IsLowConfidenceReading { get; init; }
}

/// <summary>
///     One replacement (<c>en.norm</c> or <c>en.spell</c>) as <see cref="EnglishTextAnalyzer.AnalyzeDetailed"/>
///     reports it: the source span, the words it is spoken as, and what produced it.
/// </summary>
/// <param name="Start">The zero-based UTF-16 offset of the span's first character in the analyzed text.</param>
/// <param name="Length">The number of UTF-16 code units the span covers; always positive.</param>
/// <param name="Text">
///     The source characters of the span. This is part of the user's text, so it is for local
///     use and must not be sent to a diagnostics sink.
/// </param>
/// <param name="SpokenText">The exact replacement, equal to the <c>Value</c> of the matching annotation; empty for <see cref="NormalizationKind.Dropped"/>.</param>
/// <param name="Kind">What produced the replacement.</param>
/// <remarks>An immutable value, safe to share across threads.</remarks>
public sealed record AnalyzedNormalization(int Start, int Length, string Text, string SpokenText, NormalizationKind Kind)
{
    /// <summary>
    ///     Gets a value indicating whether the replacement is a letter sequence to be spoken by
    ///     letter name (<c>F B I</c> for <c>FBI</c>); such a normalization is annotated
    ///     <see cref="TextEnAnnotations.SpellOut"/> instead of <see cref="TextEnAnnotations.Normalized"/>.
    ///     <see langword="false"/> by default.
    /// </summary>
    public bool IsSpelled { get; init; }
}

/// <summary>
///     The rich, local-only result of <see cref="EnglishTextAnalyzer.AnalyzeDetailed"/>: every
///     token with its tag and confidence, every normalization, the sentence boundaries, and the
///     annotations the language-neutral <see cref="ITextAnalyzer"/> path would have produced.
/// </summary>
/// <remarks>
///     Unlike <see cref="TextAnalysisResult"/>, this result carries the words themselves
///     (<see cref="AnalyzedToken.Text"/>, <see cref="AnalyzedNormalization.Text"/>) and per-token
///     confidence, so it is meant for the caller's own use (tests, tooling, a model package's own
///     pipeline) and must never be forwarded to a diagnostics sink. All offsets index the exact
///     text that was analyzed. The class is immutable: it can be created only by the analyzer,
///     and its lists are read-only private copies, so it is safe to share across threads.
/// </remarks>
public sealed class EnglishTextAnalysis
{
    /// <summary>
    ///     Initializes a new instance from the analyzer's own freshly built lists; the lists are
    ///     wrapped read-only and never mutated afterwards.
    /// </summary>
    /// <param name="tokens">Every token, in text order.</param>
    /// <param name="sentenceBreaks">The sentence-break offsets, ascending.</param>
    /// <param name="annotations">The annotations, in text order.</param>
    /// <param name="normalizations">The normalizations, in text order.</param>
    internal EnglishTextAnalysis(
        IReadOnlyList<AnalyzedToken> tokens,
        IReadOnlyList<int> sentenceBreaks,
        IReadOnlyList<SegmentAnnotation> annotations,
        IReadOnlyList<AnalyzedNormalization> normalizations)
    {
        Tokens = tokens;
        SentenceBreaks = sentenceBreaks;
        Annotations = annotations;
        Normalizations = normalizations;
    }

    /// <summary>
    ///     Gets every token of the text in text order, including punctuation and symbols; empty
    ///     for a text without tokens.
    /// </summary>
    public IReadOnlyList<AnalyzedToken> Tokens { get; }

    /// <summary>
    ///     Gets the authoritative sentence boundaries in the sense of
    ///     <see cref="TextAnalysisResult.SentenceBreaks"/>: each value is the offset of the first
    ///     token of a sentence after the first. When normalization was selected they are the
    ///     tokenizer's breaks reconciled with the normalization (a sentence-final abbreviation
    ///     ends its sentence; a title never does). Never null; empty when the text has at most
    ///     one sentence.
    /// </summary>
    public IReadOnlyList<int> SentenceBreaks { get; }

    /// <summary>
    ///     Gets the annotations in text order (a token's part of speech before the replacement or
    ///     pronunciation that starts inside it); empty when nothing was selected or nothing could
    ///     be annotated.
    /// </summary>
    public IReadOnlyList<SegmentAnnotation> Annotations { get; }

    /// <summary>
    ///     Gets every normalization in text order: the same spans as the
    ///     <see cref="TextEnAnnotations.Normalized"/> and <see cref="TextEnAnnotations.SpellOut"/>
    ///     annotations (a pronunciation override is an annotation but not a replacement, so it is
    ///     not listed); empty when normalization was not selected.
    /// </summary>
    public IReadOnlyList<AnalyzedNormalization> Normalizations { get; }
}
