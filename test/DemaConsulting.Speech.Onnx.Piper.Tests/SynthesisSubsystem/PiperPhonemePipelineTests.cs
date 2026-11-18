using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.SynthesisSubsystem;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;
using DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.Piper.Tests.SynthesisSubsystem;

/// <summary>
///     Pipeline tests for the text-to-phoneme-ids path of the Piper engine: the model's real English text analysis, the
///     session's own sentence chunking, the real phonemizer and renderer and the real symbol table of the voice (the
///     trimmed copy of its configuration), with no ONNX model involved.
/// </summary>
/// <remarks>
///     Three corpora: the renderer's own Piper golden (<c>piper-ipa-golden.tsv</c>, linked from the Phonemes.En tests:
///     150 sentences, every symbol of which must be in the voice's table and encode to the ids pinned there), and the
///     pipeline golden (<c>piper-pipeline-golden.tsv</c>: the 40 sentences of the Kokoro pipeline corpus, 12 of which
///     carry both readings of a heteronym such as <c>record</c> or <c>wind</c>, run the way a session runs them and
///     pinned with their phoneme string and framed ids). A change in the pipeline golden means the audio changes: review
///     it by listening, not only by reading.
/// </remarks>
public sealed class PiperPhonemePipelineTests
{
    private const char SeparatorOfParts = '|';

    private static readonly PiperVoiceConfig Config = PiperVoiceConfig.Parse(PiperTestFixtures.TrimmedConfigJson);
    private static readonly PiperPhonemePipeline Pipeline = new(Config);
    private static readonly EnglishPhonemizer Phonemizer = new();
    private static readonly EspeakIpaRenderer Renderer = new(EspeakIpaProfile.EnUs);
    private static readonly OnnxPiperLibriTtsEnSynthesisModel Model = new();

    /// <summary>Gets the rows of the renderer's Piper golden as xunit theory data: id, sentence, phonemes, unframed ids.</summary>
    public static TheoryData<string, string, string, string> RendererGoldenRows() => Rows("piper-ipa-golden.tsv");

    /// <summary>Gets the rows of the pipeline golden as xunit theory data: id, sentence, phonemes, framed ids.</summary>
    public static TheoryData<string, string, string, string> PipelineGoldenRows() => Rows("piper-pipeline-golden.tsv");

    /// <summary>
    ///     Proves 100% symbol coverage: every symbol the renderer emits for a sentence of its Piper golden corpus is in the
    ///     voice's symbol table, the encoding reports no unknown symbol, and the ids are the ones the golden pins.
    /// </summary>
    /// <param name="id">The sentence id.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="phonemes">The phoneme string the renderer produced for the sentence.</param>
    /// <param name="ids">The unframed ids pinned by the renderer's golden.</param>
    [Theory]
    [MemberData(nameof(RendererGoldenRows))]
    public void Encode_RendererGoldenSentence_CoversEverySymbolWithThePinnedIds(string id, string sentence, string phonemes, string ids)
    {
        // Act
        var utterance = Pipeline.Encode(phonemes);
        var unframed = utterance.Ids.Skip(1).Take(utterance.Ids.Count - 2).Where((_, index) => index % 2 == 0);

        // Assert
        Assert.True(sentence.Length > 0, id);
        Assert.Empty(utterance.UnknownSymbols);
        Assert.Equal(phonemes.EnumerateRunes().Count(), utterance.SymbolCount);
        Assert.Equal(ids, string.Join(' ', unframed));
        var single = Assert.Single(Pipeline.ToUtterances(sentence, Model.TextAnalyzer!.Analyze(sentence, CancellationToken.None).Annotations));
        Assert.Equal(phonemes, single.Phonemes);
    }

    /// <summary>
    ///     Proves the framing: the begin id, each symbol's id followed by the pad id, the end id.
    /// </summary>
    [Fact]
    public void Encode_Phonemes_AreFramedWithBeginPadAndEnd()
    {
        // Arrange
        Config.TryGetIds("h", out var h);
        Config.TryGetIds("ˈ", out var stress);

        // Act
        var utterance = Pipeline.Encode("hˈ");

        // Assert
        Assert.Equal([1L, h[0], 0L, stress[0], 0L, 2L], utterance.Ids);
        Assert.Equal(2, utterance.SymbolCount);
    }

    /// <summary>
    ///     Proves a symbol the voice does not know is skipped but reported (distinct, in first-seen order), never dropped
    ///     silently.
    /// </summary>
    [Fact]
    public void Encode_SymbolsOutsideTheTable_AreReportedAndSkipped()
    {
        // Act
        var utterance = Pipeline.Encode("h\u4E2D\u4E2Dh\u00FF");

        // Assert
        Assert.Equal(["\u4E2D", "\u00FF"], utterance.UnknownSymbols);
        Assert.Equal(2, utterance.SymbolCount);
        Assert.Equal(6, utterance.Ids.Count);
    }

    /// <summary>
    ///     Proves that every sentence of the pipeline golden corpus, run the way a session runs it (analyze the whole text,
    ///     chunk with the annotations, speak each chunk), produces exactly the pinned phoneme string and framed ids.
    /// </summary>
    /// <param name="id">The sentence id.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="phonemes">The pinned phoneme string, chunks separated by <c>|</c>.</param>
    /// <param name="ids">The pinned framed ids, chunks separated by <c>|</c>.</param>
    [Theory]
    [MemberData(nameof(PipelineGoldenRows))]
    public void Pipeline_GoldenSentence_ProducesPinnedPhonemesAndIds(string id, string sentence, string phonemes, string ids)
    {
        // Arrange
        var utterances = AnalyzeAndChunk(sentence)
            .Select(chunk => Assert.Single(Pipeline.ToUtterances(chunk.Text, NullIfEmpty(chunk.Annotations))))
            .ToList();

        // Act
        var actualPhonemes = string.Join($" {SeparatorOfParts} ", utterances.Select(utterance => utterance.Phonemes));
        var actualIds = string.Join($" {SeparatorOfParts} ", utterances.Select(utterance => string.Join(',', utterance.Ids)));

        // Assert
        Assert.True(sentence.Length > 0, id);
        Assert.Equal(phonemes, actualPhonemes);
        Assert.Equal(ids, actualIds);
        Assert.All(utterances, utterance => Assert.Empty(utterance.UnknownSymbols));
    }

    /// <summary>
    ///     Proves that the reading the text analysis chooses for a heteronym reaches the model: in each of the twelve
    ///     heteronym sentences, which carry both readings of the word, the ids differ from the ids of the same text spoken
    ///     with default readings.
    /// </summary>
    /// <param name="id">The sentence id.</param>
    [Theory]
    [InlineData("s01")]
    [InlineData("s02")]
    [InlineData("s03")]
    [InlineData("s04")]
    [InlineData("s05")]
    [InlineData("s06")]
    [InlineData("s07")]
    [InlineData("s08")]
    [InlineData("s09")]
    [InlineData("s10")]
    [InlineData("s11")]
    [InlineData("s12")]
    public void Pipeline_HeteronymSentence_UsesTheChosenReadings(string id)
    {
        // Arrange
        var sentence = GoldenSentence(id);
        var analysis = Model.TextAnalyzer!.Analyze(sentence, CancellationToken.None);

        // Act
        var withReadings = Assert.Single(Pipeline.ToUtterances(sentence, analysis.Annotations));
        var defaultReadings = Assert.Single(Pipeline.ToUtterances(sentence, null));

        // Assert
        Assert.NotEqual(defaultReadings.Ids, withReadings.Ids);
    }

    /// <summary>
    ///     Proves that the noun and the verb <c>record</c> get different phonemes (and so different ids) when the text
    ///     analysis has classified them.
    /// </summary>
    [Fact]
    public void Pipeline_RecordNounAndVerb_GetDifferentIds()
    {
        // Arrange
        var noun = PhonemesOfWord(GoldenSentence("e01"), "record");
        var verb = PhonemesOfWord(GoldenSentence("e02"), "record");

        // Act
        var nounIds = Pipeline.Encode(noun).Ids;
        var verbIds = Pipeline.Encode(verb).Ids;

        // Assert
        Assert.NotEqual(noun, verb);
        Assert.NotEqual(nounIds, verbIds);
    }

    /// <summary>
    ///     Proves that a segment longer than one utterance is split into utterances that each fit the limit, that no text
    ///     is dropped, and that the utterances end after a clause mark when the text has them.
    /// </summary>
    [Fact]
    public void Pipeline_TextBeyondOneUtterance_IsSplitAtClauseBoundariesWithoutLoss()
    {
        // Arrange
        const string sentence = "The cat sat on the mat, and the dog barked.";
        var single = Assert.Single(Pipeline.ToUtterances(sentence, null)).SymbolCount;
        var text = string.Join(' ', Enumerable.Repeat(sentence, 40));
        Config.TryGetIds(",", out var comma);
        Config.TryGetIds(".", out var period);

        // Act
        var utterances = Pipeline.ToUtterances(text, null);

        // Assert
        Assert.True(utterances.Count >= 3, "About 40 sentences of 40 symbols need at least three utterances.");
        Assert.All(utterances, utterance => Assert.InRange(utterance.SymbolCount, 1, PiperPhonemePipeline.MaxPartSymbols));
        Assert.True(utterances.Sum(utterance => utterance.SymbolCount) >= 40 * single, "No word may be dropped.");
        Assert.All(
            utterances.Take(utterances.Count - 1),
            utterance => Assert.Contains(utterance.Ids[^3], comma.Concat(period)));
    }

    /// <summary>
    ///     Proves that text without a sound in this voice produces no utterance and that a null text is rejected.
    /// </summary>
    [Fact]
    public void Pipeline_EmptyOrSoundlessText_ProducesNoUtterancesAndNullIsRejected()
    {
        // Act & Assert
        Assert.Empty(Pipeline.ToUtterances(string.Empty, null));
        Assert.Empty(Pipeline.ToUtterances("\U0001F600", null));
        Assert.Throws<ArgumentNullException>(() => Pipeline.ToUtterances(null!, null));
        Assert.Throws<ArgumentNullException>(() => new PiperPhonemePipeline(null!));
    }

    /// <summary>Reads a tab-separated corpus (a header row, then id, sentence, phonemes, ids) as theory data.</summary>
    private static TheoryData<string, string, string, string> Rows(string fileName)
    {
        var rows = new TheoryData<string, string, string, string>();
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        foreach (var line in File.ReadAllLines(path).Skip(1).Where(line => line.Length > 0))
        {
            var columns = line.Split('\t');
            rows.Add(columns[0], columns[1], columns[2], columns[3]);
        }

        return rows;
    }

    /// <summary>Runs the model's analysis over a text and chunks it with the annotations, as the session does.</summary>
    private static IReadOnlyList<SentenceChunk> AnalyzeAndChunk(string text)
    {
        var analysis = Model.TextAnalyzer!.Analyze(text, CancellationToken.None);
        return SentenceChunker.ChunkAligned(text, analysis.Annotations, analysis.SentenceBreaks);
    }

    /// <summary>Returns the annotations, or null when there are none (the session then calls the plain generation).</summary>
    private static IReadOnlyList<SegmentAnnotation>? NullIfEmpty(IReadOnlyList<SegmentAnnotation> annotations) =>
        annotations.Count > 0 ? annotations : null;

    /// <summary>Returns the sentence of a row of the Kokoro pipeline corpus, which the Piper pipeline golden shares.</summary>
    private static string GoldenSentence(string id)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "kokoro-pipeline-golden.tsv");
        return File.ReadAllLines(path).Select(line => line.Split('\t')).First(columns => columns[0] == id)[1];
    }

    /// <summary>Renders the first word with the given text of an analyzed sentence.</summary>
    private static string PhonemesOfWord(string sentence, string word)
    {
        var analysis = Model.TextAnalyzer!.Analyze(sentence, CancellationToken.None);
        var items = Phonemizer.Phonemize(sentence, analysis.Annotations).Items;
        var pieces = Renderer.RenderItems(items);
        var index = items.ToList().FindIndex(item => string.Equals(item.Text, word, StringComparison.OrdinalIgnoreCase));
        return pieces[index];
    }
}
