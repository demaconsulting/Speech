### HttpModelDownloadClient

#### Verification Approach

Verified through direct unit tests in `HttpModelDownloadClientTests.cs` against an in-process
`WireMock.Net` `WireMockServer`, stubbing HTTP responses without any real network access. This
proves the concrete implementation genuinely performs an HTTP download with progress reporting -
not just a mocked seam - and that construction-time mirror authentication is correctly applied as
request headers.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Server**: An in-process `WireMockServer` stubbing one fixed response per test. Kestrel
  (WireMock.Net's self-hosted server) otherwise always responds with `Transfer-Encoding: chunked`
  and strips any explicit `Content-Length` header; the progress test works around this with a
  `PreWireMockMiddlewareInit` buffering middleware that captures the response body into a
  `MemoryStream`, sets `Response.ContentLength` from its length, and then copies it to the real
  response stream - `PreWireMockMiddlewareInit` (not `PostWireMockMiddlewareInit`) is required
  because it registers *before* WireMock's own terminal response-writing handler, so it wraps
  around (and can intercept the write after) that handler, whereas `PostWireMockMiddlewareInit`
  registers after it and so never executes for a matched request.
- Progress is observed via a hand-written synchronous `IProgress<T>` test double rather than
  `System.Progress<T>`, for the same ordering reason documented in `speech-model-downloader.md`

#### Acceptance Criteria

Tests pass when the exact requested bytes are downloaded, progress reports are present,
monotonically increasing, and end at the total; a non-2xx response throws
`HttpRequestException` rather than writing a truncated or error-page body to the destination;
with no mirror configured, no `Authorization` header is sent at all; with a bearer-token mirror,
every request carries a `Bearer` authorization header exactly; and with a credentials
mirror, every request carries a preemptive `Authorization: Basic` header (no challenge/response
handshake) that decodes to the exact configured username/password, sent through the same
`HttpClient` instance as the download itself. Also verified: the redirect response to an
in-scope mirror request leaves the mirror's authorization header on the initial request, but
the follow-up (redirected) request carries no authorization header at all, confirming
`HttpClient`'s own redirect handling - not this class - is what prevents the mirror's secret from
ever reaching a redirect target.

#### Test Scenarios

##### Download: Stubbed Server Downloads Exact Bytes With Progress

**Test**: `HttpModelDownloadClient_DownloadAsync_StubbedServer_DownloadsExactBytesWithProgress`

##### Download: Non-Success Response Throws HttpRequestException

**Test**: `HttpModelDownloadClient_DownloadAsync_NonSuccessResponse_ThrowsHttpRequestException`

##### Download: No Mirror Auth Sends No Authorization Header

**Test**: `HttpModelDownloadClient_DownloadAsync_NoMirrorAuth_SendsNoAuthorizationHeader`

##### Download: Bearer Token Mirror Sends Bearer Authorization Header

**Test**: `HttpModelDownloadClient_DownloadAsync_BearerTokenMirror_SendsBearerAuthorizationHeader`

##### Download: Credentials Mirror Sends Basic Authorization Header

**Test**: `HttpModelDownloadClient_DownloadAsync_CredentialsMirror_SendsBasicAuthorizationHeader`

##### Download: Mirror Redirect Strips Authorization Header On Redirected Request

**Test**: `HttpModelDownloadClient_DownloadAsync_MirrorRedirect_StripsAuthorizationHeaderOnRedirectedRequest`
