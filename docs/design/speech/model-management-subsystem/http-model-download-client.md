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
- **IsWithinMirror(requestUri, mirrorBaseUri)** *(private)*: The mirror-scope check used by
  `ApplyMirrorAuthentication`. A request is in scope only when its scheme, host and port equal the
  mirror's and its path equals, or is nested beneath, the mirror's base path on a *segment boundary*
  (deliberately not `Uri.IsBaseOf`, which is an ordinal string-prefix test under which a mirror at
  `https://mirror/models` would wrongly be the base of `https://mirror/modelsEvil/file.bin`). All
  trailing slashes are trimmed from both paths before comparing, so `https://mirror/models/` and
  `https://mirror/models` denote the same base. A request outside the scope carries no
  `Authorization` header, because a caller can hold this public type directly (bypassing
  `SpeechModelDownloader`'s URI rewriting) and ask it to fetch an unrelated address.
- **Dispose()**: Disposes the internally created `HttpClient` only when this instance created it; a
  caller-supplied `HttpClient` remains the caller's to dispose.
- **DownloadFromOffsetAsync(sourceUri, destination, offset, progress, cancellationToken)** *(internal)*: The
  resume path `SpeechModelDownloader` uses when its client is this concrete type, kept internal so the public
  `IModelDownloadClient` signature is never widened. The destination must be writable and seekable and is
  expected to already hold the first `offset` bytes. With `offset` zero it truncates the destination and calls
  `DownloadAsync`. Otherwise it sends the same `GET` with `Range: bytes={offset}-` (mirror authentication applied
  by the same `ApplyMirrorAuthentication` rule) and handles the answer:
  - `206 Partial Content` whose `Content-Range` starts at `offset`: seeks to `offset` and appends the body. Progress
    starts at `BytesTransferred = offset` and reports `TotalBytes` as the `Content-Range` complete length (or
    `offset` plus `Content-Length` when the length is `*`).
  - Any other success status (a server that ignores `Range` answers `200 OK`): truncates the destination and
    writes the full body.
  - `416 Range Not Satisfiable`, or a `206` for any other start: the held bytes cannot be trusted to line up, so
    the destination is truncated and `DownloadAsync` runs once more without `Range`. There is no further retry.
  - Any other status: `EnsureSuccessStatusCode()` throws `HttpRequestException`, as for `DownloadAsync`.

  The resumed bytes are not verified here: the caller verifies the complete file's SHA-256, so stale or
  mismatched held bytes are caught there. On failure or cancellation the bytes already written stay in the
  destination, which is what lets a later call resume. Both download methods stream the body through one private
  `CopyBodyAsync` helper, which reports an initial sample and one per chunk.

**Requirement mapping**: the unit requirements describe only the observable wire behavior (exact
bytes and progress, failure on non-2xx, which requests carry which `Authorization` header, nothing on
redirected requests); the single shared `HttpClient`, `ApplyMirrorAuthentication`, the segment-boundary
scope rule and the reliance on `SocketsHttpHandler` redirect handling are design detail documented
above. `HttpClient`-originated timeouts surface as a `TaskCanceledException` whose `InnerException` is
a `TimeoutException`; this client does not translate them - `SpeechModelDownloader` recognizes that
shape (see its design) when the concrete client is in use.

**Error Handling**: Throws `HttpRequestException` (via `EnsureSuccessStatusCode()`) for a
non-success response, so a non-2xx response never results in an error page's body being silently
written to `destination`. Propagates `OperationCanceledException` when `cancellationToken` is
canceled mid-transfer. `DownloadFromOffsetAsync` additionally throws `ArgumentOutOfRangeException` for a negative
offset and `ArgumentException` for a destination that is not writable and seekable.

**Dependencies**: `System.Net.Http.HttpClient`, `IModelDownloadClient` (implements it),
`SpeechModelDownloadProgress`, `DownloadMirror`.

**Callers**: `SpeechModelDownloader`, as its default `IModelDownloadClient` when none is injected; its internal
`DownloadFromOffsetAsync` is called only by `SpeechModelDownloader`'s resumable fetch.
