using DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ExternalEvaluationSubsystem;

/// <summary>
///     Unit tests for <see cref="ExternalSourcePins"/> and <see cref="ExternalSourceCache"/>: loading and checking the
///     pins, and fetching, verifying and reading pinned files, with fake archives and no network.
/// </summary>
public sealed class ExternalSourceCacheTests
{
    /// <summary>Proves the committed pins load, which includes the check that both pin files agree, and pin the two sources with only the files the evaluation reads.</summary>
    [Fact]
    public void ExternalSourcePins_Load_CommittedConfig_IsConsistent()
    {
        // Arrange: find the committed pins above the test binaries
        var root = ExternalSourceFixture.FindRepositoryRoot();
        Assert.SkipUnless(root is not null, "The repository root (Speech.slnx) was not found above the test binaries.");
        var path = Path.Combine(root, "tools", "DemaConsulting.Speech.Text.En.Trainer", "external-sources.json");

        // Act
        var pins = ExternalSourcePins.Load(path);

        // Assert: the WHD files are the license plus both splits, the benchmark files are two licenses and the English read set
        Assert.Equal(["whd", "bench"], pins.Sources.Select(source => source.Id));
        Assert.All(pins.Find("whd").Files, static file => Assert.Matches(@"^(?:LICENSE|data/(?:train|eval)/[A-Za-z0-9._-]+\.tsv)$", file.Path));
        Assert.Equal(["LICENSE", "LICENSE-DATA", "data/readset_en_v2.json"], pins.Find("bench").Files.Select(file => file.Path));
        Assert.All(pins.Sources, source =>
        {
            Assert.Matches("^[0-9a-f]{40}$", source.Commit);
            Assert.Equal(Uri.UriSchemeHttps, source.Archive.Scheme);
            Assert.Contains(source.Commit, source.Archive.AbsoluteUri, StringComparison.Ordinal);
        });
    }

    /// <summary>Proves a pin file edited without the other is refused, so the two cannot drift apart.</summary>
    [Fact]
    public void ExternalSourcePins_Load_EditedFilesTsv_FailsConsistencyCheck()
    {
        // Arrange: change the SHA-256 of the first whd row in the per-file pins only
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);
        var edited = CopyPins(external, "edited", static json => json, static tsv => EditRow(tsv, "whd", cells => [cells[0], cells[1], new string('0', 64), cells[3]]));

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => ExternalSourcePins.Load(edited));

        // Assert
        Assert.Contains("'whd'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("external-files.tsv", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves malformed pins are refused with an error that names the problem.</summary>
    [Theory]
    [InlineData("short-commit", "40 lowercase")]
    [InlineData("parent-path", "safe relative path")]
    [InlineData("absolute-path", "safe relative path")]
    [InlineData("backslash-path", "safe relative path")]
    [InlineData("http-archive", "HTTPS")]
    [InlineData("unsorted-rows", "unique and sorted")]
    [InlineData("bad-schema", "schema")]
    [InlineData("bad-header", "header")]
    public void ExternalSourcePins_Load_MalformedPins_Throws(string defect, string expected)
    {
        // Arrange: the bench rows come first in the per-file pins and the whd rows second
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);
        var path = defect switch
        {
            "short-commit" => CopyPins(external, defect, static json => json.Replace(ExternalContent.BenchCommit, ExternalContent.BenchCommit[..39], StringComparison.Ordinal), static tsv => tsv),
            "parent-path" => CopyPins(external, defect, static json => json, static tsv => EditRow(tsv, "bench", cells => [cells[0], "../" + cells[1], cells[2], cells[3]])),
            "absolute-path" => CopyPins(external, defect, static json => json, static tsv => EditRow(tsv, "bench", cells => [cells[0], "/" + cells[1], cells[2], cells[3]])),
            "backslash-path" => CopyPins(external, defect, static json => json, static tsv => EditRow(tsv, "bench", cells => [cells[0], "data\\" + cells[1], cells[2], cells[3]])),
            "http-archive" => CopyPins(external, defect, static json => json.Replace("https://archive", "http://archive", StringComparison.Ordinal), static tsv => tsv),
            "unsorted-rows" => CopyPins(external, defect, static json => json, static tsv => SwapFirstRows(tsv)),
            "bad-schema" => CopyPins(external, defect, static json => json.Replace("\"schema\": 1", "\"schema\": 2", StringComparison.Ordinal), static tsv => tsv),
            _ => CopyPins(external, defect, static json => json, static tsv => tsv.Replace("source\tpath", "src\tpath", StringComparison.Ordinal)),
        };

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => ExternalSourcePins.Load(path));

        // Assert
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves per-file pins that are not valid UTF-8 are refused with an error that names the per-file pins, not an unwrapped decoder exception.</summary>
    [Fact]
    public void ExternalSourcePins_Load_InvalidUtf8InFilesTsv_ThrowsNamingFilesTsv()
    {
        // Arrange: valid per-file pins followed by an invalid UTF-8 byte sequence
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);
        var path = CopyPins(external, "bad-utf8-tsv", static json => json, static tsv => tsv);
        var tsvPath = Path.Combine(Path.GetDirectoryName(path)!, "external-files.tsv");
        File.WriteAllBytes(tsvPath, [.. File.ReadAllBytes(tsvPath), 0xC3, 0x28]);

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => ExternalSourcePins.Load(path));

        // Assert
        Assert.StartsWith("external-files.tsv", exception.Message, StringComparison.Ordinal);
        Assert.Contains("UTF-8", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a JSON pins document that is not valid UTF-8 is refused with an error that names the JSON document.</summary>
    [Fact]
    public void ExternalSourcePins_Load_InvalidUtf8InJson_ThrowsNamingJson()
    {
        // Arrange: a license string that holds a lone continuation byte
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);
        var path = CopyPins(external, "bad-utf8-json", static json => json.Replace("Fake", "F\u0001ke", StringComparison.Ordinal), static tsv => tsv);
        var bytes = File.ReadAllBytes(path);
        var index = Array.IndexOf(bytes, (byte)0x01);
        Assert.True(index >= 0, "the fixture document has a license string to damage");
        bytes[index] = 0xFF;
        File.WriteAllBytes(path, bytes);

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => ExternalSourcePins.Load(path));

        // Assert
        Assert.StartsWith("external-sources.json", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a byte count that is negative, signed, not a number, beyond a 64-bit integer or beyond the largest accepted file is refused at parse time.</summary>
    [Theory]
    [InlineData("-1", "malformed")]
    [InlineData("-9223372036854775808", "malformed")]
    [InlineData("+5", "malformed")]
    [InlineData("12abc", "malformed")]
    [InlineData("", "malformed")]
    [InlineData("99999999999999999999", "malformed")]
    [InlineData("16777217", "larger than")]
    [InlineData("9223372036854775807", "larger than")]
    public void ExternalSourcePins_Load_InvalidByteCount_Throws(string bytes, string expected)
    {
        // Arrange: the byte count of the first bench row is replaced
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);
        var path = CopyPins(external, "bad-bytes", static json => json, tsv => EditRow(tsv, "bench", cells => [cells[0], cells[1], cells[2], bytes]));

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => ExternalSourcePins.Load(path));

        // Assert: the error names the per-file pins and the row, before any consistency check against the JSON document
        Assert.Contains("external-files.tsv:", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a missing pins file is refused.</summary>
    [Fact]
    public void ExternalSourcePins_Load_MissingFile_Throws()
    {
        // Arrange
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ExternalSourcePins.Load(external.NewFolder("nothing.json")));
    }

    /// <summary>Proves a valid archive is extracted so that exactly the pinned files are written: an entry no pin lists and one that tries to leave the folder are not.</summary>
    [Fact]
    public void ExternalSourceCache_EnsureCached_ValidArchive_ExtractsOnlyPinnedFiles()
    {
        // Arrange: the archive also holds an unpinned file and entries that climb out of the cache
        var content = ExternalContent.Standard();
        content.WhdArchive.Extras.Add(("extra/unpinned.txt", [1, 2, 3]));
        content.WhdArchive.Extras.Add(("../escape.txt", [4, 5, 6]));
        content.WhdArchive.Extras.Add(("../../escape-two.txt", [7, 8, 9]));
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training, content);
        var cache = external.NewFolder("cache");
        var whd = external.Pins.Find("whd");

        // Act
        external.OpenCache(cache).EnsureCached(whd, fetch: true);

        // Assert: the cache holds the pinned files with the pinned bytes and nothing else, and nothing was written anywhere else
        var expected = whd.Files.Select(file => $"whd/{whd.Commit}/{file.Path}").Order(StringComparer.Ordinal).ToList();
        Assert.Equal(expected, ExternalSourceFixture.FilesBelow(cache));
        Assert.All(whd.Files, file => Assert.Equal(external.PinnedBytes("whd", file.Path), File.ReadAllBytes(Path.Combine(cache, "whd", whd.Commit, file.Path))));
        Assert.DoesNotContain(ExternalSourceFixture.FilesBelow(external.Folder), path => path.Contains("escape", StringComparison.Ordinal) || path.Contains("unpinned", StringComparison.Ordinal));
        Assert.Equal([whd.Archive], external.FetchedUris);
    }

    /// <summary>Proves an archive that lacks a pinned file is refused and leaves nothing in the cache.</summary>
    [Fact]
    public void ExternalSourceCache_EnsureCached_MissingPinnedFile_Throws()
    {
        // Arrange
        var content = ExternalContent.Standard();
        content.WhdArchive.Omissions.Add("data/eval/wind.tsv");
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training, content);
        var cache = external.NewFolder("cache");

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => external.OpenCache(cache).EnsureCached(external.Pins.Find("whd"), fetch: true));

        // Assert
        Assert.Contains("data/eval/wind.tsv", exception.Message, StringComparison.Ordinal);
        Assert.Empty(ExternalSourceFixture.FilesBelow(cache));
        Assert.False(Directory.Exists(Path.Combine(cache, "whd", external.Pins.Find("whd").Commit)));
    }

    /// <summary>Proves a failed download leaves the cache exactly as it was, whether it was empty or held a damaged copy.</summary>
    [Fact]
    public void ExternalSourceCache_EnsureCached_FetchFails_LeavesCacheUnchanged()
    {
        // Arrange: one cache is empty, another holds a damaged copy; the download fails
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);
        external.FetchFails = true;
        var whd = external.Pins.Find("whd");
        var empty = external.NewFolder("empty-cache");
        var damaged = external.WriteCache(external.NewFolder("damaged-cache"), "whd");
        external.Tamper(damaged, "whd", "LICENSE");
        var before = ExternalSourceFixture.FilesBelow(damaged).ToDictionary(path => path, path => File.ReadAllBytes(Path.Combine(damaged, path)));

        // Act
        var onEmpty = Assert.Throws<IOException>(() => external.OpenCache(empty).EnsureCached(whd, fetch: true));
        var onDamaged = Assert.Throws<IOException>(() => external.OpenCache(damaged).EnsureCached(whd, fetch: true));

        // Assert
        Assert.Contains("'whd'", onEmpty.Message, StringComparison.Ordinal);
        Assert.Contains("'whd'", onDamaged.Message, StringComparison.Ordinal);
        Assert.Empty(ExternalSourceFixture.FilesBelow(empty));
        var after = ExternalSourceFixture.FilesBelow(damaged).ToDictionary(path => path, path => File.ReadAllBytes(Path.Combine(damaged, path)));
        Assert.Equal(before.Keys, after.Keys);
        Assert.All(before, pair => Assert.Equal(pair.Value, after[pair.Key]));
    }

    /// <summary>Proves an archive whose file differs from its pin is refused with an error that names the file and both hashes, and nothing is published.</summary>
    [Fact]
    public void ExternalSourceCache_EnsureCached_Sha256Mismatch_ThrowsAndLeavesNothingInCache()
    {
        // Arrange: the archive holds the same number of bytes with other values
        var content = ExternalContent.Standard();
        var original = content.WhdContent()["data/train/lead.tsv"];
        content.WhdArchive.Replacements["data/train/lead.tsv"] = [.. original.Select(static b => (byte)(b ^ 1))];
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training, content);
        var cache = external.NewFolder("cache");
        var whd = external.Pins.Find("whd");

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => external.OpenCache(cache).EnsureCached(whd, fetch: true));

        // Assert
        Assert.Contains("whd/data/train/lead.tsv", exception.Message, StringComparison.Ordinal);
        Assert.Contains("SHA-256", exception.Message, StringComparison.Ordinal);
        Assert.Contains(ExternalSourceFixture.Sha256Of(original), exception.Message, StringComparison.Ordinal);
        Assert.Empty(ExternalSourceFixture.FilesBelow(cache));
        Assert.False(Directory.Exists(Path.Combine(cache, "whd", whd.Commit)));
    }

    /// <summary>Proves an archive entry larger than its pin is refused, and nothing is published.</summary>
    [Fact]
    public void ExternalSourceCache_EnsureCached_OversizedEntry_Throws()
    {
        // Arrange: the archive holds the pinned bytes and more
        var content = ExternalContent.Standard();
        content.WhdArchive.Replacements["data/eval/lead.tsv"] = [.. content.WhdContent()["data/eval/lead.tsv"], .. new byte[1000]];
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training, content);
        var cache = external.NewFolder("cache");

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => external.OpenCache(cache).EnsureCached(external.Pins.Find("whd"), fetch: true));

        // Assert
        Assert.Contains("data/eval/lead.tsv", exception.Message, StringComparison.Ordinal);
        Assert.Contains("larger", exception.Message, StringComparison.Ordinal);
        Assert.Empty(ExternalSourceFixture.FilesBelow(cache));
    }

    /// <summary>Proves a cache that fails verification is replaced by a verified download when a fetch is allowed.</summary>
    [Fact]
    public void ExternalSourceCache_EnsureCached_DamagedCacheWithFetch_IsReplacedByVerifiedDownload()
    {
        // Arrange
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);
        var cache = external.WriteCache(external.NewFolder("cache"), "whd");
        external.Tamper(cache, "whd", "LICENSE");
        var cached = external.OpenCache(cache);
        var whd = external.Pins.Find("whd");

        // Act
        cached.EnsureCached(whd, fetch: true);

        // Assert
        cached.EnsureCached(whd, fetch: false);
        Assert.Equal(1, external.FetchCount);
    }

    /// <summary>Proves a cache that verifies is used without calling the fetcher, even when a fetch is allowed.</summary>
    [Fact]
    public void ExternalSourceCache_EnsureCached_CacheAlreadyValid_DoesNotFetch()
    {
        // Arrange
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);
        var cache = external.WriteCache(external.NewFolder("cache"));
        var cached = external.OpenCache(cache);

        // Act
        cached.EnsureCached(external.Pins.Find("whd"), fetch: true);
        cached.EnsureCached(external.Pins.Find("bench"), fetch: false);

        // Assert
        Assert.Equal(0, external.FetchCount);
    }

    /// <summary>Proves a missing cache without permission to fetch is refused with a message that says to fetch, and the fetcher is never called.</summary>
    [Fact]
    public void ExternalSourceCache_EnsureCached_NoCacheAndNoFetch_ThrowsWithoutFetching()
    {
        // Arrange
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);
        var cached = external.OpenCache(external.NewFolder("cache"));

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => cached.EnsureCached(external.Pins.Find("whd"), fetch: false));

        // Assert
        Assert.Contains("--fetch", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'whd'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, external.FetchCount);
    }

    /// <summary>Proves a damaged cache without permission to fetch is refused and is not repaired.</summary>
    [Fact]
    public void ExternalSourceCache_EnsureCached_DamagedCacheAndNoFetch_ThrowsWithoutFetching()
    {
        // Arrange
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);
        var cache = external.WriteCache(external.NewFolder("cache"));
        external.Tamper(cache, "bench", "LICENSE");

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => external.OpenCache(cache).EnsureCached(external.Pins.Find("bench"), fetch: false));

        // Assert
        Assert.Contains("bench/LICENSE", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, external.FetchCount);
    }

    /// <summary>Proves a pinned file is read once it verifies, and a modified or resized file or a path that is not pinned is refused.</summary>
    [Fact]
    public void ExternalSourceCache_ReadAllBytes_ModifiedFileOrUnpinnedPath_Throws()
    {
        // Arrange
        using var training = new TrainingFixture();
        using var external = new ExternalSourceFixture(training);
        var cache = external.WriteCache(external.NewFolder("cache"));
        var cached = external.OpenCache(cache);
        var whd = external.Pins.Find("whd");
        File.WriteAllText(Path.Combine(cache, "whd", whd.Commit, "unpinned.txt"), "not pinned");
        var good = cached.ReadAllBytes(whd, "data/eval/lead.tsv");
        external.Tamper(cache, "whd", "data/eval/wind.tsv");
        File.AppendAllText(Path.Combine(cache, "whd", whd.Commit, "LICENSE"), "x");

        // Act
        var modified = Assert.Throws<InvalidDataException>(() => cached.ReadAllBytes(whd, "data/eval/wind.tsv"));
        var resized = Assert.Throws<InvalidDataException>(() => cached.ReadAllBytes(whd, "LICENSE"));
        var unpinned = Assert.Throws<InvalidDataException>(() => cached.ReadAllBytes(whd, "unpinned.txt"));
        var climbing = Assert.Throws<InvalidDataException>(() => cached.ReadAllBytes(whd, "../whd/LICENSE"));

        // Assert
        Assert.Equal(external.PinnedBytes("whd", "data/eval/lead.tsv"), good);
        Assert.Contains("whd/data/eval/wind.tsv", modified.Message, StringComparison.Ordinal);
        Assert.Contains("whd/LICENSE", resized.Message, StringComparison.Ordinal);
        Assert.Contains("not a pinned file", unpinned.Message, StringComparison.Ordinal);
        Assert.Contains("not a pinned file", climbing.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Copies the fixture's pin files to a new folder, applying an edit to each.
    /// </summary>
    /// <param name="external">The fixture.</param>
    /// <param name="name">The folder name.</param>
    /// <param name="editJson">Edits the JSON text.</param>
    /// <param name="editTsv">Edits the per-file pins text.</param>
    /// <returns>The full path of the copied JSON document.</returns>
    private static string CopyPins(ExternalSourceFixture external, string name, Func<string, string> editJson, Func<string, string> editTsv)
    {
        var folder = external.NewFolder(name);
        Directory.CreateDirectory(folder);
        var source = Path.GetDirectoryName(external.PinsPath)!;
        File.WriteAllText(Path.Combine(folder, "external-sources.json"), editJson(File.ReadAllText(Path.Combine(source, "external-sources.json"))));
        File.WriteAllText(Path.Combine(folder, "external-files.tsv"), editTsv(File.ReadAllText(Path.Combine(source, "external-files.tsv"))));
        return Path.Combine(folder, "external-sources.json");
    }

    /// <summary>
    ///     Edits the first row of a source in the per-file pins.
    /// </summary>
    /// <param name="tsv">The per-file pins text.</param>
    /// <param name="source">The source id.</param>
    /// <param name="edit">Returns the new cells of the row.</param>
    /// <returns>The edited text.</returns>
    private static string EditRow(string tsv, string source, Func<string[], string[]> edit)
    {
        var lines = tsv.Split('\n');
        var index = Array.FindIndex(lines, line => line.StartsWith(source + "\t", StringComparison.Ordinal));
        lines[index] = string.Join('\t', edit(lines[index].Split('\t')));
        return string.Join('\n', lines);
    }

    /// <summary>
    ///     Swaps the first two data rows of the per-file pins.
    /// </summary>
    /// <param name="tsv">The text.</param>
    /// <returns>The edited text.</returns>
    private static string SwapFirstRows(string tsv)
    {
        var lines = tsv.Split('\n');
        (lines[1], lines[2]) = (lines[2], lines[1]);
        return string.Join('\n', lines);
    }
}
