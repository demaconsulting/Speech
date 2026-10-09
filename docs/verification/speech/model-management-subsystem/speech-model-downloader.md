### SpeechModelDownloader

#### Verification Approach

Verified through direct unit tests in `SpeechModelDownloaderTests.cs` against a fake
`IModelDownloadClient` (`FakeModelDownloadClient`, `BarrierModelDownloadClient`,
`OrderTrackingModelDownloadClient`, and `ThrowingModelDownloadClient`), so every scenario -
concurrency, checksum verification, atomic swap, the already-installed fast path, mirror URI
resolution/forwarding, and honest failure classification - runs deterministically with no real
network access. `ThrowingModelDownloadClient` throws an exact, caller-chosen exception shape
(including a wrapped `AuthenticationException`/`SocketException`, a bare `TaskCanceledException`,
an `IOException`, an `UnauthorizedAccessException`, and an unmatched `InvalidOperationException`)
so each `ClassifyFailure` branch is exercised through the real `DownloadAsync` catch chain rather
than by calling a private method directly. `FakeModelDownloadClient` additionally records each
call's source `Uri` (in a `Calls` list) so mirror-configured and no-mirror scenarios can assert
exactly which effective URI `SpeechModelDownloader` forwarded to the download client; mirror
*authentication* coverage (the `Authorization` header applied only to a request within the
configured mirror's base URI) is instead verified against the real `HttpModelDownloadClient` in
`HttpModelDownloadClientTests`, since `IModelDownloadClient` itself carries no auth parameter for
a generic fake to observe. `ResolveEffectiveUri`
itself is exercised directly (it is `internal`, reachable via this project's
`InternalsVisibleTo` grant from the core project) across trailing-slash, subdirectory, and
URL-unsafe-character edge cases. Cross-model concurrency and same-model-id serialization are both
proven with deterministic synchronization primitives (a `TaskCompletionSource`-based arrival
barrier and an explicit release gate, respectively) rather than timing-based delays, so a
regression back to a global single-flight lock fails these tests with a bounded timeout instead
of passing by coincidence or hanging indefinitely. Progress is observed via a hand-written
synchronous `IProgress<T>` test double rather than `System.Progress<T>`, since `Progress<T>`
posts each report independently (via a captured `SynchronizationContext` or the thread pool) with
no ordering guarantee across successive reports - `SynchronousProgress<T>` forwards
deterministically in the exact order reports are raised, which these tests' monotonicity
assertions depend on. The `ISpeechModel`-aware `DownloadAsync(model, ...)` overload is verified
both against small, in-test `ISpeechModel` stubs (`RecordingInstallHookModel`,
`ThrowingInstallHookModel`) that isolate exactly when/how `InstallAsync` is invoked, and end to
end via `FakeRecognitionModel(useZipArchivePayload: true)` through the store's real `current/`
directory. `SpeechModelCatalogTests.cs` additionally proves the catalog's own `DownloadAsync` call
path (not just the downloader in isolation) genuinely runs a zip-archive model's install hook.
The already-installed fast path is proven both directly (a `Substitute.For<IModelDownloadClient>`
that must never be invoked) and indirectly (a second `DownloadAsync` call for an
already-installed model id, supplying a descriptor that would fail checksum verification or a
model whose `InstallAsync` hook would throw, confirming the fast path short-circuits before
either would ever run).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- Each test constructs a `SpeechModelStore` rooted at a unique scratch directory

#### Acceptance Criteria

Tests pass when a valid payload installs the model and reports monotonically increasing,
correctly-indexed progress; a checksum mismatch installs nothing, reports `ChecksumMismatch`,
and populates `Error` with a descriptive exception; a second `DownloadAsync` call for an
already-installed model id returns `Installed` immediately without ever invoking the download
client, reporting an `Info` diagnostic, regardless of what descriptor or model it is supplied
(including one whose checksum would mismatch, or whose `InstallAsync` hook would throw), leaving
the prior successful install completely untouched; cancellation mid-download throws
`OperationCanceledException` and installs nothing; two concurrent download requests for different
model ids both complete successfully having genuinely run concurrently; two concurrent download
requests for the same, not-yet-installed model id serialize (the second never reaches the client
until the first completes), with the second then observing the already-installed fast path once
it acquires the lock in turn; the `ISpeechModel`-aware overload invokes `InstallAsync` on the
verified staged files before `current/` exists; a zip-archive model's `current/` directory ends
up containing the archive's extracted entry rather than the archive itself, whether reached
through the downloader directly or through `SpeechModelCatalog.DownloadAsync`; and an
`InstallAsync` failure of a listed or unlisted exception type on a fresh (not-yet-installed)
model reports `Failed` with `Error` populated and installs nothing. With no mirror configured,
`ResolveEffectiveUri` returns a file's own declared `Uri` unchanged (including across
trailing-slash and URL-unsafe-character variations in the no-mirror case, where those values are
simply never consulted) and `DownloadAsync` forwards the original URI with a `null` mirror auth
to the download client; with a mirror configured, `ResolveEffectiveUri` resolves exactly one
separating slash regardless of `BaseUri`'s own trailing slash, preserves every subdirectory
segment of a multi-segment `RelativeInstallPath`, and percent-escapes URL-unsafe characters in
both `modelId` and path segments, and `DownloadAsync` forwards the resolved effective URI together
with the configured mirror as the mirror auth to the download client. Each new failure
classification path is exercised and reports its documented, distinct outcome:
`HttpRequestException` with a `StatusCode` reports `HttpError`; `HttpRequestException` wrapping an
`AuthenticationException` or a `SocketException` reports `NetworkBlocked`; a bare
`TaskCanceledException` not caused by the caller's own token reports `NetworkBlocked`;
`IOException` and `UnauthorizedAccessException` both report `IoFailure`; and an unmatched
exception type still reports `Failed` - while a genuinely caller-canceled token still propagates
as `OperationCanceledException` completely unchanged, never reclassified as `NetworkBlocked`.

#### Test Scenarios

##### Download: Valid Payload Installs Model and Reports Progress

**Test**: `SpeechModelDownloader_DownloadAsync_ValidPayload_InstallsModelAndReportsProgress`

##### Download: Checksum Mismatch Reports Mismatch, Populates Error, and Installs Nothing

**Test**: `SpeechModelDownloader_DownloadAsync_ChecksumMismatch_ReportsChecksumMismatchAndInstallsNothing`

##### Download: Already Installed Returns Installed Without Fetching or Staging

**Test**: `SpeechModelDownloader_DownloadAsync_AlreadyInstalled_ReturnsInstalledWithoutFetchingOrStaging`

##### Download: Already Installed Cleans Up Leftover Staging Directory

**Test**: `SpeechModelDownloader_DownloadAsync_AlreadyInstalledWithLeftoverStaging_CleansUpLeftover`

##### Download: Second Call After Prior Success Takes Fast Path and Leaves Prior Install Intact

**Test**: `SpeechModelDownloader_DownloadAsync_SecondCallAfterPriorSuccess_TakesFastPathAndLeavesPriorInstallIntact`

##### Download: Canceled Mid-Download Throws and Installs Nothing

**Test**: `SpeechModelDownloader_DownloadAsync_CanceledMidDownload_ThrowsAndInstallsNothing`

##### Download: Two Concurrent Requests For Different Models Run Concurrently

**Test**: `SpeechModelDownloader_DownloadAsync_TwoConcurrentRequestsForDifferentModels_RunConcurrently`

##### Download: Two Concurrent Requests For The Same Model Id Are Serialized

**Test**: `SpeechModelDownloader_DownloadAsync_TwoConcurrentRequestsForSameModelId_AreSerialized`

##### Download: Model-Aware Overload Invokes InstallAsync Before CompleteInstall

**Test**: `SpeechModelDownloader_DownloadAsync_WithModel_InvokesInstallAsyncBeforeCompleteInstall`

##### Download: Zip-Archive Model's Current Directory Contains Extracted Files, Not the Archive

**Test**: `SpeechModelDownloader_DownloadAsync_WithZipArchiveModel_CurrentContainsExtractedFilesNotTheArchive`

##### Download: Model-Aware Overload's InstallAsync Failure Reports Failed for an Unlisted Exception Type

**Test**: `SpeechModelDownloader_DownloadAsync_WithModel_InstallAsyncThrowsUnlistedExceptionType_ReportsFailed`

##### Download: Model-Aware Overload's Second Call After Prior Success Takes Fast Path and Never Invokes InstallAsync

**Test**: `SpeechModelDownloader_DownloadAsync_WithModel_SecondCallAfterPriorSuccess_TakesFastPathAndNeverInvokesInstallHook`

##### Catalog: Zip-Archive Model Installs Extracted Content and Reports Downloaded

**Test**: `SpeechModelCatalog_DownloadAsync_ZipArchiveModel_InstallsExtractedContentAndReportsDownloaded`

##### ResolveEffectiveUri: No Mirror Returns Original Uri Unchanged

**Test**: `SpeechModelDownloader_ResolveEffectiveUri_NoMirror_ReturnsOriginalUriUnchanged`

##### ResolveEffectiveUri: Trailing Slash Variations Resolve Identically

**Test**: `SpeechModelDownloader_ResolveEffectiveUri_TrailingSlashVariations_ResolveIdentically`

##### ResolveEffectiveUri: Subdirectory Relative Path Preserves All Segments

**Test**: `SpeechModelDownloader_ResolveEffectiveUri_SubdirectoryRelativePath_PreservesAllSegments`

##### ResolveEffectiveUri: Model Id With Unsafe Characters Is Escaped

**Test**: `SpeechModelDownloader_ResolveEffectiveUri_ModelIdWithUnsafeCharacters_IsEscaped`

##### ResolveEffectiveUri: Path Segment With Unsafe Characters Is Escaped Per Segment

**Test**: `SpeechModelDownloader_ResolveEffectiveUri_PathSegmentWithUnsafeCharacters_IsEscapedPerSegment`

##### Download: No Mirror Configured Forwards Original Uri and Null Mirror Auth

**Test**: `SpeechModelDownloader_DownloadAsync_NoMirrorConfigured_ForwardsOriginalUriUnchanged`

##### Download: Mirror Configured Forwards Effective Uri and Mirror Auth

**Test**: `SpeechModelDownloader_DownloadAsync_MirrorConfigured_ForwardsEffectiveUri`

##### Download: HttpRequestException With Status Code Reports HttpError

**Test**: `SpeechModelDownloader_DownloadAsync_HttpRequestExceptionWithStatusCode_ReportsHttpError`

##### Download: HttpRequestException Wrapping AuthenticationException Reports NetworkBlocked

**Test**: `SpeechModelDownloader_DownloadAsync_HttpRequestExceptionWrappingAuthenticationException_ReportsNetworkBlocked`

##### Download: HttpRequestException Wrapping SocketException Reports NetworkBlocked

**Test**: `SpeechModelDownloader_DownloadAsync_HttpRequestExceptionWrappingSocketException_ReportsNetworkBlocked`

##### Download: TaskCanceledException Not From Caller Token Reports NetworkBlocked

**Test**: `SpeechModelDownloader_DownloadAsync_TaskCanceledExceptionNotFromCallerToken_ReportsNetworkBlocked`

##### Download: IOException Reports IoFailure

**Test**: `SpeechModelDownloader_DownloadAsync_IoException_ReportsIoFailure`

##### Download: UnauthorizedAccessException Reports IoFailure

**Test**: `SpeechModelDownloader_DownloadAsync_UnauthorizedAccessException_ReportsIoFailure`

##### Download: Unmatched Exception Type Reports Failed

**Test**: `SpeechModelDownloader_DownloadAsync_UnmatchedExceptionType_ReportsFailed`
