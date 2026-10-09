### HttpModelDownloadClient

**Purpose**: Provide the real, `HttpClient`-backed implementation of `IModelDownloadClient` that
streams a file from an HTTP(S) URI to a destination stream, reporting progress as bytes arrive.
Production model downloads always use HTTPS; the client itself is scheme-agnostic (matching the
seam's contract) so it can be verified against a stubbed HTTP server (WireMock.Net) without a TLS
certificate.

**Data Model**: A private `HttpClient` (either caller-supplied or created and owned internally),
a fixed `81920`-byte read buffer size used between progress reports and cancellation checks, and
the optional `DownloadMirror` supplied at construction whose authentication (if any) is applied
to every request this instance issues.

**Key Methods**:

- **HttpModelDownloadClient(httpClient?)**: Accepts an optional pre-configured `HttpClient` (whose
  lifetime remains the caller's), or creates and owns a default instance internally, applying no
  mirror authentication.
- **HttpModelDownloadClient(httpClient?, mirror?)**: As above, but applies `mirror`'s
  authentication (if any) to every request this instance issues for its entire lifetime -
  `SpeechModelDownloader` only ever supports one configured `DownloadMirror` at a time, so a
  single mirror configured once at construction (never per call) is sufficient.
- **DownloadAsync(sourceUri, destination, progress, cancellationToken)**: Issues a `GET` request
  with `HttpCompletionOption.ResponseHeadersRead` so a large file's `Content-Length` is known
  before any bytes are read, then streams the response body to `destination` in `81920`-byte
  chunks, reporting an initial zero-byte sample immediately and a further sample after every
  chunk. Before sending, `ApplyMirrorAuthentication` applies the construction-time mirror's
  authentication (if any) to the request: a non-null `Credentials` is applied as a preemptive
  `Authorization: Basic` header (computed directly from the `NetworkCredential`'s username and
  password); a non-null `BearerToken` is instead applied as an `Authorization: Bearer` header.
  The two are mutually exclusive by `DownloadMirror`'s own constructor, so at most one branch ever
  applies. Both are sent preemptively - on the very first request, with no challenge/response
  handshake - through the single `HttpClient` this instance was constructed with (whether
  caller-supplied or internally owned), never a second, separately configured client; this means
  a true NTLM handshake is never negotiated, since that would require a dedicated
  `HttpClientHandler` that bypasses whatever `HttpClient` a host has already configured (proxy,
  timeouts, certificate validation). With no mirror configured (the default), no `Authorization`
  header is sent at all - the exact pre-mirror request shape. This mirror-scope check runs once,
  against the initial request, and is deliberately never re-run against an HTTP redirect
  response: `HttpClient`'s default handler (`SocketsHttpHandler` on every supported .NET runtime
  here) unconditionally strips the `Authorization` header from the follow-up request on every
  automatic redirect it follows, in-scope or out-of-scope, same-host or cross-origin, so the
  mirror's secret can never reach a redirect target either way. The accepted trade-off is
  functional, not a security gap: a legitimate in-scope mirror redirect loses its authentication
  and will most likely fail with `401`/`403` rather than complete, so a mirror expected to
  redirect should be configured at its final, non-redirecting URI instead.

**Error Handling**: Throws `HttpRequestException` (via `EnsureSuccessStatusCode()`) for a
non-success response, so a non-2xx response never results in an error page's body being silently
written to `destination`. Propagates `OperationCanceledException` when `cancellationToken` is
canceled mid-transfer.

**Dependencies**: `System.Net.Http.HttpClient`, `IModelDownloadClient` (implements it),
`SpeechModelDownloadProgress`, `DownloadMirror`.

**Callers**: `SpeechModelDownloader`, as its default `IModelDownloadClient` when none is injected.
