using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

/// <summary>
///     One line of <c>MANIFEST.tsv</c>: a data file with its expected hash, size and row count.
/// </summary>
/// <param name="Line">The 1-based line in <c>MANIFEST.tsv</c>.</param>
/// <param name="Path">The file path relative to the data root, with <c>/</c> separators.</param>
/// <param name="Sha256">The expected SHA-256 of the file bytes as 64 lower-case hex digits.</param>
/// <param name="Bytes">The expected size in bytes.</param>
/// <param name="Rows">The expected row count: lines minus the header for <c>.tsv</c> files, lines otherwise.</param>
internal sealed record ManifestEntry(int Line, string Path, string Sha256, long Bytes, long Rows);

/// <summary>
///     Reads <c>MANIFEST.tsv</c> and verifies it against the files of the data folder.
/// </summary>
/// <remarks>
///     The manifest pins the SHA-256, byte count and row count of every data file, so any change to any file is
///     visible as a change of the manifest and the check fails until the manifest is regenerated. The manifest
///     lists every file under the data root except itself and the local caches <c>external/</c> and
///     <c>work/</c>, sorted by ordinal path with <c>/</c> separators, and starts with <c>#</c> comment lines. This
///     unit only verifies; it never writes the manifest.
/// </remarks>
internal sealed class DataManifest
{
    /// <summary>The manifest file name.</summary>
    public const string FileName = "MANIFEST.tsv";

    /// <summary>The check name used for findings.</summary>
    private const string Check = "manifest";

    /// <summary>The top-level folders that hold local caches and are never listed or checked.</summary>
    private static readonly string[] LocalCacheFolders = ["external", "work"];

    /// <summary>The valid form of a SHA-256 digest.</summary>
    private static readonly Regex Sha256Pattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>Gets the well-formed entries in file order.</summary>
    public IReadOnlyList<ManifestEntry> Entries { get; }

    /// <summary>
    ///     Initializes a manifest.
    /// </summary>
    /// <param name="entries">The entries.</param>
    private DataManifest(IReadOnlyList<ManifestEntry> entries) => Entries = entries;

    /// <summary>
    ///     Loads <c>MANIFEST.tsv</c>, reporting malformed lines as findings.
    /// </summary>
    /// <param name="root">The data root folder.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <returns>The manifest with its well-formed entries.</returns>
    public static DataManifest Load(string root, CheckReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var layout = new TsvLayout(AllowComments: true, HasHeader: false, Check: Check);
        var table = TsvTable.Load(System.IO.Path.Combine(root, FileName), FileName, report, layout);

        var entries = new List<ManifestEntry>();
        foreach (var row in table.Rows)
        {
            var entry = ParseEntry(row, report);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return new DataManifest(entries);
    }

    /// <summary>
    ///     Loads the manifest and verifies it against the files under <paramref name="root"/>.
    /// </summary>
    /// <param name="root">The data root folder.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <returns>The verified manifest.</returns>
    public static DataManifest Verify(string root, CheckReport report)
    {
        var manifest = Load(root, report);
        manifest.VerifyFiles(root, report);
        return manifest;
    }

    /// <summary>
    ///     Lists every checked data file under <paramref name="root"/> as a relative path with <c>/</c> separators.
    /// </summary>
    /// <param name="root">The data root folder.</param>
    /// <returns>The relative paths sorted by ordinal comparison; <c>MANIFEST.tsv</c> and the local caches are excluded.</returns>
    public static IReadOnlyList<string> EnumerateDataFiles(string root)
    {
        var result = new List<string>();
        if (!Directory.Exists(root))
        {
            return result;
        }

        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = System.IO.Path.GetRelativePath(root, path).Replace('\\', '/');
            var firstSegment = relative.Split('/')[0];
            var isCache = relative.Contains('/') && LocalCacheFolders.Contains(firstSegment, StringComparer.Ordinal);
            if (!isCache && !string.Equals(relative, FileName, StringComparison.Ordinal))
            {
                result.Add(relative);
            }
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>
    ///     Counts the lines of a file the way the manifest does: line terminators, plus one for an unterminated last line.
    /// </summary>
    /// <param name="bytes">The file bytes.</param>
    /// <returns>The number of lines.</returns>
    internal static long CountLines(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return 0;
        }

        long lines = bytes.Count((byte)'\n');
        return bytes[^1] == (byte)'\n' ? lines : lines + 1;
    }

    /// <summary>
    ///     Parses one manifest row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="report">The report that receives findings.</param>
    /// <returns>The entry, or <see langword="null"/> when the row is malformed.</returns>
    private static ManifestEntry? ParseEntry(TsvRow row, CheckReport report)
    {
        var location = $"{FileName}:{row.LineNumber}";
        if (row.Cells.Count != 4)
        {
            report.Add(Check, location, $"expected 4 columns (file, sha256, bytes, rows) but found {row.Cells.Count}");
            return null;
        }

        var valid = true;
        if (!Sha256Pattern.IsMatch(row[1]))
        {
            report.Add(Check, location, $"'{row[1]}' is not a lower-case 64-digit SHA-256");
            valid = false;
        }

        if (!long.TryParse(row[2], NumberStyles.None, CultureInfo.InvariantCulture, out var bytes)
            || !long.TryParse(row[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var rows))
        {
            report.Add(Check, location, "bytes and rows must be integers");
            return null;
        }

        return valid ? new ManifestEntry(row.LineNumber, row[0], row[1], bytes, rows) : null;
    }

    /// <summary>
    ///     Verifies the entries against the files and reports files that are not listed.
    /// </summary>
    /// <param name="root">The data root folder.</param>
    /// <param name="report">The report that receives findings.</param>
    private void VerifyFiles(string root, CheckReport report)
    {
        // The listing itself must be sorted and free of duplicates so regeneration is deterministic
        CheckOrder(report);

        // Each listed file must exist with exactly the recorded hash, size and row count
        foreach (var entry in Entries)
        {
            VerifyEntry(root, entry, report);
        }

        // Every file under the root must be listed so nothing can change without changing the manifest
        var listed = new HashSet<string>(Entries.Select(entry => entry.Path), StringComparer.Ordinal);
        foreach (var file in EnumerateDataFiles(root).Where(file => !listed.Contains(file)))
        {
            report.Add(Check, file, $"file is not listed in {FileName}");
        }
    }

    /// <summary>
    ///     Reports entries that are duplicated or not in ordinal path order.
    /// </summary>
    /// <param name="report">The report that receives findings.</param>
    private void CheckOrder(CheckReport report)
    {
        for (var index = 1; index < Entries.Count; index++)
        {
            var order = string.CompareOrdinal(Entries[index - 1].Path, Entries[index].Path);
            if (order == 0)
            {
                report.Add(Check, $"{FileName}:{Entries[index].Line}", $"duplicate entry for '{Entries[index].Path}'");
            }
            else if (order > 0)
            {
                report.Add(Check, $"{FileName}:{Entries[index].Line}", $"'{Entries[index].Path}' is not in sorted order");
            }
        }
    }

    /// <summary>
    ///     Verifies one listed file.
    /// </summary>
    /// <param name="root">The data root folder.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="report">The report that receives findings.</param>
    private static void VerifyEntry(string root, ManifestEntry entry, CheckReport report)
    {
        var location = $"{FileName}:{entry.Line}";

        // Only plain relative '/' paths below the root are acceptable, so a manifest can never point elsewhere
        var segments = entry.Path.Split('/');
        if (entry.Path.Contains('\\') || System.IO.Path.IsPathRooted(entry.Path) || segments.Any(segment => segment is "" or "." or ".."))
        {
            report.Add(Check, location, $"'{entry.Path}' is not a relative path with '/' separators");
            return;
        }

        var full = System.IO.Path.Combine(root, entry.Path);
        if (!File.Exists(full))
        {
            report.Add(Check, location, $"listed file '{entry.Path}' does not exist");
            return;
        }

        // Hash and measure the actual bytes
        var bytes = File.ReadAllBytes(full);
        var actualHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var lines = CountLines(bytes);
        var actualRows = entry.Path.EndsWith(".tsv", StringComparison.Ordinal) ? lines - 1 : lines;

        if (!string.Equals(actualHash, entry.Sha256, StringComparison.Ordinal))
        {
            report.Add(Check, entry.Path, $"SHA-256 is {actualHash} but {FileName} lists {entry.Sha256}");
        }

        if (bytes.LongLength != entry.Bytes)
        {
            report.Add(Check, entry.Path, $"size is {bytes.LongLength} bytes but {FileName} lists {entry.Bytes}");
        }

        if (actualRows != entry.Rows)
        {
            report.Add(Check, entry.Path, $"row count is {actualRows} but {FileName} lists {entry.Rows}");
        }
    }
}
