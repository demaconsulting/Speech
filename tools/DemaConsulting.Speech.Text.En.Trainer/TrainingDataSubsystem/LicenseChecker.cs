using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

/// <summary>
///     Enforces the license policy of the training data: only open licenses, per-source license classes, no
///     evaluation-only material, attribution where required.
/// </summary>
/// <remarks>
///     This is the C# port of the reference <c>check_licenses</c> rules, with one deliberate reduction: the
///     reference recomputed the license of every harvested document from the source tables of an external corpus
///     package, which CI does not have. Here a closed per-source policy (see <see cref="SourceInfo"/>) takes its
///     place; the one-off recomputation result stays recorded in the data folder. Misaki data is not used anywhere,
///     so the name is rejected like the other restricted terms. Every violation is a finding on the
///     <c>license</c> check; nothing is changed.
/// </remarks>
internal sealed class LicenseChecker
{
    /// <summary>The check name used for findings.</summary>
    private const string CheckName = "license";

    /// <summary>The attribution file that must name the MASC corpus when MASC rows exist.</summary>
    private const string AttributionFile = "ATTRIBUTION.md";

    /// <summary>The only path that may name an evaluation-only set: it holds ids, never text.</summary>
    private const string AllowedEvaluationPath = "whd-mapping.tsv";

    /// <summary>The documents that must declare a <c>License:</c> line.</summary>
    private static readonly string[] DocumentsWithLicenseLine = ["README.md", "prompts/README.md", "audit/audit-summary.md"];

    /// <summary>Terms that signal a restricted license, restricted content or evaluation-only data.</summary>
    private static readonly Regex RestrictedTerms = new(
        @"share-?alike|by-sa|by-nc|by-nd|non-?commercial|no-?derivat|wikipedia|misaki|creativecommons\.org/licenses/by-",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(5));

    /// <summary>File paths that name an evaluation-only data set.</summary>
    private static readonly Regex EvaluationOnlyPaths = new(
        @"wikipedia|whd|heteronym-bench|heteronymbench|llama|ewt|semcor|brown|misaki|\.conllu$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(5));

    /// <summary>The <c>License:</c> line of a document.</summary>
    private static readonly Regex LicenseLine = new(@"^License:[ \t]*(.+)$", RegexOptions.CultureInvariant | RegexOptions.Multiline, TimeSpan.FromSeconds(5));

    /// <summary>A license identifier inside a license expression.</summary>
    private static readonly Regex LicenseToken = new(@"[A-Za-z0-9.\-]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>The data root folder.</summary>
    private readonly string _root;

    /// <summary>The data set under test.</summary>
    private readonly TextEnDataSet _data;

    /// <summary>The report that receives findings.</summary>
    private readonly CheckReport _report;

    /// <summary>
    ///     Creates the checker.
    /// </summary>
    /// <param name="root">The data root folder.</param>
    /// <param name="data">The loaded data set.</param>
    /// <param name="report">The report that receives findings and informational lines.</param>
    public LicenseChecker(string root, TextEnDataSet data, CheckReport report)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    /// <summary>
    ///     Runs every license check.
    /// </summary>
    public void Run()
    {
        CheckRowLicenses();

        // File-level rules: the file names and the TSV contents
        var files = DataManifest.EnumerateDataFiles(_root);
        foreach (var file in files)
        {
            CheckPath(file);
            if (file.EndsWith(".tsv", StringComparison.Ordinal))
            {
                ScanForRestrictedTerms(file);
            }
        }

        CheckAttribution();
        foreach (var document in DocumentsWithLicenseLine)
        {
            CheckLicenseLine(document);
        }
    }

    /// <summary>
    ///     Checks the license of every labeled row against the allow-list and the source policy.
    /// </summary>
    private void CheckRowLicenses()
    {
        var provenance = _data.HarvestProvenance.Select(record => record.Id).ToHashSet(StringComparer.Ordinal);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in _data.Rows)
        {
            counts[row.License] = counts.GetValueOrDefault(row.License) + 1;
            var error = JudgeRow(row);
            if (error is not null)
            {
                _report.Add(CheckName, row.Location, $"{row.Id}: {error}");
            }

            // A harvested sentence must say where it came from; an unknown source is a schema finding only
            if (SourceInfo.KindOf(row.Source) == SourceKind.Harvest && !provenance.Contains(row.Id))
            {
                _report.Add(CheckName, row.Location, $"{row.Id}: harvested row without an entry in harvest-provenance.tsv");
            }
        }

        _report.AddInfo($"rows by license: {string.Join(", ", counts.Select(pair => $"{pair.Key}: {pair.Value}"))}");
    }

    /// <summary>
    ///     Judges the license of one row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>An error description, or <see langword="null"/> when the license is acceptable.</returns>
    private static string? JudgeRow(DataRow row)
    {
        if (!LicenseIds.Allowed.Contains(row.License))
        {
            return $"license '{row.License}' is not allowed (allowed: {string.Join(", ", LicenseIds.Allowed.Order(StringComparer.Ordinal))})";
        }

        // Unknown sources are a schema finding; the license is judged against the policy only when the source is known
        if (!SourceInfo.TryGet(row.Source, out var info))
        {
            return null;
        }

        if (info.IsGenerated && !string.Equals(row.License, LicenseIds.Cc0, StringComparison.Ordinal))
        {
            return $"generated row (source '{row.Source}') must be {LicenseIds.Cc0} but is '{row.License}'";
        }

        return info.AllowedLicenses.Contains(row.License)
            ? null
            : $"license '{row.License}' is not allowed for source '{row.Source}' (allowed: {string.Join(", ", info.AllowedLicenses.Order(StringComparer.Ordinal))})";
    }

    /// <summary>
    ///     Rejects a file whose path names an evaluation-only set.
    /// </summary>
    /// <param name="relativePath">The path relative to the data root.</param>
    private void CheckPath(string relativePath)
    {
        if (EvaluationOnlyPaths.IsMatch(relativePath) && !string.Equals(relativePath, AllowedEvaluationPath, StringComparison.Ordinal))
        {
            _report.Add(CheckName, relativePath, "path names an evaluation-only set (such data must never be committed)");
        }
    }

    /// <summary>
    ///     Scans a TSV file for restricted-license terms; for the labeled data files only the id, source and license cells are scanned.
    /// </summary>
    /// <param name="relativePath">The path relative to the data root.</param>
    private void ScanForRestrictedTerms(string relativePath)
    {
        // The sentence column is free text from other works and is never scanned: only the metadata is
        var isDataFile = TextEnDataSet.DataFiles.Any(file => file.FileName == relativePath);
        var lineNumber = 0;
        foreach (var line in File.ReadLines(Path.Combine(_root, relativePath)))
        {
            lineNumber++;
            var text = line;
            if (isDataFile)
            {
                var cells = line.Split('\t');
                text = string.Join('\t', new[] { 0, 6, 7 }.Where(index => index < cells.Length).Select(index => cells[index]));
            }

            var match = RestrictedTerms.Match(text);
            if (match.Success)
            {
                _report.Add(CheckName, $"{relativePath}:{lineNumber}", $"restricted-license term '{match.Value}'");
            }
        }
    }

    /// <summary>
    ///     Requires an attribution file naming MASC when any row carries the attribution license.
    /// </summary>
    private void CheckAttribution()
    {
        if (!_data.Rows.Any(row => row.License == LicenseIds.CcBy30Us))
        {
            return;
        }

        var path = Path.Combine(_root, AttributionFile);
        if (!File.Exists(path))
        {
            _report.Add(CheckName, AttributionFile, $"{LicenseIds.CcBy30Us} rows exist but {AttributionFile} is missing");
        }
        else if (!File.ReadAllText(path).Contains("MASC", StringComparison.Ordinal))
        {
            _report.Add(CheckName, AttributionFile, $"{LicenseIds.CcBy30Us} rows exist but {AttributionFile} does not name MASC");
        }
    }

    /// <summary>
    ///     Requires a document to declare <c>License:</c> with allowed identifiers only.
    /// </summary>
    /// <param name="relativePath">The path relative to the data root.</param>
    private void CheckLicenseLine(string relativePath)
    {
        var path = Path.Combine(_root, relativePath);
        if (!File.Exists(path))
        {
            _report.Add(CheckName, relativePath, "document is missing");
            return;
        }

        var match = LicenseLine.Match(File.ReadAllText(path));
        var ids = match.Success
            ? LicenseToken.Matches(match.Groups[1].Value).Select(token => token.Value).Where(token => token is not ("AND" or "OR")).ToList()
            : [];
        if (ids.Count == 0 || ids.Any(id => !LicenseIds.Allowed.Contains(id)))
        {
            _report.Add(CheckName, relativePath, "missing `License:` line, or it names a license that is not allowed");
        }
    }
}
