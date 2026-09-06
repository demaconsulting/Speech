### SpeechModelCatalog Verification

#### Verification Approach

`SpeechModelCatalog` is verified through deterministic unit tests using the internal test
constructor to inject a fake known-model list, a real temp-directory-backed `SpeechModelStore`,
and fake `IModelDownloadClient` implementations (a fixed-payload client for a successful/
checksum-mismatched download, and a `TaskCompletionSource`-gated client to deterministically
observe the `Downloading` state while a download is in flight). A system-level end-to-end test
(`SpeechTests.cs`) additionally proves the full public composition (real store, real HTTP
download client) enumerates and tracks state correctly.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test uses a unique scratch directory under `Path.GetTempPath()`

#### Acceptance Criteria

An empty `KnownModels`/known-model list enumerates to an empty list; a never-downloaded known
model reports `NotDownloaded`; a download in flight reports `Downloading`; a completed
successful download reports `Downloaded`; a checksum-mismatched download reports
`FailedOrCorrupt`; requesting a download for an unknown model id throws `ArgumentException`.

#### Test Scenarios

##### Enumerate with an empty known-model list returns an empty list

**Test**: `SpeechModelCatalog_Enumerate_EmptyKnownModels_ReturnsEmptyList`

##### The compiled-in KnownModels list contains all three real, production models

**Test**: `SpeechModelCatalog_KnownModels_ContainsAllThreeRealModels`

##### A never-downloaded known model reports NotDownloaded

**Test**: `SpeechModelCatalog_Enumerate_NeverDownloaded_ReportsNotDownloaded`

##### A download in flight reports Downloading

**Test**: `SpeechModelCatalog_GetState_DownloadInFlight_ReportsDownloading`

##### A successful download reports Downloaded after completion

**Test**: `SpeechModelCatalog_DownloadAsync_ValidModel_ReportsDownloadedAfterCompletion`

##### A checksum-mismatched download reports FailedOrCorrupt

**Test**: `SpeechModelCatalog_DownloadAsync_ChecksumMismatch_ReportsFailedOrCorrupt`

##### Downloading an unknown model id throws ArgumentException

**Test**: `SpeechModelCatalog_DownloadAsync_UnknownModelId_ThrowsArgumentException`

##### System integration: the catalog enumerates and tracks download state end-to-end

**Test**: `Speech_SystemIntegration_ModelCatalog_EnumeratesAndTracksDownloadState`
