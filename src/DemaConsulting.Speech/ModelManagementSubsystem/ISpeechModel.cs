namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Common per-model contract describing a speech model's identity, role, downloadable
///     payload, and declared tunable parameters/audio-tag support, independent of any specific
///     inference engine.
/// </summary>
/// <remarks>
///     Per this library's "one backing class per model" decision, each shippable model is a
///     single class implementing either <see cref="IRecognitionModel"/> or
///     <see cref="ISynthesisModel"/> (never this interface directly), carrying its own download
///     URL(s)/checksum(s), declared parameters, audio-tag support declaration, own archive
///     unpacking (<see cref="InstallAsync"/>), own text normalization/correction
///     (<see cref="NormalizeText"/>), and own declared license identification
///     (<see cref="LicenseName"/>/<see cref="LicenseUrl"/>). This pass (Sub-phase 2b) defines
///     only the generic identity/catalog contract plus these model-owned hooks; the recognition- and
///     synthesis-engine construction members that Phase 3/4 add live on the role-specific
///     interfaces, not here, so this contract never needs to change to add a new engine backend.
///     <see cref="SpeechModelCatalog"/> depends only on this common contract, so it can
///     enumerate and report install state for any model regardless of role.
/// </remarks>
public interface ISpeechModel
{
    /// <summary>
    ///     Gets the model's stable identifier, used as its directory name under
    ///     <see cref="SpeechModelStore"/> and as the key passed to
    ///     <see cref="SpeechModelDownloader.DownloadAsync(ISpeechModel,IProgress{SpeechModelDownloadProgress}?,CancellationToken)"/>.
    ///     Never null or empty.
    /// </summary>
    string Id { get; }

    /// <summary>
    ///     Gets the model's short, human-readable name for display in a host UI (for example a
    ///     model-selection list). Never null.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    ///     Gets which speech capability this model provides.
    /// </summary>
    SpeechModelRole Role { get; }

    /// <summary>
    ///     Gets the model's declared tunable parameters, in the order a host should present them.
    ///     Never null; may be empty when the model exposes no tunable parameters.
    /// </summary>
    IReadOnlyList<ISpeechModelParameter> Parameters { get; }

    /// <summary>
    ///     Gets how (if at all) this model can honor inline Natural Language Audio Tags.
    /// </summary>
    SpeechModelAudioTagSupport AudioTagSupport { get; }

    /// <summary>
    ///     Gets the descriptor of the file(s) that must be downloaded and verified to install
    ///     this model. Never null.
    /// </summary>
    SpeechModelDownloadDescriptor DownloadDescriptor { get; }

    /// <summary>
    ///     Unpacks this model's verified, downloaded file(s) in place within
    ///     <paramref name="stagedFilesDirectory"/> before <see cref="SpeechModelStore"/> atomically
    ///     promotes that directory to the model's installed <c>current/</c> content. The default
    ///     implementation is a no-op, matching the common case of a model whose single downloaded
    ///     file needs no unpacking at all.
    /// </summary>
    /// <param name="stagedFilesDirectory">
    ///     The staging directory already containing this model's fully checksum-verified downloaded
    ///     file(s), named per <see cref="DownloadDescriptor"/>'s declared relative install paths.
    ///     Never <see cref="SpeechModelStore"/>'s <c>current/</c> directory - this method must
    ///     never be given, and must never touch, installed content.
    /// </param>
    /// <param name="cancellationToken">A token to observe while unpacking.</param>
    /// <returns>A task that completes once unpacking finishes.</returns>
    /// <remarks>
    ///     Override this only when the model's declared download payload is an archive (zip/tar)
    ///     or otherwise needs post-download processing before use (e.g. expanding a zip and
    ///     removing the original archive file, leaving only the files the model's engine-config
    ///     member will later reference). A model whose payload is already directly usable (the
    ///     common single-file case) does not need to implement this at all.
    ///     <see cref="SpeechModelDownloader"/> invokes this after every declared file has been
    ///     fetched and its checksum verified, and before the staging directory is handed to
    ///     <see cref="SpeechModelStore"/> for the atomic swap - an exception thrown here is
    ///     handled identically to a download failure: nothing is installed, and any prior
    ///     successful install of the same model is left completely untouched.
    /// </remarks>
    Task InstallAsync(string stagedFilesDirectory, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    ///     Applies this model's own text normalization/correction to a string before it is passed
    ///     to the model's inference engine. The default implementation returns the input
    ///     unchanged.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text; by default, <paramref name="text"/> unchanged.</returns>
    /// <remarks>
    ///     Per this library's per-model "own text-normalization / correction (e.g. punctuation
    ///     restoration)" responsibility. This pass defines only the hook's existence and identity
    ///     default; the actual normalization *content* for any real model is a Phase 4 synthesis
    ///     concern, per this library's synthesis-subsystem scope - this member exists now
    ///     purely so the contract shape matches the approved plan and does not need a breaking
    ///     change later.
    /// </remarks>
    string NormalizeText(string text) => text;

    /// <summary>
    ///     Gets the model's declared license name or identifier, for display in a host UI and for
    ///     programmatic discovery without parsing <see cref="DisplayName"/>. The default
    ///     implementation returns <c>"Unknown"</c>.
    /// </summary>
    /// <remarks>
    ///     Never null or empty. When a model's license is not certainly known (for example
    ///     because the upstream model card could not be independently confirmed), that
    ///     uncertainty must be encoded directly in the string itself (for example
    ///     <c>"Apache-2.0 (likely)"</c>) rather than silently upgraded to a certain claim - a
    ///     consumer relying on this value for a legally significant decision must be able to see
    ///     the uncertainty in the value it reads, not just in this class's own XML documentation.
    /// </remarks>
    string LicenseName => "Unknown";

    /// <summary>
    ///     Gets the canonical URL to the full text of this model's declared license
    ///     (<see cref="LicenseName"/>), when one is known. The default implementation returns
    ///     <see langword="null"/>.
    /// </summary>
    /// <remarks>
    ///     Null when no canonical license URL is known or applicable - for example while
    ///     <see cref="LicenseName"/> is still the <c>"Unknown"</c> default. When non-null, this
    ///     is expected to resolve to the license's own authoritative text (for example an
    ///     SPDX-canonical license URL, or a model owner's own published license agreement), not a
    ///     third-party summary or mirror.
    /// </remarks>
    Uri? LicenseUrl => null;
}
