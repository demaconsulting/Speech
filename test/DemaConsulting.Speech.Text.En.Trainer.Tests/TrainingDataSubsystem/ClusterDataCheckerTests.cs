using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingDataSubsystem;

/// <summary>
///     Unit tests for <see cref="ClusterDataChecker"/>: the schema of the three word-cluster tables, the document-level
///     leakage rule and the offline comparison of <c>clustering.json</c> with the committed files and the tokenizer.
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
        Assert.Contains(report.Info, line => line.Contains("word-cluster table: 3 words, 3 paths, 1 removals", StringComparison.Ordinal));
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

    /// <summary>Proves the four files are committed together: one missing file is a finding.</summary>
    [Fact]
    public void ClusterDataChecker_Run_OneFileMissing_IsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles();
        File.Delete(fixture.PathOf(ClusterDataChecker.TableFile));

        // Act
        var report = fixture.CheckClusters();

        // Assert
        var finding = Assert.Single(report.Findings);
        Assert.Equal((ClusterDataChecker.CheckName, ClusterDataChecker.TableFile), (finding.Check, finding.Location));
    }

    /// <summary>Proves an edit of the source list, the removals or the table after induction makes the record stale.</summary>
    [Theory]
    [InlineData(ClusterDataChecker.SourcesFile, "A Book", "A Novel", ClusterDataChecker.SourcesShaField)]
    [InlineData(ClusterDataChecker.RemovalsFile, "syn-00003", "syn-00004", ClusterDataChecker.RemovalsShaField)]
    [InlineData(ClusterDataChecker.TableFile, "the\t01\t30", "the\t01\t31", ClusterDataChecker.TableShaField)]
    public void ClusterDataChecker_Run_FileChangedAfterInduction_RecordIsStale(string file, string oldText, string newText, string field)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles();
        fixture.Replace(file, oldText, newText);

        // Act
        var report = fixture.CheckClusters();

        // Assert
        var finding = Assert.Single(report.Findings);
        Assert.Equal(ClusterDataChecker.RecordFile, finding.Location);
        Assert.Contains($"{field} ", finding.Message, StringComparison.Ordinal);
        Assert.Contains($"does not match {file}", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a record from another tokenizer is a finding, and an unknown tokenizer version skips the comparison.</summary>
    [Fact]
    public void ClusterDataChecker_Run_TokenizerVersion_IsCompared()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles("1.0.0-old");

        // Act
        var mismatch = fixture.CheckClusters("1.0.0-new");
        var unknown = fixture.CheckClusters(null);

        // Assert
        var finding = Assert.Single(mismatch.Findings);
        Assert.Contains("tokenizerVersion '1.0.0-old' is not the tokenizer of this build '1.0.0-new'", finding.Message, StringComparison.Ordinal);
        Assert.Empty(unknown.Findings);
        Assert.Contains(unknown.Info, line => line.Contains("tokenizer version not compared", StringComparison.Ordinal));
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
        // Arrange: break the used row, then make the record current so only the rule under test fails
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles();
        fixture.EditRow(ClusterDataChecker.SourcesFile, "gutenberg-1", (column, value));
        fixture.RewriteClusterRecord();

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
        fixture.RewriteClusterRecord();

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
        fixture.RewriteClusterRecord();

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
        fixture.RewriteClusterRecord();

        // Act
        var report = fixture.CheckClusters();

        // Assert
        Assert.True(DatasetFixture.Has(report, ClusterDataChecker.CheckName, "unique and sorted by source_id, paragraph and sentence"));
    }

    /// <summary>Proves each rule of the table: words, order, paths, counts, and the limits of the record.</summary>
    [Theory]
    [InlineData("the\t01\t30\n", "the\t0\t30\n", "is a prefix of")]
    [InlineData("the\t01\t30\n", "the\t0x\t30\n", "not a bit string")]
    [InlineData("the\t01\t30\n", "the\t01\t3\n", "below the record's minCount 5")]
    [InlineData("the\t01\t30\n", "the\t01\t030\n", "not a positive integer")]
    [InlineData("the\t01\t30\n", "zzz\t01\t30\n", "unique and in ordinal order")]
    [InlineData("the\t01\t30\n", "th\u0101\t01\t30\n", "eligible characters")]
    [InlineData("wind\t1\t5\n", "wind\t10\t5\nzeal\t11\t5\n", "4 distinct paths but the record has 3 clusters")]
    public void ClusterDataChecker_Run_InvalidTable_IsFinding(string oldRow, string newRow, string message)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles();
        fixture.Replace(ClusterDataChecker.TableFile, oldRow, newRow);
        fixture.RewriteClusterRecord();

        // Act
        var report = fixture.CheckClusters();

        // Assert
        Assert.True(DatasetFixture.Has(report, ClusterDataChecker.CheckName, message), string.Join('\n', report.Findings));
    }

    /// <summary>Proves the record's row, path and book counts must match the files.</summary>
    [Fact]
    public void ClusterDataChecker_Run_RecordCountsDiffer_IsFinding()
    {
        // Arrange: a fourth word in an existing cluster and a book count change, with a record of the old counts
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles();
        var record = File.ReadAllText(fixture.PathOf(ClusterDataChecker.RecordFile));
        fixture.AppendRow(ClusterDataChecker.TableFile, "zeal", "1", "6");
        fixture.EditRow(ClusterDataChecker.SourcesFile, "gutenberg-2", ("status", "rejected-license"));
        var updated = ClusteringRecord.Parse(System.Text.Encoding.UTF8.GetBytes(record));
        string Sha(string file) => GutenbergBody.Sha256Hex(File.ReadAllBytes(fixture.PathOf(file)));
        updated = updated with
        {
            Inputs = updated.Inputs with { SourcesSha256 = Sha(ClusterDataChecker.SourcesFile), UsedBooks = 2, ExcludedBooks = 0 },
            Result = updated.Result with { TableSha256 = Sha(ClusterDataChecker.TableFile) },
        };
        fixture.WriteBytes(ClusterDataChecker.RecordFile, updated.Write());

        // Act
        var report = fixture.CheckClusters();

        // Assert
        Assert.True(DatasetFixture.Has(report, ClusterDataChecker.CheckName, "words 3 and paths 3 do not match word-clusters.tsv (4 rows, 3 paths)"));
        Assert.True(DatasetFixture.Has(report, ClusterDataChecker.CheckName, "usedBooks 2 and excludedBooks 0 do not match cluster-sources.tsv (1 used, 1 not)"));
    }

    /// <summary>Proves an unreadable record or one of another schema is a finding.</summary>
    [Theory]
    [InlineData("{", "not a readable record")]
    [InlineData("{\"schema\": 2}", "schema 2 is not 1")]
    [InlineData("{\"schema\": 1}", "not a readable record")]
    public void ClusterDataChecker_Run_UnreadableRecord_IsFinding(string json, string message)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteClusterFiles();
        fixture.Write(ClusterDataChecker.RecordFile, json);

        // Act
        var report = fixture.CheckClusters();

        // Assert
        Assert.True(DatasetFixture.Has(report, ClusterDataChecker.CheckName, message));
    }
}
