using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

/// <summary>
///     What goes wrong or extra in a fake source archive: entries the pins do not list, entries replaced by other
///     bytes, and entries left out.
/// </summary>
internal sealed class ArchiveKnobs
{
    /// <summary>Gets the content an archive holds instead of the pinned content, by pinned path.</summary>
    public Dictionary<string, byte[]> Replacements { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the pinned paths the archive leaves out.</summary>
    public HashSet<string> Omissions { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the entries the archive holds that no pin lists, with names relative to the top-level folder.</summary>
    public List<(string Name, byte[] Data)> Extras { get; } = [];
}

/// <summary>One hand-written item of the fake heteronym-bench document.</summary>
/// <param name="Id">The item id.</param>
/// <param name="Word">The target word.</param>
/// <param name="Answer">The fake reading key.</param>
/// <param name="Source">The <c>src</c> value.</param>
/// <param name="Text">The sentence.</param>
/// <param name="Start">The first value of <c>pos</c>.</param>
/// <param name="End">The second value of <c>pos</c>.</param>
internal sealed record BenchItem(string Id, string Word, string Answer, string Source, string Text, int Start, int End);

/// <summary>
///     The hand-written content of the two fake sources. Every sentence is about the fixture words <c>lead</c> and
///     <c>wind</c>, every label is an invented key, and nothing here is text of a real dataset.
/// </summary>
internal sealed class ExternalContent
{
    /// <summary>The fake commit of the fake WHD source.</summary>
    public const string WhdCommit = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>The fake commit of the fake benchmark source.</summary>
    public const string BenchCommit = "fedcba9876543210fedcba9876543210fedcba98";

    /// <summary>The pinned path of the fake benchmark document.</summary>
    public const string BenchPath = "data/readset_en_v2.json";

    /// <summary>The header of a fake WHD file.</summary>
    public const string WhdHeader = "\"homograph\"\t\"wordid\"\t\"sentence\"\t\"start\"\t\"end\"";

    /// <summary>The UTF-8 encoding without byte order mark.</summary>
    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>Gets the text of the fake WHD files by pinned path.</summary>
    public SortedDictionary<string, string> WhdFiles { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the items of the fake benchmark document.</summary>
    public List<BenchItem> BenchItems { get; } = [];

    /// <summary>Gets or sets a document that replaces the rendered benchmark document.</summary>
    public string? BenchDocument { get; set; }

    /// <summary>Gets or sets the exact bytes that replace the benchmark document, for content that is not valid UTF-8.</summary>
    public byte[]? BenchDocumentBytes { get; set; }

    /// <summary>Gets the rows of <c>whd-mapping.tsv</c> as label and reading id.</summary>
    public List<string[]> WhdMapping { get; } = [];

    /// <summary>Gets the rows of <c>bench-mapping.tsv</c> as label and reading id.</summary>
    public List<string[]> BenchMapping { get; } = [];

    /// <summary>Gets the archive knobs of the fake WHD source.</summary>
    public ArchiveKnobs WhdArchive { get; } = new();

    /// <summary>Gets the archive knobs of the fake benchmark source.</summary>
    public ArchiveKnobs BenchArchive { get; } = new();

    /// <summary>Gets every sentence the content holds.</summary>
    public List<string> Sentences { get; } = [];

    /// <summary>Gets every external label the content holds.</summary>
    public List<string> Labels { get; } = [];

    /// <summary>
    ///     Creates the standard content: a few fake rows of the words <c>lead</c> and <c>wind</c> in each WHD split and in
    ///     the benchmark, two benchmark items marked as copied from WHD, and the mappings of every fake label.
    /// </summary>
    /// <returns>The content.</returns>
    public static ExternalContent Standard()
    {
        var content = new ExternalContent();
        content.WhdFiles["LICENSE"] = "Fake license text for the test fixture.\n";
        content.WhdMapping.AddRange([["lead_guide", "lead.verb"], ["lead_metal", "lead.noun"], ["wind_air", "wind.noun"]]);
        content.BenchMapping.AddRange([["lead:guide", "lead.verb"], ["lead:metal", "lead.noun"], ["wind:air", "wind.noun"]]);
        content.Labels.AddRange(["lead_guide", "lead_metal", "wind_air", "lead:guide", "lead:metal", "wind:air"]);

        content.AddWhdRow("eval", "lead", "lead", "lead_guide", "Someone should lead the hikers home.");
        content.AddWhdRow("eval", "lead", "lead", "lead_metal", "That lead pipe looks very old.");
        content.AddWhdRow("eval", "lead", "lead", "lead_guide", "Mara will lead the choir tonight.");
        content.AddWhdRow("eval", "lead", "lead", "lead_metal", "A lead plate covered the hole.");
        content.AddWhdRow("eval", "wind", "wind", "wind_air", "A strong wind came from the sea.");
        content.AddWhdRow("train", "lead", "lead", "lead_guide", "They lead the march every year.");
        content.AddWhdRow("train", "lead", "lead", "lead_metal", "The lead roof was heavy.");
        content.AddWhdRow("train", "wind", "wind", "wind_air", "The wind was cold today.");

        content.AddBenchItem("fake-lead-1", "lead", "guide", "v1", "Our captain will lead the squad onward.");
        content.AddBenchItem("fake-lead-2", "lead", "metal", "v1", "The lead sinker dropped quickly.");
        content.AddBenchItem("fake-lead-3", "lead", "guide", "claude_v2", "He wants to lead the discussion.");
        content.AddBenchItem("fake-wind-1", "wind", "air", "claude_v2", "The wind rattled the shutters.");
        content.AddBenchItem("fake-copy-1", "lead", "guide", "whd", "A copied sentence where we lead the way.");
        content.AddBenchItem("fake-copy-2", "lead", "metal", "whd", "A copied sentence about a lead weight.");
        return content;
    }

    /// <summary>
    ///     Quotes a field the way the fake WHD files do: in quotes, with every quote inside doubled.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The quoted field.</returns>
    public static string Quote(string field) => "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    /// <summary>
    ///     Appends a fake WHD row whose offsets are the UTF-8 byte offsets of the first occurrence of the homograph.
    /// </summary>
    /// <param name="split">The split folder, <c>train</c> or <c>eval</c>.</param>
    /// <param name="file">The file name without extension.</param>
    /// <param name="homograph">The homograph column, which must occur in the sentence.</param>
    /// <param name="wordId">The fake word id.</param>
    /// <param name="sentence">The sentence.</param>
    public void AddWhdRow(string split, string file, string homograph, string wordId, string sentence)
    {
        var characterStart = sentence.IndexOf(homograph, StringComparison.Ordinal);
        if (characterStart < 0)
        {
            throw new InvalidOperationException("the homograph must occur in the sentence");
        }

        var byteStart = Utf8.GetByteCount(sentence[..characterStart]);
        AddWhdRowAt(split, file, homograph, wordId, sentence, byteStart, byteStart + Utf8.GetByteCount(homograph));
    }

    /// <summary>
    ///     Appends a fake WHD row with the given byte offsets, right or wrong.
    /// </summary>
    /// <param name="split">The split folder.</param>
    /// <param name="file">The file name without extension.</param>
    /// <param name="homograph">The homograph column.</param>
    /// <param name="wordId">The fake word id.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="byteStart">The first byte offset.</param>
    /// <param name="byteEnd">The second byte offset.</param>
    public void AddWhdRowAt(string split, string file, string homograph, string wordId, string sentence, int byteStart, int byteEnd)
    {
        var path = $"data/{split}/{file}.tsv";
        var text = WhdFiles.GetValueOrDefault(path, WhdHeader + "\n");
        var line = string.Join('\t', Quote(homograph), Quote(wordId), Quote(sentence), byteStart.ToString(CultureInfo.InvariantCulture), byteEnd.ToString(CultureInfo.InvariantCulture));
        WhdFiles[path] = text + line + "\n";
        Sentences.Add(sentence);
        Labels.Add(wordId);
    }

    /// <summary>
    ///     Adds a fake benchmark item whose <c>pos</c> is the character span of the first occurrence of the word.
    /// </summary>
    /// <param name="id">The item id.</param>
    /// <param name="word">The target word, which must occur in the text.</param>
    /// <param name="answer">The fake reading key.</param>
    /// <param name="source">The <c>src</c> value.</param>
    /// <param name="text">The sentence.</param>
    public void AddBenchItem(string id, string word, string answer, string source, string text)
    {
        var start = text.IndexOf(word, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException("the word must occur in the text");
        }

        AddBenchItemAt(id, word, answer, source, text, start, start + word.Length);
    }

    /// <summary>
    ///     Adds a fake benchmark item with the given <c>pos</c>, right or wrong.
    /// </summary>
    /// <param name="id">The item id.</param>
    /// <param name="word">The target word.</param>
    /// <param name="answer">The fake reading key.</param>
    /// <param name="source">The <c>src</c> value.</param>
    /// <param name="text">The sentence.</param>
    /// <param name="start">The first value of <c>pos</c>.</param>
    /// <param name="end">The second value of <c>pos</c>.</param>
    public void AddBenchItemAt(string id, string word, string answer, string source, string text, int start, int end)
    {
        BenchItems.Add(new BenchItem(id, word, answer, source, text, start, end));
        Sentences.Add(text);
        Labels.Add($"{word}:{answer}");
    }

    /// <summary>
    ///     Gets the final bytes of every pinned file of the fake WHD source.
    /// </summary>
    /// <returns>The files by pinned path.</returns>
    public SortedDictionary<string, byte[]> WhdContent() =>
        new(WhdFiles.ToDictionary(static pair => pair.Key, static pair => Utf8.GetBytes(pair.Value), StringComparer.Ordinal), StringComparer.Ordinal);

    /// <summary>
    ///     Gets the final bytes of every pinned file of the fake benchmark source.
    /// </summary>
    /// <returns>The files by pinned path.</returns>
    public SortedDictionary<string, byte[]> BenchContent() =>
        new(StringComparer.Ordinal)
        {
            ["LICENSE"] = Utf8.GetBytes("Fake code license for the test fixture.\n"),
            ["LICENSE-DATA"] = Utf8.GetBytes("Fake data license for the test fixture.\n"),
            [BenchPath] = BenchDocumentBytes ?? Utf8.GetBytes(BenchDocument ?? RenderBenchDocument()),
        };

    /// <summary>
    ///     Renders the fake benchmark document from the items.
    /// </summary>
    /// <returns>The JSON text.</returns>
    private string RenderBenchDocument()
    {
        var items = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var item in BenchItems)
        {
            items[item.Id] = new Dictionary<string, object>
            {
                ["lang"] = "en",
                ["text"] = item.Text,
                ["word"] = item.Word,
                ["pos"] = new[] { item.Start, item.End },
                ["answer"] = item.Answer,
                ["cands"] = Array.Empty<string>(),
                ["minority"] = false,
                ["hold"] = false,
                ["src"] = item.Source,
            };
        }

        return JsonSerializer.Serialize(new Dictionary<string, object> { ["lang"] = "en", ["version"] = "fake", ["words"] = new Dictionary<string, object>(), ["items"] = items });
    }
}

/// <summary>
///     Builds, in a temporary folder, fake WHD and benchmark archives (in-memory <c>.tar.gz</c> files with a top-level
///     folder), the matching pins (<c>external-sources.json</c> and <c>external-files.tsv</c> with correct sizes, SHA-256
///     values and listing hashes), the two mapping files in the data root of a <see cref="TrainingFixture"/>, and a
///     fetcher that serves the archives and counts its calls. Nothing here touches the network or a real dataset.
/// </summary>
internal sealed class ExternalSourceFixture : IDisposable
{
    /// <summary>The UTF-8 encoding without byte order mark.</summary>
    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>The archive bytes by source id.</summary>
    private readonly Dictionary<string, byte[]> _archives = new(StringComparer.Ordinal);

    /// <summary>The pinned content by source id.</summary>
    private readonly Dictionary<string, SortedDictionary<string, byte[]>> _files = new(StringComparer.Ordinal);

    /// <summary>The commit by source id.</summary>
    private readonly Dictionary<string, string> _commits = new(StringComparer.Ordinal);

    /// <summary>The archive URLs by source id.</summary>
    private readonly Dictionary<string, Uri> _uris = new(StringComparer.Ordinal);

    /// <summary>The URLs the fetcher was called with.</summary>
    private readonly List<Uri> _fetched = [];

    /// <summary>
    ///     Creates the fixture: archives, pins and mapping files.
    /// </summary>
    /// <param name="training">The training fixture whose data root receives the mapping files.</param>
    /// <param name="content">The content, or null for <see cref="ExternalContent.Standard"/>.</param>
    public ExternalSourceFixture(TrainingFixture training, ExternalContent? content = null)
    {
        ArgumentNullException.ThrowIfNull(training);
        Content = content ?? ExternalContent.Standard();
        Folder = Path.Combine(Path.GetTempPath(), "texten-external-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);

        AddSource("whd", ExternalContent.WhdCommit, Content.WhdContent(), Content.WhdArchive);
        AddSource("bench", ExternalContent.BenchCommit, Content.BenchContent(), Content.BenchArchive);

        var pins = Path.Combine(Folder, "pins");
        Directory.CreateDirectory(pins);
        PinsPath = Path.Combine(pins, "external-sources.json");
        File.WriteAllText(PinsPath, RenderPinsJson(), Utf8);
        File.WriteAllText(Path.Combine(pins, "external-files.tsv"), RenderFilesTsv(), Utf8);

        training.Write("whd-mapping.tsv", Mapping(ReadingIdMap.WhdHeader, Content.WhdMapping));
        training.Write("bench-mapping.tsv", Mapping(ReadingIdMap.BenchHeader, Content.BenchMapping));
        Pins = ExternalSourcePins.Load(PinsPath);
    }

    /// <summary>Gets the content the fixture was built from.</summary>
    public ExternalContent Content { get; }

    /// <summary>Gets the temporary folder that holds the pins, the archives' working folders and the caches the tests ask for.</summary>
    public string Folder { get; }

    /// <summary>Gets the full path of <c>external-sources.json</c>; the per-file pins are its sibling.</summary>
    public string PinsPath { get; }

    /// <summary>Gets the pins as the production code loads them.</summary>
    public ExternalSourcePins Pins { get; }

    /// <summary>Gets or sets a value indicating whether the fetcher fails with a network error.</summary>
    public bool FetchFails { get; set; }

    /// <summary>Gets the number of times the fetcher was called.</summary>
    public int FetchCount => _fetched.Count;

    /// <summary>Gets the URLs the fetcher was called with, in order.</summary>
    public IReadOnlyList<Uri> FetchedUris => _fetched;

    /// <summary>
    ///     Finds the repository root by walking up from the test binaries to the folder that holds <c>Speech.slnx</c>.
    /// </summary>
    /// <returns>The repository root, or <see langword="null"/> when none is found.</returns>
    public static string? FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Speech.slnx")))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    /// <summary>
    ///     Computes the lowercase hexadecimal SHA-256 of some bytes.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The hash.</returns>
    public static string Sha256Of(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>
    ///     Serves the fake archives and counts its calls; it fails when <see cref="FetchFails"/> is set.
    /// </summary>
    /// <param name="uri">The archive URL.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>A stream over the archive bytes.</returns>
    /// <exception cref="HttpRequestException">The fetcher is set to fail or the URL is not one of the fake archives.</exception>
    public Stream Fetch(Uri uri, CancellationToken cancellation)
    {
        _fetched.Add(uri);
        if (FetchFails)
        {
            throw new HttpRequestException("fixture: the download failed");
        }

        foreach (var (id, archive) in _uris)
        {
            if (archive == uri)
            {
                return new MemoryStream(_archives[id], writable: false);
            }
        }

        throw new HttpRequestException("fixture: there is no such archive");
    }

    /// <summary>
    ///     Gets the fetcher delegate, which serves the archives and counts its calls.
    /// </summary>
    /// <returns>The delegate.</returns>
    public Func<Uri, CancellationToken, Stream> Fetcher() => Fetch;

    /// <summary>
    ///     Gets the pinned bytes of a file of a fake source (not what a tampered archive holds).
    /// </summary>
    /// <param name="id">The source id.</param>
    /// <param name="path">The pinned path.</param>
    /// <returns>The bytes.</returns>
    public byte[] PinnedBytes(string id, string path) => _files[id][path];

    /// <summary>
    ///     Gets the path of a folder of the fixture that does not exist yet.
    /// </summary>
    /// <param name="name">The folder name.</param>
    /// <returns>The full path.</returns>
    public string NewFolder(string name) => Path.Combine(Folder, name);

    /// <summary>
    ///     Writes the exact pinned content of sources into a cache folder, as a verified earlier fetch would have left it.
    /// </summary>
    /// <param name="cache">The cache folder.</param>
    /// <param name="ids">The source ids; every source when none is given.</param>
    /// <returns>The cache folder.</returns>
    public string WriteCache(string cache, params string[] ids)
    {
        foreach (var id in ids.Length == 0 ? [.. _files.Keys] : ids)
        {
            foreach (var (path, bytes) in _files[id])
            {
                var target = Path.Combine(cache, id, _commits[id], path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, bytes);
            }
        }

        return cache;
    }

    /// <summary>
    ///     Changes one byte of a cached file without changing its size, so only its SHA-256 differs from the pin.
    /// </summary>
    /// <param name="cache">The cache folder.</param>
    /// <param name="id">The source id.</param>
    /// <param name="path">The pinned path.</param>
    public void Tamper(string cache, string id, string path)
    {
        var target = Path.Combine(cache, id, _commits[id], path.Replace('/', Path.DirectorySeparatorChar));
        var bytes = File.ReadAllBytes(target);
        bytes[0] ^= 1;
        File.WriteAllBytes(target, bytes);
    }

    /// <summary>
    ///     Opens a cache over a folder with this fixture's fetcher.
    /// </summary>
    /// <param name="cache">The cache folder.</param>
    /// <returns>The cache.</returns>
    public ExternalSourceCache OpenCache(string cache) => new(cache, Fetch);

    /// <summary>
    ///     Gets every file below a folder as paths relative to it with <c>/</c> separators, in ordinal order.
    /// </summary>
    /// <param name="folder">The folder, which may not exist.</param>
    /// <returns>The relative paths.</returns>
    public static List<string> FilesBelow(string folder) =>
        Directory.Exists(folder)
            ? [.. Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(folder, file).Replace('\\', '/')).Order(StringComparer.Ordinal)]
            : [];

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, true);
        }
    }

    /// <summary>
    ///     Renders a mapping file.
    /// </summary>
    /// <param name="header">The header line.</param>
    /// <param name="rows">The rows.</param>
    /// <returns>The file text.</returns>
    private static string Mapping(string header, IEnumerable<string[]> rows) =>
        header + "\n" + string.Concat(rows.Select(static row => string.Join('\t', row) + "\n"));

    /// <summary>
    ///     Builds the archive of a source and remembers its pinned content.
    /// </summary>
    /// <param name="id">The source id.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="files">The pinned content.</param>
    /// <param name="knobs">What differs in the archive.</param>
    private void AddSource(string id, string commit, SortedDictionary<string, byte[]> files, ArchiveKnobs knobs)
    {
        _commits[id] = commit;
        _files[id] = files;
        _uris[id] = new Uri($"https://archive.example.invalid/{id}/tar.gz/{commit}");
        _archives[id] = BuildArchive(commit, files, knobs);
    }

    /// <summary>
    ///     Builds a gzip-compressed tar archive with a top-level folder, as hosting services serve a commit.
    /// </summary>
    /// <param name="commit">The commit, which names the top-level folder.</param>
    /// <param name="files">The pinned content.</param>
    /// <param name="knobs">What differs in the archive.</param>
    /// <returns>The archive bytes.</returns>
    private static byte[] BuildArchive(string commit, SortedDictionary<string, byte[]> files, ArchiveKnobs knobs)
    {
        var top = "Repo-" + commit;
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true))
        {
            tar.WriteEntry(new PaxTarEntry(TarEntryType.Directory, top + "/"));
            foreach (var (path, bytes) in files.Where(pair => !knobs.Omissions.Contains(pair.Key)))
            {
                WriteFile(tar, $"{top}/{path}", knobs.Replacements.GetValueOrDefault(path, bytes));
            }

            foreach (var (name, data) in knobs.Extras)
            {
                WriteFile(tar, $"{top}/{name}", data);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>
    ///     Writes one regular file entry.
    /// </summary>
    /// <param name="tar">The writer.</param>
    /// <param name="name">The entry name.</param>
    /// <param name="data">The content.</param>
    private static void WriteFile(TarWriter tar, string name, byte[] data) =>
        tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(data, writable: false) });

    /// <summary>
    ///     Renders <c>external-sources.json</c> for the fake sources.
    /// </summary>
    /// <returns>The JSON text.</returns>
    private string RenderPinsJson()
    {
        var sources = new List<object>();
        foreach (var id in new[] { "whd", "bench" })
        {
            var files = _files[id];
            sources.Add(new Dictionary<string, object>
            {
                ["id"] = id,
                ["repository"] = $"https://repository.example.invalid/{id}",
                ["commit"] = _commits[id],
                ["archive"] = _uris[id].AbsoluteUri,
                ["license"] = "Fake license for the test fixture",
                ["files"] = files.Count,
                ["bytes"] = files.Values.Sum(static bytes => (long)bytes.Length),
                ["filesSha256"] = ListingSha256(files),
            });
        }

        return JsonSerializer.Serialize(new Dictionary<string, object> { ["schema"] = 1, ["sources"] = sources }, new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    /// <summary>
    ///     Renders <c>external-files.tsv</c> for the fake sources: rows sorted by source and path in ordinal order.
    /// </summary>
    /// <returns>The file text.</returns>
    private string RenderFilesTsv()
    {
        var text = new StringBuilder("source\tpath\tsha256\tbytes\n");
        foreach (var id in _files.Keys.Order(StringComparer.Ordinal))
        {
            foreach (var (path, bytes) in _files[id])
            {
                text.Append(id).Append('\t').Append(path).Append('\t').Append(Sha256Of(bytes)).Append('\t').Append(bytes.Length.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>
    ///     Computes the listing hash of a source: the SHA-256 of <c>path TAB sha256 LF</c> per file in ordinal path order.
    /// </summary>
    /// <param name="files">The files.</param>
    /// <returns>The hash.</returns>
    private static string ListingSha256(SortedDictionary<string, byte[]> files)
    {
        var listing = new StringBuilder();
        foreach (var (path, bytes) in files)
        {
            listing.Append(path).Append('\t').Append(Sha256Of(bytes)).Append('\n');
        }

        return Sha256Of(Utf8.GetBytes(listing.ToString()));
    }
}
