using DemaConsulting.Speech.Proofreader.Session;

namespace DemaConsulting.Speech.Proofreader.Tests;

/// <summary>
///     Tests for <see cref="TsvExporter"/>: the export format is a contract with spreadsheets and golden-data tooling.
/// </summary>
public class TsvExporterTests
{
    /// <summary>
    ///     Creates a flag with the given identity and text.
    /// </summary>
    /// <param name="id">The flag id.</param>
    /// <param name="sentenceIndex">The sentence index.</param>
    /// <param name="spanStart">The span start (with a length of 4 when positive).</param>
    /// <param name="note">The note.</param>
    /// <returns>The flag.</returns>
    private static ProofreadFlag CreateFlag(int id, int sentenceIndex, int spanStart, string note) => new()
    {
        Id = id,
        SentenceIndex = sentenceIndex,
        SentenceText = "He will lead\tthe lead\r\nrole.",
        SpanStart = spanStart,
        SpanLength = spanStart > 0 ? 4 : 0,
        Word = spanStart > 0 ? "lead" : string.Empty,
        Category = FlagCategory.WrongHeteronym,
        Note = note,
        Model = new SessionModel
        {
            ModelId = "kokoro-onnx-v1_0-en",
            DisplayName = "Kokoro v1.0",
            Parameters = new Dictionary<string, object> { ["voice"] = "af_heart", ["speed"] = 1.25 },
        },
        Readings = "lead=lead.verb(0.97)",
        Phonemes = "first phonemes | second phonemes",
    };

    /// <summary>
    ///     Validates the header, the column order, the flattening of tabs and line breaks, and the invariant formatting.
    /// </summary>
    [Fact]
    public void TsvExporter_Export_OneFlag_WritesHeaderAndOneFlatRow()
    {
        // Arrange: one flag whose sentence and note contain tabs and line breaks
        var flag = CreateFlag(5, 11, 8, "wrong\tvowel\nsee note");

        // Act
        var lines = TsvExporter.Export([flag]).Split('\n');

        // Assert: header, one row, and the terminating LF; the row has exactly the documented columns
        Assert.Equal(3, lines.Length);
        Assert.Equal(string.Empty, lines[2]);
        Assert.Equal(
            "id\tsentence\tspan\tword\tcategory\tnote\tmodel\tsettings\treadings\tphonemes\tsentence_index",
            lines[0]);

        var fields = lines[1].Split('\t');
        Assert.Equal(11, fields.Length);
        Assert.Multiple(
            () => Assert.Equal("5", fields[0]),
            () => Assert.Equal("He will lead the lead role.", fields[1]),
            () => Assert.Equal("8-12", fields[2]),
            () => Assert.Equal("lead", fields[3]),
            () => Assert.Equal("wrong-heteronym", fields[4]),
            () => Assert.Equal("wrong vowel see note", fields[5]),
            () => Assert.Equal("Kokoro v1.0", fields[6]),
            () => Assert.Equal("speed=1.25; voice=af_heart", fields[7]),
            () => Assert.Equal("lead=lead.verb(0.97)", fields[8]),
            () => Assert.Equal("first phonemes | second phonemes", fields[9]),
            () => Assert.Equal("11", fields[10]));
    }

    /// <summary>
    ///     Validates that rows are ordered by sentence and then span, and that a whole-sentence flag has an empty span and word.
    /// </summary>
    [Fact]
    public void TsvExporter_Export_UnorderedFlags_SortsBySentenceThenSpan()
    {
        // Arrange: flags added out of order, one flagging the whole sentence
        var flags = new[] { CreateFlag(1, 7, 8, "c"), CreateFlag(2, 2, 0, "a"), CreateFlag(3, 7, 3, "b") };

        // Act
        var rows = TsvExporter.Export(flags).Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1)
            .Select(row => row.Split('\t'))
            .ToList();

        // Assert: sentence 2 first, then sentence 7 by span start; the whole-sentence flag has no span or word
        Assert.Equal(["a", "b", "c"], rows.Select(fields => fields[5]));
        Assert.Equal(string.Empty, rows[0][2]);
        Assert.Equal(string.Empty, rows[0][3]);
    }
}
