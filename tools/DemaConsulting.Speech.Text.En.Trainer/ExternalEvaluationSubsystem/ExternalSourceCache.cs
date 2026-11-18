using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;

/// <summary>One pinned file of an external source: its path inside the source, its size and its SHA-256.</summary>
/// <param name="Path">The relative, <c>/</c>-separated path inside the source archive.</param>
/// <param name="Sha256">The lowercase hexadecimal SHA-256 of the file.</param>
/// <param name="Bytes">The size of the file in bytes.</param>
internal sealed record PinnedFile(string Path, string Sha256, long Bytes);

/// <summary>One external source pinned to a revision: where it comes from and every file it consists of.</summary>
/// <param name="Id">The source id (<c>whd</c> or <c>bench</c>).</param>
/// <param name="Repository">The repository URL (documentation of origin).</param>
/// <param name="Commit">The 40-character lowercase hexadecimal commit.</param>
/// <param name="Archive">The HTTPS URL of the archive of that commit.</param>
/// <param name="License">The license statement of the source.</param>
/// <param name="FilesSha256">The SHA-256 of the canonical listing <c>path TAB sha256 LF</c> of the files in ordinal path order.</param>
/// <param name="Files">The pinned files in ordinal path order.</param>
internal sealed record ExternalSourcePin(
    string Id,
    string Repository,
    string Commit,
    Uri Archive,
    string License,
    string FilesSha256,
    IReadOnlyList<PinnedFile> Files)
{
    /// <summary>Gets the total size of the pinned files in bytes.</summary>
    public long TotalBytes => Files.Sum(static file => file.Bytes);
}

/// <summary>
///     The pins of the external sources: a small JSON document naming each source's revision and the sibling
///     tab-separated file that lists every pinned file with its size and SHA-256.
/// </summary>
internal sealed class ExternalSourcePins
{
    /// <summary>The name of the per-file pins file, next to the JSON document.</summary>
    public const string FilesFileName = "external-files.tsv";

    /// <summary>The header of the per-file pins file.</summary>
    private const string FilesHeader = "source\tpath\tsha256\tbytes";

    /// <summary>
    ///     Initializes the pins.
    /// </summary>
    /// <param name="sources">The pinned sources.</param>
    private ExternalSourcePins(IReadOnlyList<ExternalSourcePin> sources) => Sources = sources;

    /// <summary>Gets the pinned sources in the order of the JSON document.</summary>
    public IReadOnlyList<ExternalSourcePin> Sources { get; }

    /// <summary>
    ///     Loads and validates the pins.
    /// </summary>
    /// <param name="jsonPath">The full path of the JSON document; the per-file pins are the sibling <c>external-files.tsv</c>.</param>
    /// <returns>The pins.</returns>
    /// <exception cref="InvalidDataException">
    ///     A file is missing or malformed, a commit is not 40 lowercase hexadecimal characters, an archive is not an HTTPS
    ///     URL of that commit, a path is not a safe relative path or the rows are not unique and sorted, or a source's
    ///     file count, byte total or listing hash differs between the two files.
    /// </exception>
    public static ExternalSourcePins Load(string jsonPath)
    {
        ArgumentNullException.ThrowIfNull(jsonPath);
        if (!File.Exists(jsonPath))
        {
            throw new InvalidDataException($"the pins file {jsonPath} was not found");
        }

        var tsvPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(jsonPath)) ?? string.Empty, FilesFileName);
        if (!File.Exists(tsvPath))
        {
            throw new InvalidDataException($"the per-file pins file {tsvPath} was not found");
        }

        var files = ReadFiles(File.ReadAllText(tsvPath, new UTF8Encoding(false, true)));
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(jsonPath));
            return Build(document.RootElement, files);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or UriFormatException)
        {
            throw new InvalidDataException($"external-sources.json: {exception.Message}");
        }
    }

    /// <summary>
    ///     Finds a pinned source.
    /// </summary>
    /// <param name="id">The source id.</param>
    /// <returns>The source.</returns>
    /// <exception cref="InvalidDataException">The pins have no such source.</exception>
    public ExternalSourcePin Find(string id) =>
        Sources.FirstOrDefault(source => string.Equals(source.Id, id, StringComparison.Ordinal))
        ?? throw new InvalidDataException($"the pins have no source '{id}'");

    /// <summary>
    ///     Reads and validates the per-file pins.
    /// </summary>
    /// <param name="text">The file text.</param>
    /// <returns>The pinned files by source id, each in file order.</returns>
    private static Dictionary<string, List<PinnedFile>> ReadFiles(string text)
    {
        var lines = text.Split('\n');
        if (lines[0] != FilesHeader)
        {
            throw new InvalidDataException($"{FilesFileName}: the header is not '{FilesHeader.Replace("\t", "<TAB>", StringComparison.Ordinal)}'");
        }

        var result = new Dictionary<string, List<PinnedFile>>(StringComparer.Ordinal);
        string? previousSource = null;
        string? previousPath = null;
        for (var i = 1; i < lines.Length; i++)
        {
            if (i == lines.Length - 1 && lines[i].Length == 0)
            {
                break;
            }

            var cells = lines[i].Split('\t');
            if (cells.Length != 4)
            {
                throw new InvalidDataException($"{FilesFileName}:{i + 1}: expected four cells");
            }

            if (!IsSafePath(cells[1]))
            {
                throw new InvalidDataException($"{FilesFileName}:{i + 1}: the path is not a safe relative path");
            }

            if (!IsHex(cells[2], 64) || !long.TryParse(cells[3], NumberStyles.None, CultureInfo.InvariantCulture, out var bytes))
            {
                throw new InvalidDataException($"{FilesFileName}:{i + 1}: the SHA-256 or the byte count is malformed");
            }

            if (previousSource is not null && (string.CompareOrdinal(previousSource, cells[0]) > 0
                || (previousSource == cells[0] && string.CompareOrdinal(previousPath, cells[1]) >= 0)))
            {
                throw new InvalidDataException($"{FilesFileName}:{i + 1}: the rows are not unique and sorted by source and path");
            }

            previousSource = cells[0];
            previousPath = cells[1];
            if (!result.TryGetValue(cells[0], out var list))
            {
                list = [];
                result[cells[0]] = list;
            }

            list.Add(new PinnedFile(cells[1], cells[2], bytes));
        }

        return result;
    }

    /// <summary>
    ///     Builds the pinned sources from the JSON document and checks them against the per-file pins.
    /// </summary>
    /// <param name="root">The JSON root.</param>
    /// <param name="files">The per-file pins by source id.</param>
    /// <returns>The pins.</returns>
    private static ExternalSourcePins Build(JsonElement root, Dictionary<string, List<PinnedFile>> files)
    {
        if (root.GetProperty("schema").GetInt32() != 1)
        {
            throw new InvalidDataException("external-sources.json: the schema version is not 1");
        }

        var sources = new List<ExternalSourcePin>();
        foreach (var element in root.GetProperty("sources").EnumerateArray())
        {
            var id = element.GetProperty("id").GetString() ?? string.Empty;
            var commit = element.GetProperty("commit").GetString() ?? string.Empty;
            var archive = new Uri(element.GetProperty("archive").GetString() ?? string.Empty, UriKind.Absolute);
            if (id.Length == 0 || sources.Any(source => source.Id == id))
            {
                throw new InvalidDataException("external-sources.json: a source id is empty or repeated");
            }

            if (!IsHex(commit, 40))
            {
                throw new InvalidDataException($"external-sources.json: the commit of '{id}' is not 40 lowercase hexadecimal characters");
            }

            if (archive.Scheme != Uri.UriSchemeHttps || !archive.AbsoluteUri.Contains(commit, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"external-sources.json: the archive of '{id}' is not an HTTPS URL of its commit");
            }

            if (!files.TryGetValue(id, out var list))
            {
                throw new InvalidDataException($"{FilesFileName}: there are no files for the source '{id}'");
            }

            var filesSha = element.GetProperty("filesSha256").GetString() ?? string.Empty;
            var pin = new ExternalSourcePin(
                id,
                element.GetProperty("repository").GetString() ?? string.Empty,
                commit,
                archive,
                element.GetProperty("license").GetString() ?? string.Empty,
                filesSha,
                list);
            if (pin.Files.Count != element.GetProperty("files").GetInt32()
                || pin.TotalBytes != element.GetProperty("bytes").GetInt64()
                || !string.Equals(ListingSha256(list), filesSha, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"external-sources.json: the file count, byte total or listing hash of '{id}' does not match {FilesFileName}");
            }

            sources.Add(pin);
        }

        var unknown = files.Keys.FirstOrDefault(key => sources.All(source => source.Id != key));
        if (unknown is not null)
        {
            throw new InvalidDataException($"{FilesFileName}: the source '{unknown}' is not in external-sources.json");
        }

        return new ExternalSourcePins(sources);
    }

    /// <summary>
    ///     Computes the SHA-256 of the canonical listing of a source's files.
    /// </summary>
    /// <param name="files">The files in ordinal path order.</param>
    /// <returns>The lowercase hexadecimal hash.</returns>
    private static string ListingSha256(IReadOnlyList<PinnedFile> files)
    {
        var listing = new StringBuilder();
        foreach (var file in files)
        {
            listing.Append(file.Path).Append('\t').Append(file.Sha256).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(listing.ToString()))).ToLowerInvariant();
    }

    /// <summary>
    ///     Determines whether a path is a safe relative path made of letters, digits, dot, underscore, hyphen and slash.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true"/> when the path is safe.</returns>
    private static bool IsSafePath(string path) =>
        path.Length > 0
        && path[0] != '/'
        && path[^1] != '/'
        && path.All(static c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '_' or '-' or '/')
        && !path.Split('/').Any(static part => part.Length == 0 || part == "." || part == "..");

    /// <summary>
    ///     Determines whether a text is lowercase hexadecimal of a given length.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="length">The required length.</param>
    /// <returns><see langword="true"/> when the text matches.</returns>
    private static bool IsHex(string text, int length) =>
        text.Length == length && text.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}

/// <summary>
///     The local cache of the pinned external sources: fetches a pinned archive, verifies every file against its pin and
///     hands out only verified bytes.
/// </summary>
/// <remarks>
///     <para>
///     Layout: <c>&lt;cache&gt;/&lt;source id&gt;/&lt;commit&gt;/&lt;pinned path&gt;</c>. A fetch extracts into a sibling
///     <c>&lt;commit&gt;.partial</c> folder, verifies every pinned file and only then renames it, so a failed fetch
///     leaves the cache as it was. Only pinned paths are ever written or read; every other archive entry (a symbolic
///     link, a directory, an unpinned file, a path that escapes the folder) is ignored. A file is read at most its
///     pinned size plus one byte. The network is reached only through the fetcher delegate, and only on request.
///     Not thread-safe.
///     </para>
/// </remarks>
internal sealed class ExternalSourceCache
{
    /// <summary>The suffix of the folder a fetch extracts into before publishing.</summary>
    private const string PartialSuffix = ".partial";

    /// <summary>The cache folder.</summary>
    private readonly string _root;

    /// <summary>Opens an archive; the stream is a gzip-compressed tar.</summary>
    private readonly Func<Uri, CancellationToken, Stream> _fetcher;

    /// <summary>
    ///     Initializes a cache.
    /// </summary>
    /// <param name="root">The cache folder.</param>
    /// <param name="fetcher">Opens an archive, or null for the default HTTPS download.</param>
    public ExternalSourceCache(string root, Func<Uri, CancellationToken, Stream>? fetcher = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = Path.GetFullPath(root);
        _fetcher = fetcher ?? HttpArchiveFetcher.Open;
    }

    /// <summary>
    ///     Makes sure a source is cached and verified.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="fetch">Whether a missing or failing cache may be replaced by a download.</param>
    /// <exception cref="InvalidDataException">The cache is missing or fails verification and <paramref name="fetch"/> is false, or the download fails verification.</exception>
    /// <exception cref="IOException">The download failed.</exception>
    public void EnsureCached(ExternalSourcePin source, bool fetch)
    {
        ArgumentNullException.ThrowIfNull(source);
        var problem = Check(source);
        if (problem is null)
        {
            return;
        }

        if (!fetch)
        {
            throw new InvalidDataException(Directory.Exists(RootOf(source))
                ? $"the cache for '{source.Id}' fails verification: {problem}; run with --fetch to replace it"
                : $"the cache for '{source.Id}' is missing; run with --fetch");
        }

        Download(source);
    }

    /// <summary>
    ///     Reads a pinned file, verifying its size and SHA-256 first.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="relativePath">The pinned relative path.</param>
    /// <returns>The verified bytes.</returns>
    /// <exception cref="InvalidDataException">The path is not pinned, or the file is missing or differs from its pin.</exception>
    public byte[] ReadAllBytes(ExternalSourcePin source, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(relativePath);
        var pinned = source.Files.FirstOrDefault(file => string.Equals(file.Path, relativePath, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"'{relativePath}' is not a pinned file of '{source.Id}'");
        var problem = CheckFile(RootOf(source), source, pinned);
        if (problem is not null)
        {
            throw new InvalidDataException($"the cache for '{source.Id}' fails verification: {problem}");
        }

        return File.ReadAllBytes(Path.Combine(RootOf(source), pinned.Path.Replace('/', Path.DirectorySeparatorChar)));
    }

    /// <summary>
    ///     Gets the folder of a source's cached revision.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The folder.</returns>
    private string RootOf(ExternalSourcePin source) => Path.Combine(_root, source.Id, source.Commit);

    /// <summary>
    ///     Checks every pinned file of a cached source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The first problem found, or null when the cache is complete and exact.</returns>
    private string? Check(ExternalSourcePin source)
    {
        var root = RootOf(source);
        if (!Directory.Exists(root))
        {
            return "the folder does not exist";
        }

        return source.Files.Select(file => CheckFile(root, source, file)).FirstOrDefault(static problem => problem is not null);
    }

    /// <summary>
    ///     Checks one pinned file of a folder.
    /// </summary>
    /// <param name="root">The folder.</param>
    /// <param name="source">The source.</param>
    /// <param name="file">The pinned file.</param>
    /// <returns>The problem, or null when the file is present and exact.</returns>
    private static string? CheckFile(string root, ExternalSourcePin source, PinnedFile file)
    {
        var path = Path.Combine(root, file.Path.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return $"{source.Id}/{file.Path} is missing";
        }

        if (new FileInfo(path).Length != file.Bytes)
        {
            return $"{source.Id}/{file.Path} has a different size than its pin";
        }

        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return string.Equals(actual, file.Sha256, StringComparison.Ordinal)
            ? null
            : $"{source.Id}/{file.Path} has SHA-256 {actual} but the pin is {file.Sha256}";
    }

    /// <summary>
    ///     Downloads and extracts a source's archive, verifies it and publishes it.
    /// </summary>
    /// <param name="source">The source.</param>
    private void Download(ExternalSourcePin source)
    {
        var final = RootOf(source);
        var partial = final + PartialSuffix;
        if (Directory.Exists(partial))
        {
            Directory.Delete(partial, true);
        }

        var published = false;
        try
        {
            Directory.CreateDirectory(partial);
            Extract(source, partial);
            var missing = source.Files.Select(file => CheckFile(partial, source, file)).FirstOrDefault(static problem => problem is not null);
            if (missing is not null)
            {
                throw new InvalidDataException($"the download for '{source.Id}' fails verification: {missing}");
            }

            if (Directory.Exists(final))
            {
                Directory.Delete(final, true);
            }

            Directory.Move(partial, final);
            published = true;
        }
        finally
        {
            if (!published && Directory.Exists(partial))
            {
                Directory.Delete(partial, true);
            }
        }
    }

    /// <summary>
    ///     Extracts the pinned files of an archive into a folder.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="folder">The folder.</param>
    private void Extract(ExternalSourcePin source, string folder)
    {
        var pinned = source.Files.ToDictionary(static file => file.Path, StringComparer.Ordinal);
        try
        {
            using var archive = _fetcher(source.Archive, CancellationToken.None);
            using var gzip = new GZipStream(archive, CompressionMode.Decompress);
            using var tar = new TarReader(gzip);
            while (tar.GetNextEntry() is { } entry)
            {
                // The archive has one top-level folder; strip it. Only regular files with a pinned path are written.
                var slash = entry.Name.IndexOf('/', StringComparison.Ordinal);
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || slash < 0 || entry.DataStream is null)
                {
                    continue;
                }

                if (!pinned.TryGetValue(entry.Name[(slash + 1)..], out var file))
                {
                    continue;
                }

                WritePinned(folder, file, entry.DataStream);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new IOException($"could not download the archive of '{source.Id}': {exception.Message}", exception);
        }
    }

    /// <summary>
    ///     Writes one pinned file, reading at most its pinned size plus one byte.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <param name="file">The pinned file.</param>
    /// <param name="data">The entry's data.</param>
    private static void WritePinned(string folder, PinnedFile file, Stream data)
    {
        var path = Path.Combine(folder, file.Path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var buffer = new byte[file.Bytes + 1];
        var count = 0;
        int read;
        while (count < buffer.Length && (read = data.Read(buffer, count, buffer.Length - count)) > 0)
        {
            count += read;
        }

        if (count > file.Bytes)
        {
            throw new InvalidDataException($"the archive entry for {file.Path} is larger than its pin");
        }

        File.WriteAllBytes(path, buffer.AsSpan(0, count).ToArray());
    }
}

/// <summary>
///     Opens a pinned archive over HTTPS.
/// </summary>
internal static class HttpArchiveFetcher
{
    /// <summary>The shared client; a download that takes longer than five minutes fails.</summary>
    private static readonly HttpClient Client = CreateClient();

    /// <summary>
    ///     Opens an archive.
    /// </summary>
    /// <param name="uri">The HTTPS URL.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>The response stream; the caller disposes it.</returns>
    /// <exception cref="InvalidDataException">The URL is not HTTPS.</exception>
    /// <exception cref="HttpRequestException">The request fails.</exception>
    public static Stream Open(Uri uri, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException("archives are fetched over HTTPS only");
        }

        return Client.GetStreamAsync(uri, cancellation).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Creates the client.
    /// </summary>
    /// <returns>The client.</returns>
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DemaConsulting.Speech.Text.En.Trainer");
        return client;
    }
}
