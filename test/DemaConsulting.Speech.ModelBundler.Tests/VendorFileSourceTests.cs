using DemaConsulting.Speech.ModelBundler.Tests.Fakes;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.ModelBundler.Tests;

/// <summary>
///     Unit tests for <see cref="VendorFileSource"/>: the source order (store, cache, download), the fallback across
///     model ids, and the pin check on every source.
/// </summary>
public sealed class VendorFileSourceTests
{
    /// <summary>The fixture's weights declaration.</summary>
    private static readonly SpeechModelDownloadFile Weights = TestBundles.CreateContents().Files[0];

    /// <summary>Validates that an installed copy in the source store is used first, without any download.</summary>
    [Fact]
    public async Task VendorFileSource_ResolveAsync_FileInSourceStore_UsesStoreCopy()
    {
        // Arrange: the file is installed, cached and downloadable
        using var directory = new TemporaryDirectory();
        TestBundles.WriteStore(directory.Combine("store"), TestBundles.ModelId);
        Directory.CreateDirectory(directory.Combine("cache"));
        await File.WriteAllBytesAsync(directory.Combine("cache", Weights.Sha256Checksum), TestBundles.WeightsBytes, TestContext.Current.CancellationToken);
        using var handler = new FakeHttpMessageHandler();
        handler.Serve(TestBundles.WeightsUri, TestBundles.WeightsBytes);
        var source = new VendorFileSource(directory.Combine("store"), directory.Combine("cache"), new HttpClient(handler, false));

        // Act: resolve the weights
        var resolved = await source.ResolveAsync(Weights, [TestBundles.ModelId], TestContext.Current.CancellationToken);

        // Assert: the store copy is used and nothing is downloaded
        Assert.Equal(VendorFileOrigin.SourceStore, resolved.Origin);
        Assert.Equal(directory.Combine("store", TestBundles.ModelId, "current", "onnx", "weights.bin"), resolved.LocalPath);
        Assert.Equal(TestBundles.WeightsBytes.Length, resolved.Size);
        Assert.Equal("onnx/weights.bin", resolved.EntryName);
        Assert.Empty(handler.Requests);
    }

    /// <summary>Validates that each model id is tried in turn when the first has no installed copy.</summary>
    [Fact]
    public async Task VendorFileSource_ResolveAsync_FileOnlyUnderSecondModelId_UsesSecondModelCopy()
    {
        // Arrange: only the alternate model is installed
        using var directory = new TemporaryDirectory();
        TestBundles.WriteStore(directory.Combine("store"), TestBundles.AlternateModelId);
        using var handler = new FakeHttpMessageHandler();
        var source = new VendorFileSource(directory.Combine("store"), directory.Combine("cache"), new HttpClient(handler, false));

        // Act: resolve with both model ids
        var resolved = await source.ResolveAsync(
            Weights, [TestBundles.ModelId, TestBundles.AlternateModelId], TestContext.Current.CancellationToken);

        // Assert: the alternate model's copy is used
        Assert.Equal(VendorFileOrigin.SourceStore, resolved.Origin);
        Assert.Contains(TestBundles.AlternateModelId, resolved.LocalPath, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    /// <summary>Validates that the SHA-keyed cache is used when the store has no copy.</summary>
    [Fact]
    public async Task VendorFileSource_ResolveAsync_FileOnlyInCache_UsesCacheCopy()
    {
        // Arrange: an empty store and a cached copy
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(directory.Combine("store"));
        Directory.CreateDirectory(directory.Combine("cache"));
        await File.WriteAllBytesAsync(directory.Combine("cache", Weights.Sha256Checksum), TestBundles.WeightsBytes, TestContext.Current.CancellationToken);
        using var handler = new FakeHttpMessageHandler();
        var source = new VendorFileSource(directory.Combine("store"), directory.Combine("cache"), new HttpClient(handler, false));

        // Act: resolve the weights
        var resolved = await source.ResolveAsync(Weights, [TestBundles.ModelId], TestContext.Current.CancellationToken);

        // Assert: the cache copy is used and nothing is downloaded
        Assert.Equal(VendorFileOrigin.Cache, resolved.Origin);
        Assert.Empty(handler.Requests);
    }

    /// <summary>Validates that a missing file is downloaded from its original URL and cached under its SHA-256.</summary>
    [Fact]
    public async Task VendorFileSource_ResolveAsync_FileNowhereLocal_DownloadsIntoCache()
    {
        // Arrange: no store, an empty cache, a server with the file
        using var directory = new TemporaryDirectory();
        using var handler = new FakeHttpMessageHandler();
        handler.Serve(TestBundles.WeightsUri, TestBundles.WeightsBytes);
        var source = new VendorFileSource(null, directory.Combine("cache"), new HttpClient(handler, false));

        // Act: resolve the weights
        var resolved = await source.ResolveAsync(Weights, [TestBundles.ModelId], TestContext.Current.CancellationToken);

        // Assert: downloaded from the original URL into the SHA-keyed cache name
        Assert.Equal(VendorFileOrigin.Download, resolved.Origin);
        Assert.Equal([TestBundles.WeightsUri.AbsoluteUri], handler.Requests);
        Assert.Equal(directory.Combine("cache", Weights.Sha256Checksum), resolved.LocalPath);
        Assert.Equal(TestBundles.WeightsBytes, (await File.ReadAllBytesAsync(resolved.LocalPath, TestContext.Current.CancellationToken)));
    }

    /// <summary>Validates that a store copy that differs from its pin fails, without falling through to a download.</summary>
    [Fact]
    public async Task VendorFileSource_ResolveAsync_StoreCopyDiffersFromPin_ThrowsInvalidDataException()
    {
        // Arrange: a tampered installed copy and a server with the genuine file
        using var directory = new TemporaryDirectory();
        TestBundles.WriteStore(directory.Combine("store"), TestBundles.ModelId, weights: [1, 2, 3]);
        using var handler = new FakeHttpMessageHandler();
        handler.Serve(TestBundles.WeightsUri, TestBundles.WeightsBytes);
        var source = new VendorFileSource(directory.Combine("store"), directory.Combine("cache"), new HttpClient(handler, false));

        // Act / Assert: the mismatch fails and nothing is downloaded
        await Assert.ThrowsAsync<InvalidDataException>(
            () => source.ResolveAsync(Weights, [TestBundles.ModelId], TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    /// <summary>Validates that a download that differs from its pin fails and never enters the cache.</summary>
    [Fact]
    public async Task VendorFileSource_ResolveAsync_DownloadDiffersFromPin_ThrowsAndCachesNothing()
    {
        // Arrange: a server returning altered bytes
        using var directory = new TemporaryDirectory();
        using var handler = new FakeHttpMessageHandler();
        handler.Serve(TestBundles.WeightsUri, [9, 9, 9]);
        var source = new VendorFileSource(null, directory.Combine("cache"), new HttpClient(handler, false));

        // Act / Assert: the mismatch fails and the cache holds no file
        await Assert.ThrowsAsync<InvalidDataException>(
            () => source.ResolveAsync(Weights, [TestBundles.ModelId], TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(directory.Combine("cache")));
    }

    /// <summary>Validates that a file no source holds and the server cannot serve is reported as unavailable.</summary>
    [Fact]
    public async Task VendorFileSource_ResolveAsync_DownloadFails_ThrowsVendorFileUnavailableException()
    {
        // Arrange: a server that answers 404
        using var directory = new TemporaryDirectory();
        using var handler = new FakeHttpMessageHandler();
        var source = new VendorFileSource(null, directory.Combine("cache"), new HttpClient(handler, false));

        // Act / Assert: the file is unavailable and no partial download remains
        await Assert.ThrowsAsync<VendorFileUnavailableException>(
            () => source.ResolveAsync(Weights, [TestBundles.ModelId], TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(directory.Combine("cache")));
    }
}
