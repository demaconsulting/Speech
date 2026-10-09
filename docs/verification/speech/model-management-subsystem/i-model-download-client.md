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

#### Acceptance Criteria

Tests pass when a fake implementation of this seam lets `SpeechModelDownloader`'s
queueing/verification/atomic-swap logic be exercised deterministically with no real network
access, and when `SpeechModelDownloader` resolves and forwards the correct request URI to
`DownloadAsync` - the original, model-declared URI unchanged when no mirror is configured, or the
mirror-resolved effective URI when one is configured - with no per-call mirror-authentication
parameter ever passed, since this interface's `DownloadAsync` signature carries none.

#### Test Scenarios

##### Download: Valid Payload Installs Model and Reports Progress (via fake seam)

**Test**: `SpeechModelDownloader_DownloadAsync_ValidPayload_InstallsModelAndReportsProgress`

##### Download: Two Concurrent Requests for the Same Model Are Serialized (via fake seam)

**Test**: `SpeechModelDownloader_DownloadAsync_TwoConcurrentRequestsForSameModelId_AreSerialized`

##### Download: Real HttpModelDownloadClient Downloads Exact Bytes from a Stubbed Server

**Test**: `HttpModelDownloadClient_DownloadAsync_StubbedServer_DownloadsExactBytesWithProgress`

##### Download: No Mirror Configured Forwards Original URI Unchanged (via fake seam)

**Test**: `SpeechModelDownloader_DownloadAsync_NoMirrorConfigured_ForwardsOriginalUriUnchanged`

##### Download: Mirror Configured Forwards Effective URI (via fake seam)

**Test**: `SpeechModelDownloader_DownloadAsync_MirrorConfigured_ForwardsEffectiveUri`
