### HttpModelDownloadClient

**Purpose**: Provide the real, `HttpClient`-backed implementation of `IModelDownloadClient` that
streams a file from an HTTP(S) URI to a destination stream, reporting progress as bytes arrive.
Production model downloads always use HTTPS; the client itself is scheme-agnostic (matching the
seam's contract) so it can be verified against a real loopback HTTP server without a TLS
certificate.

**Data Model**: A private `HttpClient` (either caller-supplied or created and owned internally),
a fixed `81920`-byte read buffer size used between progress reports and cancellation checks, and
a lazily-created, always internally-owned second `HttpClient` (guarded by a plain `object` lock,
since `System.Threading.Lock` is unavailable on this library's oldest targeted framework) whose
`HttpClientHandler.Credentials` is set only the first time a mirror with `Credentials` is actually
used - never constructed when no mirror, or only a bearer-token mirror, is configured.

**Key Methods**:

- **HttpModelDownloadClient(httpClient?)**: Accepts an optional pre-configured `HttpClient` (whose
  lifetime remains the caller's), or creates and owns a default instance internally. This
  ownership contract is unchanged by the mirror feature: the lazily-created credential
  `HttpClient` described above is always internally owned and disposed by this type regardless of
  whether the primary `HttpClient` was caller-supplied.
- **DownloadAsync(sourceUri, destination, progress, cancellationToken, mirrorAuth)**: Issues a
  `GET` request with `HttpCompletionOption.ResponseHeadersRead` so a large file's `Content-Length`
  is known before any bytes are read, then streams the response body to `destination` in
  `81920`-byte chunks, reporting an initial zero-byte sample immediately and a further sample
  after every chunk. When `mirrorAuth` is non-null, applies its authentication to this request
  before sending it: a non-null `BearerToken` is applied as an explicit
  `Authorization: Bearer <token>` request header (no handshake is needed for a bearer token, so a
  header is simplest); non-null `Credentials` are instead applied by issuing the request through
  the lazily-created credential `HttpClient` whose `HttpClientHandler.Credentials` is set to those
  credentials, because `HttpClient` has no per-request handler-level credentials mechanism and
  only a real `HttpClientHandler.Credentials` negotiation (not a single manually-built
  `Authorization` header) correctly drives NTLM's multi-round-trip challenge/response handshake.
  With no `mirrorAuth` supplied (the default), no `Authorization` header is sent and the primary
  `HttpClient` is used exactly as before the mirror feature existed.

**Error Handling**: Throws `HttpRequestException` (via `EnsureSuccessStatusCode()`) for a
non-success response, so a non-2xx response never results in an error page's body being silently
written to `destination`. Propagates `OperationCanceledException` when `cancellationToken` is
canceled mid-transfer.

**Dependencies**: `System.Net.Http.HttpClient`, `System.Net.Http.HttpClientHandler`,
`IModelDownloadClient` (implements it), `SpeechModelDownloadProgress`, `DownloadMirror`.

**Callers**: `SpeechModelDownloader`, as its default `IModelDownloadClient` when none is injected.
