using System.Net;
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
///     Verified in tests against a real, in-process loopback WireMock.Net server bound to
///     <c>127.0.0.1</c>, proving the streaming-download-with-progress code path end-to-end
///     (headers, <c>Content-Length</c>, chunked reads) without ever reaching a real host.
///     Thread-safety follows <see cref="HttpClient"/>'s own contract: a single instance is
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
///     <para>
///     This mirror-scope check is applied once, to the initial request, before it is sent - an
///     HTTP redirect response is never re-checked against it, because it never needs to be:
///     <see cref="HttpClient"/>'s default handler (<see cref="System.Net.Http.SocketsHttpHandler"/>
///     on every supported .NET runtime here) unconditionally strips the authorization header
///     from the follow-up request on every automatic redirect it follows, regardless of whether
///     the redirect target is in-scope or out-of-scope, same-host or cross-origin. This means the
///     mirror's secret can never reach a redirect target, in-scope or not - but it also means a
///     legitimate in-scope mirror redirect loses its authentication and will most likely fail
///     with <c>401</c>/<c>403</c> rather than complete; that functional limitation is an accepted,
///     documented constraint of relying on the framework's own redirect handling rather than
///     implementing a manual bounded-redirect loop with per-hop re-authentication, which would
///     add non-trivial security-sensitive code to re-solve a risk .NET's handler already
///     mitigates. A mirror expected to redirect should instead be configured at its final,
///     non-redirecting URI.
///     </para>
///     <para>
///     Besides the public <see cref="DownloadAsync"/>, the internal <c>DownloadFromOffsetAsync</c> continues an
///     interrupted download with an HTTP <c>Range</c> request (falling back to a full download when the server ignores
///     or refuses the range). <see cref="SpeechModelDownloader"/> uses it when its client is this concrete type, so
///     resume never requires widening <see cref="IModelDownloadClient"/>.
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

        await CopyBodyAsync(response, destination, 0, response.Content.Headers.ContentLength, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Downloads a file into a seekable destination that may already hold its first
    ///     <paramref name="offset"/> bytes from an interrupted earlier attempt, asking the server for only the
    ///     remaining bytes (an HTTP <c>Range</c> request) and appending them.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     This is the resume path <see cref="SpeechModelDownloader"/> uses when its client is this concrete type; it
    ///     is internal because <see cref="IModelDownloadClient"/>'s public signature is never widened. With
    ///     <paramref name="offset"/> zero it is exactly <see cref="DownloadAsync"/> (after truncating the
    ///     destination). Otherwise it sends <c>Range: bytes={offset}-</c>, with the same mirror authentication rule as
    ///     <see cref="DownloadAsync"/>, and handles the response as follows:
    ///     </para>
    ///     <list type="bullet">
    ///         <item><description>
    ///             <c>206 Partial Content</c> whose <c>Content-Range</c> starts at <paramref name="offset"/>: the body is
    ///             appended after the existing bytes. Progress reports count the existing bytes, so
    ///             <see cref="SpeechModelDownloadProgress.BytesTransferred"/> starts at <paramref name="offset"/> and
    ///             <see cref="SpeechModelDownloadProgress.TotalBytes"/> is the <c>Content-Range</c> complete length.
    ///         </description></item>
    ///         <item><description>
    ///             Any other success status (typically <c>200 OK</c>, a server that ignores <c>Range</c>): the
    ///             destination is truncated and the full body written.
    ///         </description></item>
    ///         <item><description>
    ///             <c>416 Range Not Satisfiable</c>, or a <c>206</c> whose range does not start at
    ///             <paramref name="offset"/>: the existing bytes cannot be trusted to line up, so the destination is
    ///             truncated and the file downloaded once more from the start without <c>Range</c>. There is no further
    ///             retry.
    ///         </description></item>
    ///         <item><description>
    ///             Any other status: <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/> throws, exactly as for
    ///             <see cref="DownloadAsync"/>.
    ///         </description></item>
    ///     </list>
    ///     <para>
    ///     The resumed bytes are not verified here; the caller verifies the whole file's SHA-256 afterwards, so a
    ///     server that served mismatched bytes is caught there. On failure or cancellation the bytes already written
    ///     stay in <paramref name="destination"/>, which is what lets a later call resume.
    ///     </para>
    /// </remarks>
    /// <param name="sourceUri">The file's URI.</param>
    /// <param name="destination">
    ///     A writable, seekable stream holding the bytes already received (its length is expected to equal
    ///     <paramref name="offset"/>). Not disposed by this method.
    /// </param>
    /// <param name="offset">The number of bytes already received; zero for a fresh download. Must not be negative.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">A token that aborts the transfer after the current chunk.</param>
    /// <returns>A task that completes when the destination holds the whole file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sourceUri"/> or <paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is not writable and seekable.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset"/> is negative.</exception>
    /// <exception cref="HttpRequestException">Thrown for a non-success response or a transport failure.</exception>
    internal async Task DownloadFromOffsetAsync(
        Uri sourceUri,
        Stream destination,
        long offset,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (!destination.CanSeek || !destination.CanWrite)
        {
            throw new ArgumentException("The destination must be writable and seekable to resume a download.", nameof(destination));
        }

        if (offset > 0)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
            ApplyMirrorAuthentication(request, sourceUri);
            request.Headers.Range = new RangeHeaderValue(offset, null);

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            // 206 for exactly the requested range: append the remaining bytes after the ones already held
            var contentRange = response.Content.Headers.ContentRange;
            if (response.StatusCode == HttpStatusCode.PartialContent && contentRange is { HasRange: true } && contentRange.From == offset)
            {
                var totalBytes = contentRange.Length ?? offset + response.Content.Headers.ContentLength;
                destination.Seek(offset, SeekOrigin.Begin);
                await CopyBodyAsync(response, destination, offset, totalBytes, progress, cancellationToken).ConfigureAwait(false);
                return;
            }

            // Any other success except a mismatched 206 is a full body (the server ignored Range): rewrite from zero
            if (response.StatusCode != HttpStatusCode.RequestedRangeNotSatisfiable && response.StatusCode != HttpStatusCode.PartialContent)
            {
                response.EnsureSuccessStatusCode();
                destination.SetLength(0);
                destination.Seek(0, SeekOrigin.Begin);
                await CopyBodyAsync(response, destination, 0, response.Content.Headers.ContentLength, progress, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            // 416, or a 206 for some other range: the held bytes cannot be trusted, so fall through and restart once
        }

        destination.SetLength(0);
        destination.Seek(0, SeekOrigin.Begin);
        await DownloadAsync(sourceUri, destination, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Streams a response body to the destination in chunks, reporting progress after each chunk.
    /// </summary>
    /// <param name="response">The response whose body is copied.</param>
    /// <param name="destination">The stream written at its current position.</param>
    /// <param name="initialBytes">The bytes the destination already held for this file (counted in progress).</param>
    /// <param name="totalBytes">The file's total size when known; otherwise <see langword="null"/>.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">A token observed between chunks.</param>
    /// <returns>A task that completes when the whole body is written.</returns>
    private static async Task CopyBodyAsync(
        HttpResponseMessage response,
        Stream destination,
        long initialBytes,
        long? totalBytes,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        await using var responseStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        var buffer = new byte[BufferSize];
        var bytesTransferred = initialBytes;

        // Report an initial progress sample immediately, so a caller wiring up a progress
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
    ///     one branch below ever applies. This method runs once, against the initial request,
    ///     before it is sent; it is deliberately never re-run against an HTTP redirect response -
    ///     see this type's own remarks for why <see cref="HttpClient"/>'s redirect handling
    ///     already strips this header on every automatic redirect regardless.
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
    ///     prefix of <c>"modelsEvil"</c> even though it is not a nested path segment. Both paths
    ///     have all trailing slashes trimmed before comparison, so a mirror configured with
    ///     a trailing slash (<c>https://mirror/models/</c>) is recognized as the base of a request
    ///     spelled without one (<c>https://mirror/models</c>), and vice versa.
    /// </remarks>
    private static bool IsWithinMirror(Uri requestUri, Uri mirrorBaseUri)
    {
        if (!string.Equals(requestUri.Scheme, mirrorBaseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(requestUri.Host, mirrorBaseUri.Host, StringComparison.OrdinalIgnoreCase) ||
            requestUri.Port != mirrorBaseUri.Port)
        {
            return false;
        }

        // Normalize away all trailing slashes from both paths before comparing, so
        // "https://host/models/" and "https://host/models" are recognized as the same base path
        // (matching ResolveEffectiveUri's own equivalent trailing-slash trim) rather than only
        // one spelling satisfying the equality check below.
        var basePath = mirrorBaseUri.AbsolutePath.TrimEnd('/');
        var requestPath = requestUri.AbsolutePath.TrimEnd('/');

        if (string.Equals(requestPath, basePath, StringComparison.Ordinal))
        {
            return true;
        }

        var basePrefix = basePath + "/";
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
