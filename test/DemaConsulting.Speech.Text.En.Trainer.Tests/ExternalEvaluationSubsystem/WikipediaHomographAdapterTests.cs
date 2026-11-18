using DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ExternalEvaluationSubsystem;

/// <summary>
///     Unit tests for <see cref="WikipediaHomographAdapter"/>: reading the pinned WHD-format files (quoted fields, byte
///     offsets, strict format) from a verified cache, with hand-written fake files.
/// </summary>
public sealed class WikipediaHomographAdapterTests
{
    /// <summary>The header line of a fake WHD file.</summary>
    private const string Header = "\"homograph\"\t\"wordid\"\t\"sentence\"\t\"start\"\t\"end\"\n";

    /// <summary>Proves a quoted sentence with doubled quotes is read back exactly and its offsets still land on the target.</summary>
    [Fact]
    public void WikipediaHomographAdapter_ReadSplit_QuotedSentence_KeepsDoubledQuotesAndOffsets()
    {
        // Arrange
        const string sentence = "He said \"we will lead the group\" loudly.";
        var content = ExternalContent.Standard();
        content.AddWhdRow("eval", "quoted", "lead", "lead_guide", sentence);

        // Act
        var result = Read(content, "eval");

        // Assert
        var record = Assert.Single(result.Records, candidate => candidate.Sentence == sentence);
        Assert.Equal("lead", sentence.Substring(record.Start, record.End - record.Start));
        Assert.Equal("lead_guide", record.Label);
        Assert.Equal(0, result.Skipped[SkipReasons.BadOffsets]);
    }

    /// <summary>Proves byte offsets of sentences with accented and astral characters are converted so the span is the target, and the conversions are counted.</summary>
    [Fact]
    public void WikipediaHomographAdapter_ReadSplit_NonAsciiSentence_ConvertsByteOffsets()
    {
        // Arrange: the target follows non-ASCII characters in one sentence and precedes them in the other
        var content = ExternalContent.Standard();
        content.AddWhdRow("eval", "unicode", "lead", "lead_guide", "Zo\u00eb will lead the \U0001F600 tour.");
        content.AddWhdRow("eval", "unicode", "lead", "lead_guide", "We lead the caf\u00e9 crowd.");

        // Act
        var result = Read(content, "eval");

        // Assert
        var unicode = result.Records.Where(record => ByteOffsets.HasNonAscii(record.Sentence)).ToList();
        Assert.Equal(2, unicode.Count);
        Assert.All(unicode, record => Assert.Equal("lead", record.Sentence.Substring(record.Start, record.End - record.Start)));
        Assert.Equal(2, result.NonAsciiRows);
        Assert.Equal(1, result.OffsetsConverted);
        Assert.Equal(0, result.Skipped[SkipReasons.BadOffsets]);
        Assert.Equal(7, result.Records.Count);
    }

    /// <summary>Proves a row whose span is not its homograph, or whose offsets cut a character, is skipped as bad offsets and never guessed.</summary>
    [Fact]
    public void WikipediaHomographAdapter_ReadSplit_HomographMismatch_SkipsAsBadOffsets()
    {
        // Arrange: a span that is another word than the homograph column, and offsets inside an accented letter
        var content = ExternalContent.Standard();
        content.AddWhdRowAt("eval", "mismatch", "wind", "wind_air", "Someone will lead the hikers home.", 13, 17);
        content.AddWhdRowAt("eval", "mismatch", "lead", "lead_guide", "Caf\u00e9 lead", 4, 8);

        // Act
        var result = Read(content, "eval");

        // Assert: only the five standard rows are usable
        Assert.Equal(2, result.Skipped[SkipReasons.BadOffsets]);
        Assert.Equal(5, result.Records.Count);
        Assert.Equal(7, result.RowsRead);
    }

    /// <summary>Proves a file that is not exactly the expected format stops the read with an error that names the file.</summary>
    [Theory]
    [InlineData("\"homograph\"\t\"wordid\"\t\"sentence\"\t\"begin\"\t\"end\"\n", "header")]
    [InlineData(Header + "\"lead\"\t\"lead_guide\"\t\"Someone will lead.\"\t13\n", "five fields")]
    [InlineData(Header + "\"lead\"\t\"lead_guide\"\t\"Someone will lead.\"\tfive\t17\n", "integer offsets")]
    [InlineData(Header + "\"lead\"\t\"lead_guide\"\t\"never closed\t13\t17\n", "not closed")]
    [InlineData(Header + "\"lead\"x\t\"lead_guide\"\t\"Someone will lead.\"\t13\t17\n", "closing quote")]
    public void WikipediaHomographAdapter_ReadSplit_MalformedFile_Throws(string text, string expected)
    {
        // Arrange
        var content = ExternalContent.Standard();
        content.WhdFiles["data/eval/lead.tsv"] = text;

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => Read(content, "eval"));

        // Assert
        Assert.Contains("data/eval/lead.tsv", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a file with CRLF line endings is read like one with LF.</summary>
    [Fact]
    public void WikipediaHomographAdapter_ReadSplit_CrLfFile_IsRead()
    {
        // Arrange
        var content = ExternalContent.Standard();
        content.WhdFiles["data/eval/lead.tsv"] = Header.Replace("\n", "\r\n", StringComparison.Ordinal)
            + "\"lead\"\t\"lead_guide\"\t\"Someone will lead the hikers home.\"\t13\t17\r\n"
            + "\"lead\"\t\"lead_metal\"\t\"That lead pipe is old.\"\t5\t9\r\n";

        // Act
        var result = Read(content, "eval");

        // Assert: two rows of lead.tsv and the one row of wind.tsv
        Assert.Equal(3, result.Records.Count);
        Assert.Equal(["lead_guide", "lead_metal", "wind_air"], result.Records.Select(record => record.Label));
    }

    /// <summary>Proves each split reads only its own pinned data files, in ordinal path order, and ignores pinned files that are not data tables.</summary>
    [Fact]
    public void WikipediaHomographAdapter_ReadSplit_TrainAndEval_ReadOnlyTheirOwnPinnedFilesInOrder()
    {
        // Arrange: an eval file that sorts first, and a pinned note that is not a table
        var content = ExternalContent.Standard();
        content.AddWhdRow("eval", "aaa", "lead", "lead_guide", "First we lead the way out.");
        content.WhdFiles["data/eval/notes.txt"] = "this is not a table\n";

        // Act
        var eval = Read(content, "eval");
        var train = Read(content, "train");

        // Assert
        Assert.Equal(["data/eval/aaa.tsv", "data/eval/lead.tsv", "data/eval/wind.tsv"], eval.Records.Select(PathOf).Distinct());
        Assert.Equal(6, eval.Records.Count);
        Assert.All(eval.Records, record => Assert.Equal("whd-eval", record.Stratum));
        Assert.Equal(["data/train/lead.tsv", "data/train/wind.tsv"], train.Records.Select(PathOf).Distinct());
        Assert.Equal(3, train.Records.Count);
        Assert.All(train.Records, record => Assert.Equal("whd-train", record.Stratum));
        Assert.Empty(eval.Records.Select(static record => record.Sentence).Intersect(train.Records.Select(static record => record.Sentence)));
    }

    /// <summary>
    ///     Reads a split of fake content through a verified cache.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="split">The split.</param>
    /// <returns>What the adapter read.</returns>
    private static ExternalReadResult Read(ExternalContent content, string split)
    {
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training, content);
        var cache = external.WriteCache(external.NewFolder("cache"));
        return WikipediaHomographAdapter.ReadSplit(external.OpenCache(cache), external.Pins.Find("whd"), split);
    }

    /// <summary>
    ///     Gets the pinned path a record came from, which its id carries as the second field.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>The path.</returns>
    private static string PathOf(ExternalRecord record) => record.Id.Split(':')[1];
}
