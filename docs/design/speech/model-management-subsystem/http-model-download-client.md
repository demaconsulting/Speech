### HttpModelDownloadClient

**Purpose**: Provide the real, `HttpClient`-backed implementation of `IModelDownloadClient` that
streams a file from an HTTP(S) URI to a destination stream, reporting progress as bytes arrive.
Production model downloads always use HTTPS; the client itself is scheme-agnostic (matching the
seam's contract) so it can be verified against a real loopback HTTP server without a TLS
certificate.

**Data Model**: A private `HttpClient` (either caller-supplied or created and owned internally)
and a fixed `81920`-byte read buffer size used between progress reports and cancellation checks.

**Key Methods**:

- **HttpModelDownloadClient(httpClient?)**: Accepts an optional pre-configured `HttpClient` (whose
  lifetime remains the caller's), or creates and owns a default instance internally.
- **DownloadAsync(sourceUri, destination, progress, cancellationToken)**: Issues a `GET` request
  with `HttpCompletionOption.ResponseHeadersRead` so a large file's `Content-Length` is known
  before any bytes are read, then streams the response body to `destination` in
  `81920`-byte chunks, reporting an initial zero-byte sample immediately and a further sample
  after every chunk.

**Error Handling**: Throws `HttpRequestException` (via `EnsureSuccessStatusCode()`) for a
non-success response, so a non-2xx response never results in an error page's body being silently
written to `destination`. Propagates `OperationCanceledException` when `cancellationToken` is
canceled mid-transfer.

**Dependencies**: `System.Net.Http.HttpClient`, `IModelDownloadClient` (implements it),
`SpeechModelDownloadProgress`.

**Callers**: `SpeechModelDownloader`, as its default `IModelDownloadClient` when none is injected.
