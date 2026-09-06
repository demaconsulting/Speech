namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Mockable seam for fetching one downloadable file's bytes to a local destination stream,
///     isolating <see cref="SpeechModelDownloader"/> from any real HTTP client so its
///     queueing/verification/atomic-swap orchestration logic can be unit tested without any real
///     network access.
/// </summary>
/// <remarks>
///     Mirrors the existing <c>IPortAudioApi</c>/<c>IPortAudioStream</c> seam pattern already
///     used for PortAudioSharp2: a small library-owned interface with one real implementation
///     (<see cref="HttpModelDownloadClient"/>) and hand-written or NSubstitute fakes for fast,
///     deterministic tests.
/// </remarks>
public interface IModelDownloadClient
{
    /// <summary>
    ///     Downloads the bytes at <paramref name="sourceUri"/>, writing them to
    ///     <paramref name="destination"/> as they arrive.
    /// </summary>
    /// <param name="sourceUri">
    ///     The absolute HTTP(S) location to download from. Production model downloads always use
    ///     HTTPS; the seam itself is scheme-agnostic so tests can exercise it against a loopback
    ///     HTTP server.
    /// </param>
    /// <param name="destination">
    ///     The stream to write downloaded bytes to. The caller owns this stream's lifetime; this
    ///     method writes to it but does not dispose it.
    /// </param>
    /// <param name="progress">
    ///     An optional progress sink invoked as bytes arrive. Implementations report
    ///     <see cref="SpeechModelDownloadProgress.FileIndex"/> as <c>0</c> and
    ///     <see cref="SpeechModelDownloadProgress.FileCount"/> as <c>1</c>, since a single call
    ///     only knows about the one file it was asked to fetch; a caller downloading multiple
    ///     files rewrites these fields itself.
    /// </param>
    /// <param name="cancellationToken">
    ///     A token observed between chunks so an in-progress download can be canceled promptly
    ///     without waiting for the whole transfer to complete.
    /// </param>
    /// <returns>A task that completes when the entire file has been written to <paramref name="destination"/>.</returns>
    /// <exception cref="OperationCanceledException">
    ///     Thrown when <paramref name="cancellationToken"/> is canceled before the transfer
    ///     completes.
    /// </exception>
    /// <remarks>
    ///     Implementations must throw (rather than silently truncate) on any non-success response
    ///     or transport failure, so a caller can distinguish "verified download" from "partial or
    ///     failed download" without inspecting <paramref name="destination"/>'s length itself.
    /// </remarks>
    Task DownloadAsync(
        Uri sourceUri,
        Stream destination,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken);
}
