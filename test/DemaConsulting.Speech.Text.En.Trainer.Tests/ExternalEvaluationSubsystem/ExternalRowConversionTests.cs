using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ExternalEvaluationSubsystem;

/// <summary>
///     Unit tests for the external row conversion: the byte-to-character offset conversion, the id-to-id label
///     mapping, and the conversion of adapter records to labeled rows with the accounting of every skipped row.
/// </summary>
public sealed class ExternalRowConversionTests
{
    /// <summary>Proves byte spans of a sentence's UTF-8 encoding convert to the matching UTF-16 spans.</summary>
    [Theory]
    [InlineData("lead now", 0, 4, 0, 4)]
    [InlineData("Caf\u00e9 lead", 6, 10, 5, 9)]
    [InlineData("\U0001F600 lead", 5, 9, 3, 7)]
    public void ByteOffsets_TryToCharOffsets_ValidSpans_ConvertsToUtf16(string sentence, int byteStart, int byteEnd, int expectedStart, int expectedEnd)
    {
        // Act
        var converted = ByteOffsets.TryToCharOffsets(sentence, byteStart, byteEnd, out var start, out var end);

        // Assert
        Assert.True(converted);
        Assert.Equal(expectedStart, start);
        Assert.Equal(expectedEnd, end);
    }

    /// <summary>Proves spans that are empty, reversed, outside the sentence or cut a character in two are refused.</summary>
    [Theory]
    [InlineData("lead", 3, 2)]
    [InlineData("lead", -1, 2)]
    [InlineData("lead", 0, 5)]
    [InlineData("Caf\u00e9 lead", 4, 8)]
    [InlineData("\U0001F600 lead", 0, 3)]
    public void ByteOffsets_TryToCharOffsets_InvalidSpans_ReturnsFalse(string sentence, int byteStart, int byteEnd)
    {
        // Act
        var converted = ByteOffsets.TryToCharOffsets(sentence, byteStart, byteEnd, out _, out _);

        // Assert
        Assert.False(converted);
    }

    /// <summary>Proves a mapping file maps its labels to reading ids and leaves other labels unmapped.</summary>
    [Fact]
    public void ReadingIdMap_Load_ValidFile_MapsLabels()
    {
        // Arrange
        using var training = new TrainingFixture();
        var inventory = TrainingCorpus.Load(training.Root).Inventory;
        training.Write("map.tsv", "whd_wordid\treading_id\nlead_guide\tlead.verb\nlead_metal\tlead.noun\nwind_air\twind.noun\n");

        // Act
        var map = ReadingIdMap.Load(training.PathOf("map.tsv"), ReadingIdMap.WhdHeader, inventory);
        var guide = map.TryGet("lead_guide", out var guideId);
        var unknown = map.TryGet("lead_unlisted", out var unknownId);

        // Assert
        Assert.Equal(3, map.Count);
        Assert.True(guide);
        Assert.Equal("lead.verb", guideId);
        Assert.False(unknown);
        Assert.Equal(string.Empty, unknownId);
    }

    /// <summary>Proves a mapping file that is missing or malformed is refused with an error that names the problem.</summary>
    [Theory]
    [InlineData(null, "not found")]
    [InlineData("wrong_header\treading_id\nlead_guide\tlead.verb\n", "header")]
    [InlineData("whd_wordid\treading_id\nlead_guide\tlead.verb\nlead_guide\tlead.noun\n", "more than once")]
    [InlineData("whd_wordid\treading_id\nlead_guide\tno.such-reading\n", "no.such-reading")]
    [InlineData("whd_wordid\treading_id\nlead_guide\n", "two non-empty cells")]
    [InlineData("whd_wordid\treading_id\n\tlead.verb\n", "two non-empty cells")]
    [InlineData("whd_wordid\treading_id\nlead_guide\t\n", "two non-empty cells")]
    public void ReadingIdMap_Load_InvalidFile_Throws(string? content, string expected)
    {
        // Arrange
        using var training = new TrainingFixture();
        var inventory = TrainingCorpus.Load(training.Root).Inventory;
        if (content is not null)
        {
            training.Write("map.tsv", content);
        }

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => ReadingIdMap.Load(training.PathOf("map.tsv"), ReadingIdMap.WhdHeader, inventory));

        // Assert
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a mapping file that is not valid UTF-8 is refused.</summary>
    [Fact]
    public void ReadingIdMap_Load_InvalidUtf8_Throws()
    {
        // Arrange
        using var training = new TrainingFixture();
        var inventory = TrainingCorpus.Load(training.Root).Inventory;
        File.WriteAllBytes(training.PathOf("map.tsv"), [.. System.Text.Encoding.UTF8.GetBytes("whd_wordid\treading_id\nlead_guide\tlead.verb\n"), 0xC3, 0x28]);

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => ReadingIdMap.Load(training.PathOf("map.tsv"), ReadingIdMap.WhdHeader, inventory));

        // Assert
        Assert.Contains("UTF-8", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves usable records become labeled rows that carry the mapped reading id, the stratum as the source and the offsets, and that clustering follows the choice made.</summary>
    [Fact]
    public void ExternalRowConverter_Convert_MappedRows_CarryReadingIdAndStratum()
    {
        // Arrange
        using var training = new TrainingFixture();
        var corpus = TrainingCorpus.Load(training.Root);
        var map = Map(training, corpus);
        var records = new[]
        {
            Record("a1", "lead_guide", "Someone will lead the hikers home.", "lead", "set-a"),
            Record("a2", "lead_metal", "That lead pipe is old.", "lead", "set-b"),
        };
        var result = new ExternalReadResult(records.Take(2).ToList(), new Dictionary<string, int>(), 0, 0, 0);

        // Act
        var bySentence = ExternalRowConverter.Convert("whd-eval", result, map, corpus.Inventory, new HashSet<string>(), clusterBySentence: true);
        var perRow = ExternalRowConverter.Convert("bench", result, map, corpus.Inventory, new HashSet<string>(), clusterBySentence: false);

        // Assert
        Assert.Equal("whd-eval", bySentence.Name);
        Assert.Equal(["lead.verb", "lead.noun"], bySentence.Rows.Select(row => row.ReadingId));
        Assert.Equal(["set-a", "set-b"], bySentence.Rows.Select(row => row.Source));
        Assert.Equal(["a1", "a2"], bySentence.Rows.Select(row => row.Id));
        Assert.All(bySentence.Rows, row => Assert.Equal("lead", row.Sentence.Substring(row.Start, row.End - row.Start)));
        Assert.Equal(2, bySentence.Counts.Used);
        Assert.Equal(2, perRow.Counts.Read);
        Assert.Equal(bySentence.ClusterOf(bySentence.Rows[0]), bySentence.ClusterOf(new LabeledRow("other-id", "lead.verb", bySentence.Rows[0].Sentence, 0, 4, "x")));
        Assert.NotEqual(perRow.ClusterOf(perRow.Rows[0]), perRow.ClusterOf(perRow.Rows[1]));
    }

    /// <summary>Proves every skip reason is counted, the adapter's own skips are added, and the rows read always equal the rows used plus the rows skipped.</summary>
    [Fact]
    public void ExternalRowConverter_Convert_EachSkipReason_IsCountedAndReadEqualsUsedPlusSkipped()
    {
        // Arrange: two usable records and one of each reason, plus rows the adapter already skipped
        using var training = new TrainingFixture();
        var corpus = TrainingCorpus.Load(training.Root);
        var map = Map(training, corpus);
        var records = new List<ExternalRecord>
        {
            Record("ok-1", "lead_guide", "Someone will lead the hikers home.", "lead", "s"),
            Record("ok-2", "lead_metal", "That lead pipe is old.", "lead", "s"),
            Record("unmapped", "lead_unlisted", "Someone will lead the hikers home.", "lead", "s"),
            Record("unknown-form", "wind_air", "A gust of air came in.", "gust", "s"),
            Record("not-candidate", "wind_air", "Someone will lead the hikers home.", "lead", "s"),
            Record("bad-offsets", "lead_guide", "Someone will lead the hikers home.", "lead", "s") with { End = 400 },
        };
        var adapterSkips = new Dictionary<string, int> { [SkipReasons.BadOffsets] = 2, [SkipReasons.DuplicateOfWhd] = 3 };
        var result = new ExternalReadResult(records, adapterSkips, NonAsciiRows: 4, OffsetsConverted: 2, OffsetsRepaired: 1);

        // Act
        var set = ExternalRowConverter.Convert("whd-eval", result, map, corpus.Inventory, new HashSet<string>(), clusterBySentence: true);

        // Assert
        var counts = set.Counts;
        Assert.Equal(3, counts.Skipped[SkipReasons.BadOffsets]);
        Assert.Equal(3, counts.Skipped[SkipReasons.DuplicateOfWhd]);
        Assert.Equal(1, counts.Skipped[SkipReasons.ReadingNotCandidate]);
        Assert.Equal(1, counts.Skipped[SkipReasons.UnknownForm]);
        Assert.Equal(1, counts.Skipped[SkipReasons.UnmappedLabel]);
        Assert.Equal(SkipReasons.All.Order(StringComparer.Ordinal), counts.Skipped.Keys);
        Assert.Equal(2, counts.Used);
        Assert.Equal(6 + 5, counts.Read);
        Assert.Equal(counts.Read, counts.Used + counts.Skipped.Values.Sum());
        Assert.Equal(4, counts.NonAsciiRows);
        Assert.Equal(2, counts.OffsetsConverted);
        Assert.Equal(1, counts.OffsetsRepaired);
        Assert.Equal(["ok-1", "ok-2"], set.Rows.Select(row => row.Id));
    }

    /// <summary>Proves a row whose sentence equals a training sentence after normalization is kept and counted as an overlap.</summary>
    [Fact]
    public void ExternalRowConverter_Convert_SentenceEqualToTrainingSentence_IsKeptAndCounted()
    {
        // Arrange: the first record is a training sentence with other case and punctuation
        using var training = new TrainingFixture();
        var corpus = TrainingCorpus.Load(training.Root);
        var map = Map(training, corpus);
        var records = new[]
        {
            Record("same", "lead_guide", "they will lead the team, TODAY!", "lead", "s"),
            Record("different", "lead_guide", "Someone will lead the hikers home.", "lead", "s"),
        };
        var result = new ExternalReadResult(records, new Dictionary<string, int>(), 0, 0, 0);

        // Act
        var set = ExternalRowConverter.Convert("whd-train", result, map, corpus.Inventory, ExternalRowConverter.TrainDevKeys(corpus), clusterBySentence: true);

        // Assert
        Assert.Equal(1, set.Counts.ExactOverlap);
        Assert.Equal(2, set.Counts.Used);
        Assert.Equal(["same", "different"], set.Rows.Select(row => row.Id));
    }

    /// <summary>
    ///     Loads a mapping of three fake labels.
    /// </summary>
    /// <param name="training">The training fixture.</param>
    /// <param name="corpus">The corpus whose inventory the ids must belong to.</param>
    /// <returns>The map.</returns>
    private static ReadingIdMap Map(TrainingFixture training, TrainingCorpus corpus)
    {
        training.Write("map.tsv", "whd_wordid\treading_id\nlead_guide\tlead.verb\nlead_metal\tlead.noun\nwind_air\twind.noun\n");
        return ReadingIdMap.Load(training.PathOf("map.tsv"), ReadingIdMap.WhdHeader, corpus.Inventory);
    }

    /// <summary>
    ///     Builds a record whose span is the first occurrence of a word.
    /// </summary>
    /// <param name="id">The record id.</param>
    /// <param name="label">The label.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="word">The target word as written in the sentence.</param>
    /// <param name="stratum">The stratum.</param>
    /// <returns>The record.</returns>
    private static ExternalRecord Record(string id, string label, string sentence, string word, string stratum)
    {
        var start = sentence.IndexOf(word, StringComparison.Ordinal);
        return new ExternalRecord(id, label, sentence, start, start + word.Length, stratum);
    }
}
