using System.Security.Cryptography;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.ModelBundler;

/// <summary>Where a vendor file's bytes were found.</summary>
internal enum VendorFileOrigin
{
    /// <summary>An installed copy in the per-user model store (<c>--source-store</c>).</summary>
    SourceStore,

    /// <summary>The tool's SHA-keyed download cache.</summary>
    Cache,

    /// <summary>A fresh HTTPS download of the file's original upstream URL (then cached).</summary>
    Download,
}

/// <summary>A vendor file whose local copy has been verified against its compiled SHA-256 pin.</summary>
/// <param name="File">The compiled declaration: original URL, pin and install path.</param>
/// <param name="EntryName">The <c>/</c>-separated install path, which is also the zip entry name.</param>
/// <param name="LocalPath">The verified local copy.</param>
/// <param name="Size">The verified copy's length in bytes.</param>
/// <param name="Origin">Where the copy was found.</param>
internal sealed record ResolvedVendorFile(
    SpeechModelDownloadFile File,
    string EntryName,
    string LocalPath,
    long Size,
    VendorFileOrigin Origin);

/// <summary>
///     Thrown when a vendor file cannot be obtained from any source (not installed, not cached and not downloadable),
///     which <c>build --skip-unavailable</c> turns into a skipped bundle.
/// </summary>
/// <remarks>
///     A copy that exists but fails its pin is never "unavailable": it is an <see cref="InvalidDataException"/>, which
///     always fails the build, because it means some source holds altered bytes.
/// </remarks>
public sealed class VendorFileUnavailableException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="VendorFileUnavailableException"/> class.</summary>
    public VendorFileUnavailableException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="VendorFileUnavailableException"/> class.</summary>
    /// <param name="message">The reason the file is unavailable.</param>
    public VendorFileUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="VendorFileUnavailableException"/> class.</summary>
    /// <param name="message">The reason the file is unavailable.</param>
    /// <param name="innerException">The download failure that made the file unavailable.</param>
    public VendorFileUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
///     Finds a local copy of each vendor file and proves it equals the compiled SHA-256 pin before the bundle uses it.
/// </summary>
/// <remarks>
///     <para>
///     Sources are tried in a fixed order: (1) with a source store, the installed copy at
///     <c>{store}/{modelId}/current/{path}</c> for each of the bundle's model ids in turn; (2) the SHA-keyed cache file
///     <c>{cache}/{sha256}</c>; (3) an HTTPS download of the file's original upstream URL into the cache. The first
///     copy found is hashed in full; a copy that fails its pin is an error and never falls through to the next source,
///     because altered bytes in any source must be investigated, not silently bypassed.
///     </para>
///     <para>
///     A download is written to <c>{cache}/{sha256}.download</c>, hashed as it arrives, and renamed to the cache name
///     only after it matches its pin, so the cache never holds an unverified file. The source store is only read.
///     Not thread-safe for concurrent resolution of the same file.
///     </para>
/// </remarks>
internal sealed class VendorFileSource
{
    /// <summary>The copy buffer size for downloads.</summary>
    private const int BufferSize = 1024 * 1024;

    /// <summary>The per-user model store root to search first, or <see langword="null"/> for none.</summary>
    private readonly string? _sourceStoreRoot;

    /// <summary>The SHA-keyed cache directory (created on first download).</summary>
    private readonly string _cacheDirectory;

    /// <summary>The HTTP client used for downloads; owned by the caller.</summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VendorFileSource"/> class.
    /// </summary>
    /// <param name="sourceStoreRoot">The model store root to search first, or <see langword="null"/> to skip it.</param>
    /// <param name="cacheDirectory">The SHA-keyed cache directory. Must not be empty.</param>
    /// <param name="httpClient">The client used to download a file missing from the store and cache. Not disposed.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cacheDirectory"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    public VendorFileSource(string? sourceStoreRoot, string cacheDirectory, HttpClient httpClient)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheDirectory);
        ArgumentNullException.ThrowIfNull(httpClient);
        _sourceStoreRoot = string.IsNullOrEmpty(sourceStoreRoot) ? null : sourceStoreRoot;
        _cacheDirectory = cacheDirectory;
        _httpClient = httpClient;
    }

    /// <summary>
    ///     Normalizes a declared install path to the <c>/</c>-separated form used as the zip entry name and in
    ///     <c>model.json</c>.
    /// </summary>
    /// <param name="file">The declared vendor file.</param>
    /// <returns>The install path with each separator replaced by <c>/</c> and empty segments removed.</returns>
    public static string GetEntryName(SpeechModelDownloadFile file) =>
        string.Join('/', file.RelativeInstallPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    ///     Finds a verified local copy of one vendor file.
    /// </summary>
    /// <param name="file">The compiled declaration of the file (original URL, pin, install path).</param>
    /// <param name="modelIds">The catalog model ids whose installed copies may hold the file, in search order.</param>
    /// <param name="cancellationToken">A token that aborts hashing or downloading.</param>
    /// <returns>The verified copy.</returns>
    /// <exception cref="InvalidDataException">Thrown when a copy is found but its SHA-256 differs from the pin.</exception>
    /// <exception cref="VendorFileUnavailableException">
    ///     Thrown when no source holds the file and the download fails (network, HTTP status or timeout).
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public async Task<ResolvedVendorFile> ResolveAsync(
        SpeechModelDownloadFile file,
        IReadOnlyList<string> modelIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(modelIds);
        var entryName = GetEntryName(file);
        var pin = file.Sha256Checksum.ToLowerInvariant();

        // 1. The installed copy of any model that uses the bundle
        if (_sourceStoreRoot is not null)
        {
            foreach (var modelId in modelIds)
            {
                var candidate = Path.Combine([_sourceStoreRoot, modelId, "current", .. entryName.Split('/')]);
                if (File.Exists(candidate))
                {
                    return await VerifyAsync(file, entryName, candidate, VendorFileOrigin.SourceStore, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // 2. The SHA-keyed cache
        var cached = Path.Combine(_cacheDirectory, pin);
        if (File.Exists(cached))
        {
            return await VerifyAsync(file, entryName, cached, VendorFileOrigin.Cache, cancellationToken).ConfigureAwait(false);
        }

        // 3. The original upstream URL, verified before it enters the cache
        await DownloadAsync(file, pin, cached, cancellationToken).ConfigureAwait(false);
        return new ResolvedVendorFile(file, entryName, cached, new FileInfo(cached).Length, VendorFileOrigin.Download);
    }

    /// <summary>Hashes an existing copy and accepts it only when it equals the pin.</summary>
    /// <param name="file">The compiled declaration.</param>
    /// <param name="entryName">The normalized install path.</param>
    /// <param name="path">The copy to verify.</param>
    /// <param name="origin">Where the copy was found.</param>
    /// <param name="cancellationToken">A token that aborts hashing.</param>
    /// <returns>The verified copy.</returns>
    /// <exception cref="InvalidDataException">Thrown when the copy's SHA-256 differs from the pin.</exception>
    private static async Task<ResolvedVendorFile> VerifyAsync(
        SpeechModelDownloadFile file,
        string entryName,
        string path,
        VendorFileOrigin origin,
        CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(actual, file.Sha256Checksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"The {origin} copy '{path}' of '{entryName}' has SHA-256 {actual}, but the compiled pin is {file.Sha256Checksum.ToLowerInvariant()}.");
            }

            return new ResolvedVendorFile(file, entryName, path, stream.Length, origin);
        }
    }

    /// <summary>Downloads the original URL into the cache, keeping it only when it equals the pin.</summary>
    /// <param name="file">The compiled declaration.</param>
    /// <param name="pin">The lowercase pin.</param>
    /// <param name="cached">The final cache path.</param>
    /// <param name="cancellationToken">A token that aborts the download.</param>
    /// <returns>A task that completes when the verified file is in the cache.</returns>
    /// <exception cref="InvalidDataException">Thrown when the downloaded bytes differ from the pin.</exception>
    /// <exception cref="VendorFileUnavailableException">Thrown when the download fails.</exception>
    private async Task DownloadAsync(SpeechModelDownloadFile file, string pin, string cached, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_cacheDirectory);
        var partial = cached + ".download";
        string actual;
        try
        {
            // Stream the body to a temporary name, hashing it as it arrives
            using var response = await _httpClient
                .GetAsync(file.Uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
                await using (output.ConfigureAwait(false))
                {
                    var buffer = new byte[BufferSize];
                    int read;
                    while ((read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        sha.AppendData(buffer, 0, read);
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            actual = Convert.ToHexStringLower(sha.GetHashAndReset());
        }
        catch (Exception ex) when (ex is HttpRequestException or HttpIOException)
        {
            File.Delete(partial);
            throw new VendorFileUnavailableException($"Downloading '{file.Uri}' failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation the caller did not request
            File.Delete(partial);
            throw new VendorFileUnavailableException($"Downloading '{file.Uri}' timed out.", ex);
        }

        // Only bytes that equal the pin ever take the cache name
        if (!string.Equals(actual, pin, StringComparison.Ordinal))
        {
            File.Delete(partial);
            throw new InvalidDataException($"The download of '{file.Uri}' has SHA-256 {actual}, but the compiled pin is {pin}.");
        }

        File.Move(partial, cached, overwrite: true);
    }
}
