using System.Linq;
using System.Security.Cryptography;
using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Sherpa;

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
    ///     Proves that the factory-taking constructor rejects a missing factory, just as it
    ///     rejects a missing catalog.
    /// </summary>
    [Fact]
    public void ModelCatalogService_Constructor_NullCatalogFactory_ThrowsArgumentNullException()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(IsolatedOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ModelCatalogService(catalog, null!));
    }

    /// <summary>
    ///     Proves that a service constructed without a catalog factory refuses to change its
    ///     mirror, rather than silently doing nothing.
    /// </summary>
    [Fact]
    public void ModelCatalogService_ApplyMirror_NoFactory_ThrowsInvalidOperationException()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(IsolatedOptions());
        var service = new ModelCatalogService(catalog);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.ApplyMirror(null));
    }

    /// <summary>
    ///     Proves that a service constructed with a catalog factory rebuilds its catalog through
    ///     that factory when a mirror is applied, so subsequent calls observe the new catalog's
    ///     known models.
    /// </summary>
    [Fact]
    public void ModelCatalogService_ApplyMirror_WithFactory_RebuildsCatalogThroughFactory()
    {
        // Arrange: the factory returns a catalog with a different known-model count each time it
        // is invoked, so a successful rebuild is observable through Enumerate() alone
        var store = new SpeechModelStore(IsolatedOptions());
        var callCount = 0;
        SpeechModelCatalog Factory(SpeechModelDownloaderOptions? _)
        {
            callCount++;
            var models = callCount == 1
                ? []
                : new List<ISpeechModel> { new FakeModel("model-after-apply") };
            return new SpeechModelCatalog(models, store, null, null, null);
        }

        var initialCatalog = Factory(null);
        var service = new ModelCatalogService(initialCatalog, Factory);
        Assert.Empty(service.Enumerate());

        // Act
        service.ApplyMirror(null);

        // Assert: the service now forwards to the catalog the factory built on its second call
        Assert.Equal(2, callCount);
        Assert.Single(service.Enumerate());
        Assert.Equal("model-after-apply", service.Enumerate()[0].Id);
    }

    /// <summary>
    ///     Proves that applying a mirror disposes the catalog it replaces, rather than leaking it
    ///     for the lifetime of the application.
    /// </summary>
    /// <remarks>
    ///     Uses the same successful-download-first technique as
    ///     <see cref="ModelCatalogService_Dispose_WithFactory_DisposesCurrentCatalog"/>: a
    ///     per-model-id lock already exists on the replaced catalog's downloader before
    ///     <see cref="ModelCatalogService.ApplyMirror"/> runs, so a genuinely disposed replaced
    ///     catalog surfaces <see cref="ObjectDisposedException"/> from a later download attempt
    ///     against it directly.
    /// </remarks>
    [Fact]
    public async Task ModelCatalogService_ApplyMirror_WithFactory_DisposesReplacedCatalog()
    {
        // Arrange
        var payload = "genuine model bytes"u8.ToArray();
        var model = new DownloadableFakeModel("replaced-catalog-probe-model", SpeechModelRole.Synthesis, payload);
        var store = new SpeechModelStore(IsolatedOptions());
        var initialCatalog = new SpeechModelCatalog([model], store, new SucceedingModelDownloadClient(payload));
        var service = new ModelCatalogService(
            initialCatalog, _ => new SpeechModelCatalog([model], store, new SucceedingModelDownloadClient(payload)));

        // One successful download so the model id's per-model-id lock already exists
        var firstResult = await initialCatalog.DownloadAsync(model.Id, null, CancellationToken.None);
        Assert.Equal(SpeechModelDownloadOutcome.Installed, firstResult.Outcome);

        // Act
        service.ApplyMirror(null);

        // Assert: the replaced catalog is genuinely disposed, not merely detached
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => initialCatalog.DownloadAsync(model.Id, null, CancellationToken.None));
    }

    /// <summary>
    ///     Proves that disposing a factory-constructed service disposes its current catalog, so
    ///     the composition root does not need to separately track every catalog ever built.
    /// </summary>
    /// <remarks>
    ///     A successful download first, so the per-model-id lock this model's id resolves to
    ///     already exists in the catalog's downloader before disposal - the same lock is reused
    ///     on the next <see cref="SpeechModelCatalog.DownloadAsync(string,IProgress{SpeechModelDownloadProgress}?,CancellationToken)"/>
    ///     call rather than one created fresh. Disposing therefore leaves a genuinely disposed
    ///     lock in place, and a later call to download the same model id surfaces the documented
    ///     <see cref="ObjectDisposedException"/>.
    /// </remarks>
    [Fact]
    public async Task ModelCatalogService_Dispose_WithFactory_DisposesCurrentCatalog()
    {
        // Arrange
        var payload = "genuine model bytes"u8.ToArray();
        var model = new DownloadableFakeModel("disposal-probe-model", SpeechModelRole.Synthesis, payload);
        var store = new SpeechModelStore(IsolatedOptions());
        var catalog = new SpeechModelCatalog([model], store, new SucceedingModelDownloadClient(payload));
        var service = new ModelCatalogService(
            catalog, _ => new SpeechModelCatalog([model], store, new SucceedingModelDownloadClient(payload)));

        // One successful download so the model id's per-model-id lock already exists
        var firstResult = await catalog.DownloadAsync(model.Id, null, CancellationToken.None);
        Assert.Equal(SpeechModelDownloadOutcome.Installed, firstResult.Outcome);

        // Act
        service.Dispose();

        // Assert: the now-disposed, reused per-model-id lock surfaces the documented
        // ObjectDisposedException rather than silently succeeding against a disposed catalog
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => catalog.DownloadAsync(model.Id, null, CancellationToken.None));
    }

    /// <summary>
    ///     Proves that disposing a service constructed with the single-catalog constructor does
    ///     not dispose the catalog, preserving the pre-existing "shared catalog outlives any one
    ///     adapter" contract for callers that never apply a mirror.
    /// </summary>
    [Fact]
    public void ModelCatalogService_Dispose_WithoutFactory_DoesNotDisposeCatalog()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(IsolatedOptions());
        var service = new ModelCatalogService(catalog);

        // Act
        service.Dispose();

        // Assert: the catalog is still usable
        Assert.Empty(catalog.Enumerate());
    }

    /// <summary>
    ///     Minimal <see cref="ISpeechModel"/> stand-in used only to prove that
    ///     <see cref="ModelCatalogService.ApplyMirror"/> genuinely swaps which catalog is in use.
    /// </summary>
    private sealed class FakeModel(string id) : ISpeechModel
    {
        /// <inheritdoc/>
        public string Id => id;

        /// <inheritdoc/>
        public string DisplayName => id;

        /// <inheritdoc/>
        public SpeechModelRole Role => SpeechModelRole.Synthesis;

        /// <inheritdoc/>
        public IReadOnlyList<ISpeechModelParameter> Parameters => [];

        /// <inheritdoc/>
        public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

        /// <inheritdoc/>
        public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = new(
        [
            new SpeechModelDownloadFile(
                new Uri($"https://example.test/{id}.bin"),
                new string('0', 64),
                "model.bin")
        ]);
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
        using var catalog = new SpeechModelCatalog(IsolatedOptions()).AddSherpaModels();
        var service = new ModelCatalogService(catalog);

        // Act: enumerate through the demo's seam
        var models = service.Enumerate();

        // Assert: the same models the library itself knows about are passed through unchanged,
        // preserving each model's own declared role rather than assuming they are all one role
        Assert.Equal(4, models.Count);
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
        using var catalog = new SpeechModelCatalog(IsolatedOptions()).AddSherpaModels();
        var first = new ModelCatalogService(catalog);
        var second = new ModelCatalogService(catalog);

        // Act: use the first adapter, abandon it, then use the second
        first.Enumerate();
        var models = second.Enumerate();

        // Assert: the shared catalog is still usable and reports the same known models
        Assert.Equal(4, models.Count);
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

        // Assert: the simulated failure is an IOException, which this library's richer
        // SpeechModelDownloadOutcome classification now honestly reports as IoFailure rather
        // than the old generic Failed fallback - the event-not-raised behavior under test is
        // unaffected by which specific non-Installed outcome was reported.
        Assert.Equal(SpeechModelDownloadOutcome.IoFailure, result.Outcome);
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
