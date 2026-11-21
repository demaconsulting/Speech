using System.Globalization;
using System.Net;
using System.Text;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

/// <summary>One row of <c>cluster-sources.tsv</c>: a candidate book of the word-cluster corpus with its license record and body pin.</summary>
/// <param name="Line">The 1-based line in the file.</param>
/// <param name="Id">The source id, <c>gutenberg-N</c>.</param>
/// <param name="Number">The e-book number <c>N</c>.</param>
/// <param name="Group">The selection group, for example <c>core-open</c> or <c>domain-anatomy</c>.</param>
/// <param name="Title">The title.</param>
/// <param name="Author">The author with dates.</param>
/// <param name="Year">The year of first publication, or <c>-</c>.</param>
/// <param name="Url">The HTTPS URL of the plain text on an allowed mirror.</param>
/// <param name="LicenseBasis">The recorded US public-domain basis.</param>
/// <param name="BasisRef">Where the basis was recorded.</param>
/// <param name="BodySha256">The pinned SHA-256 of the normalized body, or null when not yet pinned.</param>
/// <param name="BodyBytes">The pinned size of the normalized body in bytes, or null when not yet pinned.</param>
/// <param name="Status">The status: <c>used</c>, <c>excluded-dev</c>, <c>excluded-holdout</c>, <c>rejected-license</c> or <c>rejected-content</c>.</param>
internal sealed record ClusterSource(
    int Line,
    string Id,
    int Number,
    string Group,
    string Title,
    string Author,
    string Year,
    Uri Url,
    string LicenseBasis,
    string BasisRef,
    string? BodySha256,
    long? BodyBytes,
    string Status)
{
    /// <summary>Gets a value indicating whether the book is part of the corpus.</summary>
    public bool IsUsed => string.Equals(Status, ClusterSourceList.UsedStatus, StringComparison.Ordinal);

    /// <summary>Gets a value indicating whether the body is pinned.</summary>
    public bool IsPinned => BodySha256 is not null;
}

/// <summary>
///     The parsed and validated <c>cluster-sources.tsv</c>: every candidate book of the word-cluster corpus, its license
///     record and its body pin.
/// </summary>
/// <remarks>
///     The file is read with the strict data-file rules of <see cref="TsvTable"/> (UTF-8 without byte order mark, LF,
///     NFC, constant columns) and every cell is validated; the first problems are thrown together so a broken list is
///     never fetched from. The raw bytes are kept because the record pins the list by their SHA-256. Immutable.
/// </remarks>
internal sealed class ClusterSourceList
{
    /// <summary>The file name of the list in the data folder.</summary>
    public const string FileName = ClusterDataChecker.SourcesFile;

    /// <summary>The status of a book that is part of the corpus.</summary>
    public const string UsedStatus = ClusterDataChecker.UsedStatus;

    /// <summary>The cell that marks a missing pin, year or value.</summary>
    public const string None = ClusterDataChecker.None;

    /// <summary>The header of the list.</summary>
    public static readonly IReadOnlyList<string> Header = ClusterDataChecker.SourcesHeader;

    /// <summary>The selection groups.</summary>
    public static readonly IReadOnlySet<string> Groups = ClusterDataChecker.Groups;

    /// <summary>The statuses.</summary>
    public static readonly IReadOnlySet<string> Statuses = ClusterDataChecker.Statuses;

    /// <summary>The only hosts books may be fetched from: official mirrors, never the main web site.</summary>
    public static readonly IReadOnlySet<string> AllowedHosts = ClusterDataChecker.AllowedHosts;

    /// <summary>The latest year of first publication accepted (the US public-domain rule the corpus applies).</summary>
    private const int LatestYear = ClusterDataChecker.LatestYear;

    /// <summary>The number of problems listed in the exception of an invalid list.</summary>
    private const int ReportedProblems = 10;

    /// <summary>The column index of <c>body_sha256</c>.</summary>
    private const int ShaColumn = 8;

    /// <summary>The column index of <c>body_bytes</c>.</summary>
    private const int BytesColumn = 9;

    /// <summary>
    ///     Initializes the list.
    /// </summary>
    /// <param name="bytes">The file bytes.</param>
    /// <param name="sources">The rows.</param>
    private ClusterSourceList(byte[] bytes, IReadOnlyList<ClusterSource> sources)
    {
        Bytes = bytes;
        Sources = sources;
        Sha256 = GutenbergBody.Sha256Hex(bytes);
    }

    /// <summary>Gets the file bytes.</summary>
    public byte[] Bytes { get; }

    /// <summary>Gets the lowercase hexadecimal SHA-256 of the file bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Gets the rows in file order (ascending e-book number).</summary>
    public IReadOnlyList<ClusterSource> Sources { get; }

    /// <summary>Gets the books of the corpus in ordinal source-id order, the order every corpus pass uses.</summary>
    public IReadOnlyList<ClusterSource> UsedInCorpusOrder =>
        [.. Sources.Where(static source => source.IsUsed).OrderBy(static source => source.Id, StringComparer.Ordinal)];

    /// <summary>
    ///     Loads and validates the list.
    /// </summary>
    /// <param name="path">The full path of <c>cluster-sources.tsv</c>.</param>
    /// <returns>The list.</returns>
    /// <exception cref="InvalidDataException">The file is missing, breaks a data-file format rule, or a cell is invalid.</exception>
    public static ClusterSourceList Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"the source list {path} was not found");
        }

        // The strict reader reports format defects as findings; any finding makes the list unusable
        var report = new CheckReport();
        var table = TsvTable.Load(path, FileName, report, new TsvLayout(Header));
        var problems = report.Findings.Select(static finding => $"{finding.Location}: {finding.Message}").ToList();
        var sources = new List<ClusterSource>(table.Rows.Count);
        var previous = 0;
        foreach (var row in table.Rows)
        {
            var source = ParseRow(row, problems);
            if (source is null)
            {
                continue;
            }

            if (source.Number <= previous)
            {
                problems.Add($"{FileName}:{row.LineNumber}: rows must be sorted by ascending e-book number without duplicates");
            }

            previous = source.Number;
            sources.Add(source);
        }

        if (problems.Count > 0)
        {
            throw new InvalidDataException($"{FileName} is invalid: {string.Join("; ", problems.Take(ReportedProblems))}");
        }

        return new ClusterSourceList(File.ReadAllBytes(path), sources);
    }

    /// <summary>
    ///     Builds the file bytes with new pins filled into rows that have none.
    /// </summary>
    /// <param name="pins">The new pins by source id; a row that already has a pin is never changed.</param>
    /// <returns>The new file bytes; every other byte of the file is unchanged.</returns>
    public byte[] WithPins(IReadOnlyDictionary<string, (string Sha256, long Bytes)> pins)
    {
        ArgumentNullException.ThrowIfNull(pins);
        var lines = Encoding.UTF8.GetString(Bytes).Split('\n');
        foreach (var source in Sources.Where(source => !source.IsPinned))
        {
            if (!pins.TryGetValue(source.Id, out var pin))
            {
                continue;
            }

            // Rewrite only the two pin cells of this row
            var cells = lines[source.Line - 1].Split('\t');
            cells[ShaColumn] = pin.Sha256;
            cells[BytesColumn] = pin.Bytes.ToString(CultureInfo.InvariantCulture);
            lines[source.Line - 1] = string.Join('\t', cells);
        }

        return Encoding.UTF8.GetBytes(string.Join('\n', lines));
    }

    /// <summary>
    ///     Parses and validates one row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="problems">Receives the problems of the row.</param>
    /// <returns>The source, or null when the row is invalid.</returns>
    private static ClusterSource? ParseRow(TsvRow row, List<string> problems)
    {
        var location = $"{FileName}:{row.LineNumber}";
        var count = problems.Count;

        // The id names the e-book number; the number has no sign and no leading zero
        var id = row[0];
        var number = 0;
        if (!id.StartsWith("gutenberg-", StringComparison.Ordinal)
            || !int.TryParse(id.AsSpan("gutenberg-".Length), NumberStyles.None, CultureInfo.InvariantCulture, out number)
            || number <= 0
            || !string.Equals(id, $"gutenberg-{number.ToString(CultureInfo.InvariantCulture)}", StringComparison.Ordinal))
        {
            problems.Add($"{location}: source_id '{id}' is not gutenberg-N");
        }

        if (!Groups.Contains(row[1]))
        {
            problems.Add($"{location}: unknown group '{row[1]}'");
        }

        problems.AddRange(new[] { 2, 3, 6, 7 }.Where(column => string.IsNullOrWhiteSpace(row[column])).Select(column => $"{location}: {Header[column]} is empty"));

        var year = row[4];
        if (year != None && (!int.TryParse(year, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value > LatestYear))
        {
            problems.Add($"{location}: year '{year}' is not a year of {LatestYear} or earlier, or '-'");
        }

        var url = ParseUrl(row[5], number, location, problems);
        var status = row[10];
        if (!Statuses.Contains(status))
        {
            problems.Add($"{location}: unknown status '{status}'");
        }

        var (sha, bytes) = ParsePin(row[ShaColumn], row[BytesColumn], status, location, problems);
        return problems.Count > count || url is null
            ? null
            : new ClusterSource(row.LineNumber, id, number, row[1], row[2], row[3], year, url, row[6], row[7], sha, bytes, status);
    }

    /// <summary>
    ///     Validates the URL of a row: HTTPS, an allowed mirror, and a plain-text file of the row's e-book number.
    /// </summary>
    /// <param name="text">The cell.</param>
    /// <param name="number">The e-book number.</param>
    /// <param name="location">The location for problems.</param>
    /// <param name="problems">Receives the problems.</param>
    /// <returns>The URL, or null when invalid.</returns>
    private static Uri? ParseUrl(string text, int number, string location, List<string> problems)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps || !AllowedHosts.Contains(url.Host)
            || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment) || !url.IsDefaultPort)
        {
            problems.Add($"{location}: url '{text}' is not an HTTPS URL on an allowed mirror ({string.Join(", ", AllowedHosts.Order(StringComparer.Ordinal))})");
            return null;
        }

        var n = number.ToString(CultureInfo.InvariantCulture);
        var path = url.AbsolutePath;
        if (!path.EndsWith($"/{n}/pg{n}.txt", StringComparison.Ordinal) && !path.EndsWith($"/{n}/{n}-0.txt", StringComparison.Ordinal)
            && !path.EndsWith($"/{n}/{n}.txt", StringComparison.Ordinal))
        {
            problems.Add($"{location}: url '{text}' is not a plain-text file of e-book {n}");
            return null;
        }

        return url;
    }

    /// <summary>
    ///     Validates the two pin cells of a row.
    /// </summary>
    /// <param name="sha">The <c>body_sha256</c> cell.</param>
    /// <param name="bytes">The <c>body_bytes</c> cell.</param>
    /// <param name="status">The status of the row.</param>
    /// <param name="location">The location for problems.</param>
    /// <param name="problems">Receives the problems.</param>
    /// <returns>The pin, or nulls when the row is not pinned.</returns>
    private static (string? Sha, long? Bytes) ParsePin(string sha, string bytes, string status, string location, List<string> problems)
    {
        if (sha == None && bytes == None)
        {
            return (null, null);
        }

        // Only a book of the corpus carries a pin, and then both cells are valid
        if (!string.Equals(status, UsedStatus, StringComparison.Ordinal))
        {
            problems.Add($"{location}: only a used book has a body pin; write '-' in body_sha256 and body_bytes");
            return (null, null);
        }

        var shaValid = sha.Length == 64 && sha.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
        var bytesValid = long.TryParse(bytes, NumberStyles.None, CultureInfo.InvariantCulture, out var size) && size > 0;
        if (!shaValid || !bytesValid)
        {
            problems.Add($"{location}: the body pin must be a lowercase 64-digit SHA-256 and a positive byte count, or '-' in both");
            return (null, null);
        }

        return (sha, size);
    }
}

/// <summary>The outcome of making one book's body available in the cache.</summary>
/// <param name="SourceId">The source id.</param>
/// <param name="Outcome">One of <c>cached</c>, <c>fetched</c>, <c>pinned</c> (a new pin was computed) or <c>failed</c>.</param>
/// <param name="Problem">The problem of a failed book; null otherwise.</param>
/// <param name="Body">The verified body, or null when failed.</param>
internal sealed record CorpusFetchResult(string SourceId, string Outcome, string? Problem, GutenbergBodyText? Body);

/// <summary>
///     The local cache of the word-cluster corpus: fetches each book's plain text and metadata from an official mirror
///     with a polite rate limit, extracts the pinned body and hands out only bodies that match their pins.
/// </summary>
/// <remarks>
///     <para>
///     Layout below the cache folder: <c>raw/pgN.txt</c> and <c>raw/pgN.rdf</c> as downloaded, and
///     <c>bodies/gutenberg-N.txt</c>, the normalized body. Every download is written to a <c>.partial</c> file first and
///     renamed only when complete, so a failed or interrupted fetch never leaves a truncated file. Only books with
///     status <c>used</c> are fetched, only missing or failing files are requested, requests are sequential with at least
///     the configured delay between them (the first request of a run is not delayed), a failed request is retried at most
///     three times after 5, 15 and 45 seconds, a refusal (HTTP 403 or 429) stops the whole run, and a redirect (which
///     the default fetcher never follows) fails only that book, without a retry. A cancellation by the caller is never
///     retried or reported as a failed download: it propagates as soon as it is seen, also before and after every
///     pause, while a canceled task without a canceled caller (the HTTP client's timeout) is retried. The network is
///     reached only through the fetcher delegate and the pause only through the sleeper delegate, so tests run offline
///     and instantly. Not thread-safe.
///     </para>
/// </remarks>
internal sealed class ClusterCorpusCache
{
    /// <summary>The default polite delay between two requests to the mirror.</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(2000);

    /// <summary>The waits before the first, second and third retry of a failed request.</summary>
    public static readonly IReadOnlyList<TimeSpan> RetryWaits = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45)];

    /// <summary>The largest download accepted, in bytes (the largest real book is far smaller).</summary>
    public const long MaximumDownloadBytes = 64L * 1024 * 1024;

    /// <summary>The suffix of a download that is not yet complete.</summary>
    private const string PartialSuffix = ".partial";

    /// <summary>The cache folder.</summary>
    private readonly string _root;

    /// <summary>Opens a URL; the default is the HTTPS mirror download.</summary>
    private readonly Func<Uri, CancellationToken, Stream> _fetcher;

    /// <summary>Pauses between requests, or null to wait on the run's cancellation token (which a cancellation wakes).</summary>
    private readonly Action<TimeSpan>? _sleeper;

    /// <summary>The delay between two requests.</summary>
    private readonly TimeSpan _delay;

    /// <summary>The mirror host that replaces the host of every URL, or null to use the listed URLs.</summary>
    private readonly string? _mirror;

    /// <summary>Whether a request has been made in this run (the first one is not delayed).</summary>
    private bool _requested;

    /// <summary>
    ///     Initializes a cache.
    /// </summary>
    /// <param name="root">The cache folder; created on the first write.</param>
    /// <param name="fetcher">Opens a URL, or null for the HTTPS mirror download.</param>
    /// <param name="sleeper">Pauses, or null to wait on the cancellation token of the run, which a cancellation wakes.</param>
    /// <param name="delay">The delay between two requests; not negative.</param>
    /// <param name="mirror">A host from <see cref="ClusterSourceList.AllowedHosts"/> that replaces each listed host, or null.</param>
    /// <exception cref="ArgumentException"><paramref name="mirror"/> is not an allowed host, or <paramref name="delay"/> is negative.</exception>
    public ClusterCorpusCache(string root, Func<Uri, CancellationToken, Stream>? fetcher = null, Action<TimeSpan>? sleeper = null, TimeSpan? delay = null, string? mirror = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (mirror is not null && !ClusterSourceList.AllowedHosts.Contains(mirror))
        {
            throw new ArgumentException($"the mirror '{mirror}' is not an allowed mirror", nameof(mirror));
        }

        _root = Path.GetFullPath(root);
        _fetcher = fetcher ?? GutenbergMirrorFetcher.Open;
        _sleeper = sleeper;
        _delay = delay ?? DefaultDelay;
        if (_delay < TimeSpan.Zero)
        {
            throw new ArgumentException("the delay must not be negative", nameof(delay));
        }

        _mirror = mirror;
    }

    /// <summary>Gets the number of requests made so far, including retries.</summary>
    public int Requests { get; private set; }

    /// <summary>
    ///     Gets the path of a book's normalized body in the cache.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The full path.</returns>
    public string BodyPath(ClusterSource source) => Path.Combine(_root, "bodies", $"{source.Id}.txt");

    /// <summary>
    ///     Makes every used book's body and metadata available, fetching only what is missing or fails its pin.
    /// </summary>
    /// <param name="sources">The source list.</param>
    /// <param name="log">Receives one progress line per book.</param>
    /// <param name="cancellationToken">Stops between books, and before or during a request or a pause; a cancellation is never retried.</param>
    /// <returns>The outcome per used book in ordinal source-id order.</returns>
    /// <exception cref="IOException">The mirror refused a request (HTTP 403 or 429); the run stops at once.</exception>
    /// <exception cref="OperationCanceledException">The run was canceled.</exception>
    public IReadOnlyList<CorpusFetchResult> Fetch(ClusterSourceList sources, TextWriter log, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(log);
        var used = sources.UsedInCorpusOrder;
        var results = new List<CorpusFetchResult>(used.Count);
        for (var index = 0; index < used.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = used[index];
            var result = FetchOne(source, cancellationToken);
            results.Add(result);
            log.WriteLine($"cluster: fetch {index + 1}/{used.Count} {source.Id}: {result.Outcome}{(result.Problem is null ? string.Empty : $" ({result.Problem})")}");
        }

        return results;
    }

    /// <summary>
    ///     Checks a book's cached body against its pin without touching the network.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The problem, or null when the body is present and matches its pin.</returns>
    public string? CheckBody(ClusterSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.IsPinned)
        {
            return $"{source.Id} has no body pin; run 'cluster --fetch --write-pins'";
        }

        var path = BodyPath(source);
        if (!File.Exists(path))
        {
            return $"the body of {source.Id} is not cached; run 'cluster --fetch'";
        }

        return CheckPin(source, File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Reads the verified bodies of every used book from the cache.
    /// </summary>
    /// <param name="sources">The source list.</param>
    /// <returns>The bodies in ordinal source-id order.</returns>
    /// <exception cref="InvalidDataException">A used book is unpinned, missing from the cache or differs from its pin.</exception>
    public IReadOnlyList<(ClusterSource Source, string Text)> ReadBodies(ClusterSourceList sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var result = new List<(ClusterSource, string)>();
        foreach (var source in sources.UsedInCorpusOrder)
        {
            var problem = CheckBody(source);
            if (problem is not null)
            {
                throw new InvalidDataException(problem);
            }

            result.Add((source, Encoding.UTF8.GetString(File.ReadAllBytes(BodyPath(source)))));
        }

        return result;
    }

    /// <summary>
    ///     Reads the verified bodies of every used book from a snapshot archive instead of the cache.
    /// </summary>
    /// <param name="sources">The source list.</param>
    /// <param name="snapshotPath">The snapshot archive.</param>
    /// <returns>The bodies in ordinal source-id order.</returns>
    /// <exception cref="InvalidDataException">The archive is malformed, or a used book is unpinned, absent or differs from its pin.</exception>
    public static IReadOnlyList<(ClusterSource Source, string Text)> ReadBodiesFromSnapshot(ClusterSourceList sources, string snapshotPath)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var members = CorpusSnapshot.Read(snapshotPath);
        var result = new List<(ClusterSource, string)>();
        foreach (var source in sources.UsedInCorpusOrder)
        {
            if (!source.IsPinned)
            {
                throw new InvalidDataException($"{source.Id} has no body pin");
            }

            if (!members.TryGetValue(CorpusSnapshot.BodyMemberName(source.Id), out var bytes))
            {
                throw new InvalidDataException($"the snapshot has no body of {source.Id}");
            }

            var problem = CheckPin(source, bytes);
            if (problem is not null)
            {
                throw new InvalidDataException(problem);
            }

            result.Add((source, Encoding.UTF8.GetString(bytes)));
        }

        return result;
    }

    /// <summary>
    ///     Compares body bytes with a source's pin.
    /// </summary>
    /// <param name="source">The pinned source.</param>
    /// <param name="bytes">The body bytes.</param>
    /// <returns>The problem, or null when the bytes match.</returns>
    private static string? CheckPin(ClusterSource source, byte[] bytes)
    {
        if (bytes.LongLength != source.BodyBytes)
        {
            return $"the body of {source.Id} has {bytes.LongLength} bytes but the pin is {source.BodyBytes}";
        }

        var sha = GutenbergBody.Sha256Hex(bytes);
        return string.Equals(sha, source.BodySha256, StringComparison.Ordinal)
            ? null
            : $"the body of {source.Id} has SHA-256 {sha} but the pin is {source.BodySha256}";
    }

    /// <summary>
    ///     Makes one book's body and metadata available.
    /// </summary>
    /// <param name="source">The used source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    private CorpusFetchResult FetchOne(ClusterSource source, CancellationToken cancellationToken)
    {
        var raw = Path.Combine(_root, "raw", $"pg{source.Number.ToString(CultureInfo.InvariantCulture)}.txt");
        var rdf = Path.Combine(_root, "raw", $"pg{source.Number.ToString(CultureInfo.InvariantCulture)}.rdf");
        try
        {
            // The metadata is fetched once; it documents the license check and is never re-fetched
            if (!File.Exists(rdf))
            {
                Download(RdfUrl(source), rdf, cancellationToken);
            }

            // A pinned body already in the cache needs nothing else
            if (source.IsPinned && CheckBody(source) is null)
            {
                return new CorpusFetchResult(source.Id, "cached", null, ReadCachedBody(source));
            }

            // Reuse the raw download when it yields the pinned body (or when there is no pin yet); otherwise fetch again
            var fetched = false;
            if (!File.Exists(raw) || (source.IsPinned && Matches(source, raw) is not null))
            {
                Download(Locate(source.Url), raw, cancellationToken);
                fetched = true;
            }

            var body = GutenbergBody.Extract(File.ReadAllBytes(raw));
            if (source.IsPinned)
            {
                var problem = CheckPin(source, body.Bytes);
                if (problem is not null)
                {
                    return new CorpusFetchResult(source.Id, "failed", $"the mirror's text differs from the pin: {problem}", null);
                }
            }

            WriteAtomically(BodyPath(source), body.Bytes);
            var outcome = fetched ? "fetched" : "cached";
            return new CorpusFetchResult(source.Id, source.IsPinned ? outcome : "pinned", null, body);
        }
        catch (InvalidDataException exception)
        {
            return new CorpusFetchResult(source.Id, "failed", exception.Message, null);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return new CorpusFetchResult(source.Id, "failed", $"not found on the mirror: {exception.Message}", null);
        }
    }

    /// <summary>
    ///     Re-reads a cached body that already matches its pin, as an extraction result.
    /// </summary>
    /// <param name="source">The pinned source.</param>
    /// <returns>The body.</returns>
    private GutenbergBodyText ReadCachedBody(ClusterSource source)
    {
        var bytes = File.ReadAllBytes(BodyPath(source));
        return new GutenbergBodyText(Encoding.UTF8.GetString(bytes), bytes, source.BodySha256!, 0, 0);
    }

    /// <summary>
    ///     Checks whether a raw download yields a source's pinned body.
    /// </summary>
    /// <param name="source">The pinned source.</param>
    /// <param name="raw">The raw download path.</param>
    /// <returns>The problem, or null when it matches.</returns>
    private static string? Matches(ClusterSource source, string raw)
    {
        try
        {
            return CheckPin(source, GutenbergBody.Extract(File.ReadAllBytes(raw)).Bytes);
        }
        catch (InvalidDataException exception)
        {
            return exception.Message;
        }
    }

    /// <summary>
    ///     Gets the metadata URL of a book on the same mirror as its text.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The URL of <c>cache/epub/N/pgN.rdf</c>.</returns>
    private Uri RdfUrl(ClusterSource source)
    {
        var n = source.Number.ToString(CultureInfo.InvariantCulture);
        return Locate(new Uri($"https://{source.Url.Host}/cache/epub/{n}/pg{n}.rdf"));
    }

    /// <summary>
    ///     Replaces the host of a URL with the configured mirror, if any.
    /// </summary>
    /// <param name="url">The listed URL.</param>
    /// <returns>The URL to request.</returns>
    private Uri Locate(Uri url) => _mirror is null ? url : new UriBuilder(url) { Host = _mirror }.Uri;

    /// <summary>
    ///     Downloads a URL into a file with the rate limit and the retry policy.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <param name="path">The destination file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="IOException">The mirror refused the request, or every attempt failed.</exception>
    /// <exception cref="HttpRequestException">The file does not exist on the mirror (HTTP 404).</exception>
    /// <exception cref="InvalidDataException">The URL is not an allowed mirror, the mirror answered with a redirect, or the download is too large.</exception>
    /// <exception cref="OperationCanceledException">The caller canceled, before or during a request or a pause; never retried.</exception>
    private void Download(Uri url, string path, CancellationToken cancellationToken)
    {
        if (!GutenbergMirrorFetcher.IsAllowedMirror(url))
        {
            throw new InvalidDataException($"refusing to fetch {url}: not an allowed mirror");
        }

        var attempt = 0;
        while (true)
        {
            // Be polite: at least the configured delay between the starts of two requests
            if (_requested)
            {
                Pause(_delay, cancellationToken);
            }

            _requested = true;
            Requests++;
            try
            {
                using var stream = _fetcher(url, cancellationToken);
                WriteAtomically(path, ReadLimited(stream, url, cancellationToken));
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller canceled: never retried and never turned into a download failure. A canceled task
                // without a canceled caller (the client's own timeout) falls through to the retry below.
                throw;
            }
            catch (HttpRequestException exception) when (exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                throw new IOException($"the mirror refused {url} (HTTP {(int)exception.StatusCode}); stopping the run", exception);
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                throw;
            }
            catch (Exception exception) when ((exception is HttpRequestException or IOException or TaskCanceledException) && attempt < RetryWaits.Count)
            {
                Pause(RetryWaits[attempt], cancellationToken);
                attempt++;
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
            {
                throw new IOException($"could not download {url} after {RetryWaits.Count + 1} attempts: {exception.Message}", exception);
            }
        }
    }

    /// <summary>
    ///     Pauses before a request or a retry, observing the caller's cancellation before and after the pause.
    /// </summary>
    /// <remarks>
    ///     An injected sleeper cannot be interrupted, so the token is checked again when it returns; a sleeper that
    ///     observes a cancellation therefore stops the run before the next request. Without an injected sleeper the
    ///     pause waits on the token, so a cancellation ends it at once.
    /// </remarks>
    /// <param name="wait">The length of the pause; not negative.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <exception cref="OperationCanceledException">The caller canceled before or during the pause.</exception>
    private void Pause(TimeSpan wait, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_sleeper is not null)
        {
            _sleeper(wait);
        }
        else if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.WaitHandle.WaitOne(wait);
        }
        else
        {
            Thread.Sleep(wait);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    ///     Reads a response completely, refusing one larger than <see cref="MaximumDownloadBytes"/>.
    /// </summary>
    /// <remarks>
    ///     Every chunk is read through the cancelable read path and the token is checked between chunks, so a caller's
    ///     cancellation ends a slow or large body promptly instead of waiting for the stream or the client timeout. The
    ///     file is written only after the whole body was read.
    /// </remarks>
    /// <param name="stream">The response stream.</param>
    /// <param name="url">The URL, for the message.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="OperationCanceledException">The caller canceled while the body was being read.</exception>
    private static byte[] ReadLimited(Stream stream, Uri url, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = stream.ReadAsync(chunk.AsMemory(), cancellationToken).AsTask().GetAwaiter().GetResult();
            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaximumDownloadBytes)
            {
                throw new InvalidDataException($"{url} is larger than {MaximumDownloadBytes} bytes");
            }
        }

        return buffer.ToArray();
    }

    /// <summary>
    ///     Writes a file through a <c>.partial</c> sibling and a rename, so a reader never sees a truncated file.
    /// </summary>
    /// <param name="path">The destination.</param>
    /// <param name="bytes">The content.</param>
    private static void WriteAtomically(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var partial = path + PartialSuffix;
        File.WriteAllBytes(partial, bytes);
        File.Move(partial, path, true);
    }
}

/// <summary>
///     Opens a file on an official e-book mirror over HTTPS with an identifying user agent.
/// </summary>
/// <remarks>
///     The client never follows a redirect: its handler has automatic redirects off and any 3xx answer becomes an
///     <see cref="InvalidDataException"/>, so a book can never be fetched from the main web site through a redirect and
///     the cache fails only that book, without a retry. Any other non-success status becomes an
///     <see cref="HttpRequestException"/> carrying the status code, so the cache can stop on a refusal and skip a missing
///     file. Thread-safe (one shared client).
/// </remarks>
internal static class GutenbergMirrorFetcher
{
    /// <summary>The user agent that identifies every request.</summary>
    public const string UserAgent = "DemaConsulting.Speech.Text.En.Trainer (+https://github.com/demaconsulting/Speech)";

    /// <summary>The shared client; a request that takes longer than five minutes fails.</summary>
    private static readonly HttpClient Client = CreateClient();

    /// <summary>
    ///     Checks whether a URL may be fetched: HTTPS on a host of <see cref="ClusterSourceList.AllowedHosts"/>.
    /// </summary>
    /// <param name="uri">The URL.</param>
    /// <returns>True when the URL is HTTPS on an allowed mirror.</returns>
    public static bool IsAllowedMirror(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.Scheme == Uri.UriSchemeHttps && ClusterSourceList.AllowedHosts.Contains(uri.Host);
    }

    /// <summary>
    ///     Opens a URL with the shared client.
    /// </summary>
    /// <param name="uri">The HTTPS URL on an allowed mirror.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>The response body; the caller disposes it.</returns>
    /// <exception cref="InvalidDataException">The URL is not HTTPS on an allowed mirror, or the mirror answered with a redirect.</exception>
    /// <exception cref="HttpRequestException">The request fails or the status is not a success.</exception>
    public static Stream Open(Uri uri, CancellationToken cancellation) => Open(Client, uri, cancellation);

    /// <summary>
    ///     Opens a URL with a given client; the seam that lets tests answer through a fake handler.
    /// </summary>
    /// <param name="client">The client; it must not follow redirects (see <see cref="CreateHandler"/>).</param>
    /// <param name="uri">The HTTPS URL on an allowed mirror.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>The response body; the caller disposes it.</returns>
    /// <exception cref="InvalidDataException">The URL is not HTTPS on an allowed mirror, or the mirror answered with a redirect.</exception>
    /// <exception cref="HttpRequestException">The request fails or the status is not a success.</exception>
    public static Stream Open(HttpClient client, Uri uri, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(uri);
        if (!IsAllowedMirror(uri))
        {
            throw new InvalidDataException($"refusing to fetch {uri}: not HTTPS on an allowed mirror");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).GetAwaiter().GetResult();
        var code = (int)response.StatusCode;

        // A redirect is never followed: it could lead away from the mirror, for example to the main web site
        if (code is >= 300 and <= 399)
        {
            var location = response.Headers.Location;
            response.Dispose();
            throw new InvalidDataException(
                $"refusing the redirect of {uri} (HTTP {code}) to {location?.ToString() ?? "no location"}: books are fetched only from an allowed mirror and redirects are never followed");
        }

        if (!response.IsSuccessStatusCode)
        {
            var status = response.StatusCode;
            response.Dispose();
            throw new HttpRequestException($"HTTP {code} for {uri}", null, status);
        }

        return response.Content.ReadAsStream(cancellation);
    }

    /// <summary>
    ///     Creates the handler of the shared client, with automatic redirects off.
    /// </summary>
    /// <returns>The handler.</returns>
    public static SocketsHttpHandler CreateHandler() => new() { AllowAutoRedirect = false };

    /// <summary>
    ///     Creates the client.
    /// </summary>
    /// <returns>The client.</returns>
    private static HttpClient CreateClient() => new(CreateHandler()) { Timeout = TimeSpan.FromMinutes(5) };
}
