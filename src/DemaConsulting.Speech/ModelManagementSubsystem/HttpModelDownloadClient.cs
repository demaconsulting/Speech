using System.Net;
using System.Net.Http.Headers;

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
///     <para>
///     When a request's <see cref="DownloadMirror"/> declares <see cref="DownloadMirror.Credentials"/>,
///     this class routes that request through a second, lazily-created, always internally-owned
///     <see cref="HttpClient"/> whose <see cref="HttpClientHandler.Credentials"/> is set to that
///     credential - never the caller-supplied or default client - so the documented
///     caller-supplied-vs-internally-owned ownership contract of the primary client is never
///     disturbed by mirror authentication.
///     </para>
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
    ///     A lazily-created, always internally-owned second <see cref="HttpClient"/> used only
    ///     for a request whose <see cref="DownloadMirror"/> declares
    ///     <see cref="DownloadMirror.Credentials"/>, or <see langword="null"/> until the first
    ///     such request is made.
    /// </summary>
    /// <remarks>
    ///     Created and cached the first time a credentialed-mirror request arrives, guarded by
    ///     <see cref="_credentialClientLock"/> since different models' downloads can run
    ///     concurrently through one shared <see cref="HttpModelDownloadClient"/> instance. This
    ///     client is never the caller-supplied <see cref="_httpClient"/>, regardless of whether
    ///     the caller supplied one to the constructor - it is always created and disposed by this
    ///     instance, so it can never violate the documented caller-supplied-vs-internally-owned
    ///     ownership contract of <see cref="_httpClient"/>.
    /// </remarks>
    private HttpClient? _credentialClient;

    /// <summary>Guards lazy, thread-safe creation of <see cref="_credentialClient"/>.</summary>
    private readonly object _credentialClientLock = new();

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
        CancellationToken cancellationToken,
        DownloadMirror? mirrorAuth = null)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);
        ArgumentNullException.ThrowIfNull(destination);

        // Choose which client issues this request: HttpClientHandler.Credentials lets .NET's own
        // Basic/NTLM challenge-response negotiation handle both schemes transparently (NTLM is a
        // genuine multi-round-trip handshake that a single, manually-built Authorization: Basic
        // header alone cannot support) - so a credentialed mirror request always goes through the
        // dedicated, lazily-created, always internally-owned credential client rather than the
        // caller-supplied/default one. A bearer token needs no handshake at all, so it is instead
        // applied as an explicit Authorization: Bearer header on a per-request HttpRequestMessage
        // below, sent through whichever client is already in use (mirrorAuth's constructor makes
        // Credentials and BearerToken mutually exclusive, so never both at once).
        var httpClient = mirrorAuth?.Credentials is not null
            ? GetOrCreateCredentialClient(mirrorAuth.Credentials)
            : _httpClient;

        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        if (mirrorAuth?.BearerToken is { } bearerToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        // Request headers before the body so a large file's Content-Length is known before any
        // bytes are read, letting progress reports include a meaningful total from the start.
        using var response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
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
    ///     Returns the lazily-created, always internally-owned <see cref="HttpClient"/> wired
    ///     with <paramref name="credentials"/>, creating it on first use.
    /// </summary>
    /// <param name="credentials">
    ///     The Basic/NTLM credential the returned client's <see cref="HttpClientHandler"/> should
    ///     present when challenged.
    /// </param>
    /// <remarks>
    ///     <see cref="System.Net.Http.HttpClientHandler.Credentials"/> is set once, at creation,
    ///     from whichever mirror credential first requests this client - this library's download
    ///     mirror support allows exactly one configured <see cref="DownloadMirror"/> at a time
    ///     (via <see cref="SpeechModelDownloaderOptions.Mirror"/>), so every call reaching this
    ///     method within one <see cref="HttpModelDownloadClient"/> instance's lifetime supplies
    ///     the same credential.
    /// </remarks>
    private HttpClient GetOrCreateCredentialClient(NetworkCredential credentials)
    {
        if (_credentialClient is not null)
        {
            return _credentialClient;
        }

        lock (_credentialClientLock)
        {
            return _credentialClient ??= new HttpClient(new HttpClientHandler { Credentials = credentials });
        }
    }

    /// <summary>
    ///     Disposes the internally created <see cref="HttpClient"/>, when this instance owns one,
    ///     and the lazily-created mirror-credential <see cref="HttpClient"/>, when one was ever
    ///     created.
    /// </summary>
    /// <remarks>
    ///     Does nothing to <see cref="_httpClient"/> when constructed with a caller-supplied
    ///     <see cref="HttpClient"/>, since that client's lifetime remains the caller's
    ///     responsibility; the mirror-credential client, by contrast, is always created and owned
    ///     internally by this instance (never caller-supplied), so it is disposed unconditionally
    ///     whenever one was created.
    /// </remarks>
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _credentialClient?.Dispose();
    }
}
