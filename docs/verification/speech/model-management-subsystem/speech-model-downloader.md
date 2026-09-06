### SpeechModelDownloader

#### Verification Approach

Verified through direct unit tests in `SpeechModelDownloaderTests.cs` against a fake
`IModelDownloadClient` (`FakeModelDownloadClient`, `BarrierModelDownloadClient`, and
`OrderTrackingModelDownloadClient`), so every scenario - concurrency, checksum verification,
atomic swap, and honest failure states - runs deterministically with no real network access.
Cross-model concurrency and same-model-id serialization are both proven with deterministic
synchronization primitives (a `TaskCompletionSource`-based arrival barrier and an explicit
release gate, respectively) rather than timing-based delays, so a regression back to a global
single-flight lock fails these tests with a bounded timeout instead of passing by coincidence or
hanging indefinitely. Progress is observed via a hand-written synchronous `IProgress<T>` test
double rather than `System.Progress<T>`, since `Progress<T>` posts each report independently (via
a captured `SynchronizationContext` or the thread pool) with no ordering guarantee across
successive reports - `SynchronousProgress<T>` forwards deterministically in the exact order
reports are raised, which these tests' monotonicity assertions depend on. The `ISpeechModel`-aware
`DownloadAsync(model, ...)` overload is verified both against small, in-test `ISpeechModel` stubs
(`RecordingInstallHookModel`, `ThrowingInstallHookModel`) that isolate exactly when/how
`InstallAsync` is invoked, and end to end via `FakeRecognitionModel(useZipArchivePayload: true)`
through the store's real `current/` directory. `SpeechModelCatalogTests.cs` additionally proves
the catalog's own `DownloadAsync` call path (not just the downloader in isolation) genuinely runs
a zip-archive model's install hook.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- Each test constructs a `SpeechModelStore` rooted at a unique scratch directory

#### Acceptance Criteria

Tests pass when a valid payload installs the model and reports monotonically increasing,
correctly-indexed progress; a checksum mismatch installs nothing and reports
`ChecksumMismatch`; a checksum mismatch on a repair attempt leaves a prior successful install
untouched; cancellation mid-download throws `OperationCanceledException` and installs nothing;
two concurrent download requests for different model ids both complete successfully having
genuinely run concurrently; two concurrent download requests for the same model id serialize
(the second never reaches the client until the first completes) and both still complete
successfully in order; the `ISpeechModel`-aware overload invokes `InstallAsync` on the verified
staged files before `current/` exists; a zip-archive model's `current/` directory ends up
containing the archive's extracted entry rather than the archive itself, whether reached through
the downloader directly or through `SpeechModelCatalog.DownloadAsync`; and an `InstallAsync`
failure on a repair attempt reports `Failed` and leaves a prior successful install completely
untouched.

#### Test Scenarios

##### Download: Valid Payload Installs Model and Reports Progress

**Test**: `SpeechModelDownloader_DownloadAsync_ValidPayload_InstallsModelAndReportsProgress`

##### Download: Checksum Mismatch Reports Mismatch and Installs Nothing

**Test**: `SpeechModelDownloader_DownloadAsync_ChecksumMismatch_ReportsChecksumMismatchAndInstallsNothing`

##### Download: Checksum Mismatch After Prior Success Leaves Prior Install Intact

**Test**: `SpeechModelDownloader_DownloadAsync_ChecksumMismatchAfterPriorSuccess_LeavesPriorInstallIntact`

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

##### Download: Model-Aware Overload's InstallAsync Failure Reports Failed and Leaves Prior Install Intact

**Test**: `SpeechModelDownloader_DownloadAsync_WithModel_InstallAsyncThrows_ReportsFailedAndLeavesPriorInstallIntact`

##### Catalog: Zip-Archive Model Installs Extracted Content and Reports Downloaded

**Test**: `SpeechModelCatalog_DownloadAsync_ZipArchiveModel_InstallsExtractedContentAndReportsDownloaded`
