using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.ModelCatalogSubsystem;

/// <summary>
///     Demo-owned seam over the library's speech-model catalog surface.
/// </summary>
/// <remarks>
///     The library exposes its catalog through the concrete, sealed
///     <see cref="SpeechModelCatalog"/> rather than through an injectable interface, so a
///     ViewModel that depended on it directly could not be unit tested against a controlled model
///     list or a controlled download outcome. This interface exists purely as the demo
///     application's own composition-root seam: the production implementation delegates straight
///     to <see cref="SpeechModelCatalog"/>, while tests substitute a fake that can supply models
///     and download outcomes the real, deliberately empty compiled-in registry cannot. It adds no
///     public API to <c>DemaConsulting.Speech</c>.
/// </remarks>
public interface IModelCatalogService
{
    /// <summary>
    ///     Raised after a model successfully finishes installing (that is, after a
    ///     <see cref="DownloadAsync"/> call whose result reports
    ///     <see cref="SpeechModelDownloadOutcome.Installed"/>), so panels showing models of the
    ///     same <see cref="SpeechModelRole"/> can refresh themselves without a manual click or app
    ///     restart. Never raised for a failed, canceled, or checksum-mismatched download attempt.
    /// </summary>
    event EventHandler<ModelInstalledEventArgs>? ModelInstalled;

    /// <summary>
    ///     Enumerates every model the library knows about, alongside its current install state.
    /// </summary>
    /// <returns>
    ///     A snapshot list of model descriptors. Never <see langword="null"/>; empty when the
    ///     library ships no known models, which is the honest state until a production model is
    ///     released.
    /// </returns>
    /// <remarks>Never throws.</remarks>
    IReadOnlyList<SpeechModelDescriptor> Enumerate();

    /// <summary>
    ///     Downloads, verifies, and installs one known model, reporting transfer progress.
    /// </summary>
    /// <param name="modelId">
    ///     The identifier of a model present in the list returned by <see cref="Enumerate"/>.
    /// </param>
    /// <param name="progress">
    ///     An optional sink invoked as the model's file(s) transfer, or <see langword="null"/> to
    ///     ignore progress.
    /// </param>
    /// <param name="cancellationToken">A token that, when canceled, aborts the download.</param>
    /// <returns>
    ///     The outcome describing whether the model was installed, or the honest failure reason
    ///     when it was not.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="modelId"/> matches no known model.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     Thrown when <paramref name="cancellationToken"/> is canceled.
    /// </exception>
    Task<SpeechModelDownloadResult> DownloadAsync(
        string modelId,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken);
}

/// <summary>
///     Payload carried by <see cref="IModelCatalogService.ModelInstalled"/>, identifying which
///     model finished installing and which speech capability it provides.
/// </summary>
/// <param name="modelId">The identifier of the model that finished installing.</param>
/// <param name="role">The speech capability the installed model provides.</param>
public sealed class ModelInstalledEventArgs(string modelId, SpeechModelRole role) : EventArgs
{
    /// <summary>Gets the identifier of the model that finished installing.</summary>
    public string ModelId { get; } = modelId;

    /// <summary>Gets the speech capability the installed model provides.</summary>
    public SpeechModelRole Role { get; } = role;
}

