using System.Linq;
using System.Security.Cryptography;
using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.ModelCatalogSubsystem;

/// <summary>
///     Unit tests for <see cref="ModelCatalogService"/>.
/// </summary>
public class ModelCatalogServiceTests
{
    /// <summary>
    ///     Builds catalog options rooted inside this test run's own output directory, so no test
    ///     ever reads or writes a developer's real installed-model store.
    /// </summary>
    /// <returns>Options whose store root is a fresh directory under the test output folder.</returns>
    private static SpeechModelStoreOptions IsolatedOptions() => new()
    {
        RootPathOverride = Path.Join(
            AppContext.BaseDirectory, "ModelCatalogServiceTests", Guid.NewGuid().ToString("N"))
    };

    /// <summary>
    ///     Proves that the adapter rejects a missing catalog rather than deferring the failure to
    ///     the first enumeration.
    /// </summary>
    [Fact]
    public void ModelCatalogService_Constructor_NullCatalog_ThrowsArgumentNullException()
    {
        // Act & Assert: composing without a catalog is a programming error surfaced immediately
        Assert.Throws<ArgumentNullException>(() => new ModelCatalogService(null!));
    }

    /// <summary>
    ///     Proves that the adapter reports exactly what the library's catalog reports - the
    ///     four real models this phase registers (two recognition, two synthesis) - rather than
    ///     inventing placeholder models to fill the panel.
    /// </summary>
    [Fact]
    public void ModelCatalogService_Enumerate_LibraryKnowsRealModels_ReturnsThem()
    {
        // Arrange: the real library catalog, whose compiled-in known-model registry now
        // contains this phase's four real models
        using var catalog = new SpeechModelCatalog(IsolatedOptions());
        var service = new ModelCatalogService(catalog);

        // Act: enumerate through the demo's seam
        var models = service.Enumerate();

        // Assert: the same models the library itself knows about are passed through unchanged,
        // preserving each model's own declared role rather than assuming they are all one role
        Assert.Equal(SpeechModelCatalog.KnownModels.Count, models.Count);
        Assert.Equal(2, models.Count(m => m.Role == SpeechModelRole.Recognition));
        Assert.Equal(2, models.Count(m => m.Role == SpeechModelRole.Synthesis));
    }

    /// <summary>
    ///     Proves that requesting a model the library does not know surfaces the library's own
    ///     documented exception rather than being swallowed by the adapter.
    /// </summary>
    [Fact]
    public async Task ModelCatalogService_DownloadAsync_UnknownModelId_ThrowsArgumentException()
    {
        // Arrange: the real library catalog; "no-such-model" still matches none of its known models
        using var catalog = new SpeechModelCatalog(IsolatedOptions());
        var service = new ModelCatalogService(catalog);

        // Act & Assert: the library's caller-error contract is preserved through the seam
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.DownloadAsync("no-such-model", null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that the adapter does not dispose the catalog it was given, so a catalog shared
    ///     with other parts of the application outlives any one adapter.
    /// </summary>
    [Fact]
    public void ModelCatalogService_Enumerate_AfterAnotherAdapterFallsOutOfUse_StillWorks()
    {
        // Arrange: two adapters over one shared catalog
        using var catalog = new SpeechModelCatalog(IsolatedOptions());
        var first = new ModelCatalogService(catalog);
        var second = new ModelCatalogService(catalog);

        // Act: use the first adapter, abandon it, then use the second
        first.Enumerate();
        var models = second.Enumerate();

        // Assert: the shared catalog is still usable and reports the same known models
        Assert.Equal(SpeechModelCatalog.KnownModels.Count, models.Count);
    }

    /// <summary>
    ///     Proves that <see cref="IModelCatalogService.ModelInstalled"/> fires exactly once, with
    ///     the correct model id and role, after a download that genuinely installs the model.
    /// </summary>
    [Fact]
    public async Task ModelCatalogService_DownloadAsync_Installed_RaisesModelInstalledWithCorrectIdAndRole()
    {
        // Arrange: an injected fake model and a download client that always succeeds, so the
        // download genuinely completes with SpeechModelDownloadOutcome.Installed - no real
        // network access
        var payload = "genuine model bytes"u8.ToArray();
        var model = new DownloadableFakeModel("fake-recognition-model", SpeechModelRole.Recognition, payload);
        var store = new SpeechModelStore(IsolatedOptions());
        using var catalog = new SpeechModelCatalog([model], store, new SucceedingModelDownloadClient(payload));
        var service = new ModelCatalogService(catalog);
        var raisedEvents = new List<ModelInstalledEventArgs>();
        service.ModelInstalled += (_, e) => raisedEvents.Add(e);

        // Act
        var result = await service.DownloadAsync(
            model.Id, progress: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Installed, result.Outcome);
        var raised = Assert.Single(raisedEvents);
        Assert.Equal(model.Id, raised.ModelId);
        Assert.Equal(SpeechModelRole.Recognition, raised.Role);
    }

    /// <summary>
    ///     Proves that <see cref="IModelCatalogService.ModelInstalled"/> is never raised for a
    ///     download that fails rather than installs.
    /// </summary>
    [Fact]
    public async Task ModelCatalogService_DownloadAsync_Failed_DoesNotRaiseModelInstalled()
    {
        // Arrange: an injected fake model and a download client that always fails
        var payload = "genuine model bytes"u8.ToArray();
        var model = new DownloadableFakeModel("fake-synthesis-model", SpeechModelRole.Synthesis, payload);
        var store = new SpeechModelStore(IsolatedOptions());
        using var catalog = new SpeechModelCatalog([model], store, new FailingModelDownloadClient());
        var service = new ModelCatalogService(catalog);
        var raisedEvents = new List<ModelInstalledEventArgs>();
        service.ModelInstalled += (_, e) => raisedEvents.Add(e);

        // Act
        var result = await service.DownloadAsync(
            model.Id, progress: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SpeechModelDownloadOutcome.Failed, result.Outcome);
        Assert.Empty(raisedEvents);
    }

    /// <summary>
    ///     Fake <see cref="ISpeechModel"/> whose single declared download file's checksum
    ///     genuinely matches a given in-memory payload, so a fake
    ///     <see cref="IModelDownloadClient"/> serving that exact payload results in a real,
    ///     verified <see cref="SpeechModelDownloadOutcome.Installed"/> outcome with no real
    ///     network access.
    /// </summary>
    private sealed class DownloadableFakeModel(string id, SpeechModelRole role, byte[] payload) : ISpeechModel
    {
        /// <inheritdoc/>
        public string Id => id;

        /// <inheritdoc/>
        public string DisplayName => id;

        /// <inheritdoc/>
        public SpeechModelRole Role => role;

        /// <inheritdoc/>
        public IReadOnlyList<ISpeechModelParameter> Parameters => [];

        /// <inheritdoc/>
        public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

        /// <inheritdoc/>
        public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = new(
        [
            new SpeechModelDownloadFile(
                new Uri($"https://example.test/{id}.bin"),
                Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(),
                "model.bin")
        ]);
    }

    /// <summary>
    ///     Fake <see cref="IModelDownloadClient"/> that always serves a fixed in-memory payload,
    ///     used to prove a genuinely successful install with no real network access.
    /// </summary>
    private sealed class SucceedingModelDownloadClient(byte[] payload) : IModelDownloadClient
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
    ///     Fake <see cref="IModelDownloadClient"/> that always fails, used to prove
    ///     <see cref="IModelCatalogService.ModelInstalled"/> is never raised for a failed
    ///     download.
    /// </summary>
    private sealed class FailingModelDownloadClient : IModelDownloadClient
    {
        /// <inheritdoc/>
        public Task DownloadAsync(
            Uri sourceUri,
            Stream destination,
            IProgress<SpeechModelDownloadProgress>? progress,
            CancellationToken cancellationToken) =>
            throw new IOException("Simulated download failure.");
    }
}
