using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.ModelCatalogSubsystem;

/// <summary>
///     Production <see cref="IModelCatalogService"/> implementation backed by the library's
///     <see cref="SpeechModelCatalog"/>.
/// </summary>
/// <remarks>
///     This adapter forwards each call to the library catalog and returns the result unchanged,
///     so the demo shows exactly the models and states the library reports - it invents no
///     placeholder models when the library's compiled-in registry is empty (presenting an honest
///     empty catalog is the demo's job, handled in the presentation layer rather than by
///     fabricating data here). Its one piece of added behavior is raising
///     <see cref="IModelCatalogService.ModelInstalled"/> after a <see cref="DownloadAsync"/> call
///     whose result reports <see cref="SpeechModelDownloadOutcome.Installed"/>, since this is the
///     one place in the demo that already observes a successful install and both the catalog
///     panel and the STT/TTS panels already depend on this seam.
///     <para>
///     The catalog is owned by the composition root, which disposes it; this adapter deliberately
///     does not dispose it so that a shared catalog can outlive any one adapter - unless this
///     adapter was given a catalog factory (see the two-argument constructor), in which case it
///     instead owns every catalog it ever points at (both the one it was constructed with and
///     each one it later builds via <see cref="ApplyMirror"/>), and disposes whichever one is
///     current when this adapter itself is disposed.
///     </para>
///     <para>
///     <b>Thread safety</b>: <see cref="ApplyMirror"/> and <see cref="Dispose"/> are not safe to
///     call concurrently with each other or with themselves - both read and then replace the
///     same current-catalog field with no synchronization, so concurrent calls could race on
///     which catalog ends up current or disposed. Callers must serialize their own calls to
///     these two members (for example, as this demo's UI does, by invoking them one at a time
///     from a single UI thread); this type provides no internal locking of its own.
///     </para>
/// </remarks>
public sealed class ModelCatalogService : IModelCatalogService, IDisposable
{
    /// <summary>The library catalog every call is currently forwarded to.</summary>
    private volatile SpeechModelCatalog _catalog;

    /// <summary>
    ///     Rebuilds a catalog (using the same known models and model-store root as the one this
    ///     service started with) for a given set of downloader options, or <see langword="null"/>
    ///     when this service was constructed without one and therefore cannot support
    ///     <see cref="ApplyMirror"/>.
    /// </summary>
    private readonly Func<SpeechModelDownloaderOptions?, SpeechModelCatalog>? _catalogFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelCatalogService"/> class that
    ///     forwards to a fixed catalog and does not support <see cref="ApplyMirror"/>.
    /// </summary>
    /// <param name="catalog">
    ///     The library model catalog to delegate to. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="catalog"/> is <see langword="null"/>.
    /// </exception>
    public ModelCatalogService(SpeechModelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
        _catalogFactory = null;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelCatalogService"/> class that
    ///     supports <see cref="ApplyMirror"/> by rebuilding its catalog through
    ///     <paramref name="catalogFactory"/> whenever the mirror changes.
    /// </summary>
    /// <param name="catalog">
    ///     The initial library model catalog to delegate to. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="catalogFactory">
    ///     Builds a fresh catalog (reusing the same known models and model-store root as
    ///     <paramref name="catalog"/>) for a given set of downloader options. Must not be
    ///     <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="catalog"/> or <paramref name="catalogFactory"/> is
    ///     <see langword="null"/>.
    /// </exception>
    public ModelCatalogService(
        SpeechModelCatalog catalog,
        Func<SpeechModelDownloaderOptions?, SpeechModelCatalog> catalogFactory)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(catalogFactory);

        _catalog = catalog;
        _catalogFactory = catalogFactory;
    }

    /// <inheritdoc/>
    public event EventHandler<ModelInstalledEventArgs>? ModelInstalled;

    /// <inheritdoc/>
    public IReadOnlyList<SpeechModelDescriptor> Enumerate()
    {
        try
        {
            return _catalog.Enumerate();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent ApplyMirror disposed the catalog read just before the swap; the
            // replacement is already published, so retry against it.
            return _catalog.Enumerate();
        }
    }

    /// <inheritdoc/>
    public async Task<SpeechModelDownloadResult> DownloadAsync(
        string modelId,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var catalog = _catalog;
        var result = await catalog.DownloadAsync(modelId, progress, cancellationToken).ConfigureAwait(true);

        if (result.Outcome == SpeechModelDownloadOutcome.Installed)
        {
            var role = Enumerate().FirstOrDefault(descriptor => descriptor.Id == modelId)?.Role;
            if (role is not null)
            {
                ModelInstalled?.Invoke(this, new ModelInstalledEventArgs(modelId, role.Value));
            }
        }

        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Disposes the catalog this call replaces immediately, which aborts - rather than lets
    ///     run to completion - any <see cref="DownloadAsync"/> call still in flight against that
    ///     catalog (its underlying HTTP operations observe a disposed object and fail). Callers
    ///     must therefore never invoke this method while one of their own downloads is in
    ///     progress; see <see cref="IModelCatalogService.ApplyMirror"/>'s remarks. This demo's
    ///     own <see cref="ModelCatalogViewModel.ApplyMirror"/> enforces that by refusing to call
    ///     through while any row is downloading.
    ///     <para>
    ///     Not safe to call concurrently with another <see cref="ApplyMirror"/> or
    ///     <see cref="Dispose"/> call - see this type's remarks for this instance's thread-safety
    ///     contract.
    ///     </para>
    /// </remarks>
    public void ApplyMirror(DownloadMirror? mirror)
    {
        if (_catalogFactory is null)
        {
            throw new InvalidOperationException(
                "This service was constructed without a catalog factory, so its mirror cannot be " +
                "changed after construction. Use the ModelCatalogService(SpeechModelCatalog, " +
                "Func<SpeechModelDownloaderOptions?, SpeechModelCatalog>) constructor instead.");
        }

        var downloaderOptions = mirror is null ? null : new SpeechModelDownloaderOptions { Mirror = mirror };
        var previousCatalog = _catalog;
        _catalog = _catalogFactory(downloaderOptions);
        previousCatalog.Dispose();
    }

    /// <summary>
    ///     Disposes the current catalog, but only when this service was constructed with a
    ///     catalog factory (see the two-argument constructor) and therefore owns the catalog's
    ///     lifetime; otherwise this is a no-op, preserving the single-catalog constructor's
    ///     contract that a shared, externally owned catalog outlives this adapter.
    /// </summary>
    /// <remarks>
    ///     Not safe to call concurrently with another <see cref="Dispose"/> or
    ///     <see cref="ApplyMirror"/> call - see this type's remarks for this instance's
    ///     thread-safety contract.
    /// </remarks>
    public void Dispose()
    {
        if (_catalogFactory is not null)
        {
            _catalog.Dispose();
        }
    }
}
