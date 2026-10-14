### SpeechModelCatalog Verification

#### Verification Approach

`SpeechModelCatalog` is verified through deterministic unit tests using the internal test
constructor to inject a fake known-model list, a real temp-directory-backed `SpeechModelStore`,
and fake `IModelDownloadClient` implementations (a fixed-payload client for a successful/
checksum-mismatched download, and a `TaskCompletionSource`-gated client to deterministically
observe the `Downloading` state while a download is in flight). A system-level end-to-end test
(`SpeechTests.cs`) additionally proves the catalog composed over a scratch-directory store and a fake download
client through the internal constructor enumerates and tracks state correctly (the public constructor's
default composition is not exercised there).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test uses a unique scratch directory under `Path.GetTempPath()`

#### Acceptance Criteria

An empty known-model list enumerates to an empty list; `AddModels` returns the same catalog
instance for chaining and every model it registers appears in `Enumerate()`; a never-downloaded known
model reports `NotDownloaded`; a download in flight reports `Downloading`; a completed
successful download reports `Downloaded`; a checksum-mismatched download reports
`FailedOrCorrupt`; requesting a download for an unknown model id throws `ArgumentException`; the
`Store` property returns the exact `SpeechModelStore` instance the catalog was composed with; a
null entry in `AddModels` is rejected; a configured mirror reaches the download client; and a
zip-archive model is installed through its own install hook.

#### Test Scenarios

##### Enumerate with an empty known-model list returns an empty list

**Test**: `SpeechModelCatalog_Enumerate_EmptyKnownModels_ReturnsEmptyList`

##### AddModels returns the same catalog instance for chaining

**Test**: `SpeechModelCatalog_AddModels_ChainedCall_ReturnsSameInstance`

##### A model registered through AddModels appears in Enumerate

**Test**: `SpeechModelCatalog_AddModels_Called_AppearsInEnumerate`

Registration of the four real, production sherpa-onnx models through `AddSherpaModels()` is
verified separately by SpeechSherpa; see _SpeechSherpa ModelManagementSubsystem Verification_.

##### The Store property returns the same store instance the catalog composes internally

**Test**: `SpeechModelCatalog_Store_Always_ReturnsInternalStoreInstance`

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

##### AddModels rejects an array containing a null entry

**Test**: `SpeechModelCatalog_AddModels_NullEntry_ThrowsArgumentException`

##### A configured mirror is forwarded through the catalog's download path

**Tests**: `SpeechModelCatalog_DownloadAsync_MirrorConfigured_ForwardsEffectiveUriThroughCatalog`,
`SpeechModelCatalog_DownloadAsync_MirrorConfigured_ForwardsEffectiveUriThroughPublicConstructor`

Verifies that the mirror-rewritten URI reaches the download client both through the internal test
constructor and through the public constructor (against an in-process stubbed server).

##### A zip-archive model is installed through its own install hook

**Test**: `SpeechModelCatalog_DownloadAsync_ZipArchiveModel_InstallsExtractedContentAndReportsDownloaded`

Verifies that the catalog's real download path runs the model's own install hook, leaving the
extracted content (not the archive) installed and the state `Downloaded`.

##### System integration: the catalog enumerates and tracks download state end-to-end

**Test**: `Speech_SystemIntegration_ModelCatalog_EnumeratesAndTracksDownloadState`
