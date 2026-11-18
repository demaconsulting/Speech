using System.Text;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

/// <summary>
///     One data row of a <see cref="TsvTable"/>: its cells and the 1-based line it came from.
/// </summary>
/// <param name="LineNumber">The 1-based line number in the file.</param>
/// <param name="Cells">The tab-separated cells; the count equals the table's column count.</param>
internal sealed record TsvRow(int LineNumber, IReadOnlyList<string> Cells)
{
    /// <summary>
    ///     Gets the cell at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based column index.</param>
    /// <returns>The cell text.</returns>
    public string this[int index] => Cells[index];
}

/// <summary>
///     Describes what a TSV file is expected to look like when it is loaded.
/// </summary>
/// <param name="ExpectedHeader">The exact header cells, or <see langword="null"/> to accept any header.</param>
/// <param name="AllowComments">Whether lines starting with <c>#</c> are comments (only <c>MANIFEST.tsv</c> has them).</param>
/// <param name="HasHeader">Whether the first non-comment line is a header row rather than data.</param>
/// <param name="Check">The check name findings are attributed to.</param>
internal sealed record TsvLayout(
    IReadOnlyList<string>? ExpectedHeader = null,
    bool AllowComments = false,
    bool HasHeader = true,
    string Check = "format");

/// <summary>
///     A strictly validated, tab-separated data file loaded into rows.
/// </summary>
/// <remarks>
///     The data files are UTF-8 without byte order mark, LF-terminated, NFC-normalized and carry no quoting at all:
///     a sentence may start with a double quote, and a quoting reader would then shift every offset after it.
///     This reader therefore splits on the tab character only. Every format defect is reported as a finding
///     (never thrown), and rows that cannot be trusted (wrong column count, or all rows when the header differs)
///     are left out of <see cref="Rows"/> so the checks that follow can index cells safely.
/// </remarks>
internal sealed class TsvTable
{
    /// <summary>The strict UTF-8 decoder: invalid byte sequences throw instead of becoming replacement characters.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>The byte order mark of UTF-8.</summary>
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>Gets the display name of the file (its path relative to the data root).</summary>
    public string Name { get; }

    /// <summary>Gets a value indicating whether the file exists.</summary>
    public bool Exists { get; }

    /// <summary>Gets the header cells; empty when the file has no header or could not be read.</summary>
    public IReadOnlyList<string> Header { get; }

    /// <summary>Gets the trustworthy data rows.</summary>
    public IReadOnlyList<TsvRow> Rows { get; }

    /// <summary>
    ///     Initializes a table.
    /// </summary>
    /// <param name="name">The display name.</param>
    /// <param name="exists">Whether the file exists.</param>
    /// <param name="header">The header cells.</param>
    /// <param name="rows">The data rows.</param>
    private TsvTable(string name, bool exists, IReadOnlyList<string> header, IReadOnlyList<TsvRow> rows)
    {
        Name = name;
        Exists = exists;
        Header = header;
        Rows = rows;
    }

    /// <summary>
    ///     Loads and validates a TSV file, adding a finding for every format defect.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="name">The name shown in findings, normally the path relative to the data root.</param>
    /// <param name="report">The report that receives the findings.</param>
    /// <param name="layout">The expected layout, or <see langword="null"/> for a plain file with a header.</param>
    /// <returns>The loaded table; empty when the file is missing or unreadable.</returns>
    public static TsvTable Load(string path, string name, CheckReport report, TsvLayout? layout = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        layout ??= new TsvLayout();

        // A missing file is a finding, not an exception: the later checks then simply see no rows
        if (!File.Exists(path))
        {
            report.Add(layout.Check, name, "required file is missing");
            return new TsvTable(name, false, [], []);
        }

        // Decode strictly after the byte-level checks (BOM) that decoding would hide
        var text = Decode(path, name, report, layout.Check);
        if (text is null)
        {
            return new TsvTable(name, true, [], []);
        }

        var lines = SplitLines(text, name, report, layout.Check);
        return Parse(lines, name, report, layout);
    }

    /// <summary>
    ///     Reads the file bytes and decodes them as strict UTF-8, reporting a byte order mark or invalid bytes.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="name">The display name.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <param name="check">The check name.</param>
    /// <returns>The decoded text, or <see langword="null"/> when the bytes are not valid UTF-8.</returns>
    private static string? Decode(string path, string name, CheckReport report, string check)
    {
        var bytes = File.ReadAllBytes(path);

        // Report and then skip a BOM so it does not additionally corrupt the first header cell
        var offset = 0;
        if (bytes.AsSpan().StartsWith(Utf8Bom))
        {
            report.Add(check, name, "UTF-8 byte order mark (the file must have none)");
            offset = Utf8Bom.Length;
        }

        try
        {
            return StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            report.Add(check, name, "file is not valid UTF-8");
            return null;
        }
    }

    /// <summary>
    ///     Splits the text into lines on LF and reports a CR character or a missing final newline.
    /// </summary>
    /// <param name="text">The decoded text.</param>
    /// <param name="name">The display name.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <param name="check">The check name.</param>
    /// <returns>The lines without their terminators.</returns>
    private static List<string> SplitLines(string text, string name, CheckReport report, string check)
    {
        // CR anywhere means CRLF line endings or a stray carriage return; the data is LF only
        var cr = text.IndexOf('\r');
        if (cr >= 0)
        {
            var line = text.AsSpan(0, cr).Count('\n') + 1;
            report.Add(check, $"{name}:{line}", "CR character (the file must use LF line endings only)");
        }

        if (text.Length == 0)
        {
            report.Add(check, name, "file is empty");
            return [];
        }

        // A well-formed file ends with a newline, which leaves one empty trailing element after the split
        var lines = text.Split('\n').ToList();
        if (lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }
        else
        {
            report.Add(check, name, "missing final newline");
        }

        return lines;
    }

    /// <summary>
    ///     Parses the lines into header and rows, enforcing the per-line rules.
    /// </summary>
    /// <param name="lines">The lines without terminators.</param>
    /// <param name="name">The display name.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <param name="layout">The expected layout.</param>
    /// <returns>The parsed table.</returns>
    private static TsvTable Parse(List<string> lines, string name, CheckReport report, TsvLayout layout)
    {
        var header = new List<string>();
        var rows = new List<TsvRow>();
        var columns = -1;

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var location = $"{name}:{index + 1}";

            // Every line must be in Unicode normalization form C so offsets mean the same everywhere
            if (!line.IsNormalized(NormalizationForm.FormC))
            {
                report.Add(layout.Check, location, "line is not in Unicode normalization form C (NFC)");
            }

            if (line.Length == 0)
            {
                report.Add(layout.Check, location, "empty line");
                continue;
            }

            if (layout.AllowComments && line.StartsWith('#'))
            {
                continue;
            }

            // Split on the tab only: there is no quoting in these files
            var cells = line.Split('\t');
            if (columns < 0)
            {
                columns = cells.Length;
            }

            if (cells.Length != columns)
            {
                report.Add(layout.Check, location, $"column count {cells.Length} (expected {columns})");
            }
            else if (layout.HasHeader && header.Count == 0)
            {
                header.AddRange(cells);
            }
            else
            {
                rows.Add(new TsvRow(index + 1, cells));
            }
        }

        return CompareHeader(name, report, layout, header, rows);
    }

    /// <summary>
    ///     Compares the parsed header with the expected one; a mismatch discards the rows.
    /// </summary>
    /// <param name="name">The display name.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <param name="layout">The expected layout.</param>
    /// <param name="header">The parsed header.</param>
    /// <param name="rows">The parsed rows.</param>
    /// <returns>The table.</returns>
    private static TsvTable CompareHeader(string name, CheckReport report, TsvLayout layout, List<string> header, List<TsvRow> rows)
    {
        if (layout.HasHeader && layout.ExpectedHeader is { } expected && !header.SequenceEqual(expected))
        {
            // Without the right header the cell positions are meaningless, so no row is handed to the checks
            report.Add(
                layout.Check,
                $"{name}:1",
                $"header is '{string.Join('\t', header)}' (expected '{string.Join('\t', expected)}')");
            return new TsvTable(name, true, header, []);
        }

        return new TsvTable(name, true, header, rows);
    }
}
