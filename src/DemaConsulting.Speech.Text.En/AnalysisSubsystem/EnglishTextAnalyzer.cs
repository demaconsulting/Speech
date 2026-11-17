using System.Text;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.NormalizationSubsystem;

namespace DemaConsulting.Speech.Text.En.AnalysisSubsystem;

/// <summary>
///     An <see cref="ITextAnalyzer"/> for English: it tags the word tokens of a speech request with
///     their Universal Dependencies part of speech, describes how abbreviations, symbols, initialisms,
///     numbers, currency, units, times and dates are spoken, and supplies authoritative sentence
///     boundaries, so a
///     phoneme-driven synthesis model can use sentence-level context the library's chunking would
///     otherwise remove.
/// </summary>
/// <remarks>
///     A model package returns an instance from <c>ISynthesisModel.TextAnalyzer</c>; the session
///     then runs <see cref="Analyze"/> once per request over the whole text, in which every audio
///     tag is one <see cref="TextAnalysisConventions.TagPlaceholder"/>.
///     <para>
///     Placeholders are neutralized to a same-length space for tokenizing, tagging and
///     normalizing only, so every offset still indexes the exact input, a placeholder is never
///     part of a word or an annotation, and the words on both sides of a tag are analyzed as one
///     sentence (context is carried across the tag). The part of speech comes from the shared
///     <c>Tagger.Default</c>, which is created lazily at most once per process; constructing an
///     analyzer, analyzing an empty text, and not selecting part-of-speech annotations all leave
///     it unloaded. Its first use inflates about 1.3 MB of embedded data, so the first request
///     that needs tags is slower than the rest. Tagging runs on the raw tokens, never on
///     normalized text, so the tags are those the tagger was trained on.
///     </para>
///     <para>
///     Normalization (selected by default) is an annotation over the original text, never a
///     rewrite: <see cref="TextEnAnnotations.Normalized"/> annotations name the spoken form of a
///     span, <see cref="TextEnAnnotations.SpellOut"/> annotations name letters to be spoken by
///     letter name (initialisms such as <c>FBI</c>), and the sentence breaks are reconciled so an
///     abbreviation period ends a sentence only where the abbreviation is its last word. It needs
///     no tagger. A host-supplied <see cref="PronunciationOverrides"/> dictionary
///     (<see cref="EnglishTextAnalyzerOptions.Overrides"/>) is applied by the same pass and beats
///     every built-in rule; its pronunciation entries become
///     <see cref="TextEnAnnotations.Pronunciation"/> annotations.
///     </para>
///     <para>
///     Instances are immutable and thread-safe: one analyzer may serve any number of concurrent
///     requests, and none keeps a reference to the analyzed text or to a result after the call
///     returns. The pinned tagger package (0.1.0-beta.5 or later) clears the working buffers in which
///     it keeps the characters of the tokens it tags on every entry point, including when an
///     exception is thrown (upstream issue
///     <see href="https://github.com/demaconsulting/PartOfSpeech/issues/29">demaconsulting/PartOfSpeech#29</see>),
///     so the core <c>ITextAnalyzer</c> contract that an analyzer must not retain the text is met.
///     Managed strings that the tagger package itself creates and the result objects it returns are
///     outside what it can clear; this library keeps neither.
///     </para>
/// </remarks>
public sealed class EnglishTextAnalyzer : ITextAnalyzer
{
    /// <summary>The tag names whose tokens are not words and therefore receive no annotation.</summary>
    private const string PunctuationTag = "PUNCT";

    /// <summary>The tag name of a symbol token, which receives no annotation.</summary>
    private const string SymbolTag = "SYM";

    /// <summary>The tag name of a numeral token, which is not counted as an unknown word.</summary>
    private const string NumeralTag = "NUM";

    /// <summary>The seam over the part-of-speech package.</summary>
    private readonly PartOfSpeechAdapter _adapter;

    /// <summary>The normalizer to use, or <see langword="null"/> for the shared default (resolved on first use, so construction loads no lexicon).</summary>
    private readonly TextNormalizer? _normalizer;

    /// <summary>The aggregate-counts reporter.</summary>
    private readonly AnalysisDiagnostics _diagnostics;

    /// <summary>Whether this analyzer tags tokens and emits part-of-speech annotations.</summary>
    private readonly bool _tagPartOfSpeech;

    /// <summary>Whether this analyzer normalizes and reconciles sentence breaks.</summary>
    private readonly bool _normalize;

    /// <summary>The host-supplied overrides, or <see langword="null"/> when there are none (an empty dictionary is stored as none); immutable, so sharing the reference is the snapshot.</summary>
    private readonly PronunciationOverrides? _overrides;

    /// <summary>
    ///     Initializes a new instance with the default options: part-of-speech and normalization
    ///     annotations on and no diagnostics sink.
    /// </summary>
    public EnglishTextAnalyzer()
        : this(new EnglishTextAnalyzerOptions())
    {
    }

    /// <summary>
    ///     Initializes a new instance with the given options.
    /// </summary>
    /// <param name="options">
    ///     The options; their values are copied now, so later changes to a derived copy never
    ///     affect this analyzer. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     <paramref name="options"/> carries a non-empty override dictionary but does not select
    ///     <see cref="TextEnAnnotationSelection.Normalization"/>.
    /// </exception>
    public EnglishTextAnalyzer(EnglishTextAnalyzerOptions options)
        : this(options, PartOfSpeechAdapter.Shared)
    {
    }

    /// <summary>
    ///     Initializes a new instance over a specific part-of-speech adapter, so tests can count
    ///     tagger creations without touching the process-wide tagger.
    /// </summary>
    /// <param name="options">The options. Must not be null.</param>
    /// <param name="adapter">The part-of-speech seam to use. Must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="adapter"/> is null.</exception>
    internal EnglishTextAnalyzer(EnglishTextAnalyzerOptions options, PartOfSpeechAdapter adapter)
        : this(options, adapter, null)
    {
    }

    /// <summary>
    ///     Initializes a new instance over a specific adapter and normalizer, so tests can observe
    ///     and cancel inside normalization.
    /// </summary>
    /// <param name="options">The options. Must not be null.</param>
    /// <param name="adapter">The part-of-speech seam to use. Must not be null.</param>
    /// <param name="normalizer">The normalizer to use, or <see langword="null"/> for the shared default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="adapter"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     <paramref name="options"/> carries a non-empty override dictionary but does not select
    ///     <see cref="TextEnAnnotationSelection.Normalization"/>, which is what applies overrides.
    /// </exception>
    internal EnglishTextAnalyzer(EnglishTextAnalyzerOptions options, PartOfSpeechAdapter adapter, TextNormalizer? normalizer)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(adapter);

        // Snapshot everything needed now so the analyzer is immutable from here on.
        _adapter = adapter;
        _normalizer = normalizer;
        _diagnostics = new AnalysisDiagnostics(options.Diagnostics);
        _tagPartOfSpeech = options.Annotations.HasFlag(TextEnAnnotationSelection.PartOfSpeech);
        _normalize = options.Annotations.HasFlag(TextEnAnnotationSelection.Normalization);
        _overrides = options.Overrides is { Count: > 0 } overrides ? overrides : null;

        // Overrides are applied by the normalization pass; silently ignoring them would hide a
        // configuration mistake, so the combination is rejected up front.
        if (_overrides is not null && !_normalize)
        {
            throw new ArgumentException("Pronunciation overrides require the Normalization annotation family to be selected.", nameof(options));
        }
    }

    /// <summary>
    ///     Analyzes the complete text of one speech request.
    /// </summary>
    /// <param name="text">
    ///     The text to analyze; each <see cref="TextAnalysisConventions.TagPlaceholder"/> stands
    ///     for one audio tag. Must not be null; not retained after the call returns.
    /// </param>
    /// <param name="cancellationToken">
    ///     Cancels the analysis; checked before any work, after the tagger is obtained, after each
    ///     sentence is tagged, periodically while the text is normalized and while the result is
    ///     built, so a canceled request never returns a result or reports diagnostics, wherever
    ///     the cancellation arrives.
    /// </param>
    /// <returns>
    ///     The selected annotations, with offsets relative to the exact <paramref name="text"/>,
    ///     and always a non-null list of sentence breaks (the analyzer's authoritative opinion,
    ///     which may be empty). An empty text yields no annotations and no breaks.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    /// <remarks>
    ///     See <see cref="AnalyzeDetailed"/> for the same analysis with the tokens and confidences
    ///     included. Thread-safe; reports one counts-only event to the optional diagnostics sink.
    /// </remarks>
    public TextAnalysisResult Analyze(string text, CancellationToken cancellationToken)
    {
        // This is the path every synthesis request takes, so it runs the shared core without a
        // detailed-token list: no AnalyzedToken and no per-token text copy is ever built.
        var (annotations, sentenceBreaks) = AnalyzeCore(text, detailedTokens: null, detailedNormalizations: null, cancellationToken);
        return new TextAnalysisResult(annotations, sentenceBreaks);
    }

    /// <summary>
    ///     Analyzes a text like <see cref="Analyze"/> and returns the rich, local-only result:
    ///     every token with its tag, confidence, known-word flag and clitic role, every
    ///     normalization, plus the sentence breaks and annotations.
    /// </summary>
    /// <param name="text">
    ///     The text to analyze; see <see cref="Analyze"/>. Must not be null; not retained.
    /// </param>
    /// <param name="cancellationToken">Cancels the analysis cooperatively.</param>
    /// <returns>
    ///     The analysis; its tokens carry the words themselves, so it must not be forwarded to a
    ///     diagnostics sink.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    /// <remarks>
    ///     When part-of-speech annotation is not selected, the text is only split (which needs no
    ///     tagger): tokens then have a null <see cref="AnalyzedToken.PartOfSpeech"/>, no part of
    ///     speech annotations are produced, and the sentence breaks are still supplied. Thread-safe.
    /// </remarks>
    public EnglishTextAnalysis AnalyzeDetailed(string text, CancellationToken cancellationToken)
    {
        // Run the shared core, asking it to materialize every token and normalization as it walks them.
        var tokens = new List<AnalyzedToken>();
        var normalizations = new List<AnalyzedNormalization>();
        var (annotations, sentenceBreaks) = AnalyzeCore(text, tokens, normalizations, cancellationToken);
        return new EnglishTextAnalysis(tokens.AsReadOnly(), sentenceBreaks, annotations, normalizations.AsReadOnly());
    }

    /// <summary>
    ///     Runs the one analysis both public entry points share, so their annotations, sentence
    ///     breaks, cancellation checkpoints, diagnostics counts and thread-safety cannot diverge,
    ///     while letting <see cref="Analyze"/> skip the per-token objects only
    ///     <see cref="AnalyzeDetailed"/> needs.
    /// </summary>
    /// <param name="text">
    ///     The text to analyze. Must not be null; not retained after the call returns.
    /// </param>
    /// <param name="detailedTokens">
    ///     When not null, a list that receives one <see cref="AnalyzedToken"/> (including its
    ///     text copy and clitic role) per token; when null, no token object and no text copy is
    ///     created and clitics are not classified.
    /// </param>
    /// <param name="detailedNormalizations">
    ///     When not null, a list that receives one <see cref="AnalyzedNormalization"/> per
    ///     normalization; null exactly when <paramref name="detailedTokens"/> is null.
    /// </param>
    /// <param name="cancellationToken">Cancels the analysis cooperatively.</param>
    /// <returns>
    ///     The read-only annotations and sentence breaks (never null), built from the same walk
    ///     whatever the detailed lists are.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    /// <remarks>
    ///     Keeps no state beyond the call (the only mutation is of the caller's own detailed
    ///     lists), so it is thread-safe; reports one counts-only event to the optional diagnostics
    ///     sink.
    /// </remarks>
    private (IReadOnlyList<SegmentAnnotation> Annotations, IReadOnlyList<int> SentenceBreaks) AnalyzeCore(
        string text,
        List<AnalyzedToken>? detailedTokens,
        List<AnalyzedNormalization>? detailedNormalizations,
        CancellationToken cancellationToken)
    {
        // Reject bad input and an already-canceled operation before any work, including before
        // the tagger could be loaded.
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();

        // Step 1: make tag placeholders opaque separators without moving a single offset.
        var analysisText = NeutralizePlaceholders(text);

        // Step 2: split (and, when selected, tag) the neutralized text; offsets index both the
        // neutralized text and, because the lengths are equal, the source. Tagging deliberately
        // sees the raw tokens: the tagger was trained on them and normalized text tags worse.
        var sentences = _adapter.Analyze(analysisText, _tagPartOfSpeech, cancellationToken);

        // Step 3: normalize over the same tokens, independently of tagging. Clitic roles are
        // public detail, so only the detailed path pays for classifying them. User overrides are
        // resolved to token directives first (one pass over the tokens, no per-token object).
        TokenDirective[] directives = [];
        AlignedText? aligned = null;
        if (_normalize)
        {
            var normalizationTokens = FlattenTokens(sentences);
            if (_overrides is not null)
            {
                directives = _overrides.Resolve(analysisText, normalizationTokens, cancellationToken);
            }

            aligned = (_normalizer ?? TextNormalizer.Default).Normalize(analysisText, normalizationTokens, classifyClitics: detailedTokens is not null, cancellationToken, directives);
        }

        var spans = aligned?.Spans;
        var pronunciations = aligned?.Pronunciations;
        var clitics = aligned?.Clitics;
        var spanIndex = 0;
        var pronunciationIndex = 0;
        var cliticIndex = 0;

        // Step 4: build the tokens, annotations and tokenizer sentence breaks from the source text.
        var annotations = new List<SegmentAnnotation>();
        var tokenizerBreaks = new List<int>();
        var tokenCount = 0;
        var unknownWords = 0;
        for (var sentenceIndex = 0; sentenceIndex < sentences.Count; sentenceIndex++)
        {
            var sentence = sentences[sentenceIndex];
            cancellationToken.ThrowIfCancellationRequested();

            // A sentence ends immediately before the first token of the next one; the first
            // sentence has no break before it.
            if (sentenceIndex > 0)
            {
                tokenizerBreaks.Add(sentence[0].Start);
            }

            foreach (var token in sentence)
            {
                // Building the result is part of the request: look at the token periodically.
                if (tokenCount % PartOfSpeechAdapter.CancellationCheckInterval == PartOfSpeechAdapter.CancellationCheckInterval - 1)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                // Only the detailed path pays for the token object and its text copy.
                if (detailedTokens is not null)
                {
                    var clitic = CliticKind.None;
                    if (clitics is not null && cliticIndex < clitics.Count && clitics[cliticIndex].Start == token.Start)
                    {
                        clitic = clitics[cliticIndex].Kind;
                        cliticIndex++;
                    }

                    detailedTokens.Add(new AnalyzedToken(
                        token.Start,
                        token.Length,
                        text.Substring(token.Start, token.Length),
                        token.Tag,
                        token.Confidence,
                        token.IsKnownWord)
                    {
                        Clitic = clitic,
                    });
                }

                tokenCount++;

                // The token's part of speech first (punctuation, symbols, untagged tokens and any token
                // without a letter or digit have none: the tagger labels a lone "*", "~" or invisible
                // character as a word in some contexts, but such a token speaks nothing of its own and
                // must not make a clause of it look like content to the Speech library's chunker).
                if (token.Tag is not null && token.Tag is not (PunctuationTag or SymbolTag)
                    && !HasNoLetterOrDigit(text, token.Start, token.Length))
                {
                    if (!token.IsKnownWord && token.Tag != NumeralTag)
                    {
                        unknownWords++;
                    }

                    // Never annotate half of a surrogate pair; the tokenizer does not split one, but
                    // an annotation that did would no longer name valid text.
                    if (!SplitsSurrogatePair(text, token.Start, token.Length))
                    {
                        annotations.Add(new SegmentAnnotation(token.Start, token.Length, TextEnAnnotations.PartOfSpeech, token.Tag));
                    }
                }

                // Then every replacement (normalization or spelling) that starts inside this token, and
                // then every pronunciation, so the annotations stay in text order with the part of
                // speech first at equal starts. A token carries either a replacement or a
                // pronunciation, never both.
                while (spans is not null && spanIndex < spans.Count && spans[spanIndex].Start < token.Start + token.Length)
                {
                    AddNormalization(text, spans[spanIndex], annotations, detailedNormalizations);
                    spanIndex++;
                }

                while (pronunciations is not null && pronunciationIndex < pronunciations.Count && pronunciations[pronunciationIndex].Start < token.Start + token.Length)
                {
                    annotations.Add(new SegmentAnnotation(pronunciations[pronunciationIndex].Start, pronunciations[pronunciationIndex].Length, TextEnAnnotations.Pronunciation, pronunciations[pronunciationIndex].Value));
                    pronunciationIndex++;
                }
            }
        }

        // A span cannot start outside every token, but flush any remainder so none is ever lost.
        while (spans is not null && spanIndex < spans.Count)
        {
            AddNormalization(text, spans[spanIndex], annotations, detailedNormalizations);
            spanIndex++;
        }

        while (pronunciations is not null && pronunciationIndex < pronunciations.Count)
        {
            annotations.Add(new SegmentAnnotation(pronunciations[pronunciationIndex].Start, pronunciations[pronunciationIndex].Length, TextEnAnnotations.Pronunciation, pronunciations[pronunciationIndex].Value));
            pronunciationIndex++;
        }

        // Step 5: the breaks are the tokenizer's, reconciled with the normalization when selected.
        IReadOnlyList<int> sentenceBreaks = aligned is null
            ? tokenizerBreaks.AsReadOnly()
            : aligned.ReconcileBreaks(tokenizerBreaks);

        // Step 6: a request canceled while the result was being built (including during its last
        // sentence) must neither return nor report; then report one aggregate-counts event and
        // return the immutable result.
        cancellationToken.ThrowIfCancellationRequested();
        _diagnostics.ReportRequest(new AnalysisCounts(
            sentenceBreaks.Count + (tokenCount > 0 ? 1 : 0),
            tokenCount,
            annotations.Count,
            unknownWords,
            aligned?.Spans.Count ?? 0,
            aligned?.AmbiguousCount ?? 0,
            aligned?.FallbackCount ?? 0,
            aligned?.OverridesApplied ?? 0,
            aligned?.OverridesIgnored ?? 0));
        return (annotations.AsReadOnly(), sentenceBreaks);
    }

    /// <summary>
    ///     Adds the <see cref="TextEnAnnotations.Normalized"/> or, for a spelled span, the
    ///     <see cref="TextEnAnnotations.SpellOut"/> annotation of one span, and its detailed
    ///     description when requested.
    /// </summary>
    /// <param name="text">The analyzed text.</param>
    /// <param name="span">The span.</param>
    /// <param name="annotations">The annotation list to append to.</param>
    /// <param name="detailedNormalizations">The detailed list to append to, or <see langword="null"/>.</param>
    private static void AddNormalization(string text, NormalizedSpan span, List<SegmentAnnotation> annotations, List<AnalyzedNormalization>? detailedNormalizations)
    {
        annotations.Add(new SegmentAnnotation(span.Start, span.Length, span.Spelled ? TextEnAnnotations.SpellOut : TextEnAnnotations.Normalized, span.Spoken));
        detailedNormalizations?.Add(new AnalyzedNormalization(span.Start, span.Length, text.Substring(span.Start, span.Length), span.Spoken, span.Kind) { IsSpelled = span.Spelled });
    }

    /// <summary>
    ///     Flattens the tokenizer sentences into the one token array the normalizer takes, marking
    ///     the first token of each sentence.
    /// </summary>
    /// <param name="sentences">The sentences, each with at least one token.</param>
    /// <returns>One exact-size array of every token in text order.</returns>
    private static NormalizationToken[] FlattenTokens(IReadOnlyList<IReadOnlyList<AdaptedToken>> sentences)
    {
        var total = 0;
        foreach (var sentence in sentences)
        {
            total += sentence.Count;
        }

        var tokens = new NormalizationToken[total];
        var position = 0;
        foreach (var sentence in sentences)
        {
            for (var i = 0; i < sentence.Count; i++)
            {
                tokens[position++] = new NormalizationToken(sentence[i].Start, sentence[i].Length, i == 0);
            }
        }

        return tokens;
    }

    /// <summary>
    ///     Replaces every <see cref="TextAnalysisConventions.TagPlaceholder"/> with a space so the
    ///     tokenizer treats it as a separator, keeping the text the same length.
    /// </summary>
    /// <param name="text">The text to neutralize.</param>
    /// <returns>
    ///     <paramref name="text"/> itself when it contains no placeholder; otherwise a new string of
    ///     identical length in which each placeholder is a space.
    /// </returns>
    internal static string NeutralizePlaceholders(string text)
    {
        // The common case has no tag, and then no copy is made.
        if (text.IndexOf(TextAnalysisConventions.TagPlaceholder) < 0)
        {
            return text;
        }

        return text.Replace(TextAnalysisConventions.TagPlaceholder, ' ');
    }

    /// <summary>
    ///     Tells whether a span of <paramref name="text"/> would cut a UTF-16 surrogate pair at
    ///     either edge.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The span's start.</param>
    /// <param name="length">The span's length.</param>
    /// <returns><see langword="true"/> when the span starts on a low surrogate that follows a high one, or ends on a high surrogate that precedes a low one.</returns>
    private static bool SplitsSurrogatePair(string text, int start, int length)
    {
        var end = start + length;
        var startsInsidePair = start > 0 && char.IsLowSurrogate(text[start]) && char.IsHighSurrogate(text[start - 1]);
        var endsInsidePair = end < text.Length && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end]);
        return startsInsidePair || endsInsidePair;
    }

    /// <summary>
    ///     Tells whether a span of <paramref name="text"/> has no letter or digit (counted per
    ///     Unicode scalar): it is made only of punctuation, symbols and invisible characters, so
    ///     it is not a word however the tagger labels it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The span's start.</param>
    /// <param name="length">The span's length.</param>
    /// <returns><see langword="true"/> when the span contains no letter or digit; also for an empty span.</returns>
    private static bool HasNoLetterOrDigit(string text, int start, int length)
    {
        // A lone surrogate enumerates as U+FFFD, which is not a letter, like the chunker's check.
        foreach (var rune in text.AsSpan(start, length).EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune))
            {
                return false;
            }
        }

        return true;
    }
}
