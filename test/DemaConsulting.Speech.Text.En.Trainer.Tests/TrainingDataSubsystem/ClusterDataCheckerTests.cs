using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingDataSubsystem;

/// <summary>
///     Unit tests for <see cref="ClusterDataChecker"/>: the presence and schema of the source list and the removals,
///     the pin rule and the document-level leakage rule.
/// </summary>
public sealed class ClusterDataCheckerTests
{
    /// <summary>Proves consistent word-cluster files pass and the counts are reported.</summary>
    [Fact]
    public void ClusterDataChecker_Run_ConsistentFiles_NoFindings()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles();

        // Act
        var report = fixture.CheckClusters();

        // Assert
        Assert.Empty(report.Findings);
        Assert.Contains(report.Info, line => line.Contains("word-cluster sources: 2 books, 1 used, 1 not used", StringComparison.Ordinal));
    }

    /// <summary>Proves a data folder without any word-cluster file skips the check with an informational line.</summary>
    [Fact]
    public void ClusterDataChecker_Run_NoFiles_IsSkipped()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var report = fixture.CheckClusters();

        // Assert
        Assert.Empty(report.Findings);
        Assert.Contains(report.Info, line => line.Contains("word-cluster check skipped", StringComparison.Ordinal));
    }

    /// <summary>Proves the two files are committed together: one missing file is a finding.</summary>
    [Fact]
    public void ClusterDataChecker_Run_OneFileMissing_IsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles();
        File.Delete(fixture.PathOf(ClusterDataChecker.RemovalsFile));

        // Act
        var report = fixture.CheckClusters();

        // Assert
        var finding = Assert.Single(report.Findings);
        Assert.Equal((ClusterDataChecker.CheckName, ClusterDataChecker.RemovalsFile), (finding.Check, finding.Location));
    }

    /// <summary>Proves a used book that holds development or natural-holdout sentences is a leakage finding.</summary>
    [Theory]
    [InlineData("harvest-00003")]
    [InlineData("harvest-u00001")]
    public void ClusterDataChecker_Run_UsedDevelopmentOrHoldoutBook_IsLeakage(string provenanceId)
    {
        // Arrange: the development (or holdout) row's source document becomes the used book
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles();
        fixture.EditRow("harvest-provenance.tsv", provenanceId, ("source_id", "gutenberg-1"));

        // Act
        var report = fixture.CheckClusters();

        // Assert
        var finding = Assert.Single(report.Findings);
        Assert.Equal((ClusterDataChecker.LeakageCheckName, $"{ClusterDataChecker.SourcesFile}:2"), (finding.Check, finding.Location));
        Assert.Contains("gutenberg-1 holds", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves each rule of the source list: pins, license basis, mirror, year, status, group and order.</summary>
    [Theory]
    [InlineData("body_sha256", "-", "needs a lowercase 64-digit body_sha256")]
    [InlineData("license_basis", "US public domain", "must state 'Public domain in the USA'")]
    [InlineData("url", "https://www.gutenberg.org/cache/epub/1/pg1.txt", "on an official mirror")]
    [InlineData("url", "http://gutenberg.pglaf.org/cache/epub/1/pg1.txt", "on an official mirror")]
    [InlineData("url", "https://gutenberg.pglaf.org/cache/epub/7/pg7.txt", "plain text of e-book 1")]
    [InlineData("year", "1930", "not a year of 1929 or earlier")]
    [InlineData("status", "maybe", "unknown status")]
    [InlineData("group", "core-other", "unknown group")]
    [InlineData("source_id", "gutenberg-3", "sorted by ascending e-book number")]
    [InlineData("title", " ", "title is empty")]
    public void ClusterDataChecker_Run_InvalidSourceRow_IsFinding(string column, string value, string message)
    {
        // Arrange: break the used row
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles();
        fixture.EditRow(ClusterDataChecker.SourcesFile, "gutenberg-1", (column, value));

        // Act
        var report = fixture.CheckClusters();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Check == ClusterDataChecker.CheckName && finding.Message.Contains(message, StringComparison.Ordinal));
    }

    /// <summary>Proves a book that is not used carries no pin.</summary>
    [Fact]
    public void ClusterDataChecker_Run_PinOnExcludedBook_IsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles();
        fixture.EditRow(ClusterDataChecker.SourcesFile, "gutenberg-2", ("body_sha256", new string('e', 64)), ("body_bytes", "9"));

        // Act
        var report = fixture.CheckClusters();

        // Assert
        Assert.True(DatasetFixture.Has(report, ClusterDataChecker.CheckName, "gutenberg-2: a book with status 'excluded-dev' has no pin"));
    }

    /// <summary>Proves each rule of the removals: a used source, the rule and file names, and the order.</summary>
    [Theory]
    [InlineData("gutenberg-2\t0\t1\texact\tdev.tsv\tsyn-00003\t", "is not a used book")]
    [InlineData("gutenberg-1\t0\t1\tfuzzy\tdev.tsv\tsyn-00003\t", "the rule must be one of")]
    [InlineData("gutenberg-1\t0\t1\texact\ttrain.tsv\tsyn-00003\t", "the rule must be one of")]
    [InlineData("gutenberg-1\t0\tx\texact\tdev.tsv\tsyn-00003\t", "must be non-negative integers")]
    public void ClusterDataChecker_Run_InvalidRemoval_IsFinding(string rowPrefix, string message)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles();
        fixture.Replace(ClusterDataChecker.RemovalsFile, "gutenberg-1\t0\t1\texact\tdev.tsv\tsyn-00003\t", rowPrefix);

        // Act
        var report = fixture.CheckClusters();

        // Assert
        Assert.True(DatasetFixture.Has(report, ClusterDataChecker.CheckName, message));
    }

    /// <summary>Proves removal rows are unique and sorted.</summary>
    [Fact]
    public void ClusterDataChecker_Run_UnsortedRemovals_IsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles();
        fixture.AppendRow(ClusterDataChecker.RemovalsFile, "gutenberg-1", "0", "0", "near", "dev.tsv", "syn-00003", new string('f', 64));

        // Act
        var report = fixture.CheckClusters();

        // Assert
        Assert.True(DatasetFixture.Has(report, ClusterDataChecker.CheckName, "unique and sorted by source_id, paragraph and sentence"));
    }
}
