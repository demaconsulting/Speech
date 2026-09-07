using System.Security.Cryptography;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="SpeechModelCatalog"/> class, using injected fake models and
///     a fake <see cref="IModelDownloadClient"/> so every scenario runs deterministically with no
///     real network access.
/// </summary>
public sealed class SpeechModelCatalogTests : IDisposable
{
    /// <summary>A scratch root directory unique to this test instance, cleaned up on dispose.</summary>
    private readonly string _testRoot;

    /// <summary>
    ///     Initializes a fresh, unique scratch directory for each test.
    /// </summary>
    public SpeechModelCatalogTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    /// <summary>
    ///     Deletes the scratch directory tree created for this test instance.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup only; a leftover temp directory does not fail the test.
        }
    }

    /// <summary>
    ///     Proves that a catalog built with an empty known-model list enumerates to an empty list
    ///     without throwing, honestly reflecting that this pass ships zero real model classes.
    /// </summary>
    [Fact]
    public void SpeechModelCatalog_Enumerate_EmptyKnownModels_ReturnsEmptyList()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog([], NewStore(), null);

        // Act
        var descriptors = catalog.Enumerate();

        // Assert
        Assert.Empty(descriptors);
    }

    /// <summary>
    ///     Proves that the compiled-in <see cref="SpeechModelCatalog.KnownModels"/> registry
    ///     contains all four of this library's real, production models - the two Phase 7a
    ///     recognition models plus the Phase 7b VITS synthesis model plus the Phase 10 Kokoro
    ///     multi-speaker synthesis model - resolving the "ships zero real model classes"
    ///     limitation every earlier pass through this type documented as accepted, for both model
    ///     roles the library defines.
    /// </summary>
    [Fact]
    public void SpeechModelCatalog_KnownModels_ContainsAllFourRealModels()
    {
        // Act
        var ids = SpeechModelCatalog.KnownModels.Select(m => m.Id).ToList();

        // Assert
        Assert.Equal(4, SpeechModelCatalog.KnownModels.Count);
        Assert.Contains(SherpaOnnxZipformerEnRecognitionModel.ModelId, ids);
        Assert.Contains(SherpaOnnxNemotronStreamingEnRecognitionModel.ModelId, ids);
        Assert.Contains(SherpaOnnxVitsLibriTtsEnglishSynthesisModel.ModelId, ids);
        Assert.Contains(SherpaOnnxKokoroEnglishSynthesisModel.ModelId, ids);
        Assert.Equal(
            2,
            SpeechModelCatalog.KnownModels.Count(m => m.Role == SpeechModelRole.Recognition));
        Assert.Equal(
            2,
            SpeechModelCatalog.KnownModels.Count(m => m.Role == SpeechModelRole.Synthesis));
    }

    /// <summary>
    ///     Proves that <see cref="SpeechModelCatalog.Store"/> always returns the exact
    ///     <see cref="SpeechModelStore"/> instance the catalog was composed with, so a host can
    ///     compose a recognizer/synthesizer through the same catalog instance without
    ///     constructing a second, potentially divergent store.
    /// </summary>
    [Fact]
    public void SpeechModelCatalog_Store_Always_ReturnsInternalStoreInstance()
    {
        // Arrange
        var store = NewStore();
        using var catalog = new SpeechModelCatalog([], store, null);

        // Act & Assert
        Assert.Same(store, catalog.Store);
    }

    /// <summary>
    ///     Proves that a known model with no installed content and no download attempt reports
    ///     <see cref="SpeechModelState.NotDownloaded"/>.
    /// </summary>
    [Fact]
    public void SpeechModelCatalog_Enumerate_NeverDownloaded_ReportsNotDownloaded()
    {
        // Arrange
        var model = new FakeRecognitionModel("model-not-downloaded");
        using var catalog = new SpeechModelCatalog([model], NewStore(), new NeverCompletingModelDownloadClient());

        // Act
        var descriptors = catalog.Enumerate();

        // Assert
        var descriptor = Assert.Single(descriptors);
        Assert.Equal(SpeechModelState.NotDownloaded, descriptor.State);
        Assert.Equal(model.Id, descriptor.Id);
        Assert.Equal(model.Role, descriptor.Role);
    }

    /// <summary>
    ///     Proves that a model with a <see cref="SpeechModelCatalog.DownloadAsync"/> call
    ///     currently in flight reports <see cref="SpeechModelState.Downloading"/>.
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalog_GetState_DownloadInFlight_ReportsDownloading()
    {
        // Arrange
        var model = new FakeRecognitionModel("model-in-flight");
        var client = new GatedModelDownloadClient();
        using var catalog = new SpeechModelCatalog([model], NewStore(), client);
        var downloadTask = catalog.DownloadAsync("model-in-flight", cancellationToken: TestContext.Current.CancellationToken);

        try
        {
            // Act: wait until the fake client has genuinely been entered before checking state
            await client.EntryStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
            var state = catalog.GetState("model-in-flight");

            // Assert
            Assert.Equal(SpeechModelState.Downloading, state);
        }
        finally
        {
            client.Release.TrySetResult();
            await downloadTask;
        }
    }

    /// <summary>
    ///     Proves that a model successfully downloaded reports <see cref="SpeechModelState.Downloaded"/>
    ///     once <see cref="SpeechModelCatalog.DownloadAsync"/> completes.
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalog_DownloadAsync_ValidModel_ReportsDownloadedAfterCompletion()
    {
        // Arrange
        var model = new FakeRecognitionModel("model-downloaded");
        var client = new FixedPayloadModelDownloadClient(FakeModelDescriptors.Payload);
        using var catalog = new SpeechModelCatalog([model], NewStore(), client);

        // Act
        var result = await catalog.DownloadAsync("model-downloaded", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.Equal(SpeechModelState.Downloaded, catalog.GetState("model-downloaded"));
        var descriptor = Assert.Single(catalog.Enumerate());
        Assert.Equal(SpeechModelState.Downloaded, descriptor.State);
    }

    /// <summary>
    ///     Proves that a model whose download fails checksum verification reports
    ///     <see cref="SpeechModelState.FailedOrCorrupt"/>, never falsely claiming success.
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalog_DownloadAsync_ChecksumMismatch_ReportsFailedOrCorrupt()
    {
        // Arrange: declare a download descriptor whose checksum does not match the fake payload
        var wrongChecksum = Convert.ToHexStringLower(SHA256.HashData("not-the-real-payload"u8.ToArray()));
        var descriptor = new SpeechModelDownloadDescriptor(
        [
            new SpeechModelDownloadFile(new Uri("https://example.test/model-mismatch.bin"), wrongChecksum, "model.bin"),
        ]);
        var model = new FakeRecognitionModel("model-mismatch", descriptor);
        var client = new FixedPayloadModelDownloadClient(FakeModelDescriptors.Payload);
        using var catalog = new SpeechModelCatalog([model], NewStore(), client);

        // Act
        var result = await catalog.DownloadAsync("model-mismatch", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.ChecksumMismatch, result.Outcome);
        Assert.Equal(SpeechModelState.FailedOrCorrupt, catalog.GetState("model-mismatch"));
    }

    /// <summary>
    ///     Proves that requesting a download for a model id absent from the known-model list
    ///     throws, since this is an explicit, user-invoked action naming an unknown model.
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalog_DownloadAsync_UnknownModelId_ThrowsArgumentException()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog([], NewStore(), null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => catalog.DownloadAsync("does-not-exist", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that the catalog's real <see cref="SpeechModelCatalog.DownloadAsync"/> call
    ///     path (not just <see cref="SpeechModelDownloader"/> in isolation) genuinely invokes a
    ///     zip-archive model's own <see cref="ISpeechModel.InstallAsync"/> hook, leaving the
    ///     extracted entry (not the archive) installed and reporting
    ///     <see cref="SpeechModelState.Downloaded"/>.
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalog_DownloadAsync_ZipArchiveModel_InstallsExtractedContentAndReportsDownloaded()
    {
        // Arrange
        var model = new FakeRecognitionModel("model-zip-catalog", useZipArchivePayload: true);
        var client = new FixedPayloadModelDownloadClient(FakeModelDescriptors.ZipArchiveBytes);
        var store = NewStore();
        using var catalog = new SpeechModelCatalog([model], store, client);

        // Act
        var result = await catalog.DownloadAsync("model-zip-catalog", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.Equal(SpeechModelState.Downloaded, catalog.GetState("model-zip-catalog"));
        var currentDirectory = store.GetCurrentDirectory("model-zip-catalog");
        var extractedPath = Path.Combine(currentDirectory, FakeModelDescriptors.ZipArchiveEntryName);
        var archivePath = Path.Combine(currentDirectory, FakeModelDescriptors.ZipArchiveRelativeInstallPath);
        Assert.True(File.Exists(extractedPath));
        var extractedBytes = await File.ReadAllBytesAsync(extractedPath, TestContext.Current.CancellationToken);
        Assert.Equal(FakeModelDescriptors.ZipArchiveEntryContent, extractedBytes);
        Assert.False(File.Exists(archivePath));
    }

    /// <summary>
    ///     Constructs a store rooted at this test's scratch directory.
    /// </summary>
    private SpeechModelStore NewStore() => new(new SpeechModelStoreOptions { RootPathOverride = _testRoot });

    /// <summary>
    ///     Fake <see cref="IModelDownloadClient"/> that writes a fixed in-memory payload
    ///     immediately, used for deterministic success/checksum-mismatch scenarios.
    /// </summary>
    private sealed class FixedPayloadModelDownloadClient(byte[] payload) : IModelDownloadClient
    {
        /// <inheritdoc/>
        public async Task DownloadAsync(
            Uri sourceUri,
            Stream destination,
            IProgress<SpeechModelDownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            await destination.WriteAsync(payload, cancellationToken);
            progress?.Report(new SpeechModelDownloadProgress(0, 1, payload.Length, payload.Length));
        }
    }

    /// <summary>
    ///     Fake <see cref="IModelDownloadClient"/> that never completes on its own, used to prove
    ///     a model with no download attempt reports <see cref="SpeechModelState.NotDownloaded"/>.
    ///     Never actually awaited to completion by any test that uses it.
    /// </summary>
    private sealed class NeverCompletingModelDownloadClient : IModelDownloadClient
    {
        /// <inheritdoc/>
        public Task DownloadAsync(
            Uri sourceUri,
            Stream destination,
            IProgress<SpeechModelDownloadProgress>? progress,
            CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    /// <summary>
    ///     Fake <see cref="IModelDownloadClient"/> that signals <see cref="EntryStarted"/> the
    ///     moment it is invoked, then blocks until the test completes <see cref="Release"/>,
    ///     letting a test deterministically observe <see cref="SpeechModelState.Downloading"/>
    ///     while a download is genuinely in flight.
    /// </summary>
    private sealed class GatedModelDownloadClient : IModelDownloadClient
    {
        /// <summary>Completed by <see cref="DownloadAsync"/> the moment it is entered.</summary>
        public TaskCompletionSource EntryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completed by the test to let the gated download proceed.</summary>
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc/>
        public async Task DownloadAsync(
            Uri sourceUri,
            Stream destination,
            IProgress<SpeechModelDownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            EntryStarted.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            await destination.WriteAsync(FakeModelDescriptors.Payload, cancellationToken);
            progress?.Report(new SpeechModelDownloadProgress(0, 1, FakeModelDescriptors.Payload.Length, FakeModelDescriptors.Payload.Length));
        }
    }
}
