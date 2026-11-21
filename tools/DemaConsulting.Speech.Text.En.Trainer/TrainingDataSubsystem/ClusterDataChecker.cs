using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

/// <summary>
///     Checks the committed word-cluster files of the data folder offline: the pinned source list
///     <c>cluster-sources.tsv</c>, the audit removals <c>cluster-removals.tsv</c>, the table <c>word-clusters.tsv</c> and
///     the record <c>clustering.json</c> that ties them together.
/// </summary>
/// <remarks>
///     <para>
///     The induction itself is a manual, hours-long command; this check is the cheap gate that runs with every
///     <c>check-data</c>. It verifies the schema of the three tables, that no book of the corpus (status <c>used</c>) is a
///     development or natural-holdout document (recomputed from <c>harvest-provenance.tsv</c>), and that the record still
///     describes the committed files: the SHA-256 of the source list, the removals and the table, the number of table
///     rows and distinct paths, the book counts, the minimum count, and the tokenizer version the removals were computed
///     with. Any edit to a pin, a removal or the table, or an upgrade of the tokenizer package, therefore fails the check
///     until the table is induced again. The record's hash of the evaluation files is not compared: it changes with any
///     development-data edit and is verified by the manual <c>cluster --check</c> instead.
///     </para>
///     <para>
///     When none of the four files exists the check is skipped (an informational line says so); when some exist, all four
///     must. The field names of the record are the constants of this class, which the record writer uses too, so the two
///     cannot drift. The check reads only the data folder and uses only the base class library. Every violation is a
///     finding on the <c>clusters</c> check (the leakage rule on the <c>leakage</c> check); nothing is changed. Not
///     thread-safe; one instance belongs to one run.
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

    /// <summary>The file name of the word-cluster table.</summary>
    public const string TableFile = "word-clusters.tsv";

    /// <summary>The file name of the record.</summary>
    public const string RecordFile = "clustering.json";

    /// <summary>The schema version of the record.</summary>
    public const int RecordSchema = 1;

    /// <summary>The status of a book that is part of the corpus.</summary>
    public const string UsedStatus = "used";

    /// <summary>The cell that marks a missing pin or year.</summary>
    public const string None = "-";

    /// <summary>The text every corpus book's license basis must contain.</summary>
    public const string PublicDomainText = "Public domain in the USA";

    /// <summary>The latest year of first publication accepted.</summary>
    public const int LatestYear = 1929;

    /// <summary>The longest table word, in UTF-16 code units.</summary>
    public const int MaximumWordLength = 40;

    /// <summary>The record field: schema version.</summary>
    public const string SchemaField = "schema";

    /// <summary>The record field: settings object.</summary>
    public const string SettingsField = "settings";

    /// <summary>The record field: inputs object.</summary>
    public const string InputsField = "inputs";

    /// <summary>The record field: corpus object.</summary>
    public const string CorpusField = "corpus";

    /// <summary>The record field: result object.</summary>
    public const string ResultField = "result";

    /// <summary>The record field: number of clusters.</summary>
    public const string ClustersField = "clusters";

    /// <summary>The record field: vocabulary limit.</summary>
    public const string MaxWordsField = "maxWords";

    /// <summary>The record field: minimum count.</summary>
    public const string MinCountField = "minCount";

    /// <summary>The record field: clusterer version.</summary>
    public const string ClustererVersionField = "clustererVersion";

    /// <summary>The record field: reader version.</summary>
    public const string ReaderVersionField = "readerVersion";

    /// <summary>The record field: audit version.</summary>
    public const string AuditVersionField = "auditVersion";

    /// <summary>The record field: SHA-256 of the source list.</summary>
    public const string SourcesShaField = "sourcesSha256";

    /// <summary>The record field: number of books in the corpus.</summary>
    public const string UsedBooksField = "usedBooks";

    /// <summary>The record field: number of listed books not in the corpus.</summary>
    public const string ExcludedBooksField = "excludedBooks";

    /// <summary>The record field: SHA-256 of the removals.</summary>
    public const string RemovalsShaField = "removalsSha256";

    /// <summary>The record field: SHA-256 of the evaluation-file listing.</summary>
    public const string EvaluationListingShaField = "evaluationListingSha256";

    /// <summary>The record field: tokenizer version.</summary>
    public const string TokenizerVersionField = "tokenizerVersion";

    /// <summary>The record field: SHA-256 of the snapshot archive.</summary>
    public const string SnapshotShaField = "snapshotSha256";

    /// <summary>The record field: size of the snapshot archive.</summary>
    public const string SnapshotBytesField = "snapshotBytes";

    /// <summary>The record field: number of files in the snapshot archive.</summary>
    public const string SnapshotFilesField = "snapshotFiles";

    /// <summary>The record field: where the snapshot is published.</summary>
    public const string SnapshotUrlField = "snapshotUrl";

    /// <summary>The record field: paragraphs read.</summary>
    public const string ParagraphsField = "paragraphs";

    /// <summary>The record field: sentences counted.</summary>
    public const string SentencesField = "sentences";

    /// <summary>The record field: tokens counted.</summary>
    public const string TokensField = "tokens";

    /// <summary>The record field: distinct eligible forms.</summary>
    public const string TypesField = "types";

    /// <summary>The record field: distinct bigrams.</summary>
    public const string BigramTypesField = "bigramTypes";

    /// <summary>The record field: removed sentences.</summary>
    public const string RemovedSentencesField = "removedSentences";

    /// <summary>The record field: removed tokens.</summary>
    public const string RemovedTokensField = "removedTokens";

    /// <summary>The record field: evaluation sentences named by a removal.</summary>
    public const string MatchedEvaluationSentencesField = "matchedEvaluationSentences";

    /// <summary>The record field: books with a removal.</summary>
    public const string BooksAffectedField = "booksAffected";

    /// <summary>The record field: SHA-256 of the table.</summary>
    public const string TableShaField = "tableSha256";

    /// <summary>The record field: table rows.</summary>
    public const string WordsField = "words";

    /// <summary>The record field: distinct paths.</summary>
    public const string PathsField = "paths";

    /// <summary>The record field: words dropped for a shared lookup hash.</summary>
    public const string DroppedHashCollisionsField = "droppedHashCollisions";

    /// <summary>The record field: words dropped for an ineligible character.</summary>
    public const string DroppedIneligibleField = "droppedIneligible";

    /// <summary>The record field: token coverage per ten thousand.</summary>
    public const string TokenCoverageField = "tokenCoveragePerTenThousand";

    /// <summary>The prefix of a source id.</summary>
    private const string SourcePrefix = "gutenberg-";

    /// <summary>The header of the source list.</summary>
    public static readonly IReadOnlyList<string> SourcesHeader =
        ["source_id", "group", "title", "author", "year", "url", "license_basis", "basis_ref", "body_sha256", "body_bytes", "status"];

    /// <summary>The header of the removals.</summary>
    public static readonly IReadOnlyList<string> RemovalsHeader = ["source_id", "paragraph", "sentence", "rule", "eval_file", "eval_id", "text_sha256"];

    /// <summary>The header of the table.</summary>
    public static readonly IReadOnlyList<string> TableHeader = ["word", "path", "count"];

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

    /// <summary>The tokenizer version of this build, or null when it is not known.</summary>
    private readonly string? _tokenizerVersion;

    /// <summary>
    ///     Creates the checker.
    /// </summary>
    /// <param name="data">The loaded data set.</param>
    /// <param name="report">The report that receives findings and informational lines.</param>
    /// <param name="tokenizerVersion">
    ///     The version of the tokenizer package of this build; <see langword="null"/> skips that comparison (an
    ///     informational line says so).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> or <paramref name="report"/> is null.</exception>
    public ClusterDataChecker(TextEnDataSet data, CheckReport report, string? tokenizerVersion)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _report = report ?? throw new ArgumentNullException(nameof(report));
        _tokenizerVersion = tokenizerVersion;
    }

    /// <summary>Gets the names of the three tables, which the generic format check of the data set leaves to this check.</summary>
    public static IReadOnlyList<string> Tables { get; } = [SourcesFile, RemovalsFile, TableFile];

    /// <summary>
    ///     Runs every word-cluster check.
    /// </summary>
    public void Run()
    {
        var files = new[] { SourcesFile, RemovalsFile, TableFile, RecordFile };
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
        var removals = CheckRemovals(sources);
        var record = ReadRecord();
        var table = CheckTable(record);
        if (record is not null)
        {
            CheckRecord(record, sources, removals, table);
        }
    }

    /// <summary>
    ///     Computes the lowercase hexadecimal SHA-256 of a file of the data folder.
    /// </summary>
    /// <param name="root">The data folder.</param>
    /// <param name="file">The file name.</param>
    /// <returns>The hash.</returns>
    private static string FileSha256(string root, string file) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, file)))).ToLowerInvariant();

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
    ///     Determines whether a character may occur in a table word: printable ASCII, Latin-1 letters and signs, or the
    ///     general punctuation block (NFC-stable on every platform).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> when it is eligible.</returns>
    private static bool IsEligible(char c) => c is (>= '\u0021' and <= '\u007e') or (>= '\u00a1' and <= '\u00ff') or (>= '\u2010' and <= '\u2027');

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
    /// <returns>The number of valid removal rows.</returns>
    private int CheckRemovals(Dictionary<string, string> sources)
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

        return table.Rows.Count;
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
    ///     Checks the table and gathers its figures.
    /// </summary>
    /// <param name="record">The record, or null when it is unreadable (its limits are then not applied).</param>
    /// <returns>The table figures.</returns>
    private TableFigures CheckTable(RecordValues? record)
    {
        var table = TsvTable.Load(Path.Combine(_data.Root, TableFile), TableFile, _report, new TsvLayout(ExpectedHeader: TableHeader));
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var minimum = long.MaxValue;
        string? previous = null;
        foreach (var row in table.Rows)
        {
            var location = $"{TableFile}:{row.LineNumber}";
            var word = row[0];
            if (word.Length is 0 or > MaximumWordLength || !word.All(IsEligible))
            {
                Add(location, "the word must be 1 to 40 eligible characters");
            }

            if (previous is not null && string.CompareOrdinal(previous, word) >= 0)
            {
                Add(location, "the words must be unique and in ordinal order");
            }

            previous = word;
            if (row[1].Length == 0 || row[1].Any(static c => c is not ('0' or '1')))
            {
                Add(location, "the path is not a bit string");
            }
            else
            {
                paths.Add(row[1]);
            }

            if (!TryParseCount(row[2], out var count) || count <= 0)
            {
                Add(location, "the count is not a positive integer");
            }
            else
            {
                minimum = Math.Min(minimum, count);
            }
        }

        // In ordinal order a path that is a prefix of another directly precedes one it prefixes
        var ordered = paths.Order(StringComparer.Ordinal).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].StartsWith(ordered[i - 1], StringComparison.Ordinal))
            {
                Add(TableFile, $"the path {ordered[i - 1]} is a prefix of {ordered[i]}");
            }
        }

        if (record is not null && minimum != long.MaxValue && minimum < record.MinCount)
        {
            Add(TableFile, $"a count of {minimum} is below the record's minCount {record.MinCount}");
        }

        if (record is not null && paths.Count > record.Clusters)
        {
            Add(TableFile, $"{paths.Count} distinct paths but the record has {record.Clusters} clusters");
        }

        return new TableFigures(table.Rows.Count, paths.Count);
    }

    /// <summary>
    ///     Reads the values of the record the check compares.
    /// </summary>
    /// <returns>The values, or null when the record is unreadable (a finding is added).</returns>
    private RecordValues? ReadRecord()
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_data.Root, RecordFile)));
            var root = document.RootElement;
            var schema = root.GetProperty(SchemaField).GetInt32();
            if (schema != RecordSchema)
            {
                Add(RecordFile, $"schema {schema} is not {RecordSchema}");
                return null;
            }

            var settings = root.GetProperty(SettingsField);
            var inputs = root.GetProperty(InputsField);
            var result = root.GetProperty(ResultField);
            return new RecordValues(
                settings.GetProperty(ClustersField).GetInt32(),
                settings.GetProperty(MinCountField).GetInt32(),
                inputs.GetProperty(SourcesShaField).GetString() ?? string.Empty,
                inputs.GetProperty(UsedBooksField).GetInt32(),
                inputs.GetProperty(ExcludedBooksField).GetInt32(),
                inputs.GetProperty(RemovalsShaField).GetString() ?? string.Empty,
                inputs.GetProperty(TokenizerVersionField).GetString() ?? string.Empty,
                result.GetProperty(TableShaField).GetString() ?? string.Empty,
                result.GetProperty(WordsField).GetInt64(),
                result.GetProperty(PathsField).GetInt64());
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            Add(RecordFile, $"not a readable record: {exception.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Compares the record with the committed files and the tokenizer of this build.
    /// </summary>
    /// <param name="record">The record values.</param>
    /// <param name="sources">The status per source id.</param>
    /// <param name="removals">The number of removal rows.</param>
    /// <param name="table">The table figures.</param>
    private void CheckRecord(RecordValues record, Dictionary<string, string> sources, int removals, TableFigures table)
    {
        CompareHash(SourcesFile, SourcesShaField, record.SourcesSha256);
        CompareHash(RemovalsFile, RemovalsShaField, record.RemovalsSha256);
        CompareHash(TableFile, TableShaField, record.TableSha256);

        var used = sources.Values.Count(static status => status == UsedStatus);
        if (record.UsedBooks != used || record.ExcludedBooks != sources.Count - used)
        {
            Add(RecordFile, $"{UsedBooksField} {record.UsedBooks} and {ExcludedBooksField} {record.ExcludedBooks} do not match {SourcesFile} ({used} used, {sources.Count - used} not)");
        }

        if (record.Words != table.Rows || record.Paths != table.Paths)
        {
            Add(RecordFile, $"{WordsField} {record.Words} and {PathsField} {record.Paths} do not match {TableFile} ({table.Rows} rows, {table.Paths} paths)");
        }

        if (_tokenizerVersion is null)
        {
            _report.AddInfo("word-cluster tokenizer version not compared (not supplied)");
        }
        else if (!string.Equals(record.TokenizerVersion, _tokenizerVersion, StringComparison.Ordinal))
        {
            Add(RecordFile, $"{TokenizerVersionField} '{record.TokenizerVersion}' is not the tokenizer of this build '{_tokenizerVersion}'; induce the table again ('cluster --audit --write', then 'cluster --write')");
        }

        _report.AddInfo($"word-cluster table: {table.Rows} words, {table.Paths} paths, {removals} removals, compared with {RecordFile}");
    }

    /// <summary>
    ///     Compares the SHA-256 of a committed file with the record.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="field">The record field.</param>
    /// <param name="recorded">The recorded hash.</param>
    private void CompareHash(string file, string field, string recorded)
    {
        var actual = FileSha256(_data.Root, file);
        if (!string.Equals(actual, recorded, StringComparison.Ordinal))
        {
            Add(RecordFile, $"{field} {recorded} does not match {file} ({actual}); the file changed after the table was induced");
        }
    }

    /// <summary>
    ///     Records a finding on the clusters check.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <param name="message">The message.</param>
    private void Add(string location, string message) => _report.Add(CheckName, location, message);

    /// <summary>The values of the record that the check compares.</summary>
    /// <param name="Clusters">The number of clusters.</param>
    /// <param name="MinCount">The minimum count.</param>
    /// <param name="SourcesSha256">The source list hash.</param>
    /// <param name="UsedBooks">The used books.</param>
    /// <param name="ExcludedBooks">The listed books not used.</param>
    /// <param name="RemovalsSha256">The removals hash.</param>
    /// <param name="TokenizerVersion">The tokenizer version.</param>
    /// <param name="TableSha256">The table hash.</param>
    /// <param name="Words">The table rows.</param>
    /// <param name="Paths">The distinct paths.</param>
    private sealed record RecordValues(
        int Clusters,
        int MinCount,
        string SourcesSha256,
        int UsedBooks,
        int ExcludedBooks,
        string RemovalsSha256,
        string TokenizerVersion,
        string TableSha256,
        long Words,
        long Paths);

    /// <summary>The figures of the table.</summary>
    /// <param name="Rows">The rows.</param>
    /// <param name="Paths">The distinct paths.</param>
    private sealed record TableFigures(int Rows, int Paths);
}
