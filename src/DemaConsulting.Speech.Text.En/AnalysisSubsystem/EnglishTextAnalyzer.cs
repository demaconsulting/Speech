using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Text.En.AnalysisSubsystem;

/// <summary>
///     An <see cref="ITextAnalyzer"/> for English: it tags the word tokens of a speech request with
///     their Universal Dependencies part of speech and supplies authoritative sentence
///     boundaries, so a phoneme-driven synthesis model can use sentence-level context the
///     library's chunking would otherwise remove.
/// </summary>
/// <remarks>
///     A model package returns an instance from <c>ISynthesisModel.TextAnalyzer</c>; the session
///     then runs <see cref="Analyze"/> once per request over the whole text, in which every audio
///     tag is one <see cref="TextAnalysisConventions.TagPlaceholder"/>.
///     <para>
///     Placeholders are neutralized to a same-length space for tokenizing and tagging only, so
///     every offset still indexes the exact input, a placeholder is never part of a word or an
///     annotation, and the words on both sides of a tag are analyzed as one sentence (context is
///     carried across the tag). The part of speech comes from the shared <c>Tagger.Default</c>,
///     which is created lazily at most once per process; constructing an analyzer, analyzing an
///     empty text, and selecting no annotations all leave it unloaded. Its first use inflates
///     about 1.3 MB of embedded data, so the first request that needs tags is slower than the
///     rest.
///     </para>
///     <para>
///     Instances are immutable and thread-safe: one analyzer may serve any number of concurrent
///     requests, and none keeps a reference to the analyzed text or to a result after the call
///     returns. That statement covers this library's own state only. The tagger package keeps
///     the characters of tokens it tagged in an internal per-thread working buffer that it
///     never clears (upstream issue
///     <see href="https://github.com/demaconsulting/PartOfSpeech/issues/29">demaconsulting/PartOfSpeech#29</see>),
///     so words analyzed on a thread can remain recoverable from the process memory after the
///     request; a host with a strict requirement that no text outlive a request must account
///     for this until the tagger package is fixed.
///     </para>
///     <para>
///     Because the core <c>ITextAnalyzer</c> contract requires an analyzer not to retain the text, this
///     retention is a violation of that contract by the current tagger package. The
///     <c>DemaConsulting.Speech.Text.En</c> package is therefore not packed or published until the
///     tagger package fixes the issue and the version pin is updated.
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

    /// <summary>The aggregate-counts reporter.</summary>
    private readonly AnalysisDiagnostics _diagnostics;

    /// <summary>Whether this analyzer tags tokens and emits part-of-speech annotations.</summary>
    private readonly bool _tagPartOfSpeech;

    /// <summary>
    ///     Initializes a new instance with the default options: part-of-speech annotations on and
    ///     no diagnostics sink.
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
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(adapter);

        // Snapshot everything needed now so the analyzer is immutable from here on.
        _adapter = adapter;
        _diagnostics = new AnalysisDiagnostics(options.Diagnostics);
        _tagPartOfSpeech = options.Annotations.HasFlag(TextEnAnnotationSelection.PartOfSpeech);
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
    ///     sentence is tagged and periodically while the result is built, so a canceled request
    ///     never returns a result or reports diagnostics, wherever the cancellation arrives.
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
        var (annotations, sentenceBreaks) = AnalyzeCore(text, detailedTokens: null, cancellationToken);
        return new TextAnalysisResult(annotations, sentenceBreaks);
    }

    /// <summary>
    ///     Analyzes a text like <see cref="Analyze"/> and returns the rich, local-only result:
    ///     every token with its tag, confidence and known-word flag, plus the sentence breaks and
    ///     annotations.
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
    ///     tagger): tokens then have a null <see cref="AnalyzedToken.PartOfSpeech"/>, no
    ///     annotations are produced, and the sentence breaks are still supplied. Thread-safe.
    /// </remarks>
    public EnglishTextAnalysis AnalyzeDetailed(string text, CancellationToken cancellationToken)
    {
        // Run the shared core, asking it to materialize every token as it walks them.
        var tokens = new List<AnalyzedToken>();
        var (annotations, sentenceBreaks) = AnalyzeCore(text, tokens, cancellationToken);
        return new EnglishTextAnalysis(tokens.AsReadOnly(), sentenceBreaks, annotations);
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
    ///     text copy) per token; when null, no token object and no text copy is created.
    /// </param>
    /// <param name="cancellationToken">Cancels the analysis cooperatively.</param>
    /// <returns>
    ///     The read-only annotations and sentence breaks (never null), built from the same walk
    ///     whatever <paramref name="detailedTokens"/> is.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    /// <remarks>
    ///     Keeps no state beyond the call (the only mutation is of the caller's own
    ///     <paramref name="detailedTokens"/>), so it is thread-safe; reports one counts-only event
    ///     to the optional diagnostics sink.
    /// </remarks>
    private (IReadOnlyList<SegmentAnnotation> Annotations, IReadOnlyList<int> SentenceBreaks) AnalyzeCore(
        string text,
        List<AnalyzedToken>? detailedTokens,
        CancellationToken cancellationToken)
    {
        // Reject bad input and an already-canceled operation before any work, including before
        // the tagger could be loaded.
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();

        // Step 1: make tag placeholders opaque separators without moving a single offset.
        var analysisText = NeutralizePlaceholders(text);

        // Step 2: split (and, when selected, tag) the neutralized text; offsets index both the
        // neutralized text and, because the lengths are equal, the source.
        var sentences = _adapter.Analyze(analysisText, _tagPartOfSpeech, cancellationToken);

        // Step 3: build the tokens, annotations and sentence breaks from the source text.
        var annotations = new List<SegmentAnnotation>();
        var sentenceBreaks = new List<int>();
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
                sentenceBreaks.Add(sentence[0].Start);
            }

            foreach (var token in sentence)
            {
                // Building the result is part of the request: look at the token periodically.
                if (tokenCount % PartOfSpeechAdapter.CancellationCheckInterval == PartOfSpeechAdapter.CancellationCheckInterval - 1)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                // Only the detailed path pays for the token object and its text copy.
                detailedTokens?.Add(new AnalyzedToken(
                    token.Start,
                    token.Length,
                    text.Substring(token.Start, token.Length),
                    token.Tag,
                    token.Confidence,
                    token.IsKnownWord));
                tokenCount++;

                if (token.Tag is null || token.Tag is PunctuationTag or SymbolTag)
                {
                    continue;
                }

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
        }

        // Step 4: a request canceled while the result was being built (including during its last
        // sentence) must neither return nor report; then report one aggregate-counts event and
        // return the immutable result.
        cancellationToken.ThrowIfCancellationRequested();
        _diagnostics.ReportRequest(new AnalysisCounts(sentences.Count, tokenCount, annotations.Count, unknownWords));
        return (annotations.AsReadOnly(), sentenceBreaks.AsReadOnly());
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
}
