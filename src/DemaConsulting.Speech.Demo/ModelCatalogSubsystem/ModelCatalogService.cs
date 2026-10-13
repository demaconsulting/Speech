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
///     <b>Thread safety</b>: <see cref="ApplyMirror"/>, <see cref="Dispose"/> and the pack
///     install/uninstall rebuilds (<see cref="InstallReferenceVoicePackAsync"/> and
///     <see cref="UninstallReferenceVoicePackAsync"/>) all read and then replace the same
///     current catalog, voice library and downloader-options state (the options field is not
///     volatile) with no synchronization, so concurrent calls could race on which pair ends up
///     current or disposed. In particular, a rebuild disposes the previous pair, which aborts any
///     <see cref="DownloadAsync"/> still running against it. Callers must serialize their own
///     calls to these members (for example, as this demo's UI does, by invoking them one at a time
///     from a single UI thread and refusing overlapping transfers); this type provides no
///     internal locking of its own. Every rebuild is a single publish-then-dispose step: the new
///     pair is built first and published by plain assignments (which cannot fail), so a failing
///     factory leaves the previous pair in place and untouched. Ownership: when constructed with a
///     factory, this service owns every catalog and voice library it ever holds and disposes each
///     replaced pair and the current pair on <see cref="Dispose"/>; otherwise it owns neither.
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
    ///     Rebuilds a catalog and its shared reference-voice library as a pair, or
    ///     <see langword="null"/> when this service was not constructed with a composition factory
    ///     and therefore has no reference-voice support.
    /// </summary>
    private readonly Func<SpeechModelDownloaderOptions?, ModelCatalogComposition>? _compositionFactory;

    /// <summary>The reference-voice library shared with the current catalog, if any.</summary>
    private volatile ReferenceVoiceLibrary? _voiceLibrary;

    /// <summary>The downloader options the current catalog was built with (mirror), reused for pack-driven rebuilds.</summary>
    private SpeechModelDownloaderOptions? _downloaderOptions;

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

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelCatalogService"/> class that supports
    ///     <see cref="ApplyMirror"/> and reference-voice pack management by rebuilding its catalog
    ///     and shared <see cref="ReferenceVoiceLibrary"/> through <paramref name="compositionFactory"/>.
    /// </summary>
    /// <param name="initial">The initial catalog and its reference-voice library. Must not be <see langword="null"/>.</param>
    /// <param name="initialDownloaderOptions">
    ///     The downloader options <paramref name="initial"/> was built with, reused when a pack
    ///     install or uninstall rebuilds the catalog; <see langword="null"/> for public downloads.
    /// </param>
    /// <param name="compositionFactory">
    ///     Builds a fresh catalog and library pair for given downloader options. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="initial"/> or <paramref name="compositionFactory"/> is <see langword="null"/>.
    /// </exception>
    public ModelCatalogService(
        ModelCatalogComposition initial,
        SpeechModelDownloaderOptions? initialDownloaderOptions,
        Func<SpeechModelDownloaderOptions?, ModelCatalogComposition> compositionFactory)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(compositionFactory);

        _catalog = initial.Catalog;
        _voiceLibrary = initial.VoiceLibrary;
        _downloaderOptions = initialDownloaderOptions;
        _compositionFactory = compositionFactory;
        _catalogFactory = options => compositionFactory(options).Catalog;
    }

    /// <inheritdoc/>
    public event EventHandler<ModelInstalledEventArgs>? ModelInstalled;

    /// <inheritdoc/>
    public event EventHandler? ReferenceVoicesChanged;

    /// <inheritdoc/>
    public IReadOnlyList<ReferenceVoicePack> ReferenceVoicePacks =>
        _voiceLibrary is null ? [] : ReferenceVoiceLibrary.Packs;

    /// <inheritdoc/>
    public string? UserVoicesDirectory => _voiceLibrary?.UserDirectory;

    /// <inheritdoc/>
    public SpeechModelState GetReferenceVoicePackState(string packId) =>
        RequireVoiceLibrary().GetPackState(packId);

    /// <inheritdoc/>
    public Task<bool> ReferenceVoicePackHasEmotionsAsync(string packId, CancellationToken cancellationToken) =>
        RequireVoiceLibrary().PackHasEmotionsAsync(packId, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    ///     Whether the install succeeds, fails or is canceled (or throws), the catalog and library are rebuilt (so
    ///     the Chatterbox model re-reads its voices) and <see cref="ReferenceVoicesChanged"/> is raised, because the
    ///     pack's installed state may have changed even then (an upgrade moves the old install aside and restores
    ///     it on failure). The rebuild aborts
    ///     any model download in flight; see <see cref="IModelCatalogService.InstallReferenceVoicePackAsync"/>.
    ///     If the install itself throws (including cancellation) and the rebuild then also throws, the
    ///     install's exception is the one that propagates; the rebuild failure is discarded so it cannot mask it.
    ///     After a successful install, a rebuild failure propagates to the caller.
    /// </remarks>
    public async Task<SpeechModelDownloadResult> InstallReferenceVoicePackAsync(
        string packId,
        bool includeEmotions,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var library = RequireVoiceLibrary();
        SpeechModelDownloadResult result;
        try
        {
            result = await library
                .InstallPackAsync(packId, includeEmotions, progress, cancellationToken)
                .ConfigureAwait(true);
        }
#pragma warning disable CA1031 // The rebuild must not mask the install's own exception.
        catch
#pragma warning restore CA1031
        {
            try
            {
                RebuildAndNotifyVoicesChanged();
            }
#pragma warning disable CA1031 // Intentionally discarded: the install's exception is rethrown below.
            catch (Exception)
#pragma warning restore CA1031
            {
                // The install's own exception is more useful to the caller than a secondary rebuild failure
            }

            throw;
        }

        RebuildAndNotifyVoicesChanged();
        return result;
    }

    /// <inheritdoc/>
    public async Task UninstallReferenceVoicePackAsync(string packId, CancellationToken cancellationToken)
    {
        await RequireVoiceLibrary().UninstallPackAsync(packId, cancellationToken).ConfigureAwait(true);
        RebuildAndNotifyVoicesChanged();
    }

    /// <summary>Returns the current voice library or throws when this service has none.</summary>
    /// <returns>The current library.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this service has no reference-voice library.</exception>
    private ReferenceVoiceLibrary RequireVoiceLibrary() =>
        _voiceLibrary ?? throw new InvalidOperationException(
            "This service was constructed without a reference-voice library. Use the " +
            "ModelCatalogService(ModelCatalogComposition, SpeechModelDownloaderOptions?, Func<...>) constructor instead.");

    /// <summary>
    ///     Replaces the catalog and library with freshly composed ones (same downloader options) and
    ///     raises <see cref="ReferenceVoicesChanged"/>.
    /// </summary>
    private void RebuildAndNotifyVoicesChanged()
    {
        Recompose(_downloaderOptions);
        ReferenceVoicesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Builds a new catalog/library pair for <paramref name="options"/>, publishes it, and
    ///     disposes the pair it replaces.
    /// </summary>
    /// <param name="options">The downloader options for the new pair.</param>
    private void Recompose(SpeechModelDownloaderOptions? options)
    {
        var factory = _compositionFactory!;
        var previousCatalog = _catalog;
        var previousLibrary = _voiceLibrary;
        var composition = factory(options);
        _catalog = composition.Catalog;
        _voiceLibrary = composition.VoiceLibrary;
        _downloaderOptions = options;
        previousCatalog.Dispose();
        previousLibrary?.Dispose();
    }

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
        if (_compositionFactory is not null)
        {
            Recompose(downloaderOptions);
            ReferenceVoicesChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

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
            _voiceLibrary?.Dispose();
        }
    }
}
