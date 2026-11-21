using System.Text;
using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ClusteringSubsystem;

/// <summary>
///     Unit tests for <see cref="ClusteringRecord"/>: the fixed text form, the round trip and the refused documents.
/// </summary>
public sealed class ClusteringRecordTests
{
    /// <summary>Gets a record with distinct values in every field.</summary>
    private static ClusteringRecord Sample => new(
        1,
        new ClusteringSettings(512, 50000, 5, 1, 1, 1),
        new ClusteringInputs(new string('a', 64), 270, 64, new string('b', 64), new string('c', 64), "0.1.0-beta.5", new string('d', 64), 10240, 272, string.Empty),
        new ClusteringCorpus(1, 2, 3, 4, 5, 6, 7, 8, 9),
        new ClusteringResult(new string('e', 64), 49000, 512, 3, 0, 9712));

    /// <summary>Proves the record is written with a fixed field order, two-space indentation, LF endings and a final LF.</summary>
    [Fact]
    public void ClusteringRecord_Write_FixedTextForm()
    {
        // Act
        var text = Encoding.UTF8.GetString(Sample.Write());

        // Assert
        Assert.StartsWith("{\n  \"schema\": 1,\n  \"settings\": {\n    \"clusters\": 512,\n    \"maxWords\": 50000,\n", text, StringComparison.Ordinal);
        Assert.Contains("\n  \"inputs\": {\n    \"sourcesSha256\": \"" + new string('a', 64) + "\",\n    \"usedBooks\": 270,", text, StringComparison.Ordinal);
        Assert.Contains("\"tokenizerVersion\": \"0.1.0-beta.5\"", text, StringComparison.Ordinal);
        Assert.EndsWith("    \"tokenCoveragePerTenThousand\": 9712\n  }\n}\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
        Assert.Equal(text, Sample.ToText());
    }

    /// <summary>Proves a written record parses back to an equal record.</summary>
    [Fact]
    public void ClusteringRecord_Parse_WrittenRecord_RoundTrips()
    {
        // Act
        var parsed = ClusteringRecord.Parse(Sample.Write());

        // Assert
        Assert.Equal(Sample, parsed);
        Assert.Equal(Sample.Write(), parsed.Write());
    }

    /// <summary>Proves invalid JSON, another schema, a missing field and a wrong type are refused.</summary>
    [Fact]
    public void ClusteringRecord_Parse_InvalidDocuments_Throw()
    {
        // Arrange
        var text = Encoding.UTF8.GetString(Sample.Write());

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ClusteringRecord.Parse(Encoding.UTF8.GetBytes("{ not json")));
        Assert.Throws<InvalidDataException>(() => ClusteringRecord.Parse(Encoding.UTF8.GetBytes(text.Replace("\"schema\": 1", "\"schema\": 2", StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() => ClusteringRecord.Parse(Encoding.UTF8.GetBytes(text.Replace("\"tableSha256\"", "\"tableHash\"", StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() => ClusteringRecord.Parse(Encoding.UTF8.GetBytes(text.Replace("\"usedBooks\": 270", "\"usedBooks\": \"270\"", StringComparison.Ordinal))));
    }
}
