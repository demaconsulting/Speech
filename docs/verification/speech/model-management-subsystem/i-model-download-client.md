### IModelDownloadClient

#### Verification Approach

Verified indirectly through `SpeechModelDownloaderTests.cs`, which substitutes hand-written fake
implementations (`FakeModelDownloadClient`, `ConcurrencyTrackingModelDownloadClient`) to prove
`SpeechModelDownloader`'s orchestration logic against this seam, and directly through
`HttpModelDownloadClientTests.cs`, which proves the one real implementation
(`HttpModelDownloadClient`) genuinely satisfies the contract against a real loopback HTTP server.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when a fake implementation of this seam lets `SpeechModelDownloader`'s
queueing/verification/atomic-swap logic be exercised deterministically with no real network
access.

#### Test Scenarios

##### Download: Valid Payload Installs Model and Reports Progress (via fake seam)

**Test**: `SpeechModelDownloader_DownloadAsync_ValidPayload_InstallsModelAndReportsProgress`

##### Download: Two Concurrent Requests for the Same Model Are Serialized (via fake seam)

**Test**: `SpeechModelDownloader_DownloadAsync_TwoConcurrentRequestsForSameModelId_AreSerialized`

##### Download: Real HttpModelDownloadClient Downloads Exact Bytes from a Loopback Server

**Test**: `HttpModelDownloadClient_DownloadAsync_LoopbackServer_DownloadsExactBytesWithProgress`
