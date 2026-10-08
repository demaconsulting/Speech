### HttpModelDownloadClient

#### Verification Approach

Verified through direct unit tests in `HttpModelDownloadClientTests.cs` against a real, in-process
loopback `System.Net.HttpListener` server (`LoopbackHttpServer`) bound to `127.0.0.1` on an
OS-assigned ephemeral port. The port is discovered via a throwaway `TcpListener(IPAddress.Loopback,
0)`, released, and then bound by `HttpListener` (which cannot itself request port `0`). This
proves the concrete implementation genuinely performs an HTTP download with progress reporting -
not just a mocked seam - with no real network access and no new test-server package dependency.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Server**: An in-process `HttpListener` loopback server serving one fixed payload or status
  code per test, disposed via a bounded-timeout best-effort shutdown so a slow-to-stop background
  loop never hangs a test; a `requireBasicAuth` option additionally lets `HttpListener` itself
  perform a genuine HTTP Basic 401/`WWW-Authenticate` challenge-response negotiation (the listener
  validates only that a well-formed Basic header is present, not any specific credential value,
  so the test asserts on the server's own captured, decoded `Authorization` header instead)
- Progress is observed via a hand-written synchronous `IProgress<T>` test double rather than
  `System.Progress<T>`, for the same ordering reason documented in `speech-model-downloader.md`

#### Acceptance Criteria

Tests pass when the exact requested bytes are downloaded, progress reports are present,
monotonically increasing, and end at the total; a non-2xx response throws
`HttpRequestException` rather than writing a truncated or error-page body to the destination;
with no `mirrorAuth` supplied, no `Authorization` header is sent at all; with a bearer-token
mirror, the request carries `Authorization: Bearer <token>` exactly; and with a credentials
mirror, the real HTTP Basic challenge/response handshake completes and the negotiated
`Authorization` header decodes to the configured username/password.

#### Test Scenarios

##### Download: Loopback Server Downloads Exact Bytes With Progress

**Test**: `HttpModelDownloadClient_DownloadAsync_LoopbackServer_DownloadsExactBytesWithProgress`

##### Download: Non-Success Response Throws HttpRequestException

**Test**: `HttpModelDownloadClient_DownloadAsync_NonSuccessResponse_ThrowsHttpRequestException`

##### Download: No Mirror Auth Sends No Authorization Header

**Test**: `HttpModelDownloadClient_DownloadAsync_NoMirrorAuth_SendsNoAuthorizationHeader`

##### Download: Bearer Token Mirror Sends Bearer Authorization Header

**Test**: `HttpModelDownloadClient_DownloadAsync_BearerTokenMirror_SendsBearerAuthorizationHeader`

##### Download: Credentials Mirror Negotiates Basic Authentication

**Test**: `HttpModelDownloadClient_DownloadAsync_CredentialsMirror_NegotiatesBasicAuthentication`
