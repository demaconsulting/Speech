using System.Text;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingDataSubsystem;

/// <summary>
///     Unit tests for <see cref="TsvTable"/>: strict file format, exact headers, comment lines and the absence of quoting.
/// </summary>
public sealed class TsvTableTests
{
    /// <summary>The header used by the small test files.</summary>
    private static readonly string[] Header = ["a", "b"];

    /// <summary>Loads a file of the fixture with a two-column header.</summary>
    /// <param name="fixture">The fixture.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <param name="layout">The layout; defaults to the two-column header.</param>
    /// <returns>The table.</returns>
    private static TsvTable Load(DatasetFixture fixture, CheckReport report, TsvLayout? layout = null) =>
        TsvTable.Load(fixture.PathOf("t.tsv"), "t.tsv", report, layout ?? new TsvLayout(ExpectedHeader: Header));

    /// <summary>Proves a well-formed file loads its header and rows with line numbers.</summary>
    [Fact]
    public void TsvTable_Load_ValidFile_ReturnsHeaderAndRowsWithoutFindings()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("t.tsv", "a\tb\n1\t2\n3\t4\n");
        var report = new CheckReport();

        // Act
        var table = Load(fixture, report);

        // Assert
        Assert.Empty(report.Findings);
        Assert.Equal(Header, table.Header);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(3, table.Rows[1].LineNumber);
        Assert.Equal("4", table.Rows[1][1]);
        Assert.Equal(1, table.ColumnIndex("b"));
        Assert.Equal(-1, table.ColumnIndex("missing"));
    }

    /// <summary>Proves a missing file is a finding, not an exception.</summary>
    [Fact]
    public void TsvTable_Load_MissingFile_ReportsFindingAndReturnsEmptyTable()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        var report = new CheckReport();

        // Act
        var table = Load(fixture, report);

        // Assert
        Assert.False(table.Exists);
        Assert.Empty(table.Rows);
        Assert.True(DatasetFixture.Has(report, "format", "required file is missing"));
    }

    /// <summary>Proves a byte order mark is reported and does not corrupt the header.</summary>
    [Fact]
    public void TsvTable_Load_ByteOrderMark_ReportsFindingOnly()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteBytes("t.tsv", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("a\tb\n1\t2\n")]);
        var report = new CheckReport();

        // Act
        var table = Load(fixture, report);

        // Assert
        Assert.Single(report.Findings);
        Assert.True(DatasetFixture.Has(report, "format", "byte order mark"));
        Assert.Single(table.Rows);
    }

    /// <summary>Proves a carriage return is reported with the line it occurs on.</summary>
    [Fact]
    public void TsvTable_Load_CarriageReturn_ReportsFindingWithLine()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("t.tsv", "a\tb\n1\t2\r\n");
        var report = new CheckReport();

        // Act
        _ = Load(fixture, report);

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "t.tsv:2" && finding.Message.Contains("CR character"));
    }

    /// <summary>Proves a missing final newline is reported.</summary>
    [Fact]
    public void TsvTable_Load_MissingFinalNewline_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("t.tsv", "a\tb\n1\t2");
        var report = new CheckReport();

        // Act
        _ = Load(fixture, report);

        // Assert
        Assert.True(DatasetFixture.Has(report, "format", "missing final newline"));
    }

    /// <summary>Proves an empty line is reported with its line number.</summary>
    [Fact]
    public void TsvTable_Load_EmptyLine_ReportsFindingWithLine()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("t.tsv", "a\tb\n\n1\t2\n");
        var report = new CheckReport();

        // Act
        var table = Load(fixture, report);

        // Assert
        Assert.Contains(
            report.Findings,
            finding => finding.Location == "t.tsv:2" && finding.Message.Contains("empty", StringComparison.OrdinalIgnoreCase));
        Assert.Single(table.Rows);
    }

    /// <summary>Proves an empty file is reported.</summary>
    [Fact]
    public void TsvTable_Load_EmptyFile_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("t.tsv", string.Empty);
        var report = new CheckReport();

        // Act
        _ = Load(fixture, report);

        // Assert
        Assert.True(DatasetFixture.Has(report, "format", "file is empty"));
    }

    /// <summary>Proves a row with the wrong number of columns is reported and left out of the rows.</summary>
    [Fact]
    public void TsvTable_Load_WrongColumnCount_ReportsFindingAndSkipsRow()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("t.tsv", "a\tb\n1\t2\t3\n4\t5\n");
        var report = new CheckReport();

        // Act
        var table = Load(fixture, report);

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "t.tsv:2" && finding.Message.Contains("column count 3 (expected 2)"));
        Assert.Single(table.Rows);
        Assert.Equal("4", table.Rows[0][0]);
    }

    /// <summary>Proves a line that is not in Unicode normalization form C is reported.</summary>
    [Fact]
    public void TsvTable_Load_NonNfcLine_ReportsFinding()
    {
        // Arrange: "e" followed by a combining acute accent is the decomposed (NFD) form of the letter e-acute
        using var fixture = new DatasetFixture();
        fixture.Write("t.tsv", "a\tb\n1\te\u0301\n");
        var report = new CheckReport();

        // Act
        _ = Load(fixture, report);

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "t.tsv:2" && finding.Message.Contains("NFC"));
    }

    /// <summary>Proves bytes that are not valid UTF-8 are reported instead of being replaced.</summary>
    [Fact]
    public void TsvTable_Load_InvalidUtf8_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteBytes("t.tsv", [0x61, 0x09, 0x62, 0x0A, 0xFF, 0x09, 0x32, 0x0A]);
        var report = new CheckReport();

        // Act
        var table = Load(fixture, report);

        // Assert
        Assert.True(DatasetFixture.Has(report, "format", "not valid UTF-8"));
        Assert.Empty(table.Rows);
    }

    /// <summary>Proves a different header is reported and no row is handed to the checks.</summary>
    [Fact]
    public void TsvTable_Load_WrongHeader_ReportsFindingAndReturnsNoRows()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("t.tsv", "a\tc\n1\t2\n");
        var report = new CheckReport();

        // Act
        var table = Load(fixture, report);

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "t.tsv:1" && finding.Message.Contains("header is"));
        Assert.Empty(table.Rows);
    }

    /// <summary>Proves comment lines are skipped when the layout allows them.</summary>
    [Fact]
    public void TsvTable_Load_CommentLinesAllowed_AreSkipped()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("t.tsv", "# comment\n# another\n1\t2\n");
        var report = new CheckReport();

        // Act
        var table = Load(fixture, report, new TsvLayout(AllowComments: true, HasHeader: false));

        // Assert
        Assert.Empty(report.Findings);
        Assert.Single(table.Rows);
        Assert.Equal(3, table.Rows[0].LineNumber);
    }

    /// <summary>Proves a comment line is data (and so a column-count finding) when comments are not allowed.</summary>
    [Fact]
    public void TsvTable_Load_CommentLinesNotAllowed_AreTreatedAsRows()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("t.tsv", "a\tb\n# comment\n1\t2\n");
        var report = new CheckReport();

        // Act
        var table = Load(fixture, report);

        // Assert
        Assert.True(DatasetFixture.Has(report, "format", "column count 1 (expected 2)"));
        Assert.Single(table.Rows);
    }

    /// <summary>Proves a sentence that starts with a double quote keeps the quote and its columns (no quoting).</summary>
    [Fact]
    public void TsvTable_Load_SentenceStartingWithQuote_KeepsQuoteAndColumns()
    {
        // Arrange: a quoting reader would swallow the quote and shift offsets
        using var fixture = new DatasetFixture();
        fixture.Write("t.tsv", "a\tb\n\"quoted\" sentence, with \"marks\"\t2\n");
        var report = new CheckReport();

        // Act
        var table = Load(fixture, report);

        // Assert
        Assert.Empty(report.Findings);
        Assert.Equal("\"quoted\" sentence, with \"marks\"", table.Rows[0][0]);
        Assert.Equal("2", table.Rows[0][1]);
    }

    /// <summary>Proves the finding location of a format defect names the file and the 1-based line.</summary>
    [Fact]
    public void TsvTable_Load_FormatDefect_FindingCarriesFileAndLine()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("t.tsv", "a\tb\n1\t2\n3\n");
        var report = new CheckReport();

        // Act
        _ = Load(fixture, report);

        // Assert
        Assert.Equal("check-data: format: t.tsv:3: column count 1 (expected 2)", report.Findings.Single().ToString());
    }
}
