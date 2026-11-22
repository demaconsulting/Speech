using System.IO.Compression;
using DemaConsulting.Speech.ModelBundler.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.ModelBundler.Tests;

/// <summary>
///     Unit tests for <see cref="BundleBuilder"/>: a built bundle installs through the runtime, reproduces byte for
///     byte from any source, refuses altered sources and oversized archives, and writes the release-level files.
/// </summary>
public sealed class BundleBuilderTests
{
    /// <summary>
    ///     The SHA-256 of the fixture bundle zip. It pins the whole output (metadata texts, entry order and zip layout),
    ///     and CI runs it on Windows, Linux and macOS, proving a bundle is identical wherever it is built.
    /// </summary>
    private const string GoldenSha256 = "a74410b82f5be3a97bb3a6c1f02cf4c107fa78a28d4343b17844c8dfac043ee1";

    /// <summary>Validates that a built bundle is accepted and installed by <see cref="SpeechModelBundle.InstallAsync"/>.</summary>
    [Fact]
    public async Task BundleBuilder_BuildAsync_FixtureBundle_InstallsThroughSpeechModelBundle()
    {
        // Arrange: build the fixture from a source store
        using var directory = new TemporaryDirectory();
        TestBundles.WriteStore(directory.Combine("store"), TestBundles.ModelId);
        using var handler = new FakeHttpMessageHandler();
        var builder = TestBundles.CreateBuilder(directory.Combine("store"), directory.Combine("cache"), handler, TextWriter.Null);
        var built = await builder.BuildAsync(TestBundles.CreateDefinition(), directory.Combine("out"), TestContext.Current.CancellationToken);

        // Act: stage the zip and install it as the runtime would, pinned by the built SHA-256
        var staging = directory.Combine("staging");
        Directory.CreateDirectory(staging);
        var bundle = new SpeechModelBundle(
            new Uri(SpeechModelBundle.ModelsReleaseBaseUri, built.Definition.FileName),
            built.Sha256,
            TestBundles.CreateContents());
        File.Copy(built.Path, Path.Combine(staging, bundle.FileName));
        await SpeechModelBundle.InstallAsync([bundle], staging, TestContext.Current.CancellationToken);

        // Assert: the vendor files are installed unmodified and the metadata is kept under licenses/{name}
        Assert.Equal(TestBundles.WeightsBytes, (await File.ReadAllBytesAsync(Path.Combine(staging, "onnx", "weights.bin"), TestContext.Current.CancellationToken)));
        Assert.Equal(TestBundles.ConfigBytes, (await File.ReadAllBytesAsync(Path.Combine(staging, "config.json"), TestContext.Current.CancellationToken)));
        Assert.Equal(
            ["LICENSE", "NOTICE", "README.txt", "model.json"],
            Directory.GetFiles(Path.Combine(staging, "licenses", TestBundles.Name)).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.False(File.Exists(Path.Combine(staging, bundle.FileName)));
    }

    /// <summary>Validates that building from a source store and from a download produces identical zips.</summary>
    [Fact]
    public async Task BundleBuilder_BuildAsync_DifferentSources_ProduceIdenticalZips()
    {
        // Arrange: one builder reads the store, the other downloads into an empty cache
        using var directory = new TemporaryDirectory();
        TestBundles.WriteStore(directory.Combine("store"), TestBundles.ModelId);
        using var handler = new FakeHttpMessageHandler();
        handler.Serve(TestBundles.WeightsUri, TestBundles.WeightsBytes);
        handler.Serve(TestBundles.ConfigUri, TestBundles.ConfigBytes);
        var fromStore = TestBundles.CreateBuilder(directory.Combine("store"), directory.Combine("cache-a"), handler, TextWriter.Null);
        var fromDownload = TestBundles.CreateBuilder(null, directory.Combine("cache-b"), handler, TextWriter.Null);

        // Act: build the fixture twice
        var first = await fromStore.BuildAsync(TestBundles.CreateDefinition(), directory.Combine("a"), TestContext.Current.CancellationToken);
        var second = await fromDownload.BuildAsync(TestBundles.CreateDefinition(), directory.Combine("b"), TestContext.Current.CancellationToken);

        // Assert: the zips are byte-identical
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal((await File.ReadAllBytesAsync(first.Path, TestContext.Current.CancellationToken)), (await File.ReadAllBytesAsync(second.Path, TestContext.Current.CancellationToken)));
    }

    /// <summary>Validates that the fixture bundle has the pinned SHA-256 (cross-platform byte identity).</summary>
    [Fact]
    public async Task BundleBuilder_BuildAsync_FixtureBundle_MatchesGoldenSha256()
    {
        // Arrange: a builder over the fixture store
        using var directory = new TemporaryDirectory();
        TestBundles.WriteStore(directory.Combine("store"), TestBundles.ModelId);
        using var handler = new FakeHttpMessageHandler();
        var builder = TestBundles.CreateBuilder(directory.Combine("store"), directory.Combine("cache"), handler, TextWriter.Null);

        // Act: build the fixture
        var built = await builder.BuildAsync(TestBundles.CreateDefinition(), directory.Combine("out"), TestContext.Current.CancellationToken);

        // Assert: the digest is the golden digest, and the reported values describe the file
        Assert.Equal(GoldenSha256, built.Sha256);
        Assert.Equal(new FileInfo(built.Path).Length, built.Size);
        Assert.Equal(directory.Combine("out", TestBundles.Name + ".zip"), built.Path);
    }

    /// <summary>Validates the fixed entry order: metadata first, then vendor files in ordinal path order.</summary>
    [Fact]
    public async Task BundleBuilder_BuildAsync_FixtureBundle_WritesEntriesInFixedOrder()
    {
        // Arrange: build the fixture
        using var directory = new TemporaryDirectory();
        TestBundles.WriteStore(directory.Combine("store"), TestBundles.ModelId);
        using var handler = new FakeHttpMessageHandler();
        var builder = TestBundles.CreateBuilder(directory.Combine("store"), directory.Combine("cache"), handler, TextWriter.Null);
        var built = await builder.BuildAsync(TestBundles.CreateDefinition(), directory.Combine("out"), TestContext.Current.CancellationToken);

        // Act: list the entries with the BCL reader
        using var zip = await ZipFile.OpenReadAsync(built.Path, TestContext.Current.CancellationToken);

        // Assert: the order is fixed and every entry is stored
        Assert.Equal(
            ["README.txt", "LICENSE", "NOTICE", "model.json", "config.json", "onnx/weights.bin"],
            zip.Entries.Select(entry => entry.FullName));
        Assert.All(zip.Entries, entry => Assert.Equal(entry.Length, entry.CompressedLength));
    }

    /// <summary>Validates that a local copy that differs from its pin fails the build and leaves no zip.</summary>
    [Fact]
    public async Task BundleBuilder_BuildAsync_LocalCopyDiffersFromPin_ThrowsAndWritesNoZip()
    {
        // Arrange: a tampered store copy
        using var directory = new TemporaryDirectory();
        TestBundles.WriteStore(directory.Combine("store"), TestBundles.ModelId, weights: [0, 1, 2]);
        using var handler = new FakeHttpMessageHandler();
        var builder = TestBundles.CreateBuilder(directory.Combine("store"), directory.Combine("cache"), handler, TextWriter.Null);

        // Act / Assert: the build fails and writes nothing
        await Assert.ThrowsAsync<InvalidDataException>(
            () => builder.BuildAsync(TestBundles.CreateDefinition(), directory.Combine("out"), TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(directory.Combine("out")) && Directory.EnumerateFiles(directory.Combine("out")).Any());
    }

    /// <summary>Validates that a download that differs from its pin fails the build.</summary>
    [Fact]
    public async Task BundleBuilder_BuildAsync_DownloadDiffersFromPin_ThrowsInvalidDataException()
    {
        // Arrange: no local copies and a server returning altered weights
        using var directory = new TemporaryDirectory();
        using var handler = new FakeHttpMessageHandler();
        handler.Serve(TestBundles.WeightsUri, [7, 7, 7]);
        handler.Serve(TestBundles.ConfigUri, TestBundles.ConfigBytes);
        var builder = TestBundles.CreateBuilder(null, directory.Combine("cache"), handler, TextWriter.Null);

        // Act / Assert: the build fails
        await Assert.ThrowsAsync<InvalidDataException>(
            () => builder.BuildAsync(TestBundles.CreateDefinition(), directory.Combine("out"), TestContext.Current.CancellationToken));
    }

    /// <summary>Validates that an archive that would not stay below the size limit is refused before writing.</summary>
    [Fact]
    public async Task BundleBuilder_BuildAsync_ArchiveNotBelowLimit_ThrowsInvalidDataException()
    {
        // Arrange: a builder whose limit is smaller than the fixture
        using var directory = new TemporaryDirectory();
        TestBundles.WriteStore(directory.Combine("store"), TestBundles.ModelId);
        using var handler = new FakeHttpMessageHandler();
        var builder = new BundleBuilder(
            TestBundles.CreateCatalog(),
            new Dictionary<string, string>(),
            new VendorFileSource(directory.Combine("store"), directory.Combine("cache"), new HttpClient(handler, false)),
            TextWriter.Null,
            archiveSizeLimit: 4096);

        // Act / Assert: the build is refused and no zip is written
        await Assert.ThrowsAsync<InvalidDataException>(
            () => builder.BuildAsync(TestBundles.CreateDefinition(), directory.Combine("out"), TestContext.Current.CancellationToken));
        Assert.False(File.Exists(directory.Combine("out", TestBundles.Name + ".zip")));
    }

    /// <summary>Validates that a bundle no package declares contents for is refused.</summary>
    [Fact]
    public async Task BundleBuilder_BuildAsync_BundleWithoutContents_ThrowsInvalidDataException()
    {
        // Arrange: a definition whose name has no compiled contents
        using var directory = new TemporaryDirectory();
        using var handler = new FakeHttpMessageHandler();
        var builder = TestBundles.CreateBuilder(null, directory.Combine("cache"), handler, TextWriter.Null);
        var definition = TestBundles.CreateDefinition() with { Name = "other-model-r1-int8-b1", Model = "other-model" };

        // Act / Assert: the build is refused
        await Assert.ThrowsAsync<InvalidDataException>(
            () => builder.BuildAsync(definition, directory.Combine("out"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that a set build writes <c>SHA256SUMS</c> and <c>release-notes.md</c> and reports
    ///     <c>UPDATE CATALOG</c> when no pin is published.
    /// </summary>
    [Fact]
    public async Task BundleBuilder_BuildSetAsync_NoPublishedPin_WritesChecksumsNotesAndUpdateCatalog()
    {
        // Arrange: a builder with no published pins
        using var directory = new TemporaryDirectory();
        TestBundles.WriteStore(directory.Combine("store"), TestBundles.ModelId);
        using var handler = new FakeHttpMessageHandler();
        using var log = new StringWriter();
        var builder = TestBundles.CreateBuilder(directory.Combine("store"), directory.Combine("cache"), handler, log);

        // Act: build the set
        var built = await builder.BuildSetAsync([TestBundles.CreateDefinition()], directory.Combine("out"), false, TestContext.Current.CancellationToken);

        // Assert: the checksum line, the notes and the console summary
        var bundle = Assert.Single(built);
        Assert.Equal(
            $"{bundle.Sha256}  {TestBundles.Name}.zip\n",
            (await File.ReadAllTextAsync(directory.Combine("out", ReleaseNotesWriter.ChecksumsFileName), TestContext.Current.CancellationToken)));
        var notes = (await File.ReadAllTextAsync(directory.Combine("out", ReleaseNotesWriter.ReleaseNotesFileName), TestContext.Current.CancellationToken));
        Assert.Contains($"## {TestBundles.Name}.zip", notes, StringComparison.Ordinal);
        Assert.Contains(bundle.Sha256, notes, StringComparison.Ordinal);
        Assert.EndsWith("Report problems with these bundles at <https://github.com/demaconsulting/Speech/issues>.\n", notes, StringComparison.Ordinal);
        Assert.Contains($"{bundle.Sha256}  {bundle.Size}  {TestBundles.Name}.zip  UPDATE CATALOG", log.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Validates that a set build reports <c>MATCH</c> when the published pin equals the zip.</summary>
    [Fact]
    public async Task BundleBuilder_BuildSetAsync_PublishedPinEqual_ReportsMatch()
    {
        // Arrange: learn the fixture digest, then publish it as the package pin
        using var directory = new TemporaryDirectory();
        TestBundles.WriteStore(directory.Combine("store"), TestBundles.ModelId);
        using var handler = new FakeHttpMessageHandler();
        var first = await TestBundles.CreateBuilder(directory.Combine("store"), directory.Combine("cache"), handler, TextWriter.Null)
            .BuildAsync(TestBundles.CreateDefinition(), directory.Combine("probe"), TestContext.Current.CancellationToken);
        using var log = new StringWriter();
        var builder = TestBundles.CreateBuilder(
            directory.Combine("store"),
            directory.Combine("cache"),
            handler,
            log,
            new Dictionary<string, string> { [TestBundles.Name] = first.Sha256.ToUpperInvariant() });

        // Act: build the set
        await builder.BuildSetAsync([TestBundles.CreateDefinition()], directory.Combine("out"), false, TestContext.Current.CancellationToken);

        // Assert: the console reports a match
        Assert.Contains($"{TestBundles.Name}.zip  MATCH", log.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Validates that an unavailable bundle is skipped and reported when skipping is requested.</summary>
    [Fact]
    public async Task BundleBuilder_BuildSetAsync_UnavailableWithSkip_SkipsBundle()
    {
        // Arrange: no store, no cache and a server that has nothing
        using var directory = new TemporaryDirectory();
        using var handler = new FakeHttpMessageHandler();
        using var log = new StringWriter();
        var builder = TestBundles.CreateBuilder(null, directory.Combine("cache"), handler, log);

        // Act: build with skipping
        var built = await builder.BuildSetAsync([TestBundles.CreateDefinition()], directory.Combine("out"), true, TestContext.Current.CancellationToken);

        // Assert: nothing is built, the skip is reported and no checksum file is written
        Assert.Empty(built);
        Assert.Contains($"SKIPPED {TestBundles.Name}", log.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(directory.Combine("out", ReleaseNotesWriter.ChecksumsFileName)));
    }

    /// <summary>Validates that an unavailable bundle fails the build when skipping is not requested.</summary>
    [Fact]
    public async Task BundleBuilder_BuildSetAsync_UnavailableWithoutSkip_ThrowsVendorFileUnavailableException()
    {
        // Arrange: no source has the files
        using var directory = new TemporaryDirectory();
        using var handler = new FakeHttpMessageHandler();
        var builder = TestBundles.CreateBuilder(null, directory.Combine("cache"), handler, TextWriter.Null);

        // Act / Assert: the build fails
        await Assert.ThrowsAsync<VendorFileUnavailableException>(
            () => builder.BuildSetAsync([TestBundles.CreateDefinition()], directory.Combine("out"), false, TestContext.Current.CancellationToken));
    }

    /// <summary>Validates that verifying an unchanged build reports it as reproduced.</summary>
    [Fact]
    public async Task BundleBuilder_VerifyAsync_UnchangedBuild_ReturnsTrue()
    {
        // Arrange: a build directory
        using var directory = new TemporaryDirectory();
        TestBundles.WriteStore(directory.Combine("store"), TestBundles.ModelId);
        using var handler = new FakeHttpMessageHandler();
        using var log = new StringWriter();
        var builder = TestBundles.CreateBuilder(directory.Combine("store"), directory.Combine("cache"), handler, log);
        await builder.BuildSetAsync([TestBundles.CreateDefinition()], directory.Combine("out"), false, TestContext.Current.CancellationToken);

        // Act: verify it
        var identical = await builder.VerifyAsync([TestBundles.CreateDefinition()], directory.Combine("out"), TestContext.Current.CancellationToken);

        // Assert: the zip and the notes reproduce
        Assert.True(identical);
        Assert.Contains($"IDENTICAL  ", log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("DIFFERENT", log.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Validates that verifying a build whose zip was altered reports a difference.</summary>
    [Fact]
    public async Task BundleBuilder_VerifyAsync_AlteredZip_ReturnsFalse()
    {
        // Arrange: a build directory whose zip has one byte changed in the README text
        using var directory = new TemporaryDirectory();
        TestBundles.WriteStore(directory.Combine("store"), TestBundles.ModelId);
        using var handler = new FakeHttpMessageHandler();
        using var log = new StringWriter();
        var builder = TestBundles.CreateBuilder(directory.Combine("store"), directory.Combine("cache"), handler, log);
        var built = await builder.BuildSetAsync([TestBundles.CreateDefinition()], directory.Combine("out"), false, TestContext.Current.CancellationToken);
        var bytes = (await File.ReadAllBytesAsync(built[0].Path, TestContext.Current.CancellationToken));
        bytes[40] ^= 0x20;
        await File.WriteAllBytesAsync(built[0].Path, bytes, TestContext.Current.CancellationToken);

        // Act: verify it
        var identical = await builder.VerifyAsync([TestBundles.CreateDefinition()], directory.Combine("out"), TestContext.Current.CancellationToken);

        // Assert: the difference is reported
        Assert.False(identical);
        Assert.Contains($"DIFFERENT  ", log.ToString(), StringComparison.Ordinal);
    }
}
