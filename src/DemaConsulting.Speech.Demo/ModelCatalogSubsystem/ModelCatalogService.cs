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
///     does not dispose it so that a shared catalog can outlive any one adapter.
///     </para>
/// </remarks>
public sealed class ModelCatalogService : IModelCatalogService
{
    /// <summary>The library catalog every call is forwarded to.</summary>
    private readonly SpeechModelCatalog _catalog;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelCatalogService"/> class.
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
    }

    /// <inheritdoc/>
    public event EventHandler<ModelInstalledEventArgs>? ModelInstalled;

    /// <inheritdoc/>
    public IReadOnlyList<SpeechModelDescriptor> Enumerate() => _catalog.Enumerate();

    /// <inheritdoc/>
    public async Task<SpeechModelDownloadResult> DownloadAsync(
        string modelId,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var result = await _catalog.DownloadAsync(modelId, progress, cancellationToken).ConfigureAwait(true);

        if (result.Outcome == SpeechModelDownloadOutcome.Installed)
        {
            var role = _catalog.Enumerate().FirstOrDefault(descriptor => descriptor.Id == modelId)?.Role;
            if (role is not null)
            {
                ModelInstalled?.Invoke(this, new ModelInstalledEventArgs(modelId, role.Value));
            }
        }

        return result;
    }
}
