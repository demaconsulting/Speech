using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingDataSubsystem;

/// <summary>
///     Unit tests for <see cref="TextEnDataSet"/> and its closed source vocabulary: loading never throws and every defect is a finding.
/// </summary>
public sealed class TextEnDataSetTests
{
    /// <summary>Proves the valid fixture loads completely and cleanly.</summary>
    [Fact]
    public void TextEnDataSet_Load_ValidFixture_LoadsAllTablesWithoutFindings()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        var report = new CheckReport();

        // Act
        var data = TextEnDataSet.Load(fixture.Root, report);

        // Assert
        Assert.Empty(report.Findings);
        Assert.Equal(3, data.Readings.Count);
        Assert.Equal(3, data.Mapping.Count);
        Assert.Equal(4, data.Forms.Count);
        Assert.Equal(3, data.DistinctFormCount);
        Assert.Equal(2, data.Decisions.Count);
        Assert.Equal(9, data.Rows.Count);
        Assert.Equal(3, data.HarvestProvenance.Count);
        Assert.Equal(3, data.SyntheticProvenance.Count);
        Assert.Equal(3, data.Gaps.Rows.Count);
    }

    /// <summary>Proves rows carry their file, logical split, line and parsed offsets.</summary>
    [Fact]
    public void TextEnDataSet_Load_Rows_CarryFileSplitLineAndOffsets()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var data = TextEnDataSet.Load(fixture.Root, new CheckReport());

        // Assert
        var row = Assert.Single(data.Rows, candidate => candidate.Id == "hind-1");
        Assert.Equal("holdout-independent.tsv", row.File);
        Assert.Equal("holdout-independent", row.Split);
        Assert.Equal("test", row.SplitColumn);
        Assert.Equal(2, row.Line);
        Assert.Equal(7, row.Start);
        Assert.Equal(11, row.End);
        Assert.Equal("holdout-independent.tsv:2", row.Location);
    }

    /// <summary>Proves the forms of a reading are indexed and an unknown reading has none.</summary>
    [Fact]
    public void TextEnDataSet_FormsOf_KnownAndUnknownReading_ReturnListedForms()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        var data = TextEnDataSet.Load(fixture.Root, new CheckReport());

        // Act
        var verbForms = data.FormsOf("lead.verb");
        var unknown = data.FormsOf("nope.noun");

        // Assert
        Assert.Equal(["lead", "leads"], verbForms.Order());
        Assert.Empty(unknown);
    }

    /// <summary>Proves a missing required file is a finding and the data set is still usable.</summary>
    [Fact]
    public void TextEnDataSet_Load_MissingFile_ReportsFindingAndContinues()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        File.Delete(fixture.PathOf("forms.tsv"));
        var report = new CheckReport();

        // Act
        var data = TextEnDataSet.Load(fixture.Root, report);

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "forms.tsv" && finding.Message.Contains("missing"));
        Assert.Empty(data.Forms);
        Assert.Equal(9, data.Rows.Count);
    }

    /// <summary>Proves non-integer offsets are a finding and leave the offsets null instead of throwing.</summary>
    [Fact]
    public void TextEnDataSet_Load_NonIntegerOffsets_ReportsFindingAndKeepsRow()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "syn-00001", ("start", "four"), ("end", "8.5"));
        var report = new CheckReport();

        // Act
        var data = TextEnDataSet.Load(fixture.Root, report);

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "train.tsv:3" && finding.Message.Contains("must be integers"));
        var row = Assert.Single(data.Rows, candidate => candidate.Id == "syn-00001");
        Assert.Null(row.Start);
        Assert.Null(row.End);
    }

    /// <summary>Proves a wrong header of a data file is a finding and its rows are not loaded.</summary>
    [Fact]
    public void TextEnDataSet_Load_WrongDataHeader_ReportsFindingAndLoadsNoRowsOfThatFile()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Replace("dev.tsv", "reading_id", "readingid");
        var report = new CheckReport();

        // Act
        var data = TextEnDataSet.Load(fixture.Root, report);

        // Assert
        Assert.True(DatasetFixture.Has(report, "format", "header is"));
        Assert.DoesNotContain(data.Rows, row => row.File == "dev.tsv");
    }

    /// <summary>Proves other TSV files of the folder are format-checked too.</summary>
    [Fact]
    public void TextEnDataSet_Load_OtherTsvFileWithBadFormat_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("audit/notes.tsv", "a\tb\n1\n");
        var report = new CheckReport();

        // Act
        _ = TextEnDataSet.Load(fixture.Root, report);

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "audit/notes.tsv:2" && finding.Message.Contains("column count"));
    }

    /// <summary>Proves the vocabulary gives each source family its allowed licenses and kind: generated, single license, and one of two.</summary>
    [Theory]
    [InlineData("synthetic", true, "CC0-1.0")]
    [InlineData("harvest-masc", false, "CC-BY-3.0-US")]
    [InlineData("harvest-silver-llm", false, "CC0-1.0|Public-Domain")]
    public void SourceInfo_TryGet_KnownSource_ReturnsKindAndLicenses(string source, bool generated, string licenses)
    {
        // Arrange / Act
        var known = SourceInfo.TryGet(source, out var info);

        // Assert
        Assert.True(known);
        Assert.Equal(generated, info.IsGenerated);
        Assert.Equal(licenses.Split('|').Order(), info.AllowedLicenses.Order());
    }

    /// <summary>Proves the vocabulary tells harvested, synthetic and independent holdout sources apart.</summary>
    [Theory]
    [InlineData("synthetic-v2", "Synthetic")]
    [InlineData("independent-holdout", "IndependentHoldout")]
    [InlineData("harvest-silver-llm", "Harvest")]
    public void SourceInfo_KindOf_Source_ReturnsKindOrNull(string source, string? expected)
    {
        // Arrange / Act
        var kind = SourceInfo.KindOf(source);

        // Assert
        Assert.Equal(expected, kind?.ToString());
    }

    /// <summary>Proves a source outside the closed vocabulary is not found and has no kind.</summary>
    [Theory]
    [InlineData("harvest-wikipedia")]
    [InlineData("")]
    public void SourceInfo_TryGet_UnknownSource_ReturnsFalse(string source)
    {
        // Arrange / Act
        var known = SourceInfo.TryGet(source, out _);
        var kind = SourceInfo.KindOf(source);

        // Assert
        Assert.False(known);
        Assert.Null(kind);
    }
}
