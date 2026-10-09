using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Orchestrates a single model's download: fetching its declared file(s) through an
///     <see cref="IModelDownloadClient"/>, verifying each against its declared SHA-256 checksum,
///     and handing a fully verified result to <see cref="SpeechModelStore"/> for atomic install.
/// </summary>
/// <remarks>
///     Per this library's "download integrity and progress" decision, downloads of different
///     models run fully concurrently with no pool limit, since each model is staged and installed
///     under its own isolated subtree in <see cref="SpeechModelStore"/>. A per-model-id lock
///     serializes only two concurrent <c>DownloadAsync</c> calls for the *same* model id - the
///     minimal guard needed to avoid racing the store's atomic <c>Directory.Move</c>-based install
///     swap for that model - so a second concurrent call for the same model id queues behind the
///     first while calls for different model ids proceed in parallel. A corrupted, partial, or
///     checksum-mismatched download is never handed to the store's atomic-install step, so a prior
///     successful install of the same model (if any) is left completely untouched and still
///     reports installed - a failed repair attempt never regresses an already-working model.
/// </remarks>
public sealed class SpeechModelDownloader : IDisposable
{
    /// <summary>The store used to stage, atomically install, and clean up this downloader's results.</summary>
    private readonly SpeechModelStore _store;

    /// <summary>The seam used to fetch each declared file's bytes.</summary>
    private readonly IModelDownloadClient _client;

    /// <summary>The sink used to report structural download failures.</summary>
    private readonly ISpeechDiagnostics _diagnostics;

    /// <summary>
    ///     Per-model-id locks that serialize only concurrent <c>DownloadAsync</c> calls sharing the
    ///     same model id, keyed by model id. Different model ids resolve to different semaphores
    ///     (created on first use via <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey,System.Func{TKey,TValue})"/>)
    ///     and so never block one another, allowing unlimited cross-model concurrency.
    /// </summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _perModelLocks = new(StringComparer.Ordinal);

    /// <summary>Whether <see cref="_client"/> was created internally and so must be disposed by this instance.</summary>
    private readonly bool _ownsClient;

    /// <summary>
    ///     The mirror every declared file's effective download URI is resolved beneath, or
    ///     <see langword="null"/> to fetch every file from exactly the URI its own
    ///     <see cref="SpeechModelDownloadFile.Uri"/> declares.
    /// </summary>
    private readonly DownloadMirror? _mirror;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelDownloader"/> class.
    /// </summary>
    /// <param name="store">The store used to stage and atomically install downloaded models.</param>
    /// <param name="client">
    ///     An optional download seam, or <see langword="null"/> to create and own a default
    ///     <see cref="HttpModelDownloadClient"/> internally. Tests substitute a fake here to
    ///     exercise queueing, checksum verification, and atomic-swap logic deterministically
    ///     without any real network access.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural download failures to, or <see langword="null"/> to use
    ///     <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     This overload's signature is never changed (see the
    ///     <see cref="SpeechModelDownloader(SpeechModelStore,IModelDownloadClient?,ISpeechDiagnostics?,SpeechModelDownloaderOptions?)"/>
    ///     overload's remarks for why) - existing compiled callers of this exact three-parameter
    ///     constructor keep resolving to it unchanged. Use the four-parameter overload to also
    ///     supply <see cref="SpeechModelDownloaderOptions"/>.
    /// </remarks>
    public SpeechModelDownloader(
        SpeechModelStore store,
        IModelDownloadClient? client = null,
        ISpeechDiagnostics? diagnostics = null)
        : this(store, client, diagnostics, options: null)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelDownloader"/> class with
    ///     host-configurable download options.
    /// </summary>
    /// <param name="store">The store used to stage and atomically install downloaded models.</param>
    /// <param name="client">
    ///     An optional download seam, or <see langword="null"/> to create and own a default
    ///     <see cref="HttpModelDownloadClient"/> internally. Tests substitute a fake here to
    ///     exercise queueing, checksum verification, and atomic-swap logic deterministically
    ///     without any real network access.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural download failures to, or <see langword="null"/> to use
    ///     <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <param name="options">
    ///     Host-configurable download options, or <see langword="null"/> to fetch every file from
    ///     exactly the URI its own <see cref="SpeechModelDownloadFile.Uri"/> declares -
    ///     byte-for-byte identical to this type's behavior before
    ///     <see cref="SpeechModelDownloaderOptions"/> existed. See
    ///     <see cref="SpeechModelDownloaderOptions.Mirror"/> for the one behavior this parameter
    ///     currently controls.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Declared as a distinct overload - rather than a fourth parameter appended to the
    ///     pre-existing three-parameter constructor - because an appended optional parameter
    ///     would still be <em>binary</em>-incompatible: an existing compiled caller's call site
    ///     to the three-argument constructor is bound to that exact constructor's metadata token,
    ///     which would no longer exist once a fourth parameter were added to it. None of this
    ///     overload's parameters carry a default value, so a one-, two-, or three-argument
    ///     constructor call can only ever resolve to the original overload above, never this one -
    ///     keeping both source- and binary-compatible for every pre-existing call shape.
    /// </remarks>
    public SpeechModelDownloader(
        SpeechModelStore store,
        IModelDownloadClient? client,
        ISpeechDiagnostics? diagnostics,
        SpeechModelDownloaderOptions? options)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _ownsClient = client is null;
        _client = client ?? new HttpModelDownloadClient(httpClient: null, options?.Mirror);
        _diagnostics = diagnostics ?? NullSpeechDiagnostics.Instance;
        _mirror = options?.Mirror;
    }

    /// <summary>
    ///     Downloads, verifies, and atomically installs one model's declared files.
    /// </summary>
    /// <param name="modelId">The model identifier to install under in the store. Must be a valid directory name.</param>
    /// <param name="descriptor">The ordered files to download and their expected checksums.</param>
    /// <param name="progress">
    ///     An optional progress sink invoked as each file transfers, with
    ///     <see cref="SpeechModelDownloadProgress.FileIndex"/>/<see cref="SpeechModelDownloadProgress.FileCount"/>
    ///     rewritten to reflect each file's true position within <paramref name="descriptor"/>.
    /// </param>
    /// <param name="cancellationToken">
    ///     A token that, when canceled, aborts the in-progress download after the current chunk,
    ///     discards its staging directory, and propagates as <see cref="OperationCanceledException"/>.
    ///     A token canceled while queued behind another download of the same model id also aborts before this
    ///     download's own work begins.
    /// </param>
    /// <returns>
    ///     A <see cref="SpeechModelDownloadResult"/> describing whether the model was installed,
    ///     or the honest, specifically classified failure reason when it was not: a checksum
    ///     mismatch, network/mirror blocking (TLS interception or DNS/connection failure, or an
    ///     internal request timeout), a real HTTP error response, a local disk/I/O failure, or
    ///     the generic fallback for any other failure - see <see cref="SpeechModelDownloadOutcome"/>'s
    ///     own members for the precise classification rules. Never reports success for a
    ///     corrupted, partial, or checksum-mismatched download.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="descriptor"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelId"/> is null, empty, or not a valid directory name.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has already been disposed.</exception>
    public async Task<SpeechModelDownloadResult> DownloadAsync(
        string modelId,
        SpeechModelDownloadDescriptor descriptor,
        IProgress<SpeechModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        // Validate modelId (and thereby fail fast, before ever touching the queue or disk) by
        // resolving its directory - GetModelDirectory performs the same validation the store
        // itself relies on everywhere else.
        _store.GetModelDirectory(modelId);

        // Serialize only against other concurrent calls for this same model id; calls for a
        // different model id resolve to a different semaphore and proceed fully in parallel.
        var modelLock = _perModelLocks.GetOrAdd(modelId, static _ => new SemaphoreSlim(1, 1));
        await modelLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await DownloadWhileLockedAsync(modelId, descriptor, model: null, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            modelLock.Release();
        }
    }

    /// <summary>
    ///     Downloads, verifies, atomically installs, and runs the model's own
    ///     <see cref="ISpeechModel.InstallAsync"/> hook for one model's declared files.
    /// </summary>
    /// <param name="model">
    ///     The model to install, supplying both its <see cref="ISpeechModel.DownloadDescriptor"/>
    ///     (via <see cref="ISpeechModel.Id"/>/<see cref="ISpeechModel.DownloadDescriptor"/>) and
    ///     its own <see cref="ISpeechModel.InstallAsync"/> hook, invoked on the verified staged
    ///     files before the atomic swap into <c>current/</c>.
    /// </param>
    /// <param name="progress">
    ///     An optional progress sink invoked as each file transfers, with
    ///     <see cref="SpeechModelDownloadProgress.FileIndex"/>/<see cref="SpeechModelDownloadProgress.FileCount"/>
    ///     rewritten to reflect each file's true position within <paramref name="model"/>'s
    ///     <see cref="ISpeechModel.DownloadDescriptor"/>.
    /// </param>
    /// <param name="cancellationToken">
    ///     A token that, when canceled, aborts the in-progress download after the current chunk,
    ///     discards its staging directory, and propagates as <see cref="OperationCanceledException"/>.
    ///     A token canceled while queued behind another download of the same model id also aborts before this
    ///     download's own work begins.
    /// </param>
    /// <returns>
    ///     A <see cref="SpeechModelDownloadResult"/> describing whether the model was installed,
    ///     or the honest, specifically classified failure reason when it was not: a checksum
    ///     mismatch, an install-hook failure, network/mirror blocking, a real HTTP error
    ///     response, a local disk/I/O failure, or the generic fallback for any other failure -
    ///     see <see cref="SpeechModelDownloadOutcome"/>'s own members for the precise
    ///     classification rules. Never reports success for a corrupted, partial, or
    ///     checksum-mismatched download, nor for one whose <see cref="ISpeechModel.InstallAsync"/>
    ///     hook failed.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="ISpeechModel.Id"/> is null, empty, or not a valid directory name.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has already been disposed.</exception>
    /// <remarks>
    ///     The existing <see cref="DownloadAsync(string,SpeechModelDownloadDescriptor,IProgress{SpeechModelDownloadProgress}?,CancellationToken)"/>
    ///     overload's behavior is completely unchanged by this overload's addition: it internally
    ///     calls the same private core with a <see langword="null"/> model, so a model's
    ///     <see cref="ISpeechModel.InstallAsync"/> hook only ever runs when this
    ///     <see cref="ISpeechModel"/>-aware overload is used, as is the case for every
    ///     <see cref="SpeechModelCatalog"/>-driven download.
    /// </remarks>
    public async Task<SpeechModelDownloadResult> DownloadAsync(
        ISpeechModel model,
        IProgress<SpeechModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        // Validate the model's id (and thereby fail fast, before ever touching the queue or
        // disk) by resolving its directory - GetModelDirectory performs the same validation the
        // store itself relies on everywhere else.
        _store.GetModelDirectory(model.Id);

        // Serialize only against other concurrent calls for this same model id; calls for a
        // different model id resolve to a different semaphore and proceed fully in parallel.
        var modelLock = _perModelLocks.GetOrAdd(model.Id, static _ => new SemaphoreSlim(1, 1));
        await modelLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await DownloadWhileLockedAsync(model.Id, model.DownloadDescriptor, model, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            modelLock.Release();
        }
    }

    /// <summary>
    ///     Performs the actual fetch/verify/install sequence for one model, assuming the
    ///     model's own per-model-id lock is already held.
    /// </summary>
    /// <param name="modelId">The model identifier to install under in the store.</param>
    /// <param name="descriptor">The ordered files to download and their expected checksums.</param>
    /// <param name="model">
    ///     The model whose <see cref="ISpeechModel.InstallAsync"/> hook should be invoked on the
    ///     verified staged files before the atomic swap, or <see langword="null"/> when the
    ///     caller used the bare <c>(modelId, descriptor, ...)</c> overload, in which case no
    ///     install hook runs at all.
    /// </param>
    /// <param name="progress">An optional progress sink invoked as each file transfers.</param>
    /// <param name="cancellationToken">A token that, when canceled, aborts the in-progress download.</param>
    private async Task<SpeechModelDownloadResult> DownloadWhileLockedAsync(
        string modelId,
        SpeechModelDownloadDescriptor descriptor,
        ISpeechModel? model,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Already-installed fast path: checked as the very first statement while holding this
        // model id's lock, so it stays race-safe with a concurrent first-time install of the
        // same id (a second caller either observes the already-completed install here, or
        // queues behind the first caller's in-flight install and observes it here once that
        // caller releases the lock). Skips the network/staging directory entirely, keeping
        // DownloadAsync a genuinely cheap no-op to call unconditionally on every launch. Still
        // opportunistically retries leftover cleanup (a cheap directory enumeration/delete, no
        // network or hashing) so a model that stays "already installed" across every future
        // launch does not leak leftover staging/replaced directories indefinitely.
        if (_store.IsInstalled(modelId))
        {
            _store.CleanUpLeftovers(modelId);
            _diagnostics.Report(
                SpeechDiagnosticLevel.Info,
                "ModelManagementSubsystem",
                $"Model '{modelId}' is already installed; DownloadAsync is a no-op.");
            return new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed);
        }

        // Opportunistically retry cleanup of any leftovers from a prior interrupted attempt
        // before starting a new one, per the store's non-blocking cleanup policy.
        _store.CleanUpLeftovers(modelId);

        var (operationId, stagingDirectory) = _store.BeginStaging(modelId);
        try
        {
            long totalSizeBytes = 0;
            for (var fileIndex = 0; fileIndex < descriptor.Files.Count; fileIndex++)
            {
                var file = descriptor.Files[fileIndex];
                var destinationPath = Path.Join(stagingDirectory, file.RelativeInstallPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? stagingDirectory);

                var effectiveUri = ResolveEffectiveUri(file.Uri, _mirror, modelId, file.RelativeInstallPath);
                await FetchFileAsync(
                        effectiveUri,
                        destinationPath,
                        fileIndex,
                        descriptor.Files.Count,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!await VerifyChecksumAsync(destinationPath, file.Sha256Checksum, cancellationToken).ConfigureAwait(false))
                {
                    var checksumError = new InvalidOperationException(
                        $"Checksum mismatch downloading model '{modelId}', file '{file.RelativeInstallPath}'.");
                    _diagnostics.Report(
                        SpeechDiagnosticLevel.Error,
                        "ModelManagementSubsystem",
                        checksumError.Message);
                    SpeechModelStore.AbandonStaging(stagingDirectory);
                    return new SpeechModelDownloadResult(SpeechModelDownloadOutcome.ChecksumMismatch, checksumError);
                }

                totalSizeBytes += new FileInfo(destinationPath).Length;
            }

            // Every file verified - let the model unpack/process its own staged files (if it
            // declares an install hook) before the atomic current/ swap. The default
            // ISpeechModel.InstallAsync implementation is a no-op, so this is always safe to
            // call even for a model that needs no post-download processing at all.
            if (model is not null)
            {
                await model.InstallAsync(stagingDirectory, cancellationToken).ConfigureAwait(false);

                // The install hook may have expanded, shrunk, added, or removed files (e.g. by
                // unpacking an archive and deleting it), so recompute the installed size from
                // the staging directory's actual post-install file tree rather than trusting the
                // pre-unpack per-file download sizes, keeping the manifest's reported size honest.
                totalSizeBytes = ComputeDirectorySizeBytes(stagingDirectory);
            }

            // Hand off the fully verified (and, if applicable, installed) staging directory to
            // the store for the atomic current/ swap.
            _store.CompleteInstall(modelId, operationId, stagingDirectory, totalSizeBytes);
            return new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Only a genuine caller cancellation (the supplied token itself was signaled) takes
            // this branch and propagates unchanged. An OperationCanceledException/
            // TaskCanceledException NOT caused by this token (for example HttpClient's own
            // internal request timeout) falls through to the catch below instead, where
            // ClassifyFailure honestly reports it as NetworkBlocked rather than being mistaken
            // for a caller-driven cancellation.
            SpeechModelStore.AbandonStaging(stagingDirectory);
            throw;
        }
        catch (Exception ex)
        {
            // Intentionally broad: this top-level download/install boundary must convert any
            // non-cancellation network, checksum, archive, or file-system failure into an honest,
            // specifically classified outcome after cleaning up the staging directory. See
            // ClassifyFailure for exactly which exception shapes map to which outcome.
            var outcome = ClassifyFailure(ex);

            // HttpFetchTimeoutException only exists to let ClassifyFailure narrowly scope its
            // NetworkBlocked classification to the HTTP fetch step's own internal timeout (see
            // FetchFileAsync) - a caller inspecting SpeechModelDownloadResult.Error should see
            // the original TaskCanceledException it wraps, never this internal-only marker type.
            var reportedException = ex is HttpFetchTimeoutException { InnerException: { } original }
                ? original
                : ex;
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                "ModelManagementSubsystem",
                $"Failed to download model '{modelId}': {reportedException.Message}");
            SpeechModelStore.AbandonStaging(stagingDirectory);
            return new SpeechModelDownloadResult(outcome, reportedException);
        }
    }

    /// <summary>
    ///     Classifies a download/install failure into the most honest, specific
    ///     <see cref="SpeechModelDownloadOutcome"/> it matches, so a host can distinguish a
    ///     network/mirror-blocked failure from a real HTTP error response from a local disk/
    ///     permission failure, rather than collapsing every failure into one generic
    ///     <see cref="SpeechModelDownloadOutcome.Failed"/>.
    /// </summary>
    /// <param name="ex">
    ///     The exception to classify. Never an <see cref="OperationCanceledException"/> caused by
    ///     the caller's own <see cref="CancellationToken"/> - that case is filtered out by the
    ///     guarded <c>catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)</c>
    ///     clause before this method is ever called.
    /// </param>
    /// <returns>
    ///     <see cref="SpeechModelDownloadOutcome.NetworkBlocked"/> for an
    ///     <see cref="HttpRequestException"/> whose <see cref="Exception.InnerException"/> is an
    ///     <see cref="AuthenticationException"/> (a TLS/certificate failure, as seen when a
    ///     network appliance intercepts and re-signs TLS traffic with an untrusted root) or a
    ///     <see cref="SocketException"/> (DNS resolution failure, connection refused, or host
    ///     unreachable), or for an <see cref="HttpFetchTimeoutException"/> (by construction, only
    ///     ever thrown by <see cref="FetchFileAsync"/>'s own guarded catch clause around the HTTP
    ///     fetch step itself - never a genuine caller cancellation, and never a
    ///     <see cref="TaskCanceledException"/> thrown by <see cref="ISpeechModel.InstallAsync"/> or
    ///     a host-injected <see cref="IModelDownloadClient"/> for an unrelated reason, which
    ///     instead fall through to the generic <see cref="SpeechModelDownloadOutcome.Failed"/>
    ///     fallback below); <see cref="SpeechModelDownloadOutcome.HttpError"/> for an
    ///     <see cref="HttpRequestException"/> with a non-<see langword="null"/>
    ///     <see cref="HttpRequestException.StatusCode"/> (a real HTTP response was received, for
    ///     example <c>404</c>/<c>403</c>/a <c>5xx</c> server error); <see cref="SpeechModelDownloadOutcome.IoFailure"/>
    ///     for an <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> not
    ///     otherwise matched above (disk full, permission denied, path too long); otherwise,
    ///     <see cref="SpeechModelDownloadOutcome.Failed"/> as the final, generic fallback for any
    ///     unmatched exception type - this classification never throws, and an unmatched
    ///     exception still produces <see cref="SpeechModelDownloadOutcome.Failed"/> with the
    ///     exception preserved in <see cref="SpeechModelDownloadResult.Error"/>, exactly as
    ///     before this richer classification existed.
    /// </returns>
    private static SpeechModelDownloadOutcome ClassifyFailure(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: not null } => SpeechModelDownloadOutcome.HttpError,
        HttpRequestException { InnerException: AuthenticationException or SocketException } =>
            SpeechModelDownloadOutcome.NetworkBlocked,
        HttpFetchTimeoutException => SpeechModelDownloadOutcome.NetworkBlocked,
        IOException or UnauthorizedAccessException => SpeechModelDownloadOutcome.IoFailure,
        _ => SpeechModelDownloadOutcome.Failed,
    };

    /// <summary>
    ///     Resolves the effective request URI for one declared file: the file's own declared
    ///     <paramref name="originalUri"/> unchanged when no mirror is configured, or a
    ///     mirror-relative URI built from <paramref name="mirror"/>'s <see cref="DownloadMirror.BaseUri"/>,
    ///     <paramref name="modelId"/>, and <paramref name="relativeInstallPath"/> when one is.
    /// </summary>
    /// <param name="originalUri">The file's own declared source URI.</param>
    /// <param name="mirror">The configured mirror, or <see langword="null"/> to fetch from <paramref name="originalUri"/> unchanged.</param>
    /// <param name="modelId">The model id the file belongs to, used as the mirror-relative path's first segment.</param>
    /// <param name="relativeInstallPath">
    ///     The file's declared relative install path (for example <c>"model.bin"</c> or
    ///     <c>"tokens/vocab.txt"</c>), appended beneath <paramref name="modelId"/> on the mirror.
    /// </param>
    /// <returns>
    ///     <paramref name="originalUri"/> unchanged when <paramref name="mirror"/> is <see langword="null"/>;
    ///     otherwise, <c>{mirror.BaseUri}/{modelId}/{relativeInstallPath}</c>, with
    ///     <paramref name="modelId"/> and each <paramref name="relativeInstallPath"/> segment
    ///     individually percent-escaped and exactly one separating slash between every segment
    ///     regardless of whether <see cref="DownloadMirror.BaseUri"/> itself ends with a
    ///     trailing slash.
    /// </returns>
    /// <remarks>
    ///     Marked <see langword="internal"/> (rather than <see langword="private"/>) specifically
    ///     so <c>DemaConsulting.Speech.Tests</c> can exercise every edge case (trailing-slash
    ///     variations, subdirectories, URL-unsafe characters) directly, via this assembly's
    ///     existing <c>InternalsVisibleTo</c> grant to that test project.
    /// </remarks>
    internal static Uri ResolveEffectiveUri(
        Uri originalUri,
        DownloadMirror? mirror,
        string modelId,
        string relativeInstallPath)
    {
        if (mirror is null)
        {
            return originalUri;
        }

        // Trim exactly one trailing slash from the mirror's base so "https://mirror/" and
        // "https://mirror" both combine identically with the segments that follow.
        var basePath = mirror.BaseUri.ToString().TrimEnd('/');

        // RelativeInstallPath may use either path separator (SpeechModelDownloadFile accepts
        // both when validating "no parent-escaping segments"), so split on both and
        // percent-escape each segment (and modelId) individually before rejoining with '/' -
        // never escape the already-combined string as a whole, which would incorrectly encode
        // the separating slashes themselves.
        var segments = relativeInstallPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var escapedSegments = segments.Select(Uri.EscapeDataString);
        var combined = string.Join('/', [basePath, Uri.EscapeDataString(modelId), .. escapedSegments]);
        return new Uri(combined);
    }

    /// <summary>
    ///     Sums the length of every file within a directory tree, used to recompute a model's
    ///     installed size after its own <see cref="ISpeechModel.InstallAsync"/> hook has run.
    /// </summary>
    /// <param name="directory">The directory tree to sum.</param>
    /// <returns>The total size, in bytes, of every file found under <paramref name="directory"/>.</returns>
    private static long ComputeDirectorySizeBytes(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length);

    /// <summary>
    ///     Fetches one declared file through <see cref="_client"/>, translating its
    ///     single-file-relative progress reports into the caller's overall, multi-file view.
    /// </summary>
    /// <exception cref="HttpFetchTimeoutException">
    ///     Thrown when <see cref="_client"/>'s <c>DownloadAsync</c> call itself throws a
    ///     <see cref="TaskCanceledException"/> not caused by <paramref name="cancellationToken"/>
    ///     (an internal HTTP request timeout) - wrapped so <see cref="ClassifyFailure"/> can
    ///     honestly classify only a genuine HTTP fetch timeout as
    ///     <see cref="SpeechModelDownloadOutcome.NetworkBlocked"/>, without also misclassifying an
    ///     unrelated <see cref="TaskCanceledException"/> thrown by <see cref="ISpeechModel.InstallAsync"/>
    ///     or by a host-injected <see cref="IModelDownloadClient"/> for a non-network reason.
    /// </exception>
    private async Task FetchFileAsync(
        Uri effectiveUri,
        string destinationPath,
        int fileIndex,
        int fileCount,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var fileProgress = progress is null
            ? null
            : new RelayProgress(progress, fileIndex, fileCount);

        await using var destinationStream =
            new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        try
        {
            await _client.DownloadAsync(effectiveUri, destinationStream, fileProgress, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // A TaskCanceledException reaching here, with the caller's own token not canceled,
            // can only be the HTTP fetch's own internal request timeout - wrap it so
            // ClassifyFailure's TaskCanceledException match cannot also accidentally catch an
            // unrelated TaskCanceledException thrown by ISpeechModel.InstallAsync or a
            // host-injected IModelDownloadClient for a non-network reason.
            throw new HttpFetchTimeoutException(ex);
        }
    }

    /// <summary>
    ///     Marks a <see cref="TaskCanceledException"/> that genuinely originated from the HTTP
    ///     fetch step's own internal request timeout (never a genuine caller cancellation - see
    ///     <see cref="FetchFileAsync"/>), so <see cref="ClassifyFailure"/> can classify only this
    ///     specific, narrowly-scoped shape as <see cref="SpeechModelDownloadOutcome.NetworkBlocked"/>.
    /// </summary>
    /// <param name="innerException">The original <see cref="TaskCanceledException"/> this instance wraps.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S3871:Exception types should be \"public\"",
        Justification = "Intentionally private: an internal-only classification marker, never thrown or caught outside this class, and always unwrapped back to its original exception before being exposed to callers.")]
    private sealed class HttpFetchTimeoutException(TaskCanceledException innerException)
        : Exception("The HTTP fetch request timed out.", innerException);

    /// <summary>
    ///     Computes a downloaded file's SHA-256 checksum and compares it against the expected
    ///     value.
    /// </summary>
    /// <param name="path">The downloaded file's path.</param>
    /// <param name="expectedSha256Checksum">The declared SHA-256 checksum to compare against, as a hexadecimal string.</param>
    /// <param name="cancellationToken">A token to observe while hashing.</param>
    /// <returns><see langword="true"/> when the computed checksum matches; otherwise, <see langword="false"/>.</returns>
    private static async Task<bool> VerifyChecksumAsync(
        string path,
        string expectedSha256Checksum,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var actualHash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actualChecksum = Convert.ToHexStringLower(actualHash);
        return string.Equals(actualChecksum, expectedSha256Checksum, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Disposes every per-model-id lock held in <see cref="_perModelLocks"/> and, when this
    ///     instance created it internally, the underlying download client.
    /// </summary>
    public void Dispose()
    {
        foreach (var modelLock in _perModelLocks.Values)
        {
            modelLock.Dispose();
        }

        if (_ownsClient && _client is IDisposable disposableClient)
        {
            disposableClient.Dispose();
        }
    }

    /// <summary>
    ///     Forwards one file's progress reports to the caller's overall <see cref="IProgress{T}"/>,
    ///     rewriting <see cref="SpeechModelDownloadProgress.FileIndex"/>/<see cref="SpeechModelDownloadProgress.FileCount"/>
    ///     to reflect the file's true position within the descriptor.
    /// </summary>
    /// <remarks>
    ///     Implemented as a direct, synchronous <see cref="IProgress{T}"/> adapter rather than by
    ///     wrapping the caller's progress in another <see cref="Progress{T}"/> instance: each
    ///     <see cref="Progress{T}"/> posts its callback independently (via a captured
    ///     <see cref="SynchronizationContext"/> or the thread pool) with no ordering guarantee
    ///     across successive reports, so stacking two such instances would let same-file progress
    ///     reports for one download arrive at the caller out of order. Forwarding synchronously
    ///     here means the order in which <see cref="_client"/> calls <c>Report</c> is exactly the
    ///     order the caller's own <see cref="IProgress{T}"/> receives them.
    /// </remarks>
    private sealed class RelayProgress(
        IProgress<SpeechModelDownloadProgress> target,
        int fileIndex,
        int fileCount) : IProgress<SpeechModelDownloadProgress>
    {
        /// <inheritdoc/>
        public void Report(SpeechModelDownloadProgress value) =>
            target.Report(value with { FileIndex = fileIndex, FileCount = fileCount });
    }
}

/// <summary>
///     Describes why a <c>SpeechModelDownloader.DownloadAsync</c> call did or did not
///     result in an installed model.
/// </summary>
/// <remarks>
///     These members are intentionally declared in this order (<see cref="Failed"/> before the
///     three richer, later-added classifications) so every existing member's ordinal value stays
///     byte-identical to this enum's original three-member shape; <see cref="Failed"/> remains
///     the classification logic's final, generic fallback (a property of
///     <c>SpeechModelDownloader.ClassifyFailure</c>'s behavior, not of declaration order) for any
///     exception shape none of <see cref="NetworkBlocked"/>/<see cref="HttpError"/>/
///     <see cref="IoFailure"/> recognizes.
/// </remarks>
public enum SpeechModelDownloadOutcome
{
    /// <summary>Every declared file was fetched, verified, and atomically installed.</summary>
    Installed,

    /// <summary>
    ///     A downloaded file's SHA-256 checksum did not match its declared value; nothing was
    ///     installed, and any prior successful install of the same model is untouched.
    /// </summary>
    ChecksumMismatch,

    /// <summary>
    ///     A download or install failure occurred whose exception type did not match any of the
    ///     more specific classifications below (<see cref="NetworkBlocked"/>,
    ///     <see cref="HttpError"/>, <see cref="IoFailure"/>) - the final, generic fallback.
    ///     Nothing was installed, and any prior successful install of the same model is
    ///     untouched.
    /// </summary>
    Failed,

    /// <summary>
    ///     The request never reached a real server, or never got a real response, in a way
    ///     consistent with network-level blocking rather than an application-level HTTP error:
    ///     either a TLS/certificate failure (for example a network appliance intercepting and
    ///     re-signing TLS traffic with an untrusted root certificate, surfacing as an
    ///     <c>HttpRequestException</c> wrapping an
    ///     <see cref="System.Security.Authentication.AuthenticationException"/>), a DNS/
    ///     connection-level failure (an <c>HttpRequestException</c> wrapping a
    ///     <see cref="System.Net.Sockets.SocketException"/> - DNS resolution failure, connection
    ///     refused, or host unreachable), or a request timeout that was <em>not</em> caused by
    ///     the caller's own <see cref="CancellationToken"/> (a genuine caller cancellation still
    ///     propagates unchanged as <see cref="OperationCanceledException"/>, never this outcome).
    ///     Nothing was installed, and any prior successful install of the same model is
    ///     untouched.
    /// </summary>
    NetworkBlocked,

    /// <summary>
    ///     A real HTTP response was received from a server, but its status code indicated
    ///     failure (for example <c>404 Not Found</c>, <c>403 Forbidden</c>, or a <c>5xx</c>
    ///     server error) - an <c>HttpRequestException</c> with a non-<see langword="null"/>
    ///     <c>StatusCode</c>. Nothing was installed, and any prior successful install of the
    ///     same model is untouched.
    /// </summary>
    HttpError,

    /// <summary>
    ///     A local file-system failure prevented the download from completing or being staged
    ///     (for example disk full, permission denied, or a path too long) - an
    ///     <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> not otherwise
    ///     classified above. Nothing was installed, and any prior successful install of the same
    ///     model is untouched.
    /// </summary>
    IoFailure,
}

/// <summary>
///     The outcome of one <c>SpeechModelDownloader.DownloadAsync</c> call.
/// </summary>
/// <param name="Outcome">Why the download did or did not result in an installed model.</param>
/// <param name="Error">
///     The underlying/descriptive exception for any non-<see cref="SpeechModelDownloadOutcome.Installed"/>
///     outcome (<see cref="SpeechModelDownloadOutcome.ChecksumMismatch"/>,
///     <see cref="SpeechModelDownloadOutcome.Failed"/>, <see cref="SpeechModelDownloadOutcome.NetworkBlocked"/>,
///     <see cref="SpeechModelDownloadOutcome.HttpError"/>, or <see cref="SpeechModelDownloadOutcome.IoFailure"/>);
///     otherwise, <see langword="null"/>.
/// </param>
public sealed record SpeechModelDownloadResult(SpeechModelDownloadOutcome Outcome, Exception? Error = null);
