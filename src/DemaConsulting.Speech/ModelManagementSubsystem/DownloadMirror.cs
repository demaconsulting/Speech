using System.Net;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Describes an internal HTTP(S) mirror that <see cref="SpeechModelDownloader"/> should
///     fetch every model's declared files from instead of each model's own hardcoded
///     <see cref="SpeechModelDownloadFile.Uri"/>, plus the single authentication mechanism (if
///     any) that mirror requires.
/// </summary>
/// <remarks>
///     Some customers' IT departments block well-known public download hosts (for example
///     <c>huggingface.co</c>) at the network level, including TLS interception that surfaces as
///     an <see cref="HttpRequestException"/> wrapping an
///     <see cref="System.Security.Authentication.AuthenticationException"/> for an untrusted
///     root certificate. Those customers instead want every model download uniformly redirected
///     to an internal HTTPS mirror they control, without having to change (or even know) each
///     individual model's hardcoded public URI. Configuring a single <see cref="DownloadMirror"/>
///     on <see cref="SpeechModelDownloaderOptions"/> is the one seam that achieves this: see
///     <see cref="SpeechModelDownloader.ResolveEffectiveUri"/> for exactly how a model's declared
///     <see cref="SpeechModelDownloadFile.Uri"/> is rewritten into a mirror-relative request URI.
///     <para>
///     <see cref="Credentials"/> models a mirror protected by HTTP Basic authentication, sent as
///     a preemptive <c>Authorization: Basic</c> header on every request (see
///     <see cref="HttpModelDownloadClient"/>) - this type stores a plain
///     <see cref="NetworkCredential"/> rather than a pre-built header value purely for a
///     host-friendly configuration shape; a true NTLM challenge/response handshake is not
///     supported, since negotiating one would require a dedicated
///     <see cref="System.Net.Http.HttpClientHandler"/> that bypasses whatever
///     <see cref="HttpClient"/> a host has already configured (proxy, timeouts, certificate
///     validation) for mirror requests.
///     </para>
///     <para>
///     <see cref="BearerToken"/> instead models a mirror protected by a static, pre-issued
///     bearer token, as used by common artifact registries (for example JFrog Artifactory, Sonatype
///     Nexus, or Azure DevOps Artifacts) fronting a mirrored file store. Unlike Basic/NTLM, a
///     bearer token needs no multi-round-trip handshake: it is sent as a single
///     <c>Authorization: Bearer &lt;token&gt;</c> request header.
///     </para>
///     <para>
///     <see cref="Credentials"/> and <see cref="BearerToken"/> are mutually exclusive by design:
///     supplying both is rejected by this constructor as a configuration error, never silently
///     resolved by preferring one over the other, so a host notices the mistake immediately
///     rather than wondering why the "other" credential appears to be ignored.
///     </para>
///     <para>
///     Since both <see cref="Credentials"/> and <see cref="BearerToken"/> are sent
///     preemptively (no challenge/response handshake gates the first request), either is
///     rejected by this constructor when <see cref="BaseUri"/> uses the plain <c>http</c>
///     scheme rather than <c>https</c> - a misconfigured <c>http://</c> mirror would otherwise
///     transmit the secret in cleartext on the wire with no indication to the host that it
///     had done so. A loopback host (<c>localhost</c> or a loopback IP literal) is exempted
///     from this rule, since that traffic never leaves the machine - this lets an in-process
///     stubbed HTTP server (for example in tests) exercise credentialed requests without TLS.
///     </para>
///     <para>
///     Only the <c>http</c> and <c>https</c> schemes are supported for <see cref="BaseUri"/>. A
///     <c>file://</c> or UNC-style mirror (serving model files from a local or network file
///     share rather than over HTTP(S)) is explicitly out of scope for this type: there is no
///     implementation path for it anywhere in <see cref="SpeechModelDownloader"/> or
///     <see cref="HttpModelDownloadClient"/>, so rejecting any other scheme up front surfaces the
///     limitation as an immediate, clear configuration error instead of a confusing failure deep
///     inside the download pipeline.
///     </para>
/// </remarks>
public sealed record DownloadMirror
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DownloadMirror"/> record, validating
    ///     every field eagerly. See the type-level remarks for the exact rules enforced.
    /// </summary>
    /// <param name="baseUri">
    ///     The absolute <c>http</c> or <c>https</c> base location of the internal mirror. Every
    ///     model download's effective request URI is resolved beneath this base - see
    ///     <see cref="SpeechModelDownloader.ResolveEffectiveUri"/>. Must be <c>https</c> when
    ///     <paramref name="credentials"/> or <paramref name="bearerToken"/> is supplied and the
    ///     host is not a loopback address, since both are sent preemptively, in cleartext, over
    ///     whatever scheme is configured.
    /// </param>
    /// <param name="credentials">
    ///     The HTTP Basic credential to present when authenticating against the mirror, or
    ///     <see langword="null"/> when the mirror needs no such credential (the default).
    ///     Mutually exclusive with <paramref name="bearerToken"/>.
    /// </param>
    /// <param name="bearerToken">
    ///     The bearer token to present (as an <c>Authorization: Bearer</c> request header) when
    ///     authenticating against the mirror, or <see langword="null"/> when the mirror needs no
    ///     such token (the default). Mutually exclusive with <paramref name="credentials"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseUri"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="baseUri"/> is not an absolute URI, when its scheme is
    ///     neither <c>http</c> nor <c>https</c>, when it includes a query string or fragment,
    ///     when both <paramref name="credentials"/> and <paramref name="bearerToken"/> are
    ///     supplied, or when either is supplied alongside an <c>http</c> (rather than
    ///     <c>https</c>) <paramref name="baseUri"/> whose host is not a loopback address.
    /// </exception>
    public DownloadMirror(Uri baseUri, NetworkCredential? credentials = null, string? bearerToken = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);

        // A relative "base" URI has no meaningful way to combine with a model's declared
        // relative install path - reject it eagerly rather than failing confusingly deep inside
        // ResolveEffectiveUri.
        if (!baseUri.IsAbsoluteUri)
        {
            throw new ArgumentException("Mirror base URI must be absolute.", nameof(baseUri));
        }

        // Only http/https are ever dereferenced by HttpModelDownloadClient - a file://UNC mirror
        // has no implementation path anywhere in this library, so reject it immediately as a
        // configuration error instead of an obscure failure deep inside the download pipeline.
        if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Mirror base URI scheme must be 'http' or 'https', but was '{baseUri.Scheme}'.",
                nameof(baseUri));
        }

        // SpeechModelDownloader.ResolveEffectiveUri appends path segments directly onto this
        // base URI's string form - a query string or fragment would end up with the model's
        // path appended after the '?'/'#' delimiter (for example "https://mirror/models?x=1"
        // becoming ".../models?x=1/model-a/model.bin", whose actual request path remains
        // "/models" and never reaches the mirror's intended file), so reject either eagerly
        // rather than silently misrouting every download.
        if (!string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new ArgumentException(
                "Mirror base URI must not include a query string or fragment.",
                nameof(baseUri));
        }

        // Supplying both would silently prefer one over the other with no indication to the
        // host that the other was ignored - a configuration error, not a case to resolve with a
        // silent fallback.
        if (credentials is not null && bearerToken is not null)
        {
            throw new ArgumentException(
                "Specify at most one of credentials or bearerToken.",
                nameof(bearerToken));
        }

        // HttpModelDownloadClient sends Credentials/BearerToken preemptively, with no
        // challenge/response handshake - a plain "http" base URI would transmit the secret in
        // cleartext on the wire. A host that fat-fingers "http://" instead of "https://" while
        // configuring a credentialed mirror must be rejected immediately as a configuration
        // error, not silently leak the secret. A loopback host is exempted since that traffic
        // never leaves the machine - this keeps the rule meaningful for a real network mirror
        // while still allowing an in-process stubbed HTTP server (for example in tests) to
        // exercise credentialed requests without TLS.
        if ((credentials is not null || bearerToken is not null) &&
            string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !IsLoopbackHost(baseUri.Host))
        {
            throw new ArgumentException(
                "A credentialed mirror (Credentials or BearerToken) must use the 'https' scheme, never plain 'http', unless the host is a loopback address.",
                nameof(baseUri));
        }

        BaseUri = baseUri;
        Credentials = credentials;
        BearerToken = bearerToken;
    }

    /// <summary>
    ///     Determines whether <paramref name="host"/> refers to the local machine only - either
    ///     the literal name <c>localhost</c> or a loopback IP literal (<c>127.0.0.1</c>,
    ///     <c>::1</c>, and so on).
    /// </summary>
    /// <param name="host">The <see cref="Uri.Host"/> value to test.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="host"/> can never resolve to traffic
    ///     leaving the machine; otherwise <see langword="false"/>.
    /// </returns>
    private static bool IsLoopbackHost(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
    }

    /// <summary>
    ///     Gets the absolute <c>http</c> or <c>https</c> base location of the internal mirror.
    /// </summary>
    public Uri BaseUri { get; }

    /// <summary>
    ///     Gets the HTTP Basic credential to present when authenticating against the mirror, or
    ///     <see langword="null"/> when the mirror needs no such credential.
    /// </summary>
    public NetworkCredential? Credentials { get; }

    /// <summary>
    ///     Gets the bearer token to present (as an <c>Authorization: Bearer</c> request header)
    ///     when authenticating against the mirror, or <see langword="null"/> when the mirror
    ///     needs no such token.
    /// </summary>
    public string? BearerToken { get; }
}
