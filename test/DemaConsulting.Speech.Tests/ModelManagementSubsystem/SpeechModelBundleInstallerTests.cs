using System.Text;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the internal <c>SpeechModelBundleInstaller</c>, using small synthetic bundle zips built in memory
///     and staged in a scratch directory.
/// </summary>
public sealed class SpeechModelBundleInstallerTests : IDisposable
{
    /// <summary>A scratch directory unique to this test instance.</summary>
    private readonly string _root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));

    /// <summary>Gets the staging directory used by every test.</summary>
    private string Staging => Path.Join(_root, "staging");

    /// <summary>Deletes the scratch directory.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>
    ///     Stages a zip built from entries and runs the installer, with the bundle pinning
    ///     <paramref name="pinnedFiles"/> (default: the standard vendor files).
    /// </summary>
    private async Task InstallAsync(
        List<(string Name, byte[] Content)> entries,
        IReadOnlyList<(string Path, byte[] Content)>? pinnedFiles = null)
    {
        var zip = ModelBundleTestZips.BuildZip(entries);
        var bundle = ModelBundleTestZips.BundleFor(zip, pinnedFiles);
        ModelBundleTestZips.Stage(Staging, zip);
        await SpeechModelBundleInstaller.InstallAsync(bundle, Staging, TestContext.Current.CancellationToken);
    }

    /// <summary>Runs the installer and returns the <see cref="InvalidDataException"/> it must throw.</summary>
    private async Task<InvalidDataException> InstallExpectingInvalidDataAsync(
        List<(string Name, byte[] Content)> entries,
        IReadOnlyList<(string Path, byte[] Content)>? pinnedFiles = null) =>
        await Assert.ThrowsAsync<InvalidDataException>(() => InstallAsync(entries, pinnedFiles));

    /// <summary>
    ///     Proves that a valid bundle installs every vendor file at its install path with its exact bytes, copies the
    ///     metadata files to <c>licenses/{name}/</c>, and deletes the zip.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundleInstaller_InstallAsync_ValidBundle_ExtractsVendorFilesAndLicenses()
    {
        // Act
        await InstallAsync(ModelBundleTestZips.BundleEntries());

        // Assert: exactly the vendor files and the per-bundle metadata, and no zip
        Assert.Equal(
            [
                "licenses/test-model-v1.0-int8-b1/LICENSE",
                "licenses/test-model-v1.0-int8-b1/NOTICE",
                "licenses/test-model-v1.0-int8-b1/README.txt",
                "licenses/test-model-v1.0-int8-b1/model.json",
                "onnx/model.onnx",
                "voices/af.bin",
            ],
            ModelBundleTestZips.ListFiles(Staging));
        Assert.Equal(
            ModelBundleTestZips.VendorFiles[0].Content,
            await File.ReadAllBytesAsync(Path.Join(Staging, "onnx", "model.onnx"), TestContext.Current.CancellationToken));
        Assert.Equal(
            "MIT License text",
            await File.ReadAllTextAsync(Path.Join(Staging, "licenses", ModelBundleTestZips.Name, "LICENSE"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that the NOTICE entry is optional: a bundle without it installs, and no NOTICE is written.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundleInstaller_InstallAsync_WithoutNotice_Installs()
    {
        // Act
        await InstallAsync(ModelBundleTestZips.BundleEntries(includeNotice: false));

        // Assert
        var files = ModelBundleTestZips.ListFiles(Staging);
        Assert.Contains("onnx/model.onnx", files);
        Assert.Contains("licenses/test-model-v1.0-int8-b1/LICENSE", files);
        Assert.DoesNotContain("licenses/test-model-v1.0-int8-b1/NOTICE", files);
    }

    /// <summary>
    ///     Proves that directory entries are accepted and ignored.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundleInstaller_InstallAsync_DirectoryEntries_AreIgnored()
    {
        // Arrange
        var entries = ModelBundleTestZips.BundleEntries();
        entries.Insert(0, ("onnx/", []));

        // Act
        await InstallAsync(entries);

        // Assert
        Assert.True(File.Exists(Path.Join(Staging, "onnx", "model.onnx")));
    }

    /// <summary>
    ///     Proves that an entry name that could escape the staging directory fails the install before anything is
    ///     written, even though the entry is not one the bundle declares.
    /// </summary>
    /// <param name="entryName">The unsafe entry name.</param>
    [Theory]
    [InlineData("../x")]
    [InlineData("onnx/../../x")]
    [InlineData("/abs")]
    [InlineData("a\\b")]
    [InlineData("C:x")]
    [InlineData("onnx/./x")]
    [InlineData("nul.txt")]
    public async Task SpeechModelBundleInstaller_InstallAsync_EntryEscapingDirectory_ThrowsInvalidDataException(string entryName)
    {
        // Arrange: put the unsafe entry last, so a writer that did not validate first would already have written files
        var entries = ModelBundleTestZips.BundleEntries();
        entries.Add((entryName, "evil"u8.ToArray()));

        // Act
        await InstallExpectingInvalidDataAsync(entries);

        // Assert: nothing at all was written besides the staged zip, inside or outside the staging directory
        Assert.Equal([ModelBundleTestZips.FileName], ModelBundleTestZips.ListFiles(Staging));
        Assert.False(File.Exists(Path.Join(_root, "x")));
    }

    /// <summary>
    ///     Proves that two entries differing only by letter case are rejected as duplicates.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundleInstaller_InstallAsync_DuplicateEntryDifferingInCase_ThrowsInvalidDataException()
    {
        // Arrange
        var entries = ModelBundleTestZips.BundleEntries();
        entries.Add(("ONNX/model.onnx", "other"u8.ToArray()));

        // Act
        var exception = await InstallExpectingInvalidDataAsync(entries);

        // Assert
        Assert.Contains("more than once", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a vendor file whose bytes differ from the SHA-256 its <c>model.json</c> declares is rejected,
    ///     even when the size matches.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundleInstaller_InstallAsync_FileHashDiffersFromModelJson_ThrowsInvalidDataException()
    {
        // Arrange: same-length tampered bytes behind a model.json (and pins) describing the original file
        var entries = ModelBundleTestZips.BundleEntries();
        var index = entries.FindIndex(entry => entry.Name == "onnx/model.onnx");
        var tampered = (byte[])entries[index].Content.Clone();
        tampered[0] ^= 0xFF;
        entries[index] = ("onnx/model.onnx", tampered);

        // Act
        var exception = await InstallExpectingInvalidDataAsync(entries);

        // Assert
        Assert.Contains("onnx/model.onnx", exception.Message, StringComparison.Ordinal);
        Assert.Contains("model.json", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a bundle whose <c>model.json</c> truthfully describes a vendor file that differs from the compiled
    ///     pin is rejected, so a self-consistent but unofficial bundle can never install.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundleInstaller_InstallAsync_FileHashDiffersFromPinnedContents_ThrowsInvalidDataException()
    {
        // Arrange: the zip and its model.json carry a different, self-consistent model graph
        var replaced = new List<(string Path, byte[] Content)>
        {
            ("onnx/model.onnx", "unofficial-graph"u8.ToArray()),
            ModelBundleTestZips.VendorFiles[1],
        };
        var entries = ModelBundleTestZips.BundleEntries(replaced);

        // Act: the bundle still pins the original files
        var exception = await InstallExpectingInvalidDataAsync(entries, ModelBundleTestZips.VendorFiles);

        // Assert
        Assert.Contains("onnx/model.onnx", exception.Message, StringComparison.Ordinal);
        Assert.Contains("pinned", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that an entry the bundle's <c>model.json</c> does not declare is rejected.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundleInstaller_InstallAsync_EntryNotInModelJson_ThrowsInvalidDataException()
    {
        // Arrange
        var entries = ModelBundleTestZips.BundleEntries();
        entries.Add(("extra/payload.bin", "unexpected"u8.ToArray()));

        // Act
        var exception = await InstallExpectingInvalidDataAsync(entries);

        // Assert
        Assert.Contains("unexpected entry 'extra/payload.bin'", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a declared vendor file missing from the zip is rejected.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundleInstaller_InstallAsync_DeclaredEntryMissing_ThrowsInvalidDataException()
    {
        // Arrange
        var entries = ModelBundleTestZips.BundleEntries();
        entries.RemoveAll(entry => entry.Name == "voices/af.bin");

        // Act
        var exception = await InstallExpectingInvalidDataAsync(entries);

        // Assert
        Assert.Contains("missing the entry 'voices/af.bin'", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a compiled-pinned vendor file the bundle does not carry at all is rejected.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundleInstaller_InstallAsync_PinnedFileMissing_ThrowsInvalidDataException()
    {
        // Arrange: the bundle pins a third file that neither the zip nor its model.json has
        var pinned = ModelBundleTestZips.VendorFiles.Append(("voices/am.bin", "another-voice"u8.ToArray())).ToList();

        // Act
        var exception = await InstallExpectingInvalidDataAsync(ModelBundleTestZips.BundleEntries(), pinned);

        // Assert
        Assert.Contains("voices/am.bin", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a vendor file declared in <c>model.json</c> without a compiled pin is rejected, so every installed
    ///     file is pinned.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundleInstaller_InstallAsync_DeclaredFileWithoutPin_ThrowsInvalidDataException()
    {
        // Act: the bundle pins only the first vendor file
        var exception = await InstallExpectingInvalidDataAsync(
            ModelBundleTestZips.BundleEntries(), [ModelBundleTestZips.VendorFiles[0]]);

        // Assert
        Assert.Contains("voices/af.bin", exception.Message, StringComparison.Ordinal);
        Assert.Contains("no compiled pin", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that an entry larger than the size its <c>model.json</c> declares is rejected (the zip-bomb cap) and
    ///     that one smaller than declared is rejected too.
    /// </summary>
    /// <param name="sizeDelta">The difference between the declared and the actual size.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task SpeechModelBundleInstaller_InstallAsync_EntrySizeDiffersFromDeclared_ThrowsInvalidDataException(int sizeDelta)
    {
        // Arrange: model.json declares the true SHA-256 but a wrong size for the model graph
        var graph = ModelBundleTestZips.VendorFiles[0];
        var voice = ModelBundleTestZips.VendorFiles[1];
        var entries = ModelBundleTestZips.BundleEntries();
        var index = entries.FindIndex(entry => entry.Name == "model.json");
        entries[index] = ("model.json", ModelBundleTestZips.ManifestJson(
            ModelBundleTestZips.Name,
            [
                (graph.Path, graph.Content.Length + sizeDelta, ModelBundleTestZips.Sha256(graph.Content)),
                (voice.Path, voice.Content.Length, ModelBundleTestZips.Sha256(voice.Content)),
            ]));

        // Act
        var exception = await InstallExpectingInvalidDataAsync(entries);

        // Assert
        Assert.Contains("onnx/model.onnx", exception.Message, StringComparison.Ordinal);
        Assert.Contains("declared", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a bundle without <c>model.json</c>, or without a required LICENSE or README.txt, is rejected.
    /// </summary>
    /// <param name="removedEntry">The metadata entry removed from an otherwise valid bundle.</param>
    [Theory]
    [InlineData("model.json")]
    [InlineData("LICENSE")]
    [InlineData("README.txt")]
    public async Task SpeechModelBundleInstaller_InstallAsync_MissingMetadataEntry_ThrowsInvalidDataException(string removedEntry)
    {
        // Arrange
        var entries = ModelBundleTestZips.BundleEntries();
        entries.RemoveAll(entry => entry.Name == removedEntry);

        // Act
        var exception = await InstallExpectingInvalidDataAsync(entries);

        // Assert
        Assert.Contains(removedEntry, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a <c>model.json</c> that is not valid JSON, declares another bundle, an unsupported schema
    ///     version, no files, a reserved path or a malformed checksum is rejected.
    /// </summary>
    /// <param name="manifest">The replacement <c>model.json</c> text.</param>
    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("{\"schemaVersion\":1,\"bundle\":\"other-model-v1.0-int8-b1\",\"files\":[{\"path\":\"onnx/model.onnx\",\"size\":17,\"sha256\":\"00\"}]}")]
    [InlineData("{\"schemaVersion\":2,\"bundle\":\"test-model-v1.0-int8-b1\",\"files\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"bundle\":\"test-model-v1.0-int8-b1\",\"files\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"bundle\":\"test-model-v1.0-int8-b1\",\"files\":[{\"path\":\"licenses/x\",\"size\":1,\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"bundle\":\"test-model-v1.0-int8-b1\",\"files\":[{\"path\":\"onnx/model.onnx\",\"size\":17,\"sha256\":\"xyz\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"bundle\":\"test-model-v1.0-int8-b1\",\"files\":[{\"path\":\"onnx/model.onnx\",\"size\":-1,\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\"}]}")]
    public async Task SpeechModelBundleInstaller_InstallAsync_MalformedModelJson_ThrowsInvalidDataException(string manifest)
    {
        // Arrange
        var entries = ModelBundleTestZips.BundleEntries();
        var index = entries.FindIndex(entry => entry.Name == "model.json");
        entries[index] = ("model.json", Encoding.UTF8.GetBytes(manifest));

        // Act
        var exception = await InstallExpectingInvalidDataAsync(entries);

        // Assert
        Assert.Contains("model.json", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a metadata entry larger than the 1 MiB cap is rejected.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundleInstaller_InstallAsync_OversizedMetadataEntry_ThrowsInvalidDataException()
    {
        // Arrange: a model.json padded with whitespace past the cap
        var entries = ModelBundleTestZips.BundleEntries();
        var index = entries.FindIndex(entry => entry.Name == "model.json");
        var padded = entries[index].Content.Concat(Enumerable.Repeat((byte)' ', (int)SpeechModelBundleInstaller.MaximumMetadataBytes)).ToArray();
        entries[index] = ("model.json", padded);

        // Act
        var exception = await InstallExpectingInvalidDataAsync(entries);

        // Assert
        Assert.Contains("exceeds", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1024L * 1024, SpeechModelBundleInstaller.MaximumMetadataBytes);
    }

    /// <summary>
    ///     Proves that a staged file that is not a zip archive is reported as invalid data, not an unrelated exception.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundleInstaller_InstallAsync_NotAZip_ThrowsInvalidDataException()
    {
        // Arrange
        var notAZip = "this is not a zip archive"u8.ToArray();
        var bundle = ModelBundleTestZips.BundleFor(notAZip);
        ModelBundleTestZips.Stage(Staging, notAZip);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            SpeechModelBundleInstaller.InstallAsync(bundle, Staging, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that a canceled token aborts the install before anything is written and keeps the zip.
    /// </summary>
    [Fact]
    public async Task SpeechModelBundleInstaller_InstallAsync_CanceledToken_ThrowsAndKeepsZip()
    {
        // Arrange
        var zip = ModelBundleTestZips.BuildZip(ModelBundleTestZips.BundleEntries());
        var bundle = ModelBundleTestZips.BundleFor(zip);
        ModelBundleTestZips.Stage(Staging, zip);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SpeechModelBundleInstaller.InstallAsync(bundle, Staging, cancellation.Token));

        // Assert
        Assert.Equal([ModelBundleTestZips.FileName], ModelBundleTestZips.ListFiles(Staging));
    }
}
