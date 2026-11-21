using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.SynthesisSubsystem;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;
using DemaConsulting.Speech.Phonemes.En.RenderingSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.Kokoro.Tests.SynthesisSubsystem;

/// <summary>
///     Pipeline tests for the text-to-token-ids path of the Kokoro engine: the model's real English text
///     analysis, the session's own sentence chunking, the real phonemizer and renderer and Kokoro's real
///     vocabulary, with no ONNX model involved. The golden corpus
///     (<c>TestData/kokoro-pipeline-golden.tsv</c>, 41 sentences written for this repository: 12 sentences that
///     each carry both readings of a heteronym such as <c>record</c> or <c>wind</c>, 18 general sentences with
///     numbers, dates, currency, abbreviations and unknown names, and 11 single-reading sentences) pins the
///     phoneme string and the token ids the engine feeds the model.
/// </summary>
/// <remarks>
///     A change in the golden means the audio changes: review it by listening, not only by reading. The
///     golden records the text analysis's actual decisions. Three heteronym sentences of the first draft
///     (<c>read</c>, <c>lead</c> and <c>content</c> in the same sentence) were replaced because the classifier
///     labeled both occurrences alike; that is a known weakness of the text analysis, not of this pipeline.
///     For a heteronym the golden pins the committed model's reading, right or wrong: whether the reading is
///     right is judged by the Text.En trainer's statistical gate through the regression overlay, not here. The
///     habitual <c>I read every night before bed.</c> (e05), which allows both readings, was forked into a present
///     (e05) and a past (e11) sentence.
/// </remarks>
public sealed class KokoroPhonemePipelineTests
{
    private const char SeparatorOfParts = '|';

    private static readonly KokoroPhonemeVocabulary Vocabulary = new();
    private static readonly KokoroPhonemePipeline Pipeline = new(Vocabulary);
    private static readonly EnglishPhonemizer Phonemizer = new();
    private static readonly MisakiRenderer Renderer = new(MisakiProfile.Us, KokoroPhonemePipeline.UseWeakForms);

    /// <summary>Gets the golden rows as xunit theory data: id, sentence, phonemes, token ids.</summary>
    public static TheoryData<string, string, string, string> GoldenRows()
    {
        var rows = new TheoryData<string, string, string, string>();
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "kokoro-pipeline-golden.tsv");
        foreach (var line in File.ReadAllLines(path).Skip(1).Where(line => line.Length > 0))
        {
            var columns = line.Split('\t');
            rows.Add(columns[0], columns[1], columns[2], columns[3]);
        }

        return rows;
    }

    /// <summary>
    ///     Proves that every sentence of the golden corpus, run the way a session runs it (analyze the whole text,
    ///     chunk with the annotations, speak each chunk), produces exactly the pinned phoneme string and token ids.
    /// </summary>
    /// <param name="id">The sentence id.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="phonemes">The pinned phoneme string, chunks separated by <c>|</c>.</param>
    /// <param name="tokenIds">The pinned token ids, chunks separated by <c>|</c>.</param>
    [Theory]
    [MemberData(nameof(GoldenRows))]
    public void KokoroPhonemePipeline_GoldenSentence_ProducesPinnedPhonemesAndTokenIds(string id, string sentence, string phonemes, string tokenIds)
    {
        // Arrange
        var chunks = AnalyzeAndChunk(sentence);

        // Act
        var actualPhonemes = string.Join(
            $" {SeparatorOfParts} ",
            chunks.Select(chunk => Renderer.Render(Phonemizer.Phonemize(chunk.Text, NullIfEmpty(chunk.Annotations)))));
        var actualIds = string.Join(
            $" {SeparatorOfParts} ",
            chunks.Select(chunk => string.Join(',', Assert.Single(Pipeline.ToTokenIds(chunk.Text, NullIfEmpty(chunk.Annotations))))));

        // Assert
        Assert.True(sentence.Length > 0, id);
        Assert.Equal(phonemes, actualPhonemes);
        Assert.Equal(tokenIds, actualIds);
    }

    /// <summary>
    ///     Proves that the reading the text analysis chooses for a heteronym reaches the model: in each of the
    ///     twelve heteronym sentences, which carry both readings of the word, the token ids differ from the ids of
    ///     the same text spoken with default readings.
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
    public void KokoroPhonemePipeline_HeteronymSentence_UsesTheChosenReadings(string id)
    {
        // Arrange
        var sentence = GoldenSentence(id);
        var analysis = new OnnxKokoroEnglishSynthesisModel().TextAnalyzer!.Analyze(sentence, CancellationToken.None);

        // Act
        var withReadings = Assert.Single(Pipeline.ToTokenIds(sentence, analysis.Annotations));
        var defaultReadings = Assert.Single(Pipeline.ToTokenIds(sentence, null));

        // Assert
        Assert.NotEqual(defaultReadings, withReadings);
    }

    /// <summary>
    ///     Proves that a segment longer than the model's token limit is split into parts that each fit the limit,
    ///     that no text is dropped, and that the parts end after a clause mark when the text has them.
    /// </summary>
    [Fact]
    public void KokoroPhonemePipeline_TextBeyondTokenLimit_IsSplitAtClauseBoundariesWithoutLoss()
    {
        // Arrange
        const string sentence = "The cat sat on the mat, and the dog barked.";
        var single = Assert.Single(Pipeline.ToTokenIds(sentence, null)).Count;
        var text = string.Join(' ', Enumerable.Repeat(sentence, 40));
        var clauseMarkIds = new[] { Vocabulary.ToTokenIds(",")[0], Vocabulary.ToTokenIds(".")[0] };

        // Act
        var parts = Pipeline.ToTokenIds(text, null);

        // Assert
        Assert.True(parts.Count >= 3, "About 1,500 tokens need at least three parts.");
        Assert.All(parts, part => Assert.InRange(part.Count, 1, KokoroPhonemeVocabulary.MaxPhonemeTokens));
        Assert.True(parts.Sum(part => part.Count) >= 40 * single, "No word may be dropped.");
        Assert.All(parts.Take(parts.Count - 1), part => Assert.Contains(part[^1], clauseMarkIds));
    }

    /// <summary>
    ///     Proves that the small function words are spoken in their weak forms: the pipeline gives the token ids
    ///     of the renderer with weak forms switched on, which differ from the ids of the full forms.
    /// </summary>
    [Fact]
    public void KokoroPhonemePipeline_FunctionWords_AreSpokenInTheirWeakForms()
    {
        // Arrange
        const string sentence = "A cup of the tea and a bowl of the soup.";
        var text = Phonemizer.Phonemize(sentence);
        var weak = Vocabulary.ToTokenIds(new MisakiRenderer(MisakiProfile.Us, useWeakForms: true).Render(text));
        var full = Vocabulary.ToTokenIds(new MisakiRenderer(MisakiProfile.Us, useWeakForms: false).Render(text));

        // Act
        var actual = Assert.Single(Pipeline.ToTokenIds(sentence, null));

        // Assert
        Assert.NotEqual(full, weak);
        Assert.Equal(weak, actual);
    }

    /// <summary>
    ///     Proves that text without a sound in this voice produces no token ids and that a null text is rejected.
    /// </summary>
    [Fact]
    public void KokoroPhonemePipeline_EmptyOrSoundlessText_ProducesNoPartsAndNullIsRejected()
    {
        // Act & Assert
        Assert.Empty(Pipeline.ToTokenIds(string.Empty, null));
        Assert.Empty(Pipeline.ToTokenIds("\U0001F600", null));
        Assert.Throws<ArgumentNullException>(() => Pipeline.ToTokenIds(null!, null));
    }

    /// <summary>Runs the model's analysis over a text and chunks it with the annotations, as the session does.</summary>
    private static IReadOnlyList<SentenceChunk> AnalyzeAndChunk(string text)
    {
        var analysis = new OnnxKokoroEnglishSynthesisModel().TextAnalyzer!.Analyze(text, CancellationToken.None);
        return SentenceChunker.ChunkAligned(text, analysis.Annotations, analysis.SentenceBreaks);
    }

    /// <summary>Returns the annotations, or null when there are none (the session then calls the plain generation).</summary>
    private static IReadOnlyList<SegmentAnnotation>? NullIfEmpty(IReadOnlyList<SegmentAnnotation> annotations) =>
        annotations.Count > 0 ? annotations : null;

    /// <summary>Returns the sentence of a golden row.</summary>
    private static string GoldenSentence(string id)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "kokoro-pipeline-golden.tsv");
        return File.ReadAllLines(path).Select(line => line.Split('\t')).First(columns => columns[0] == id)[1];
    }
}
