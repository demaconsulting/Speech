using System.Globalization;
using System.Text;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;
using DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;
using DemaConsulting.Speech.Proofreader.Synthesis;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

namespace DemaConsulting.Speech.Proofreader.Diagnostics;

/// <summary>
///     What the English front end made of one sentence, and which phonemes the model consumed.
/// </summary>
/// <remarks>
///     A listener hearing a wrong word needs to tell a front-end error (wrong normalization, wrong
///     heteronym reading, a word the lexicon does not know) from a voice error. This snapshot shows
///     the Text.En analysis (tokens with readings and confidence, normalizations, overrides, raw
///     annotations) and, for the phoneme-based models, the phonemes of every text the backend
///     received, re-rendered by the tool with the same renderer configuration the model's pipeline
///     uses. Immutable once built.
/// </remarks>
internal sealed class SentenceDiagnostics
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SentenceDiagnostics"/> class.
    /// </summary>
    /// <param name="analysisSource">A caption saying which analyzer produced the analysis.</param>
    /// <param name="analyzedText">The text that was analyzed (the sentence after the model's text normalization).</param>
    /// <param name="analysis">The detailed analysis, or <see langword="null"/> when it failed.</param>
    /// <param name="analysisError">The reason the analysis failed, or <see langword="null"/>.</param>
    /// <param name="phonemes">The phoneme pane content; see <see cref="Phonemes"/>.</param>
    public SentenceDiagnostics(
        string analysisSource,
        string analyzedText,
        EnglishTextAnalysis? analysis,
        string? analysisError,
        PhonemeDiagnostics phonemes)
    {
        AnalysisSource = analysisSource;
        AnalyzedText = analyzedText;
        Analysis = analysis;
        AnalysisError = analysisError;
        Phonemes = phonemes;
    }

    /// <summary>Gets a caption saying which analyzer produced the analysis.</summary>
    public string AnalysisSource { get; }

    /// <summary>Gets the text that was analyzed.</summary>
    public string AnalyzedText { get; }

    /// <summary>Gets the detailed analysis, or <see langword="null"/> when it failed.</summary>
    public EnglishTextAnalysis? Analysis { get; }

    /// <summary>Gets the reason the analysis failed, or <see langword="null"/>.</summary>
    public string? AnalysisError { get; }

    /// <summary>Gets the phoneme pane content.</summary>
    public PhonemeDiagnostics Phonemes { get; }

    /// <summary>
    ///     Formats the analysis pane: tokens with readings, normalizations, overrides, and raw annotations.
    /// </summary>
    /// <returns>Monospaced plain text.</returns>
    public string FormatAnalysis()
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"{AnalysisSource}\n");
        if (Analysis is null)
        {
            text.Append(AnalysisError ?? "No analysis is available.").Append('\n');
            return text.ToString();
        }

        text.Append("\nNORMALIZATIONS (source -> spoken)\n");
        if (Analysis.Normalizations.Count == 0)
        {
            text.Append("  none\n");
        }

        foreach (var normalization in Analysis.Normalizations)
        {
            var spelled = normalization.IsSpelled ? " [spelled out]" : string.Empty;
            text.Append(CultureInfo.InvariantCulture, $"  {normalization.Text}  ->  {normalization.SpokenText}   ({normalization.Kind}){spelled}\n");
        }

        text.Append("\nREADINGS (heteronyms)\n");
        var readings = Analysis.Tokens.Where(token => token.Reading is not null).ToList();
        if (readings.Count == 0)
        {
            text.Append("  none\n");
        }

        foreach (var token in readings)
        {
            var low = token.IsLowConfidenceReading ? "  LOW CONFIDENCE" : string.Empty;
            text.Append(CultureInfo.InvariantCulture, $"  {token.Text,-14} {token.Reading,-22} {token.ReadingConfidence:0.00}{low}\n");
        }

        text.Append("\nPRONUNCIATION OVERRIDES (en.pron)\n");
        var overrides = Analysis.Annotations.Where(IsOverride).ToList();
        if (overrides.Count == 0)
        {
            text.Append("  none\n");
        }

        foreach (var annotation in overrides)
        {
            text.Append(CultureInfo.InvariantCulture, $"  {Slice(annotation)}  =  {annotation.Value}\n");
        }

        text.Append("\nTOKENS (word, part of speech when tagged, clitic when split)\n");
        foreach (var token in Analysis.Tokens)
        {
            var clitic = token.Clitic == default ? string.Empty : $"  clitic: {token.Clitic}";
            text.Append(CultureInfo.InvariantCulture, $"  {token.Text,-14} {token.PartOfSpeech ?? "-",-6}{clitic}\n");
        }

        text.Append("\nANNOTATIONS (kind [start,end) text = value)\n");
        foreach (var annotation in Analysis.Annotations)
        {
            text.Append(CultureInfo.InvariantCulture, $"  {annotation.Kind,-20} [{annotation.Start},{annotation.End}) {Slice(annotation)} = {annotation.Value}\n");
        }

        return text.ToString();
    }

    /// <summary>
    ///     Formats the phoneme pane.
    /// </summary>
    /// <returns>Monospaced plain text.</returns>
    public string FormatPhonemes() => Phonemes.Format();

    /// <summary>
    ///     Summarizes the heteronym readings that apply to a span of the sentence.
    /// </summary>
    /// <param name="spanStart">The span start within the sentence, or 0 with a zero length for the whole sentence.</param>
    /// <param name="spanLength">The span length, or 0 for the whole sentence.</param>
    /// <returns><c>word=reading(confidence)</c> pairs joined by <c>; </c>, or an empty string.</returns>
    public string ReadingsFor(int spanStart, int spanLength)
    {
        if (Analysis is null)
        {
            return string.Empty;
        }

        var whole = spanLength <= 0;
        return string.Join(
            "; ",
            Analysis.Tokens
                .Where(token => token.Reading is not null && (whole || Overlaps(token, spanStart, spanLength)))
                .Select(token => string.Create(CultureInfo.InvariantCulture, $"{token.Text}={token.Reading}({token.ReadingConfidence:0.00})")));
    }

    /// <summary>
    ///     Tests whether an annotation carries a pronunciation override.
    /// </summary>
    /// <remarks>
    ///     Normalizations and spell-outs are shown in their own section; an override is an
    ///     <c>en.pron</c> annotation that the analyzer's override table produced.
    /// </remarks>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> for an <c>en.pron</c> annotation.</returns>
    private static bool IsOverride(SegmentAnnotation annotation) =>
        TextEnAnnotations.TryReadPronunciation(annotation, out _, out _);

    /// <summary>
    ///     Tests whether a token overlaps a span.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="spanStart">The span start.</param>
    /// <param name="spanLength">The span length.</param>
    /// <returns><see langword="true"/> when the ranges intersect.</returns>
    private static bool Overlaps(AnalyzedToken token, int spanStart, int spanLength) =>
        token.Start < spanStart + spanLength && token.Start + token.Length > spanStart;

    /// <summary>
    ///     Gets the analyzed text an annotation covers.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The covered text, or an empty string when the range is outside the text.</returns>
    private string Slice(SegmentAnnotation annotation) =>
        annotation.End <= AnalyzedText.Length ? AnalyzedText.Substring(annotation.Start, annotation.Length) : string.Empty;
}

/// <summary>
///     The phoneme view of a sentence: what each backend call received and how it phonemizes.
/// </summary>
internal sealed class PhonemeDiagnostics
{
    /// <summary>Gets the pane caption explaining what is shown (or why nothing is).</summary>
    public string Caption { get; init; } = string.Empty;

    /// <summary>Gets one entry per backend call.</summary>
    public IReadOnlyList<PhonemeSegmentInfo> Segments { get; init; } = [];

    /// <summary>
    ///     Gets the rendered phoneme string(s) of every segment on one line, parts separated by <c> | </c>.
    /// </summary>
    public string Summary => string.Join(" | ", Segments.SelectMany(segment => segment.RenderedParts));

    /// <summary>
    ///     Formats the pane.
    /// </summary>
    /// <returns>Monospaced plain text.</returns>
    public string Format()
    {
        var text = new StringBuilder(Caption).Append('\n');
        var number = 0;
        foreach (var segment in Segments)
        {
            number++;
            text.Append(CultureInfo.InvariantCulture, $"\nBACKEND CALL {number}  (speaker {segment.SpeakerId}, speed {segment.Speed:0.##}, {segment.AnnotationCount} annotations)\n");
            text.Append("  text: ").Append(segment.Text).Append('\n');
            for (var part = 0; part < segment.RenderedParts.Count; part++)
            {
                text.Append(CultureInfo.InvariantCulture, $"  phonemes[{part + 1}/{segment.RenderedParts.Count}]: {segment.RenderedParts[part]}\n");
            }

            foreach (var word in segment.Words)
            {
                var oov = word.Oov ? "  OOV (letter-to-sound guess)" : string.Empty;
                var reading = word.ReadingId is null ? string.Empty : $"  reading={word.ReadingId}";
                text.Append(CultureInfo.InvariantCulture, $"    {word.Text,-14} {word.Arpabet,-28} {word.Source}{reading}{oov}\n");
            }

            if (segment.OovWords.Count > 0)
            {
                text.Append("  OOV words: ").Append(string.Join(", ", segment.OovWords)).Append('\n');
            }

            if (segment.UnsupportedTokens.Count > 0)
            {
                text.Append("  unsupported tokens: ").Append(string.Join(", ", segment.UnsupportedTokens)).Append('\n');
            }
        }

        return text.ToString();
    }
}

/// <summary>
///     One word of a phonemized backend call.
/// </summary>
/// <param name="Text">The word.</param>
/// <param name="Arpabet">The ARPAbet phones the phonemizer chose.</param>
/// <param name="Source">Where the pronunciation came from.</param>
/// <param name="ReadingId">The heteronym reading id that selected it, or <see langword="null"/>.</param>
/// <param name="Oov">Whether the word was not in the lexicon and was guessed by letter-to-sound rules.</param>
internal sealed record PhonemeWordInfo(string Text, string Arpabet, PronunciationSource Source, string? ReadingId, bool Oov);

/// <summary>
///     The phonemes of one backend call.
/// </summary>
/// <param name="Text">The text the backend received.</param>
/// <param name="SpeakerId">The speaker id.</param>
/// <param name="Speed">The speed ratio.</param>
/// <param name="AnnotationCount">The number of annotations that came with the text.</param>
/// <param name="RenderedParts">The rendered phoneme string(s); a long text is split between words.</param>
/// <param name="Words">The per-word phonemizer result.</param>
/// <param name="OovWords">The words the lexicon does not know.</param>
/// <param name="UnsupportedTokens">The tokens the phonemizer could not handle.</param>
internal sealed record PhonemeSegmentInfo(
    string Text,
    int SpeakerId,
    float Speed,
    int AnnotationCount,
    IReadOnlyList<string> RenderedParts,
    IReadOnlyList<PhonemeWordInfo> Words,
    IReadOnlyList<string> OovWords,
    IReadOnlyList<string> UnsupportedTokens);

/// <summary>
///     Builds <see cref="SentenceDiagnostics"/> snapshots.
/// </summary>
/// <remarks>
///     The renderer configurations mirror the private phoneme pipelines of the Kokoro model
///     (<c>KokoroPhonemePipeline</c>: Misaki US with weak forms, parts of at most 510 symbols) and the
///     Piper model (<c>PiperPhonemePipeline</c>: eSpeak IPA en-US, parts of at most 384 symbols). They
///     are re-created here because those pipelines are internal; what is not reproducible is a
///     voice's own handling of symbols it does not know. Calls into the phonemizer are serialized because the
///     phonemizer instance is shared. Safe for concurrent callers.
/// </remarks>
internal sealed class DiagnosticsBuilder
{
    /// <summary>The Kokoro token limit per rendered part (mirrors <c>KokoroPhonemeVocabulary.MaxPhonemeTokens</c>).</summary>
    private const int KokoroMaxSymbols = 510;

    /// <summary>The Piper symbol limit per rendered part (mirrors <c>PiperPhonemePipeline.MaxPartSymbols</c>).</summary>
    private const int PiperMaxSymbols = 384;

    /// <summary>The analyzer used for models that bring none of their own.</summary>
    private readonly EnglishTextAnalyzer _fallbackAnalyzer;

    /// <summary>The shared phonemizer; guarded by <see cref="_phonemizerSync"/>.</summary>
    private readonly EnglishPhonemizer _phonemizer = new();

    /// <summary>Serializes use of <see cref="_phonemizer"/>.</summary>
    private readonly object _phonemizerSync = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagnosticsBuilder"/> class.
    /// </summary>
    /// <param name="fallbackAnalyzer">The analyzer to use for models without one. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fallbackAnalyzer"/> is <see langword="null"/>.</exception>
    public DiagnosticsBuilder(EnglishTextAnalyzer fallbackAnalyzer)
    {
        ArgumentNullException.ThrowIfNull(fallbackAnalyzer);
        _fallbackAnalyzer = fallbackAnalyzer;
    }

    /// <summary>
    ///     Builds the diagnostics for a sentence.
    /// </summary>
    /// <param name="model">The real model the sentence is (or will be) spoken with.</param>
    /// <param name="sentenceText">The sentence text.</param>
    /// <param name="audio">The synthesized audio with the recorded backend input, or <see langword="null"/> before synthesis.</param>
    /// <returns>The diagnostics snapshot. Never throws for analysis problems; they are reported in the snapshot.</returns>
    public SentenceDiagnostics Build(ISynthesisModel model, string sentenceText, SentenceAudio? audio)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(sentenceText);

        // The library normalizes the model-specific text first and analyzes that, so do the same.
        var analyzedText = model.NormalizeText(sentenceText);
        var ownAnalyzer = model.TextAnalyzer as EnglishTextAnalyzer;
        var source = ownAnalyzer is not null
            ? $"Analysis by the model's own text analyzer ({model.DisplayName})."
            : "Analysis by the tool's analyzer (this model has no English text analyzer; it speaks without readings or normalization annotations).";

        EnglishTextAnalysis? analysis = null;
        string? error = null;
        try
        {
            analysis = (ownAnalyzer ?? _fallbackAnalyzer).AnalyzeDetailed(analyzedText, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Intentionally broad: diagnostics are an aid and must never break proofreading.
            error = $"The analysis failed: {ex.Message}";
        }

        return new SentenceDiagnostics(source, analyzedText, analysis, error, BuildPhonemes(model, audio));
    }

    /// <summary>
    ///     Builds the phoneme pane for the backend calls recorded with a sentence's audio.
    /// </summary>
    /// <param name="model">The real model.</param>
    /// <param name="audio">The audio, or <see langword="null"/> before synthesis.</param>
    /// <returns>The phoneme diagnostics.</returns>
    private PhonemeDiagnostics BuildPhonemes(ISynthesisModel model, SentenceAudio? audio)
    {
        var renderer = model switch
        {
            OnnxKokoroEnglishSynthesisModel => (Func<PhonemizedText, IReadOnlyList<string>>?)(text =>
                new MisakiRenderer(MisakiProfile.Us, useWeakForms: true).RenderParts(text, KokoroMaxSymbols)),
            OnnxPiperLibriTtsEnSynthesisModel => text =>
                new EspeakIpaRenderer(EspeakIpaProfile.EnUs).RenderParts(text, PiperMaxSymbols),
            _ => null,
        };

        if (renderer is null)
        {
            return new PhonemeDiagnostics
            {
                Caption = $"{model.DisplayName} phonemizes internally; the tool cannot show its phoneme string.",
            };
        }

        if (audio is null || !audio.IsSuccess)
        {
            return new PhonemeDiagnostics
            {
                Caption = "Phonemes appear once the sentence has been synthesized (play it first).",
            };
        }

        var segments = new List<PhonemeSegmentInfo>();
        foreach (var recorded in audio.Segments)
        {
            lock (_phonemizerSync)
            {
                var phonemized = _phonemizer.Phonemize(recorded.Text, recorded.Annotations);
                var words = phonemized.Items
                    .Where(item => item.Kind == PhonemizedItemKind.Word)
                    .Select(item => new PhonemeWordInfo(item.Text, item.Arpabet, item.Source, item.ReadingId, item.Oov))
                    .ToList();
                segments.Add(new PhonemeSegmentInfo(
                    recorded.Text,
                    recorded.SpeakerId,
                    recorded.Speed,
                    recorded.Annotations.Count,
                    renderer(phonemized),
                    words,
                    phonemized.OovWords,
                    phonemized.UnsupportedTokens));
            }
        }

        return new PhonemeDiagnostics
        {
            Caption = $"Phonemes of every text {model.DisplayName} received, re-rendered by the tool with the model's renderer configuration (unknown-symbol handling of the voice is not reproduced).",
            Segments = segments,
        };
    }
}
