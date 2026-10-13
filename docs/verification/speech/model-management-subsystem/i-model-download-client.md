### IModelDownloadClient

#### Verification Approach

Verified indirectly through `SpeechModelDownloaderTests.cs`, which substitutes hand-written fake
implementations (`FakeModelDownloadClient`, `ConcurrencyTrackingModelDownloadClient`) to prove
`SpeechModelDownloader`'s orchestration logic against this seam, and directly through
`HttpModelDownloadClientTests.cs`, which proves the one real implementation
(`HttpModelDownloadClient`) genuinely satisfies the contract against a WireMock.Net-stubbed HTTP
server.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Fakes**: Hand-written `IModelDownloadClient` fakes supply deterministic payloads, delays and
  failures; no network access is involved except the in-process `WireMock.Net` server used for
  the real implementation
- **Isolation**: Each test uses its own scratch directory under `Path.GetTempPath()`

#### Acceptance Criteria

Tests pass when a fake implementation of this seam lets `SpeechModelDownloader`'s
fetch/verify/install logic be exercised deterministically with no real network
access, and when the real implementation applies mirror authentication only from its construction-time
configuration (no per-call parameter exists), throws on a non-success response, and surfaces
cancellation. How `SpeechModelDownloader` resolves the request URI is verified with that unit.

#### Test Scenarios

##### Download: Valid Payload Installs Model and Reports Progress (via fake seam)

**Scenario**: A fake client writes a valid payload through the seam.

**Expected**: The downloader installs the model and reports progress.

**Test**: `SpeechModelDownloader_DownloadAsync_ValidPayload_InstallsModelAndReportsProgress`

##### Download: Two Concurrent Requests for the Same Model Are Serialized (via fake seam)

**Scenario**: Two downloads of one model id run at once against a concurrency-tracking fake.

**Expected**: The fake never sees two overlapping calls for that id.

**Test**: `SpeechModelDownloader_DownloadAsync_TwoConcurrentRequestsForSameModelId_AreSerialized`

##### Download: Real HttpModelDownloadClient Downloads Exact Bytes from a Stubbed Server

**Scenario**: The real client downloads from a WireMock-stubbed server.

**Expected**: The destination receives exactly the served bytes and progress is reported.

**Test**: `HttpModelDownloadClient_DownloadAsync_StubbedServer_DownloadsExactBytesWithProgress`

##### Mirror Authentication Is Fixed at Construction (real implementation)

**Scenario**: The real client is built with no, bearer-token or credential mirror authentication and
downloads from the stubbed server.

**Expected**: The Authorization header is absent, bearer or basic respectively, with no per-call input.

**Tests**: `HttpModelDownloadClient_DownloadAsync_NoMirrorAuth_SendsNoAuthorizationHeader`,
`HttpModelDownloadClient_DownloadAsync_BearerTokenMirror_SendsBearerAuthorizationHeader`

##### Error path: Non-Success Response Throws (real implementation)

**Scenario**: The stubbed server answers with a non-success status.

**Expected**: The implementation throws `HttpRequestException` rather than silently truncating, as
the interface contract requires.

**Test**: `HttpModelDownloadClient_DownloadAsync_NonSuccessResponse_ThrowsHttpRequestException`

##### Error path: Cancellation Is Observed (via fake seam)

**Scenario**: The caller's token is canceled while the fake client is mid-download.

**Expected**: Cancellation propagates, nothing is installed, and the seam's cancellation contract is
honored.

**Test**: `SpeechModelDownloader_DownloadAsync_CanceledMidDownload_ThrowsAndInstallsNothing`
