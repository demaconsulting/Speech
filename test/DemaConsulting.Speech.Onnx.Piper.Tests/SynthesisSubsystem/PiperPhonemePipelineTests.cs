using DemaConsulting.Speech.ModelManagementSubsystem;
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
    public void PiperPhonemePipeline_Encode_RendererGoldenSentence_CoversEverySymbolWithThePinnedIds(string id, string sentence, string phonemes, string ids)
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
    ///     Proves that the engine's path equals the renderer's path over the whole measured corpus: for each of the 360
    ///     renderer corpus and validation sentences, run the way a session runs them, no symbol is unknown to the voice, and
    ///     the phonemes and ids of every chunk that fits one utterance are exactly what the renderer produces for it.
    /// </summary>
    [Fact]
    public void PiperPhonemePipeline_ToUtterances_RendererCorpusAndValidationSentences_MatchTheRendererPathWithNoUnknownSymbol()
    {
        // Arrange
        var sentences = new[] { "renderer-corpus.tsv", "renderer-validation.tsv" }
            .SelectMany(file => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", file)).Skip(1))
            .Where(line => line.Length > 0)
            .Select(line => line.Split('\t')[1])
            .ToList();
        var compared = 0;

        // Act & Assert
        Assert.Equal(360, sentences.Count);
        foreach (var sentence in sentences)
        {
            foreach (var chunk in AnalyzeAndChunk(sentence))
            {
                var annotations = NullIfEmpty(chunk.Annotations);
                var utterances = Pipeline.ToUtterances(chunk.Text, annotations);
                Assert.All(utterances, utterance => Assert.Empty(utterance.UnknownSymbols));
                if (utterances.Count != 1)
                {
                    continue;
                }

                var expected = Renderer.Render(Phonemizer.Phonemize(chunk.Text, annotations));
                Assert.Equal(expected, utterances[0].Phonemes);
                Assert.Equal(Pipeline.Encode(expected).Ids, utterances[0].Ids);
                compared++;
            }
        }

        Assert.True(compared >= sentences.Count, "Every sentence has at least one chunk that fits one utterance.");
    }

    /// <summary>
    ///     Proves the framing: the begin id, each symbol's id followed by the pad id, the end id.
    /// </summary>
    [Fact]
    public void PiperPhonemePipeline_Encode_Phonemes_AreFramedWithBeginPadAndEnd()
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
    public void PiperPhonemePipeline_Encode_SymbolsOutsideTheTable_AreReportedAndSkipped()
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
    public void PiperPhonemePipeline_ToUtterances_GoldenSentence_ProducesPinnedPhonemesAndIds(string id, string sentence, string phonemes, string ids)
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
    ///     Proves that the sentences of the Gettysburg Address that a listening review of the voice flagged (<i>battle-field</i>
    ///     read as two words, <i>here</i> heard as <i>he</i>, <i>nobly</i> heard as <i>knobbly</i>, a pause inside <i>the last full
    ///     measure of | devotion</i>), run the way a session runs them, produce exactly the pinned phoneme strings: the
    ///     compound has one primary stress, <i>here</i> has the vowel of <i>near</i>, <i>nobly</i> has the vowel of <i>noble</i>, and the long
    ///     sentence is cut at its dashes.
    /// </summary>
    /// <param name="id">The sentence id.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="phonemes">The pinned phoneme strings of the utterances, separated by <c>|</c>.</param>
    [Theory]
    [MemberData(nameof(GettysburgGoldenRows))]
    public void PiperPhonemePipeline_ToUtterances_GettysburgSentence_ProducesPinnedPhonemes(string id, string sentence, string phonemes)
    {
        // Arrange
        var utterances = AnalyzeAndChunk(sentence).SelectMany(chunk => Pipeline.ToUtterances(chunk.Text, NullIfEmpty(chunk.Annotations))).ToList();

        // Act
        var actual = string.Join($" {SeparatorOfParts} ", utterances.Select(utterance => utterance.Phonemes));

        // Assert
        Assert.True(sentence.Length > 0, id);
        Assert.Equal(phonemes, actual);
        Assert.All(utterances, utterance => Assert.Empty(utterance.UnknownSymbols));
        Assert.All(utterances, utterance => Assert.InRange(utterance.SymbolCount, 1, PiperPhonemePipeline.MaxPartSymbols));
    }

    /// <summary>
    ///     Proves that a sentence of more than four hundred characters is cut where a speaker pauses (at its dashes), never
    ///     between a function word and the word it belongs to: <i>of devotion</i> stays in one utterance, no utterance
    ///     ends in a function word, and every utterance but the last ends in a pause mark.
    /// </summary>
    [Fact]
    public void PiperPhonemePipeline_ToUtterances_LongSentenceOfTheAddress_IsCutAtItsDashesNeverBetweenFunctionWordAndHead()
    {
        // Arrange
        var sentence = GettysburgSentence("g04");

        // Act
        var segments = AnalyzeAndChunk(sentence);
        var utterances = segments.SelectMany(chunk => Pipeline.ToUtterances(chunk.Text, NullIfEmpty(chunk.Annotations))).ToList();

        // Assert: the sentence is far over the chunk budget and was cut into several pieces, each within it
        Assert.True(sentence.Length > 400);
        Assert.True(utterances.Count >= 2);
        Assert.All(segments, segment => Assert.InRange(segment.Text.Length, 1, PiperCapabilityProfile.MaxJoinedLength));

        // Assert: "of devotion" is not separated, and no utterance ends in the weak form of a function word
        var devotion = Renderer.Render(Phonemizer.Phonemize("devotion"));
        Assert.Contains(utterances, utterance => utterance.Phonemes.Contains("ʌv " + devotion, StringComparison.Ordinal));
        Assert.All(utterances, utterance => Assert.DoesNotMatch(@"(^|\s)(ʌv|ðə|tə|ðæt|ɐ|ænd|ɪn|wiː)[,.]?$", utterance.Phonemes));

        // Assert: every utterance but the last ends in a pause mark
        Assert.All(utterances.Take(utterances.Count - 1), utterance => Assert.Contains(utterance.Phonemes[^1], ",;:"));
    }

    /// <summary>Gets the rows of the Gettysburg golden as xunit theory data: id, sentence, phonemes of the utterances.</summary>
    public static TheoryData<string, string, string> GettysburgGoldenRows()
    {
        var rows = new TheoryData<string, string, string>();
        foreach (var columns in ReadGettysburg())
        {
            rows.Add(columns[0], columns[1], columns[2]);
        }

        return rows;
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
    public void PiperPhonemePipeline_ToUtterances_HeteronymSentence_UsesTheChosenReadings(string id)
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
    ///     Proves that a segment longer than one utterance is split into utterances that each fit the limit, that no text
    ///     is dropped, and that the utterances end after a clause mark when the text has them.
    /// </summary>
    [Fact]
    public void PiperPhonemePipeline_ToUtterances_TextBeyondOneUtterance_IsSplitAtClauseBoundariesWithoutLoss()
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
    ///     Proves that a sentence with several clauses, joined into one segment by the model's capability profile, is spoken as one
    ///     utterance that keeps its commas in place (the voice was trained on whole sentences with the comma as a symbol), that the
    ///     clause pieces the core chunker made would be several short utterances instead, and that a long sentence is still cut at
    ///     its clause marks into utterances within the engine's own limit with no word lost.
    /// </summary>
    [Fact]
    public void PiperPhonemePipeline_ToUtterances_SentenceWithClauses_IsOneUtteranceWithItsCommasInPlace()
    {
        // Arrange
        const string sentence = "Would you like tea, coffee, or something stronger?";
        var analysis = Model.TextAnalyzer!.Analyze(sentence, CancellationToken.None);
        var pieces = SentenceChunker.ChunkAligned(sentence, analysis.Annotations, analysis.SentenceBreaks);
        Config.TryGetIds(",", out var comma);

        // Act
        var segment = Assert.Single(AnalyzeAndChunk(sentence));
        var utterance = Assert.Single(Pipeline.ToUtterances(segment.Text, NullIfEmpty(segment.Annotations)));

        // Assert: the core chunker alone would speak three utterances; the segment is one, with a comma between the clauses
        Assert.Equal(3, pieces.Count);
        Assert.Equal(2, utterance.Phonemes.Count(c => c == ','));
        Assert.Contains(", ", utterance.Phonemes, StringComparison.Ordinal);
        Assert.Equal(2, utterance.Ids.Count(id => comma.Contains(id)));
        Assert.Empty(utterance.UnknownSymbols);

        // Arrange: a long sentence of many clauses, well over the engine's limit
        var longSentence = string.Join(", ", Enumerable.Repeat("the quick brown fox jumps over the lazy dog", 12)) + ".";

        // Act
        var segments = AnalyzeAndChunk(longSentence);
        var utterances = segments.SelectMany(s => Pipeline.ToUtterances(s.Text, NullIfEmpty(s.Annotations))).ToList();

        // Assert: segments within the core chunker's budget, utterances within the engine's limit, no sound lost
        var whole = Renderer.Render(Phonemizer.Phonemize(longSentence));
        Assert.True(segments.Count >= 2);
        Assert.All(segments, s => Assert.InRange(s.Text.Length, 1, PiperCapabilityProfile.MaxJoinedLength));
        Assert.All(utterances, u => Assert.InRange(u.SymbolCount, 1, PiperPhonemePipeline.MaxPartSymbols));
        Assert.Equal(SoundsOnly(whole), SoundsOnly(string.Concat(utterances.Select(u => u.Phonemes))));
    }

    /// <summary>Removes the spaces, the clause marks and the secondary stress marks of a phoneme string (the first word of an utterance carries a mark the same word inside a sentence does not).</summary>
    private static string SoundsOnly(string phonemes) =>
        new([.. phonemes.Where(c => c is not (' ' or ',' or '.' or 'ˌ'))]);

    /// <summary>
    ///     Proves that a word that is in no dictionary is still spoken: it is pronounced by the letter-to-sound model, so
    ///     the text with the unknown word has more symbols than the text without it and nothing is reported as unknown.
    /// </summary>
    [Fact]
    public void PiperPhonemePipeline_ToUtterances_UnknownWord_IsStillSpoken()
    {
        // Act
        var without = Assert.Single(Pipeline.ToUtterances("Welcome", null));
        var with = Assert.Single(Pipeline.ToUtterances("Welcome Zorblax", null));

        // Assert
        Assert.True(with.SymbolCount >= without.SymbolCount + 5, "The unknown word must add its own sounds.");
        Assert.Empty(with.UnknownSymbols);
    }

    /// <summary>
    ///     Proves that text without a sound in this voice produces no utterance and that a null text is rejected.
    /// </summary>
    [Fact]
    public void PiperPhonemePipeline_ToUtterances_EmptyOrSoundlessText_ProducesNoUtterancesAndNullIsRejected()
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

    /// <summary>
    ///     Runs the model's analysis over a text and renders the segments through the model's own capability profile, as the
    ///     session does (the core chunker's clause pieces of a sentence come back joined).
    /// </summary>
    private static IReadOnlyList<SpeechSegment> AnalyzeAndChunk(string text)
    {
        var analysis = Model.TextAnalyzer!.Analyze(text, CancellationToken.None);
        var spans = new[] { TaggedTextSpan.PlainText(text, analysis.Annotations, analysis.SentenceBreaks) };
        return ((ISynthesisModel)Model).CapabilityProfile.Render(spans, Model).Segments;
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

    /// <summary>Reads the rows of the Gettysburg golden (a header row, then id, sentence, phonemes).</summary>
    private static IEnumerable<string[]> ReadGettysburg() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", "piper-gettysburg-golden.tsv"))
            .Skip(1)
            .Where(line => line.Length > 0)
            .Select(line => line.Split('\t'));

    /// <summary>Returns the sentence of a row of the Gettysburg golden.</summary>
    private static string GettysburgSentence(string id) => ReadGettysburg().First(columns => columns[0] == id)[1];
}
