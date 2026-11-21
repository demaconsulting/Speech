using System.Globalization;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

/// <summary>
///     Checks the committed corpus files of the data folder offline: the pinned source list <c>cluster-sources.tsv</c>
///     and the audit removals <c>cluster-removals.tsv</c>.
/// </summary>
/// <remarks>
///     <para>
///     Fetching and auditing the corpus are manual commands that need the network or the local cache; this check is the
///     cheap gate that runs with every <c>check-data</c>. It verifies the schema of the two tables, the pin of every book
///     of the corpus (status <c>used</c>), and that no such book is a development or natural-holdout document
///     (recomputed from <c>harvest-provenance.tsv</c>). Whether the removals are still what the audit computes needs the
///     corpus itself, so only the manual <c>cluster --audit</c> can tell.
///     </para>
///     <para>
///     When neither file exists the check is skipped (an informational line says so); when one exists, both must. The
///     check reads only the data folder and uses only the base class library. Every violation is a finding on the
///     <c>clusters</c> check (the leakage rule on the <c>leakage</c> check); nothing is changed. Not thread-safe; one
///     instance belongs to one run.
///     </para>
/// </remarks>
internal sealed class ClusterDataChecker
{
    /// <summary>The check name used for findings.</summary>
    public const string CheckName = "clusters";

    /// <summary>The check name of the document-level leakage finding.</summary>
    public const string LeakageCheckName = "leakage";

    /// <summary>The file name of the source list.</summary>
    public const string SourcesFile = "cluster-sources.tsv";

    /// <summary>The file name of the audit removals.</summary>
    public const string RemovalsFile = "cluster-removals.tsv";

    /// <summary>The status of a book that is part of the corpus.</summary>
    public const string UsedStatus = "used";

    /// <summary>The cell that marks a missing pin or year.</summary>
    public const string None = "-";

    /// <summary>The text every corpus book's license basis must contain.</summary>
    public const string PublicDomainText = "Public domain in the USA";

    /// <summary>The latest year of first publication accepted.</summary>
    public const int LatestYear = 1929;

    /// <summary>The prefix of a source id.</summary>
    private const string SourcePrefix = "gutenberg-";

    /// <summary>The header of the source list.</summary>
    public static readonly IReadOnlyList<string> SourcesHeader =
        ["source_id", "group", "title", "author", "year", "url", "license_basis", "basis_ref", "body_sha256", "body_bytes", "status"];

    /// <summary>The header of the removals.</summary>
    public static readonly IReadOnlyList<string> RemovalsHeader = ["source_id", "paragraph", "sentence", "rule", "eval_file", "eval_id", "text_sha256"];

    /// <summary>The selection groups of the source list.</summary>
    public static readonly IReadOnlySet<string> Groups = new HashSet<string>(StringComparer.Ordinal)
    {
        "core-open", "core-silver", "domain-anatomy", "domain-textiles", "domain-sport", "domain-music", "domain-clocks",
    };

    /// <summary>The statuses of the source list.</summary>
    public static readonly IReadOnlySet<string> Statuses = new HashSet<string>(StringComparer.Ordinal)
    {
        UsedStatus, "excluded-dev", "excluded-holdout", "rejected-license", "rejected-content",
    };

    /// <summary>The only hosts a book may be fetched from: official mirrors, never the main web site.</summary>
    public static readonly IReadOnlySet<string> AllowedHosts = new HashSet<string>(StringComparer.Ordinal)
    {
        "gutenberg.pglaf.org", "aleph.gutenberg.org",
    };

    /// <summary>The audit rules a removal may name.</summary>
    public static readonly IReadOnlySet<string> RemovalRules = new HashSet<string>(["exact", "near", "contains"], StringComparer.Ordinal);

    /// <summary>The evaluation files a removal may name, in attribution order.</summary>
    public static readonly IReadOnlyList<string> EvaluationFiles =
        ["dev.tsv", "holdout-natural.tsv", "holdout-independent.tsv", TextEnDataSet.RegressionFile, TextEnDataSet.GeneralizationFile];

    /// <summary>The data files whose source documents may never be in the corpus.</summary>
    private static readonly IReadOnlySet<string> ProtectedFiles = new HashSet<string>(["dev.tsv", "holdout-natural.tsv"], StringComparer.Ordinal);

    /// <summary>The data set under test.</summary>
    private readonly TextEnDataSet _data;

    /// <summary>The report that receives findings.</summary>
    private readonly CheckReport _report;

    /// <summary>
    ///     Creates the checker.
    /// </summary>
    /// <param name="data">The loaded data set.</param>
    /// <param name="report">The report that receives findings and informational lines.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> or <paramref name="report"/> is null.</exception>
    public ClusterDataChecker(TextEnDataSet data, CheckReport report)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    /// <summary>Gets the names of the two tables, which the generic format check of the data set leaves to this check.</summary>
    public static IReadOnlyList<string> Tables { get; } = [SourcesFile, RemovalsFile];

    /// <summary>
    ///     Runs every corpus check.
    /// </summary>
    public void Run()
    {
        var files = new[] { SourcesFile, RemovalsFile };
        var present = files.Where(file => File.Exists(Path.Combine(_data.Root, file))).ToList();
        if (present.Count == 0)
        {
            _report.AddInfo("word-cluster check skipped (no word-cluster files)");
            return;
        }

        foreach (var file in files.Except(present, StringComparer.Ordinal))
        {
            _report.Add(CheckName, file, "missing: the word-cluster files are committed together");
        }

        if (present.Count < files.Length)
        {
            return;
        }

        var sources = CheckSources();
        CheckRemovals(sources);
    }

    /// <summary>
    ///     Determines whether a text is a lowercase 64-digit hexadecimal SHA-256.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsSha256(string text) => text.Length == 64 && text.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    /// <summary>
    ///     Parses a canonical non-negative integer (no sign, no leading zero).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the text is canonical.</returns>
    private static bool TryParseCount(string text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
        && string.Equals(value.ToString(CultureInfo.InvariantCulture), text, StringComparison.Ordinal);

    /// <summary>
    ///     Checks the source list, including the document-level leakage rule.
    /// </summary>
    /// <returns>The status per source id of the rows with a valid id and status.</returns>
    private Dictionary<string, string> CheckSources()
    {
        var table = TsvTable.Load(Path.Combine(_data.Root, SourcesFile), SourcesFile, _report, new TsvLayout(ExpectedHeader: SourcesHeader));
        var protectedDocuments = ProtectedDocuments();
        var statuses = new Dictionary<string, string>(StringComparer.Ordinal);
        var previous = 0L;
        foreach (var row in table.Rows)
        {
            var location = $"{SourcesFile}:{row.LineNumber}";
            var id = row[0];
            if (!id.StartsWith(SourcePrefix, StringComparison.Ordinal) || !TryParseCount(id[SourcePrefix.Length..], out var number) || number <= 0)
            {
                Add(location, $"source_id '{id}' is not gutenberg-N");
                continue;
            }

            if (number <= previous)
            {
                Add(location, $"{id}: rows must be sorted by ascending e-book number without duplicates");
            }

            previous = number;
            var status = row[10];
            CheckSourceCells(location, id, number, row);
            if (!Statuses.Contains(status))
            {
                Add(location, $"{id}: unknown status '{status}'");
                continue;
            }

            CheckPin(location, id, status, row[6], row[8], row[9]);
            statuses[id] = status;

            // Document-level leakage: a development or natural-holdout book is never part of the corpus
            if (status == UsedStatus && protectedDocuments.TryGetValue(id, out var file))
            {
                _report.Add(LeakageCheckName, location, $"{id} holds {file} sentences but its status is '{UsedStatus}'");
            }
        }

        var used = statuses.Values.Count(static status => status == UsedStatus);
        _report.AddInfo($"word-cluster sources: {statuses.Count} books, {used} used, {statuses.Count - used} not used");
        return statuses;
    }

    /// <summary>
    ///     Checks the descriptive cells of a source row: group, text cells, year and URL.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <param name="id">The source id.</param>
    /// <param name="number">The e-book number.</param>
    /// <param name="row">The row.</param>
    private void CheckSourceCells(string location, string id, long number, TsvRow row)
    {
        if (!Groups.Contains(row[1]))
        {
            Add(location, $"{id}: unknown group '{row[1]}'");
        }

        foreach (var column in new[] { 2, 3, 6, 7 }.Where(column => string.IsNullOrWhiteSpace(row[column])))
        {
            Add(location, $"{id}: {SourcesHeader[column]} is empty");
        }

        if (row[4] != None && (!TryParseCount(row[4], out var year) || year > LatestYear))
        {
            Add(location, $"{id}: year '{row[4]}' is not a year of {LatestYear} or earlier, or '-'");
        }

        var n = number.ToString(CultureInfo.InvariantCulture);
        if (!Uri.TryCreate(row[5], UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps || !AllowedHosts.Contains(url.Host)
            || !url.IsDefaultPort || url.Query.Length > 0 || url.Fragment.Length > 0
            || !(url.AbsolutePath.EndsWith($"/{n}/pg{n}.txt", StringComparison.Ordinal) || url.AbsolutePath.EndsWith($"/{n}/{n}-0.txt", StringComparison.Ordinal)
                || url.AbsolutePath.EndsWith($"/{n}/{n}.txt", StringComparison.Ordinal)))
        {
            Add(location, $"{id}: url '{row[5]}' is not the HTTPS plain text of e-book {n} on an official mirror ({string.Join(", ", AllowedHosts.Order(StringComparer.Ordinal))})");
        }
    }

    /// <summary>
    ///     Checks the license basis and the body pin of a source row.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <param name="id">The source id.</param>
    /// <param name="status">The status.</param>
    /// <param name="licenseBasis">The license basis.</param>
    /// <param name="sha">The <c>body_sha256</c> cell.</param>
    /// <param name="bytes">The <c>body_bytes</c> cell.</param>
    private void CheckPin(string location, string id, string status, string licenseBasis, string sha, string bytes)
    {
        if (status != UsedStatus)
        {
            if (sha != None || bytes != None)
            {
                Add(location, $"{id}: a book with status '{status}' has no pin; write '-' in body_sha256 and body_bytes");
            }

            return;
        }

        if (!IsSha256(sha) || !TryParseCount(bytes, out var size) || size <= 0)
        {
            Add(location, $"{id}: a used book needs a lowercase 64-digit body_sha256 and a positive body_bytes");
        }

        if (!licenseBasis.Contains(PublicDomainText, StringComparison.Ordinal))
        {
            Add(location, $"{id}: the license basis of a used book must state '{PublicDomainText}'");
        }
    }

    /// <summary>
    ///     Gets the source documents of the development and natural-holdout rows, from the harvest provenance.
    /// </summary>
    /// <returns>The data file of each such document.</returns>
    private Dictionary<string, string> ProtectedDocuments()
    {
        var documentOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in _data.HarvestProvenance)
        {
            documentOf.TryAdd(record.Id, record.Key);
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in _data.Rows.Where(static row => ProtectedFiles.Contains(row.File)))
        {
            if (documentOf.TryGetValue(row.Id, out var document))
            {
                result.TryAdd(document, row.File);
            }
        }

        return result;
    }

    /// <summary>
    ///     Checks the removals.
    /// </summary>
    /// <param name="sources">The status per source id.</param>
    private void CheckRemovals(Dictionary<string, string> sources)
    {
        var table = TsvTable.Load(Path.Combine(_data.Root, RemovalsFile), RemovalsFile, _report, new TsvLayout(ExpectedHeader: RemovalsHeader));
        (string Id, long Paragraph, long Sentence)? previous = null;
        foreach (var row in table.Rows)
        {
            var location = $"{RemovalsFile}:{row.LineNumber}";
            if (!sources.TryGetValue(row[0], out var status) || status != UsedStatus)
            {
                Add(location, $"source '{row[0]}' is not a used book of {SourcesFile}");
            }

            if (!TryParseCount(row[1], out var paragraph) || !TryParseCount(row[2], out var sentence))
            {
                Add(location, "paragraph and sentence must be non-negative integers");
                continue;
            }

            if (!RemovalRules.Contains(row[3]) || !EvaluationFiles.Contains(row[4]) || row[5].Length == 0 || !IsSha256(row[6]))
            {
                Add(location, $"the rule must be one of {string.Join(", ", RemovalRules.Order(StringComparer.Ordinal))}, the file an evaluation file, the id non-empty and text_sha256 a lowercase SHA-256");
            }

            var key = (row[0], paragraph, sentence);
            if (previous is { } last && Compare(last, key) >= 0)
            {
                Add(location, "rows must be unique and sorted by source_id, paragraph and sentence");
            }

            previous = key;
        }
    }

    /// <summary>
    ///     Orders two removal keys by source id (ordinal), paragraph and sentence.
    /// </summary>
    /// <param name="a">The first key.</param>
    /// <param name="b">The second key.</param>
    /// <returns>The comparison result.</returns>
    private static int Compare((string Id, long Paragraph, long Sentence) a, (string Id, long Paragraph, long Sentence) b)
    {
        var byId = string.CompareOrdinal(a.Id, b.Id);
        if (byId != 0)
        {
            return byId;
        }

        return a.Paragraph != b.Paragraph ? a.Paragraph.CompareTo(b.Paragraph) : a.Sentence.CompareTo(b.Sentence);
    }

    /// <summary>
    ///     Records a finding on the clusters check.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <param name="message">The message.</param>
    private void Add(string location, string message) => _report.Add(CheckName, location, message);
}
