using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingDataSubsystem;

/// <summary>
///     Unit tests for <see cref="CheckReport"/>: the exit code, the deterministic output and the per-check cap.
/// </summary>
public sealed class CheckReportTests
{
    /// <summary>Proves a report without findings exits with 0 and says OK.</summary>
    [Fact]
    public void CheckReport_ExitCode_NoFindings_IsZeroAndSummaryIsOk()
    {
        // Arrange
        var report = new CheckReport();

        // Act
        var exitCode = report.ExitCode;

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("check-data: OK (0 findings)", report.Summary);
    }

    /// <summary>Proves any finding makes the exit code 1 and the summary count them.</summary>
    [Fact]
    public void CheckReport_ExitCode_AnyFinding_IsOneAndSummaryCountsFindings()
    {
        // Arrange
        var report = new CheckReport();
        report.Add("schema", "train.tsv:2", "bad");
        report.Add("license", "dev.tsv:3", "worse");

        // Act
        var exitCode = report.ExitCode;

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Equal("check-data: FAILED (2 findings)", report.Summary);
    }

    /// <summary>Proves findings are written in insertion order in the documented line format.</summary>
    [Fact]
    public void CheckReport_WriteTo_Findings_AreWrittenInInsertionOrderWithLocation()
    {
        // Arrange
        var report = new CheckReport();
        report.AddInfo("an info line");
        report.Add("schema", "train.tsv:2", "first");
        report.Add("license", "dev.tsv:3", "second");
        var writer = new StringWriter();

        // Act
        report.WriteTo(writer);

        // Assert: info first, then the findings in order, then the summary
        var lines = writer.ToString().TrimEnd().Split(Environment.NewLine);
        Assert.Equal(
            [
                "an info line",
                "check-data: schema: train.tsv:2: first",
                "check-data: license: dev.tsv:3: second",
                "check-data: FAILED (2 findings)",
            ],
            lines);
    }

    /// <summary>Proves the output is capped per check while the finding count still includes every finding.</summary>
    [Fact]
    public void CheckReport_WriteTo_MoreFindingsThanCap_PrintsCapAndCountsSuppressed()
    {
        // Arrange
        var report = new CheckReport(2);
        for (var index = 1; index <= 5; index++)
        {
            report.Add("schema", $"train.tsv:{index}", $"problem {index}");
        }

        report.Add("license", "dev.tsv:1", "other check is not capped by schema");
        var writer = new StringWriter();

        // Act
        report.WriteTo(writer);

        // Assert
        var text = writer.ToString();
        Assert.Contains("problem 2", text);
        Assert.DoesNotContain("problem 3", text);
        Assert.Contains("check-data: schema: (3 more findings not shown)", text);
        Assert.Contains("other check is not capped by schema", text);
        Assert.Contains("check-data: FAILED (6 findings)", text);
        Assert.Equal(6, report.FindingCount);
    }

    /// <summary>Proves a cap below 1 is rejected.</summary>
    [Fact]
    public void CheckReport_Constructor_CapBelowOne_ThrowsArgumentOutOfRangeException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new CheckReport(0));
    }

    /// <summary>Proves a finding renders as the <c>check-data: check: location: message</c> line.</summary>
    [Fact]
    public void CheckReport_Finding_ToString_UsesTheDocumentedFormat()
    {
        // Arrange
        var finding = new Finding("manifest", "README.md", "changed");

        // Act
        var text = finding.ToString();

        // Assert
        Assert.Equal("check-data: manifest: README.md: changed", text);
    }
}
