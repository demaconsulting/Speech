using System.Net.Http.Headers;
using System.Text;

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
///     Mirror authentication (see the <see cref="HttpModelDownloadClient(HttpClient?,DownloadMirror?)"/>
///     overload) is configured once, at construction, rather than per request - <see cref="SpeechModelDownloader"/>
///     only ever supports one configured <see cref="DownloadMirror"/> at a time (via
///     <see cref="SpeechModelDownloaderOptions.Mirror"/>), so every request this instance issues
///     over its lifetime shares the same mirror authentication, if any. This keeps every request
///     flowing through the exact caller-supplied (or default) <see cref="HttpClient"/> this
///     instance was constructed with - its configured proxy, timeout, certificate-validation,
///     cookie, and handler-pipeline settings are never bypassed for a credentialed request.
///     <see cref="DownloadMirror.Credentials"/> is applied as a preemptive HTTP Basic
///     <c>Authorization</c> header (not a true NTLM challenge/response negotiation, which would
///     require its own dedicated <see cref="HttpClientHandler"/> and so could not honor a
///     caller-supplied client's own configuration); <see cref="DownloadMirror.BearerToken"/> is
///     applied as a <c>Bearer</c> <c>Authorization</c> header. Either is applied only to a
///     request whose URI falls within the configured mirror's <see cref="DownloadMirror.BaseUri"/>
///     - this instance can be asked to fetch an unrelated, non-mirror URI directly (bypassing
///     <see cref="SpeechModelDownloader"/>'s own URI rewriting), and the mirror's secret must
///     never be sent to that other host.
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
    ///     The mirror authentication (if any) applied to every request this instance issues, or
    ///     <see langword="null"/> to send no <c>Authorization</c> header - the exact pre-mirror
    ///     request shape.
    /// </summary>
    private readonly DownloadMirror? _mirror;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HttpModelDownloadClient"/> class with no
    ///     mirror authentication.
    /// </summary>
    /// <param name="httpClient">
    ///     An optional pre-configured <see cref="HttpClient"/> to issue requests with (its
    ///     lifetime remains owned by the caller), or <see langword="null"/> to create and own a
    ///     default instance internally.
    /// </param>
    public HttpModelDownloadClient(HttpClient? httpClient = null)
        : this(httpClient, mirror: null)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="HttpModelDownloadClient"/> class that
    ///     applies <paramref name="mirror"/>'s authentication (if any) to every request it issues.
    /// </summary>
    /// <param name="httpClient">
    ///     An optional pre-configured <see cref="HttpClient"/> to issue requests with (its
    ///     lifetime remains owned by the caller), or <see langword="null"/> to create and own a
    ///     default instance internally.
    /// </param>
    /// <param name="mirror">
    ///     The mirror whose <see cref="DownloadMirror.Credentials"/>/<see cref="DownloadMirror.BearerToken"/>
    ///     (if either is set) this instance applies to every request it issues, or <see langword="null"/>
    ///     to send no <c>Authorization</c> header.
    /// </param>
    public HttpModelDownloadClient(HttpClient? httpClient, DownloadMirror? mirror)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
        _mirror = mirror;
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

        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        ApplyMirrorAuthentication(request, sourceUri);

        // Request headers before the body so a large file's Content-Length is known before any
        // bytes are read, letting progress reports include a meaningful total from the start.
        using var response = await _httpClient
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
    ///     Applies <see cref="_mirror"/>'s configured authentication (if any) to
    ///     <paramref name="request"/> as an <c>Authorization</c> header, sent through whichever
    ///     <see cref="HttpClient"/> this instance was constructed with - never a second,
    ///     separately configured client - so the caller-supplied client's own proxy, timeout,
    ///     certificate-validation, and handler-pipeline settings always apply.
    /// </summary>
    /// <param name="request">The request to apply authentication to, before it is sent.</param>
    /// <param name="sourceUri">
    ///     The request's target URI, checked against <see cref="_mirror"/>'s
    ///     <see cref="DownloadMirror.BaseUri"/> before any authentication is applied - this
    ///     public type can be constructed with a credentialed mirror and still be asked (directly,
    ///     bypassing <see cref="SpeechModelDownloader"/>) to fetch an unrelated, non-mirror URI; the
    ///     mirror's secret must never be sent to that other host.
    /// </param>
    /// <remarks>
    ///     <see cref="DownloadMirror.Credentials"/> is applied as a preemptive HTTP Basic header
    ///     (never a true NTLM handshake, which only a dedicated <see cref="HttpClientHandler"/>
    ///     could negotiate, at the cost of discarding the caller's own client configuration);
    ///     <see cref="DownloadMirror.BearerToken"/> is applied as a <c>Bearer</c> header. The two
    ///     are mutually exclusive by <see cref="DownloadMirror"/>'s own constructor, so at most
    ///     one branch below ever applies.
    /// </remarks>
    private void ApplyMirrorAuthentication(HttpRequestMessage request, Uri sourceUri)
    {
        if (_mirror is null || !IsWithinMirror(sourceUri, _mirror.BaseUri))
        {
            return;
        }

        if (_mirror.Credentials is { } credentials)
        {
            var basicValue = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{credentials.UserName}:{credentials.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicValue);
        }
        else if (_mirror.BearerToken is { } bearerToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }

    /// <summary>
    ///     Determines whether <paramref name="requestUri"/> falls within <paramref name="mirrorBaseUri"/> -
    ///     the same scheme, host, and port, and a path that either equals or is nested beneath the
    ///     mirror's base path on a segment boundary.
    /// </summary>
    /// <param name="requestUri">The request's target URI to test.</param>
    /// <param name="mirrorBaseUri">The configured mirror's base URI to test against.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="requestUri"/> is the mirror's base URI or
    ///     nested beneath it; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///     Deliberately not <see cref="Uri.IsBaseOf"/>, which compares paths as a raw ordinal
    ///     string prefix with no segment-boundary awareness - under <see cref="Uri.IsBaseOf"/>, a
    ///     mirror based at <c>https://mirror/models</c> would incorrectly be considered a base of
    ///     <c>https://mirror/modelsEvil/file.bin</c>, since <c>"models"</c> is a literal string
    ///     prefix of <c>"modelsEvil"</c> even though it is not a nested path segment.
    /// </remarks>
    private static bool IsWithinMirror(Uri requestUri, Uri mirrorBaseUri)
    {
        if (!string.Equals(requestUri.Scheme, mirrorBaseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(requestUri.Host, mirrorBaseUri.Host, StringComparison.OrdinalIgnoreCase) ||
            requestUri.Port != mirrorBaseUri.Port)
        {
            return false;
        }

        var basePath = mirrorBaseUri.AbsolutePath;
        var requestPath = requestUri.AbsolutePath;

        if (string.Equals(requestPath, basePath, StringComparison.Ordinal))
        {
            return true;
        }

        var basePrefix = basePath.EndsWith('/') ? basePath : basePath + "/";
        return requestPath.StartsWith(basePrefix, StringComparison.Ordinal);
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
