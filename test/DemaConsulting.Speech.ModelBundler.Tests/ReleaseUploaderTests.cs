using DemaConsulting.Speech.ModelBundler.Tests.Fakes;

namespace DemaConsulting.Speech.ModelBundler.Tests;

/// <summary>
///     Unit tests for <see cref="ReleaseUploader"/> with a fake <c>gh</c>: equal assets are skipped, a name published
///     with another digest refuses the whole upload, a local file must match <c>SHA256SUMS</c>, and no command ever
///     passes <c>--clobber</c>.
/// </summary>
public sealed class ReleaseUploaderTests
{
    /// <summary>The name of the first fixture zip.</summary>
    private const string FirstZip = "alpha-r1-int8-b1.zip";

    /// <summary>The name of the second fixture zip.</summary>
    private const string SecondZip = "beta-r1-int8-b1.zip";

    /// <summary>The bytes of the first fixture zip.</summary>
    private static readonly byte[] FirstBytes = [1, 2, 3];

    /// <summary>The bytes of the second fixture zip.</summary>
    private static readonly byte[] SecondBytes = [4, 5, 6];

    /// <summary>Validates that zips missing from the release are uploaded with gh, without --clobber.</summary>
    [Fact]
    public async Task ReleaseUploader_UploadAsync_NewAssets_UploadsEachWithoutClobber()
    {
        // Arrange: an empty release
        using var directory = WriteBuild();
        var runner = new FakeProcessRunner();
        using var log = new StringWriter();

        // Act: upload
        var ok = await new ReleaseUploader(runner, log).UploadAsync(directory.Path, "owner/repo", false, TestContext.Current.CancellationToken);

        // Assert: one view, then one upload per zip to the models release, none with --clobber
        Assert.True(ok);
        Assert.Equal(["release", "view", "models", "--repo", "owner/repo", "--json", "assets"], runner.Calls[0]);
        var uploads = runner.Uploads.ToList();
        Assert.Equal(2, uploads.Count);
        Assert.Equal(["release", "upload", "models", directory.Combine(FirstZip), "--repo", "owner/repo"], uploads[0]);
        Assert.All(runner.Calls, call => Assert.DoesNotContain("--clobber", call));
    }

    /// <summary>Validates that an asset already published with the same digest is skipped.</summary>
    [Fact]
    public async Task ReleaseUploader_UploadAsync_AssetWithSameDigest_SkipsIt()
    {
        // Arrange: the first zip is already published with its digest
        using var directory = WriteBuild();
        var runner = new FakeProcessRunner { AssetsJson = AssetsJson((FirstZip, "sha256:" + TestBundles.Sha256(FirstBytes))) };
        using var log = new StringWriter();

        // Act: upload
        var ok = await new ReleaseUploader(runner, log).UploadAsync(directory.Path, "owner/repo", false, TestContext.Current.CancellationToken);

        // Assert: only the second zip is uploaded
        Assert.True(ok);
        var upload = Assert.Single(runner.Uploads);
        Assert.Equal(directory.Combine(SecondZip), upload[3]);
        Assert.Contains($"SKIPPED {FirstZip}", log.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Validates that a name published with a different digest refuses the whole upload.</summary>
    [Fact]
    public async Task ReleaseUploader_UploadAsync_AssetWithDifferentDigest_RefusesAndUploadsNothing()
    {
        // Arrange: the second zip's name is published with other bytes
        using var directory = WriteBuild();
        var runner = new FakeProcessRunner { AssetsJson = AssetsJson((SecondZip, "sha256:" + new string('a', 64))) };
        using var log = new StringWriter();

        // Act: upload
        var ok = await new ReleaseUploader(runner, log).UploadAsync(directory.Path, "owner/repo", false, TestContext.Current.CancellationToken);

        // Assert: refused, and not even the first zip is uploaded
        Assert.False(ok);
        Assert.Empty(runner.Uploads);
        Assert.Contains($"REFUSED {SecondZip}", log.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Validates that a published asset without a known digest is treated as different and refused.</summary>
    [Fact]
    public async Task ReleaseUploader_UploadAsync_AssetWithoutDigest_Refuses()
    {
        // Arrange: the first zip's name is published but GitHub reports no digest
        using var directory = WriteBuild();
        var runner = new FakeProcessRunner { AssetsJson = """{"assets":[{"name":"alpha-r1-int8-b1.zip","digest":null}]}""" };
        using var log = new StringWriter();

        // Act: upload
        var ok = await new ReleaseUploader(runner, log).UploadAsync(directory.Path, "owner/repo", false, TestContext.Current.CancellationToken);

        // Assert: refused, nothing uploaded
        Assert.False(ok);
        Assert.Empty(runner.Uploads);
    }

    /// <summary>Validates that a local zip that no longer has its listed SHA-256 refuses the upload before gh runs.</summary>
    [Fact]
    public async Task ReleaseUploader_UploadAsync_LocalZipChanged_RefusesWithoutRunningGh()
    {
        // Arrange: the first zip is altered after the build
        using var directory = WriteBuild();
        await File.WriteAllBytesAsync(directory.Combine(FirstZip), [9], TestContext.Current.CancellationToken);
        var runner = new FakeProcessRunner();
        using var log = new StringWriter();

        // Act: upload
        var ok = await new ReleaseUploader(runner, log).UploadAsync(directory.Path, "owner/repo", false, TestContext.Current.CancellationToken);

        // Assert: refused before any gh call
        Assert.False(ok);
        Assert.Empty(runner.Calls);
    }

    /// <summary>Validates that a dry run lists the assets but uploads nothing.</summary>
    [Fact]
    public async Task ReleaseUploader_UploadAsync_DryRun_PrintsCommandsWithoutUploading()
    {
        // Arrange: an empty release
        using var directory = WriteBuild();
        var runner = new FakeProcessRunner();
        using var log = new StringWriter();

        // Act: dry-run upload
        var ok = await new ReleaseUploader(runner, log).UploadAsync(directory.Path, "owner/repo", true, TestContext.Current.CancellationToken);

        // Assert: only the view ran; the upload commands are printed without --clobber
        Assert.True(ok);
        Assert.Single(runner.Calls);
        Assert.Contains($"DRY RUN: gh release upload models {directory.Combine(FirstZip)} --repo owner/repo", log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("--clobber", log.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Validates that a missing release fails the upload.</summary>
    [Fact]
    public async Task ReleaseUploader_UploadAsync_ReleaseMissing_Fails()
    {
        // Arrange: gh cannot view the release
        using var directory = WriteBuild();
        var runner = new FakeProcessRunner { ViewExitCode = 1 };
        using var log = new StringWriter();

        // Act: upload
        var ok = await new ReleaseUploader(runner, log).UploadAsync(directory.Path, "owner/repo", false, TestContext.Current.CancellationToken);

        // Assert: failed without uploading
        Assert.False(ok);
        Assert.Empty(runner.Uploads);
    }

    /// <summary>Writes a build directory with two zips and their SHA256SUMS.</summary>
    /// <returns>The directory (disposed by the caller).</returns>
    private static TemporaryDirectory WriteBuild()
    {
        var directory = new TemporaryDirectory();
        File.WriteAllBytes(directory.Combine(FirstZip), FirstBytes);
        File.WriteAllBytes(directory.Combine(SecondZip), SecondBytes);
        File.WriteAllText(
            directory.Combine(ReleaseNotesWriter.ChecksumsFileName),
            $"{TestBundles.Sha256(FirstBytes)}  {FirstZip}\n{TestBundles.Sha256(SecondBytes)}  {SecondZip}\n");
        return directory;
    }

    /// <summary>Creates a <c>gh release view --json assets</c> document.</summary>
    /// <param name="assets">The asset names and digests.</param>
    /// <returns>The JSON text.</returns>
    private static string AssetsJson(params (string Name, string Digest)[] assets) =>
        "{\"assets\":[" + string.Join(',', assets.Select(asset => $"{{\"name\":\"{asset.Name}\",\"digest\":\"{asset.Digest}\",\"size\":3}}")) + "]}";
}
