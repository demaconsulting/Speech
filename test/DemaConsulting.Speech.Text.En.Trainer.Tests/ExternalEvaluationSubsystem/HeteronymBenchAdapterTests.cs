using DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ExternalEvaluationSubsystem;

/// <summary>
///     Unit tests for <see cref="HeteronymBenchAdapter"/>: reading the pinned English read set (strata, duplicates of
///     the other source, offset repair, strict format) from a verified cache, with a hand-written fake document.
/// </summary>
public sealed class HeteronymBenchAdapterTests
{
    /// <summary>Proves items are read with the stratum of their source and the label made of the word and the benchmark's own reading key.</summary>
    [Fact]
    public void HeteronymBenchAdapter_Read_Items_ParsedWithStratumAndLabel()
    {
        // Arrange
        var content = ExternalContent.Standard();

        // Act
        var result = Read(content);

        // Assert: the four items of the benchmark's own sources, in item id order
        Assert.Equal(["fake-lead-1", "fake-lead-2", "fake-lead-3", "fake-wind-1"], result.Records.Select(static record => record.Id));
        Assert.Equal(["lead:guide", "lead:metal", "lead:guide", "wind:air"], result.Records.Select(static record => record.Label));
        Assert.Equal(["bench-v1", "bench-v1", "bench-claude_v2", "bench-claude_v2"], result.Records.Select(static record => record.Stratum));
        Assert.Equal(["lead", "lead", "lead", "wind"], result.Records.Select(static record => record.Sentence.Substring(record.Start, record.End - record.Start)));
        Assert.Equal(0, result.Skipped[SkipReasons.BadOffsets]);
        Assert.Equal(0, result.OffsetsRepaired);
        Assert.Equal(6, result.RowsRead);
    }

    /// <summary>Proves items copied from the other source are skipped as duplicates before anything else about them is looked at.</summary>
    [Fact]
    public void HeteronymBenchAdapter_Read_WhdItems_SkippedAsDuplicates()
    {
        // Arrange: a copied item whose offsets are also wrong is still only a duplicate
        var content = ExternalContent.Standard();
        content.AddBenchItemAt("fake-copy-3", "lead", "guide", "whd", "Another copied sentence to lead with.", 0, 3);

        // Act
        var result = Read(content);

        // Assert
        Assert.Equal(3, result.Skipped[SkipReasons.DuplicateOfWhd]);
        Assert.Equal(0, result.Skipped[SkipReasons.BadOffsets]);
        Assert.Equal(4, result.Records.Count);
        Assert.DoesNotContain(result.Records, static record => record.Id.StartsWith("fake-copy", StringComparison.Ordinal));
    }

    /// <summary>Proves an item whose span is in UTF-8 bytes rather than characters is repaired so the span is the word and the repair is counted, while an item whose span is the word neither way is skipped as bad offsets, not guessed.</summary>
    [Fact]
    public void HeteronymBenchAdapter_Read_ByteOffsetItem_IsRepairedAndCounted()
    {
        // Arrange: one accented letter before the word makes the byte span one unit later than the character span
        var content = ExternalContent.Standard();
        content.AddBenchItemAt("fake-byte-1", "lead", "guide", "v1", "Zo\u00eb will lead the way.", 10, 14);
        content.AddBenchItemAt("fake-bad-1", "lead", "guide", "v1", "Someone will lead the hikers home.", 0, 3);
        content.AddBenchItemAt("fake-bad-2", "lead", "guide", "v1", "Someone will lead the hikers home.", 30, 90);

        // Act
        var result = Read(content);

        // Assert
        var record = Assert.Single(result.Records, static candidate => candidate.Id == "fake-byte-1");
        Assert.Equal(9, record.Start);
        Assert.Equal("lead", record.Sentence.Substring(record.Start, record.End - record.Start));
        Assert.Equal(1, result.OffsetsRepaired);
        Assert.Equal(1, result.NonAsciiRows);
        Assert.Equal(2, result.Skipped[SkipReasons.BadOffsets]);
        Assert.Equal(5, result.Records.Count);
    }

    /// <summary>Proves a document that is not the expected format stops the read with an error that names the file and, for an item, the item.</summary>
    [Theory]
    [InlineData("{", "not valid JSON")]
    [InlineData("[]", "not an English read set")]
    [InlineData("""{"lang":"de","items":{}}""", "not an English read set")]
    [InlineData("""{"lang":"en"}""", "no items object")]
    [InlineData("""{"lang":"en","items":{"item-7":{"word":"lead","pos":[0,4],"answer":"a","src":"v1"}}}""", "item 'item-7' has no usable 'text'")]
    [InlineData("""{"lang":"en","items":{"item-8":{"text":"lead","word":"lead","pos":"0-4","answer":"a","src":"v1"}}}""", "item 'item-8' has no usable 'pos'")]
    [InlineData("""{"lang":"en","items":{"item-12":{"text":"lead","word":"lead","pos":["0",4],"answer":"a","src":"v1"}}}""", "item 'item-12' has no usable 'pos'")]
    [InlineData("""{"lang":"en","items":{"item-13":{"text":"lead","word":"lead","pos":[0,null],"answer":"a","src":"v1"}}}""", "item 'item-13' has no usable 'pos'")]
    [InlineData("""{"lang":"en","items":{"item-14":{"text":"lead","word":"lead","pos":[true,4],"answer":"a","src":"v1"}}}""", "item 'item-14' has no usable 'pos'")]
    [InlineData("""{"lang":"en","items":{"item-15":{"text":"lead","word":"lead","pos":[0.5,4],"answer":"a","src":"v1"}}}""", "item 'item-15' has no usable 'pos'")]
    [InlineData("""{"lang":"en","items":{"item-16":{"text":"lead","word":"lead","pos":[0],"answer":"a","src":"v1"}}}""", "item 'item-16' has no usable 'pos'")]
    [InlineData("""{"lang":"en","items":{"item-17":{"text":"lead","word":"lead","pos":[0,4,5],"answer":"a","src":"v1"}}}""", "item 'item-17' has no usable 'pos'")]
    [InlineData("""{"lang":"en","items":{"item-10":{"text":"lead","word":"lead","pos":[0,4],"answer":"","src":"v1"}}}""", "item 'item-10' has no usable 'answer'")]
    [InlineData("""{"lang":"en","items":{"item-11":{"text":"lead","word":"lead","pos":[0,4],"answer":"a"}}}""", "item 'item-11' has no usable 'src'")]
    public void HeteronymBenchAdapter_Read_MalformedDocument_Throws(string document, string expected)
    {
        // Arrange
        var content = ExternalContent.Standard();
        content.BenchDocument = document;

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => Read(content));

        // Assert
        Assert.Contains("data/readset_en_v2.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Reads fake content through a verified cache.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <returns>What the adapter read.</returns>
    private static ExternalReadResult Read(ExternalContent content)
    {
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training, content);
        var cache = external.WriteCache(external.NewFolder("cache"));
        return HeteronymBenchAdapter.Read(external.OpenCache(cache), external.Pins.Find("bench"));
    }
}
