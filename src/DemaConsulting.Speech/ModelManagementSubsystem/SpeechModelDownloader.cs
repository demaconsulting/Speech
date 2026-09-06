using System.Collections.Concurrent;
using System.Security.Cryptography;
using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Orchestrates a single model's download: fetching its declared file(s) through an
///     <see cref="IModelDownloadClient"/>, verifying each against its declared SHA-256 checksum,
///     and handing a fully verified result to <see cref="SpeechModelStore"/> for atomic install.
/// </summary>
/// <remarks>
///     Per architecture.md's "download integrity and progress" decision, downloads of different
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
    public SpeechModelDownloader(
        SpeechModelStore store,
        IModelDownloadClient? client = null,
        ISpeechDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _ownsClient = client is null;
        _client = client ?? new HttpModelDownloadClient();
        _diagnostics = diagnostics ?? NullSpeechDiagnostics.Instance;
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
    ///     or the honest failure reason (checksum mismatch or another download/I/O failure) when
    ///     it was not. Never reports success for a corrupted, partial, or checksum-mismatched
    ///     download.
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
    ///     or the honest failure reason (checksum mismatch, an install-hook failure, or another
    ///     download/I/O failure) when it was not. Never reports success for a corrupted, partial,
    ///     or checksum-mismatched download, nor for one whose <see cref="ISpeechModel.InstallAsync"/>
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
                var destinationPath = Path.Combine(stagingDirectory, file.RelativeInstallPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? stagingDirectory);

                await FetchFileAsync(
                        file,
                        destinationPath,
                        fileIndex,
                        descriptor.Files.Count,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!await VerifyChecksumAsync(destinationPath, file.Sha256Checksum, cancellationToken).ConfigureAwait(false))
                {
                    _diagnostics.Report(
                        SpeechDiagnosticLevel.Error,
                        "ModelManagementSubsystem",
                        $"Checksum mismatch downloading model '{modelId}', file '{file.RelativeInstallPath}'.");
                    SpeechModelStore.AbandonStaging(stagingDirectory);
                    return new SpeechModelDownloadResult(SpeechModelDownloadOutcome.ChecksumMismatch);
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
        catch (OperationCanceledException)
        {
            SpeechModelStore.AbandonStaging(stagingDirectory);
            throw;
        }
        catch (Exception ex)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                "ModelManagementSubsystem",
                $"Failed to download model '{modelId}': {ex.Message}");
            SpeechModelStore.AbandonStaging(stagingDirectory);
            return new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Failed, ex);
        }
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
    private async Task FetchFileAsync(
        SpeechModelDownloadFile file,
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
        await _client.DownloadAsync(file.Uri, destinationStream, fileProgress, cancellationToken)
            .ConfigureAwait(false);
    }

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
    ///     A transport or I/O failure prevented the download from completing; nothing was
    ///     installed, and any prior successful install of the same model is untouched.
    /// </summary>
    Failed,
}

/// <summary>
///     The outcome of one <c>SpeechModelDownloader.DownloadAsync</c> call.
/// </summary>
/// <param name="Outcome">Why the download did or did not result in an installed model.</param>
/// <param name="Error">
///     The underlying exception when <see cref="Outcome"/> is
///     <see cref="SpeechModelDownloadOutcome.Failed"/>; otherwise, <see langword="null"/>.
/// </param>
public sealed record SpeechModelDownloadResult(SpeechModelDownloadOutcome Outcome, Exception? Error = null);
