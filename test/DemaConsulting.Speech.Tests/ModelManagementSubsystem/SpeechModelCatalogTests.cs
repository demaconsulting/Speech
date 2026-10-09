using System.Security.Cryptography;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

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
        _testRoot = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));
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
    ///     Proves that <see cref="SpeechModelCatalog.AddModels"/> returns the same catalog
    ///     instance it was called on, supporting fluent call chaining (for example with an
    ///     extension method such as the sibling <c>DemaConsulting.Speech.Sherpa</c> package's
    ///     <c>AddSherpaModels</c>).
    /// </summary>
    [Fact]
    public void SpeechModelCatalog_AddModels_ChainedCall_ReturnsSameInstance()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog([], NewStore(), null);

        // Act
        var result = catalog.AddModels(new FakeRecognitionModel("model-added"));

        // Assert
        Assert.Same(catalog, result);
    }

    /// <summary>
    ///     Proves that a model appended via <see cref="SpeechModelCatalog.AddModels"/> appears in
    ///     a subsequent <see cref="SpeechModelCatalog.Enumerate"/> call, alongside any model(s)
    ///     the catalog was already seeded with.
    /// </summary>
    [Fact]
    public void SpeechModelCatalog_AddModels_Called_AppearsInEnumerate()
    {
        // Arrange
        var seeded = new FakeRecognitionModel("model-seeded");
        using var catalog = new SpeechModelCatalog([seeded], NewStore(), null);
        var synthesisModel = new FakeSynthesisModel("model-synthesis-added");
        var recognitionModel = new FakeRecognitionModel("model-recognition-added");

        // Act
        catalog.AddModels(synthesisModel, recognitionModel);
        var ids = catalog.Enumerate().Select(descriptor => descriptor.Id).ToList();

        // Assert
        Assert.Equal(["model-seeded", "model-synthesis-added", "model-recognition-added"], ids);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechModelCatalog.AddModels"/> rejects an array containing a
    ///     null entry, failing fast at this API boundary instead of accepting the null entry and
    ///     deferring the failure to a later <see cref="SpeechModelCatalog.Enumerate"/> or
    ///     <see cref="SpeechModelCatalog.DownloadAsync"/> call.
    /// </summary>
    [Fact]
    public void SpeechModelCatalog_AddModels_NullEntry_ThrowsArgumentException()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog([], NewStore(), null);
        var models = new ISpeechModel?[] { new FakeRecognitionModel("model-valid"), null };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => catalog.AddModels(models!));
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
    ///     Proves that the catalog's internal, test-only constructor forwards a configured
    ///     <see cref="DownloadMirror"/> through to an injected download client, via the
    ///     catalog's real <see cref="SpeechModelCatalog.DownloadAsync"/> orchestration - not just
    ///     <see cref="SpeechModelDownloader"/> constructed and exercised directly, which this
    ///     overload is not involved in at all. See
    ///     <see cref="SpeechModelCatalog_DownloadAsync_MirrorConfigured_ForwardsEffectiveUriThroughPublicConstructor"/>
    ///     for the equivalent proof against the catalog's public 3-parameter constructor, which
    ///     this four-argument call does not exercise (it binds to the internal
    ///     <c>(knownModels, store, client, downloaderOptions, diagnostics)</c> overload).
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalog_DownloadAsync_MirrorConfigured_ForwardsEffectiveUriThroughCatalog()
    {
        // Arrange
        var payload = FakeModelDescriptors.Payload;
        var model = new FakeRecognitionModel("model-mirror-catalog");
        var client = new FixedPayloadModelDownloadClient(payload);
        var store = NewStore();
        var mirror = new DownloadMirror(new Uri("https://mirror.internal/models"));
        var downloaderOptions = new SpeechModelDownloaderOptions { Mirror = mirror };
        using var catalog = new SpeechModelCatalog([model], store, client, downloaderOptions);

        // Act
        var result = await catalog.DownloadAsync(
            "model-mirror-catalog", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        var call = Assert.Single(client.Calls);
        Assert.StartsWith("https://mirror.internal/models/model-mirror-catalog/", call.ToString());
    }

    /// <summary>
    ///     Proves that the catalog's public 3-parameter constructor (<see cref="SpeechModelCatalog(SpeechModelStoreOptions,SpeechModelDownloaderOptions,DemaConsulting.Speech.Diagnostics.ISpeechDiagnostics)"/>)
    ///     - the only catalog composition path a real host can use - genuinely forwards a
    ///     configured <see cref="DownloadMirror"/> to the internally created
    ///     <see cref="HttpModelDownloadClient"/>, by downloading from a real in-process
    ///     <see cref="WireMockServer"/> stubbed at the mirror-rewritten path and asserting the
    ///     file actually downloads and installs. A regression in this constructor's forwarding
    ///     (unlike the sibling test above, which only proves the internal test-only constructor)
    ///     would otherwise go undetected.
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalog_DownloadAsync_MirrorConfigured_ForwardsEffectiveUriThroughPublicConstructor()
    {
        // Arrange: a stubbed mirror server expecting the mirror-rewritten request path
        // "{mirrorBase}/{modelId}/model.bin", exactly as SpeechModelDownloader.ResolveEffectiveUri
        // builds it - never the model's own declared "https://example.test/..." placeholder URI.
        const string modelId = "model-mirror-public-ctor";
        var payload = FakeModelDescriptors.Payload;
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath($"/{modelId}/model.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(payload));

        var model = new FakeRecognitionModel(modelId);
        var storeOptions = new SpeechModelStoreOptions { RootPathOverride = _testRoot };
        var mirror = new DownloadMirror(new Uri(server.Urls[0]));
        var downloaderOptions = new SpeechModelDownloaderOptions { Mirror = mirror };
        using var catalog = new SpeechModelCatalog(storeOptions, downloaderOptions, diagnostics: null);
        catalog.AddModels(model);

        // Act
        var result = await catalog.DownloadAsync(modelId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.Equal(SpeechModelState.Downloaded, catalog.GetState(modelId));
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
        var extractedPath = Path.Join(currentDirectory, FakeModelDescriptors.ZipArchiveEntryName);
        var archivePath = Path.Join(currentDirectory, FakeModelDescriptors.ZipArchiveRelativeInstallPath);
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
    ///     immediately, used for deterministic success/checksum-mismatch scenarios, recording
    ///     every requested <see cref="Uri"/> for tests that assert on the effective request URI.
    /// </summary>
    private sealed class FixedPayloadModelDownloadClient(byte[] payload) : IModelDownloadClient
    {
        /// <summary>Every requested source <see cref="Uri"/> this client was asked to download, in call order.</summary>
        public List<Uri> Calls { get; } = [];

        /// <inheritdoc/>
        public async Task DownloadAsync(
            Uri sourceUri,
            Stream destination,
            IProgress<SpeechModelDownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            Calls.Add(sourceUri);
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
