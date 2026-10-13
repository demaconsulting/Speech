using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo.ModelCatalogSubsystem;

/// <summary>
///     Demo-owned seam over the library's speech-model catalog surface.
/// </summary>
/// <remarks>
///     <para>
///     The library exposes its catalog through the concrete, sealed
///     <see cref="SpeechModelCatalog"/> rather than through an injectable interface, so a
///     ViewModel that depended on it directly could not be unit tested against a controlled model
///     list or a controlled download outcome. This interface exists purely as the demo
///     application's own composition-root seam: the production implementation delegates straight
///     to <see cref="SpeechModelCatalog"/>, while tests substitute a fake that can supply models
///     and download outcomes the real, deliberately empty compiled-in registry cannot. It adds no
///     public API to <c>DemaConsulting.Speech</c>.
///     </para>
///     <para>
///     This interface deliberately does <b>not</b> extend <see cref="IDisposable"/>: disposal is
///     a lifetime concern of the composition root that constructs an implementation (see
///     <see cref="ModelCatalogService"/>'s own <see cref="IDisposable"/> implementation and
///     remarks), not something every consumer - including the <see cref="ModelCatalogViewModel"/>
///     and test fakes that only ever read from or call through this interface - needs to know
///     about or be responsible for. Adding it here would force every current and future
///     implementer (including test doubles) to also implement disposal even when they own no
///     disposable resource of their own.
///     </para>
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
    /// <remarks>
    ///     Reads the library's in-memory view; it does not touch the network. The production service
    ///     retries once when a concurrent mirror change disposes the catalog it was reading, so a
    ///     disposed-catalog <see cref="ObjectDisposedException"/> or any other fault from the library
    ///     itself can still propagate to the caller.
    /// </remarks>
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

    /// <summary>
    ///     Reconfigures where every subsequent <see cref="DownloadAsync"/> call fetches models
    ///     from, replacing the underlying catalog with a freshly composed one that uses the same
    ///     known models and model-store root but the given mirror.
    /// </summary>
    /// <param name="mirror">
    ///     The mirror every model should now download from, or <see langword="null"/> to revert
    ///     to each model's own declared public download URI.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when this service was constructed without a catalog factory and therefore
    ///     cannot be reconfigured after construction.
    /// </exception>
    /// <remarks>
    ///     The catalog instance this call replaces is disposed as part of this call (see
    ///     <see cref="ModelCatalogService"/>'s remarks), which aborts any <see cref="DownloadAsync"/>
    ///     call still in flight against it. A caller must not invoke this method while one of its
    ///     own downloads is in progress; <see cref="ModelCatalogViewModel.ApplyMirror"/> enforces
    ///     this by refusing to call through while any row is downloading.
    /// </remarks>
    void ApplyMirror(DownloadMirror? mirror);

    /// <summary>
    ///     Raised after the reference voices a voice-cloning model offers may have changed - that is,
    ///     after a reference-voice pack install or uninstall (or a mirror change) caused the catalog
    ///     to be rebuilt - so panels showing a model's voice choice can re-read it. The voice list is
    ///     fixed when a catalog is built, so this is the demo's refresh contract: voices are refreshed
    ///     at least on every catalog rebuild.
    /// </summary>
    event EventHandler? ReferenceVoicesChanged;

    /// <summary>
    ///     Gets the reference-voice packs this service can install. Empty when the service was
    ///     composed without a reference-voice library.
    /// </summary>
    IReadOnlyList<ReferenceVoicePack> ReferenceVoicePacks { get; }

    /// <summary>
    ///     Gets the folder where the user may drop their own <c>.wav</c>/<c>.flac</c> reference
    ///     voices, or <see langword="null"/> when the service has no reference-voice library.
    /// </summary>
    string? UserVoicesDirectory { get; }

    /// <summary>
    ///     Gets the current install state of one reference-voice pack.
    /// </summary>
    /// <param name="packId">The identifier of a pack listed in <see cref="ReferenceVoicePacks"/>.</param>
    /// <returns>The pack's state.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> matches no known pack.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the service has no reference-voice library.</exception>
    SpeechModelState GetReferenceVoicePackState(string packId);

    /// <summary>
    ///     Determines whether an installed reference-voice pack was extracted with its emotion (mood) clips.
    /// </summary>
    /// <param name="packId">The identifier of a pack listed in <see cref="ReferenceVoicePacks"/>.</param>
    /// <param name="cancellationToken">A token that aborts the read.</param>
    /// <returns>A task whose result is <see langword="true"/> when the pack is installed with emotion clips; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> matches no known pack.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the service has no reference-voice library.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    Task<bool> ReferenceVoicePackHasEmotionsAsync(string packId, CancellationToken cancellationToken);

    /// <summary>
    ///     Downloads and installs one reference-voice pack, then rebuilds the catalog and raises
    ///     <see cref="ReferenceVoicesChanged"/> whether or not it was installed (an upgrade that fails is rolled
    ///     back, which can still change the installed state).
    /// </summary>
    /// <param name="packId">The identifier of a pack listed in <see cref="ReferenceVoicePacks"/>.</param>
    /// <param name="includeEmotions"><see langword="true"/> to also extract the pack's emotion (mood) clips (an installed voices-only pack is upgraded); <see langword="false"/> for the base voices only.</param>
    /// <param name="progress">An optional progress sink, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token that, when canceled, aborts the download.</param>
    /// <returns>The outcome describing whether the pack was installed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> matches no known pack.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the service has no reference-voice library.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <remarks>
    ///     The catalog rebuild aborts any model download still in flight, so callers must not start a
    ///     pack install while one of their own model downloads is in progress (see <see cref="ApplyMirror"/>).
    /// </remarks>
    Task<SpeechModelDownloadResult> InstallReferenceVoicePackAsync(
        string packId,
        bool includeEmotions,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Removes one installed reference-voice pack, then rebuilds the catalog and raises
    ///     <see cref="ReferenceVoicesChanged"/>.
    /// </summary>
    /// <param name="packId">The identifier of a pack listed in <see cref="ReferenceVoicePacks"/>.</param>
    /// <param name="cancellationToken">A token that aborts the wait for an in-flight install of the same pack.</param>
    /// <returns>A task that completes when the pack is removed and the catalog rebuilt.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> matches no known pack.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the service has no reference-voice library.</exception>
    /// <exception cref="OperationCanceledException">
    ///     Thrown when <paramref name="cancellationToken"/> is canceled while waiting for an in-flight install of the
    ///     pack; nothing is removed and the catalog is not rebuilt.
    /// </exception>
    /// <exception cref="SpeechModelStoreException">
    ///     Thrown when the pack's files are in use and cannot be removed. Nothing is rebuilt; the caller should
    ///     report the message.
    /// </exception>
    /// <remarks>
    ///     An uninstall that arrives while an install of the same pack is in flight waits for it to finish and
    ///     then removes the pack; it never blocks the calling thread.
    /// </remarks>
    Task UninstallReferenceVoicePackAsync(string packId, CancellationToken cancellationToken);
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

