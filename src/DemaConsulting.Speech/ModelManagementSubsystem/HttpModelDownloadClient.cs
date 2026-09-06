namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Real, <see cref="HttpClient"/>-backed implementation of <see cref="IModelDownloadClient"/>
///     that streams a file from an HTTP(S) URI to a destination stream, reporting progress as
///     bytes arrive. Production model downloads always use HTTPS; the client itself is
///     scheme-agnostic so it can be verified against a real loopback HTTP server in tests.
/// </summary>
/// <remarks>
///     Verified in tests against a real, in-process loopback <see cref="System.Net.HttpListener"/>
///     server bound to <c>127.0.0.1</c>, proving the streaming-download-with-progress code path
///     end-to-end (headers, <c>Content-Length</c>, chunked reads) without ever reaching a real
///     host. Thread-safety follows <see cref="HttpClient"/>'s own contract: a single instance is
///     safe to share and reuse across concurrent calls, though <see cref="SpeechModelDownloader"/>
///     only ever issues one download at a time by design.
/// </remarks>
public sealed class HttpModelDownloadClient : IModelDownloadClient, IDisposable
{
    /// <summary>
    ///     The size, in bytes, of each chunk read from the response body between progress reports
    ///     and cancellation checks.
    /// </summary>
    /// <remarks>
    ///     Chosen as a balance between responsive progress reporting/cancellation and per-chunk
    ///     overhead; not a protocol constraint, just a reasonable default for model-sized (tens of
    ///     megabytes) payloads.
    /// </remarks>
    private const int BufferSize = 81920;

    /// <summary>
    ///     The underlying HTTP client used to issue download requests.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    ///     Whether <see cref="_httpClient"/> was created internally (and so must be disposed by
    ///     this instance) or supplied by the caller (whose lifetime this instance must not own).
    /// </summary>
    private readonly bool _ownsHttpClient;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HttpModelDownloadClient"/> class.
    /// </summary>
    /// <param name="httpClient">
    ///     An optional pre-configured <see cref="HttpClient"/> to issue requests with (its
    ///     lifetime remains owned by the caller), or <see langword="null"/> to create and own a
    ///     default instance internally.
    /// </param>
    public HttpModelDownloadClient(HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="sourceUri"/> or <paramref name="destination"/> is
    ///     <see langword="null"/>.
    /// </exception>
    /// <exception cref="HttpRequestException">
    ///     Thrown when the response has a non-success status code or the request otherwise fails
    ///     at the transport level.
    /// </exception>
    public async Task DownloadAsync(
        Uri sourceUri,
        Stream destination,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);
        ArgumentNullException.ThrowIfNull(destination);

        // Request headers before the body so a large file's Content-Length is known before any
        // bytes are read, letting progress reports include a meaningful total from the start.
        using var response = await _httpClient
            .GetAsync(sourceUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

        await using var responseStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        var buffer = new byte[BufferSize];
        long bytesTransferred = 0;

        // Report an initial 0-byte progress sample immediately, so a caller wiring up a progress
        // bar sees the file's total size (when known) even before the first chunk arrives.
        progress?.Report(new SpeechModelDownloadProgress(0, 1, bytesTransferred, totalBytes));

        int bytesRead;
        while ((bytesRead = await responseStream
                   .ReadAsync(buffer, cancellationToken)
                   .ConfigureAwait(false)) > 0)
        {
            await destination
                .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                .ConfigureAwait(false);

            bytesTransferred += bytesRead;
            progress?.Report(new SpeechModelDownloadProgress(0, 1, bytesTransferred, totalBytes));
        }
    }

    /// <summary>
    ///     Disposes the internally created <see cref="HttpClient"/>, when this instance owns one.
    /// </summary>
    /// <remarks>
    ///     Does nothing when constructed with a caller-supplied <see cref="HttpClient"/>, since
    ///     that client's lifetime remains the caller's responsibility.
    /// </remarks>
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
