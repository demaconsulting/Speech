using System.Collections.Concurrent;
using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Enumerates the library's known/compiled-in models alongside each one's current install
///     state, and orchestrates downloading a known model through <see cref="SpeechModelStore"/>
///     and <see cref="SpeechModelDownloader"/>.
/// </summary>
/// <remarks>
///     Per architecture.md's "model catalog and download are new capabilities" decision, this
///     type composes the storage/download machinery from Sub-phase 2a with a compiled-in list
///     of known models. Phase 7a populated <see cref="KnownModels"/> with the library's first
///     two real, production <see cref="IRecognitionModel"/> implementations -
///     <see cref="SherpaOnnxZipformerEnRecognitionModel"/> and
///     <see cref="SherpaOnnxNemotronStreamingEnRecognitionModel"/> - resolving the honest "zero
///     real model classes yet" registry every earlier sub-phase shipped. Phase 7b (this update)
///     completes the catalog with the library's first real, production
///     <see cref="ISynthesisModel"/> implementation,
///     <see cref="SherpaOnnxVitsLibriTtsEnglishSynthesisModel"/>, so every model role the library
///     defines now has at least one concrete, shippable backing class.
///     <para>
///     <see cref="SpeechModelStore"/> itself only distinguishes installed/not-installed; this
///     catalog layers <see cref="SpeechModelState.Downloading"/> and
///     <see cref="SpeechModelState.FailedOrCorrupt"/> on top by tracking, in memory, which model
///     ids currently have a <see cref="DownloadAsync"/> call in flight and which model ids' most
///     recent attempt did not result in an installed model. This in-memory tracking is scoped to
///     one <see cref="SpeechModelCatalog"/> instance's lifetime; a fresh instance (for example
///     after an application restart) reports <see cref="SpeechModelState.NotDownloaded"/> for a
///     model whose only history is a prior failed attempt, since no durable "last attempt
///     failed" record exists on disk - this is an honest degradation, not a defect, consistent
///     with the fixed, small state set architecture.md defines.
///     </para>
/// </remarks>
public sealed class SpeechModelCatalog : IDisposable
{
    /// <summary>
    ///     Gets the library's compiled-in registry of known models, available for download.
    /// </summary>
    /// <remarks>
    ///     Phase 7a populated this registry with the library's first two real, production
    ///     <see cref="IRecognitionModel"/> implementations -
    ///     <see cref="SherpaOnnxZipformerEnRecognitionModel"/> and
    ///     <see cref="SherpaOnnxNemotronStreamingEnRecognitionModel"/> - resolving the "empty
    ///     catalog" limitation every earlier pass through this type documented as accepted and
    ///     deliberate. Phase 7b (this update) adds the library's first real, production
    ///     <see cref="ISynthesisModel"/> implementation,
    ///     <see cref="SherpaOnnxVitsLibriTtsEnglishSynthesisModel"/>, completing the catalog with
    ///     at least one shippable model of every role the library defines. Phase 10 adds a second
    ///     synthesis model, <see cref="SherpaOnnxKokoroEnglishSynthesisModel"/>, a multi-speaker
    ///     English model whose <c>ISynthesisModel.ResolveSpeakerId</c> hook and declared
    ///     <see cref="ChoiceParameter"/> give hosts a real, working voice-selection capability.
    /// </remarks>
    public static IReadOnlyList<ISpeechModel> KnownModels { get; } =
    [
        new SherpaOnnxZipformerEnRecognitionModel(),
        new SherpaOnnxNemotronStreamingEnRecognitionModel(),
        new SherpaOnnxVitsLibriTtsEnglishSynthesisModel(),
        new SherpaOnnxKokoroEnglishSynthesisModel(),
    ];

    /// <summary>The compiled-in models this catalog instance enumerates.</summary>
    private readonly IReadOnlyList<ISpeechModel> _knownModels;

    /// <summary>The store used to determine each known model's installed/not-installed state.</summary>
    private readonly SpeechModelStore _store;

    /// <summary>The downloader used to fetch, verify, and install a known model on request.</summary>
    private readonly SpeechModelDownloader _downloader;

    /// <summary>The set of model ids with a <see cref="DownloadAsync"/> call currently in flight.</summary>
    private readonly ConcurrentDictionary<string, byte> _downloadingModelIds = new(StringComparer.Ordinal);

    /// <summary>The set of model ids whose most recent download attempt did not install the model.</summary>
    private readonly ConcurrentDictionary<string, byte> _failedModelIds = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelCatalog"/> class using the
    ///     library's compiled-in <see cref="KnownModels"/>, a real <see cref="SpeechModelStore"/>,
    ///     and a real <see cref="HttpModelDownloadClient"/>.
    /// </summary>
    /// <param name="options">
    ///     Optional host-configured storage options, or <see langword="null"/> to use the
    ///     default per-user storage root. Forwarded to the internally created
    ///     <see cref="SpeechModelStore"/>.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural download failures to, or <see langword="null"/> to use
    ///     <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <remarks>Never throws; composition always succeeds, consistent with architecture.md.</remarks>
    public SpeechModelCatalog(SpeechModelStoreOptions? options = null, ISpeechDiagnostics? diagnostics = null)
        : this(KnownModels, new SpeechModelStore(options), null, diagnostics)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelCatalog"/> class with an
    ///     injected known-model list, store, and download client, for deterministic testing
    ///     without the compiled-in registry or a real network.
    /// </summary>
    /// <param name="knownModels">
    ///     The known models this catalog instance enumerates. Must not be null; may be empty.
    /// </param>
    /// <param name="store">The store used to determine each model's installed/not-installed state. Must not be null.</param>
    /// <param name="client">
    ///     An optional download seam, or <see langword="null"/> to create and own a default
    ///     <see cref="HttpModelDownloadClient"/> internally.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural download failures to, or <see langword="null"/> to use
    ///     <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="knownModels"/> or <paramref name="store"/> is <see langword="null"/>.
    /// </exception>
    internal SpeechModelCatalog(
        IReadOnlyList<ISpeechModel> knownModels,
        SpeechModelStore store,
        IModelDownloadClient? client,
        ISpeechDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(knownModels);
        ArgumentNullException.ThrowIfNull(store);

        _knownModels = knownModels;
        _store = store;
        _downloader = new SpeechModelDownloader(store, client, diagnostics);
    }

    /// <summary>
    ///     Enumerates every known model alongside its current install state.
    /// </summary>
    /// <returns>
    ///     A snapshot list of <see cref="SpeechModelDescriptor"/>, one per entry in the known
    ///     model list this catalog was constructed with, in the same order. Empty when no models
    ///     are known.
    /// </returns>
    /// <remarks>Never throws; a missing/unreadable store manifest degrades to an honest state, not an error.</remarks>
    public IReadOnlyList<SpeechModelDescriptor> Enumerate() =>
        _knownModels
            .Select(model => new SpeechModelDescriptor(model, GetState(model.Id)))
            .ToList();

    /// <summary>
    ///     Determines a single model id's current install state.
    /// </summary>
    /// <param name="modelId">The model identifier to query.</param>
    /// <returns>
    ///     <see cref="SpeechModelState.Downloading"/> when a <see cref="DownloadAsync"/> call for
    ///     <paramref name="modelId"/> is currently in flight on this catalog instance;
    ///     otherwise <see cref="SpeechModelState.Downloaded"/> when <see cref="SpeechModelStore.IsInstalled"/>
    ///     reports it installed; otherwise <see cref="SpeechModelState.FailedOrCorrupt"/> when this
    ///     catalog instance most recently observed a failed attempt for it; otherwise
    ///     <see cref="SpeechModelState.NotDownloaded"/>.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelId"/> is null, empty, or not a valid directory name.</exception>
    /// <remarks>Never throws for an honest state query; only argument validation can fail.</remarks>
    public SpeechModelState GetState(string modelId)
    {
        // Validate modelId the same way the store does, so an invalid id fails the same way for
        // every catalog member rather than silently reporting NotDownloaded for a typo'd id.
        _store.GetModelDirectory(modelId);

        if (_downloadingModelIds.ContainsKey(modelId))
        {
            return SpeechModelState.Downloading;
        }

        if (_store.IsInstalled(modelId))
        {
            return SpeechModelState.Downloaded;
        }

        return _failedModelIds.ContainsKey(modelId)
            ? SpeechModelState.FailedOrCorrupt
            : SpeechModelState.NotDownloaded;
    }

    /// <summary>
    ///     Downloads, verifies, and atomically installs a known model, tracking its state as
    ///     <see cref="SpeechModelState.Downloading"/> for the duration of the call.
    /// </summary>
    /// <param name="modelId">
    ///     The identifier of a model present in this catalog's known-model list. Must match the
    ///     <see cref="ISpeechModel.Id"/> of one of the models returned by <see cref="Enumerate"/>.
    /// </param>
    /// <param name="progress">An optional progress sink invoked as the model's file(s) transfer.</param>
    /// <param name="cancellationToken">A token that, when canceled, aborts the in-progress download.</param>
    /// <returns>
    ///     The <see cref="SpeechModelDownloadResult"/> describing whether the model was
    ///     installed, or the honest failure reason when it was not.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="modelId"/> does not match any known model. This is an
    ///     explicit, user-invoked action (never composition or enumeration), so throwing here is
    ///     consistent with architecture.md's "nothing throws at composition" carve-out.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public async Task<SpeechModelDownloadResult> DownloadAsync(
        string modelId,
        IProgress<SpeechModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var model = _knownModels.FirstOrDefault(candidate => string.Equals(candidate.Id, modelId, StringComparison.Ordinal));
        if (model is null)
        {
            throw new ArgumentException(
                $"'{modelId}' does not match any model in this catalog's known-model list.",
                nameof(modelId));
        }

        // Mark the model as downloading for the duration of this call, and clear any stale
        // failed marker up front - a fresh attempt deserves a fresh chance to report success.
        _downloadingModelIds[modelId] = 0;
        _failedModelIds.TryRemove(modelId, out _);
        try
        {
            // Use the ISpeechModel-aware overload (not the bare modelId/descriptor form) so the
            // model's own InstallAsync hook genuinely runs on its verified staged files before
            // the atomic swap - required for any model whose declared payload is an archive.
            var result = await _downloader
                .DownloadAsync(model, progress, cancellationToken)
                .ConfigureAwait(false);

            if (result.Outcome != SpeechModelDownloadOutcome.Installed)
            {
                _failedModelIds[modelId] = 0;
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not a "failed/corrupt" outcome - the caller asked to stop, and the
            // store's own cleanup already guarantees nothing was installed; report NotDownloaded
            // going forward rather than a misleading FailedOrCorrupt.
            throw;
        }
        catch
        {
            _failedModelIds[modelId] = 0;
            throw;
        }
        finally
        {
            _downloadingModelIds.TryRemove(modelId, out _);
        }
    }

    /// <summary>
    ///     Disposes the underlying <see cref="SpeechModelDownloader"/>.
    /// </summary>
    public void Dispose() => _downloader.Dispose();
}
