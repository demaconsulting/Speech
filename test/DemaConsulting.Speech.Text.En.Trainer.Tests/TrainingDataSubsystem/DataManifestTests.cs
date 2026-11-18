using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingDataSubsystem;

/// <summary>
///     Unit tests for <see cref="DataManifest"/>: hash, size and row verification, complete and sorted listing.
/// </summary>
public sealed class DataManifestTests
{
    /// <summary>Proves a manifest that matches the files produces no findings.</summary>
    [Fact]
    public void DataManifest_Verify_MatchingManifest_ReportsNothing()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var report = fixture.CheckManifest();

        // Assert
        Assert.Empty(report.Findings);
    }

    /// <summary>Proves a single changed byte in a listed file is reported as a hash mismatch.</summary>
    [Fact]
    public void DataManifest_Verify_ChangedByte_ReportsHashMismatch()
    {
        // Arrange: same length, different content
        using var fixture = new DatasetFixture();
        fixture.Replace("README.md", "Fixture data", "Fixture date");

        // Act
        var report = fixture.CheckManifest();

        // Assert
        var finding = Assert.Single(report.Findings);
        Assert.Equal("README.md", finding.Location);
        Assert.Contains("SHA-256", finding.Message);
    }

    /// <summary>Proves a wrong recorded byte count and a wrong recorded row count are each reported with the manifest value.</summary>
    [Fact]
    public void DataManifest_Verify_WrongBytes_ReportsSizeMismatch()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        SetCell(fixture, "README.md", 2, "1");
        SetCell(fixture, "train.tsv", 3, "99");

        // Act
        var report = fixture.CheckManifest();

        // Assert
        Assert.True(DatasetFixture.Has(report, "manifest", "bytes but MANIFEST.tsv lists 1"));
        Assert.True(DatasetFixture.Has(report, "manifest", "row count is 3 but MANIFEST.tsv lists 99"));
    }

    /// <summary>Proves a listed file that does not exist is reported.</summary>
    [Fact]
    public void DataManifest_Verify_MissingFile_ReportsListedFileMissing()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        File.Delete(fixture.PathOf("README.md"));

        // Act
        var report = fixture.CheckManifest();

        // Assert
        Assert.True(DatasetFixture.Has(report, "manifest", "listed file 'README.md' does not exist"));
    }

    /// <summary>Proves a file that is not listed is reported.</summary>
    [Fact]
    public void DataManifest_Verify_UnlistedFile_IsReported()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("extra.md", "# extra\n");

        // Act
        var report = fixture.CheckManifest();

        // Assert
        var finding = Assert.Single(report.Findings);
        Assert.Equal("extra.md", finding.Location);
        Assert.Contains("not listed", finding.Message);
    }

    /// <summary>Proves the local caches <c>external/</c> and <c>work/</c> are neither listed nor required to be.</summary>
    [Fact]
    public void DataManifest_Verify_LocalCaches_AreIgnored()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("external/cmudict/cmudict.dict", "cache\n");
        fixture.Write("work/scratch.tsv", "a\tb\n");

        // Act
        var report = fixture.CheckManifest();

        // Assert
        Assert.Empty(report.Findings);
    }

    /// <summary>Proves entries that are out of order and a duplicated entry are each reported.</summary>
    [Fact]
    public void DataManifest_Verify_UnsortedEntries_ReportsOrder()
    {
        // Arrange: swap the first two entries (after the two comment lines) and repeat the third
        using var fixture = new DatasetFixture();
        fixture.EditManifestLines(lines =>
        {
            (lines[2], lines[3]) = (lines[3], lines[2]);
            lines.Insert(5, lines[4]);
        });

        // Act
        var report = fixture.CheckManifest();

        // Assert
        Assert.True(DatasetFixture.Has(report, "manifest", "is not in sorted order"));
        Assert.True(DatasetFixture.Has(report, "manifest", "duplicate entry for"));
    }

    /// <summary>Proves an upper-case or short digest is reported.</summary>
    [Fact]
    public void DataManifest_Verify_UpperCaseDigest_ReportsMalformedDigest()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        SetCell(fixture, "README.md", 1, FirstDigest(fixture, "README.md").ToUpperInvariant());

        // Act
        var report = fixture.CheckManifest();

        // Assert
        Assert.True(DatasetFixture.Has(report, "manifest", "lower-case 64-digit SHA-256"));
    }

    /// <summary>Proves a backslash or parent-folder path is rejected instead of being followed.</summary>
    [Theory]
    [InlineData("audit\\audit-summary.md")]
    [InlineData("../outside.md")]
    [InlineData("/rooted.md")]
    public void DataManifest_Verify_NonRelativePath_IsRejected(string path)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditManifestLines(lines => lines.Add($"{path}\t{new string('0', 64)}\t1\t1"));

        // Act
        var report = fixture.CheckManifest();

        // Assert
        Assert.True(DatasetFixture.Has(report, "manifest", "is not a relative path"));
    }

    /// <summary>Proves a line with the wrong number of columns and an entry with a non-integer size are each reported.</summary>
    [Fact]
    public void DataManifest_Verify_MalformedLine_ReportsColumns()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditManifestLines(lines => lines.Add("only-one-column"));
        SetCell(fixture, "README.md", 2, "many");

        // Act
        var report = fixture.CheckManifest();

        // Assert
        Assert.True(DatasetFixture.Has(report, "manifest", "column count"));
        Assert.True(DatasetFixture.Has(report, "manifest", "must be integers"));
    }

    /// <summary>Proves a missing manifest is a finding on the manifest check.</summary>
    [Fact]
    public void DataManifest_Verify_MissingManifest_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        File.Delete(fixture.PathOf("MANIFEST.tsv"));

        // Act
        var report = fixture.CheckManifest();

        // Assert
        Assert.True(DatasetFixture.Has(report, "manifest", "required file is missing"));
    }

    /// <summary>Proves lines are counted like the manifest does: terminators plus an unterminated last line.</summary>
    [Theory]
    [InlineData("", 0)]
    [InlineData("a\nb", 2)]
    [InlineData("a\nb\n", 2)]
    public void DataManifest_CountLines_CountsTerminatedAndUnterminatedLines(string text, int expected)
    {
        // Arrange / Act
        var lines = DataManifest.CountLines(System.Text.Encoding.UTF8.GetBytes(text));

        // Assert
        Assert.Equal(expected, lines);
    }

    /// <summary>Reads the digest of an entry.</summary>
    /// <param name="fixture">The fixture.</param>
    /// <param name="path">The entry path.</param>
    /// <returns>The digest text.</returns>
    private static string FirstDigest(DatasetFixture fixture, string path) =>
        fixture.Read("MANIFEST.tsv").Split('\n').First(line => line.StartsWith(path + "\t", StringComparison.Ordinal)).Split('\t')[1];

    /// <summary>Sets one cell of a manifest entry.</summary>
    /// <param name="fixture">The fixture.</param>
    /// <param name="path">The entry path.</param>
    /// <param name="column">The zero-based column.</param>
    /// <param name="value">The new value.</param>
    private static void SetCell(DatasetFixture fixture, string path, int column, string value) =>
        fixture.EditManifestLines(lines =>
        {
            var index = lines.FindIndex(line => line.StartsWith(path + "\t", StringComparison.Ordinal));
            var cells = lines[index].Split('\t');
            cells[column] = value;
            lines[index] = string.Join('\t', cells);
        });
}
