## ModelManagementSubsystem Verification

### Verification Approach

The ModelManagementSubsystem is verified through deterministic unit tests against a fake
`IModelDownloadClient` for `SpeechModelStore`'s atomic-swap/install-state logic and
`SpeechModelDownloader`'s queueing/verification/failure-state orchestration, plus a real
end-to-end test of `HttpModelDownloadClient` against an in-process `WireMock.Net`-stubbed HTTP
server. This proves both the pure orchestration logic (fast,
deterministic, no network) and the one real network-facing implementation (genuinely downloads
over HTTP) without requiring any external network access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each `SpeechModelStore`/`SpeechModelDownloader` test uses a unique scratch
  directory under `Path.GetTempPath()`, deleted on test disposal
- **Stubbed HTTP**: `HttpModelDownloadClientTests` starts an in-process `WireMock.Net`
  `WireMockServer`, stubbing one fixed response per test; no real network access

### Unit Verification Documents

See each unit's own verification document for its detailed test scenarios:
`speech-model-store.md`, `speech-model-store-options.md`, `speech-model-store-exception.md`,
`speech-model-downloader.md`, `speech-model-downloader-options.md`, `download-mirror.md`,
`speech-model-download-file.md`,
`speech-model-download-descriptor.md`, `speech-model-download-progress.md`,
`i-model-download-client.md`, `http-model-download-client.md`,
`speech-model-descriptor-enums.md`, `speech-model-parameters.md`, `speech-model-contract.md`,
`speech-model-descriptor.md`, `speech-model-catalog.md`, `reference-voice-pack.md`,
`reference-voice-pack-installer.md`, `reference-voice-library.md`.

### Test Scenarios

The scenarios below are subsystem-level behaviors; each is proven by the named tests, which
exercise the units together through the fake download client (no network).

#### Download, verify and install

**Stimulus**: `SpeechModelDownloader.DownloadAsync` for a descriptor served by the fake client.

**Expected**: Content is staged, verified and swapped into `current/`; a checksum mismatch
installs nothing; a canceled download installs nothing.

**Tests**: `SpeechModelDownloaderTests.cs` -
`SpeechModelDownloader_DownloadAsync_ChecksumMismatch_ReportsChecksumMismatchAndInstallsNothing`,
`SpeechModelDownloader_DownloadAsync_CanceledMidDownload_ThrowsAndInstallsNothing`

#### Repeat install is a fast path

**Stimulus**: A second `DownloadAsync` for an installed model id.

**Expected**: `Installed` is returned without fetching or staging; the prior install is intact.

**Tests**: `SpeechModelDownloader_DownloadAsync_AlreadyInstalled_ReturnsInstalledWithoutFetchingOrStaging`,
`SpeechModelDownloader_DownloadAsync_SecondCallAfterPriorSuccess_TakesFastPathAndLeavesPriorInstallIntact`

#### Mirror redirection

**Stimulus**: A downloader configured with a mirror.

**Expected**: The client is asked for the mirror-resolved URI.

**Test**: `SpeechModelDownloader_DownloadAsync_MirrorConfigured_ForwardsEffectiveUri`

#### Catalog tracks download state

**Stimulus**: Enumerate and query the catalog before and during a download.

**Expected**: `NotDownloaded` before, `Downloading` while in flight.

**Tests**: `SpeechModelCatalog_Enumerate_NeverDownloaded_ReportsNotDownloaded`,
`SpeechModelCatalog_GetState_DownloadInFlight_ReportsDownloading`

#### Uninstall leaves live staging alone

**Stimulus**: `SpeechModelStore.Uninstall` while another operation has staging in progress.

**Expected**: The install is removed, replaced directories are cleaned and live staging survives.

**Test**: `SpeechModelStore_Uninstall_LeavesLiveStagingAndRemovesReplacedDirectories`

#### Reference voice pack install, upgrade and uninstall

**Stimulus**: `ReferenceVoiceLibrary.InstallPackAsync` for a synthetic archive, then an upgrade from
voices-only to emotions, then `UninstallPack`, including while an install is in flight.

**Expected**: Only allow-listed entries are installed; a failed or canceled upgrade keeps the previous
install; uninstall keeps the user folder and, while an install is in flight, throws
`SpeechModelStoreException` without blocking.

**Tests**: `ReferenceVoiceLibrary_InstallPack_Success_InstallsVoicesAndUpdatesState`,
`ReferenceVoiceLibrary_InstallPack_UpgradeFromVoicesOnly_AddsEmotions`,
`ReferenceVoiceLibrary_InstallPack_FailedUpgrade_KeepsPreviousInstall`,
`ReferenceVoiceLibrary_UninstallPack_RemovesPackKeepsUserFolder`,
`ReferenceVoiceLibrary_UninstallPack_WhileInstalling_ThrowsThenSucceedsAfterwards`

#### Reference voice merge

**Stimulus**: `ReferenceVoiceLibrary.EnumerateVoices` with caller, user and pack voices.

**Expected**: Voices appear in precedence order and the first id wins.

**Test**: `ReferenceVoiceLibrary_EnumerateVoices_MergesInPrecedenceOrderFirstIdWins`

#### Real HTTP download

**Stimulus**: `HttpModelDownloadClient` against the in-process WireMock.Net server.

**Expected**: Exact bytes with monotonically increasing progress; non-2xx throws
`HttpRequestException`. The test names are listed in `http-model-download-client.md`.

### Acceptance Criteria

A ModelManagementSubsystem test run passes when: `SpeechModelStore` never reports a model installed
unless both `current/` and a valid manifest are present; a second `DownloadAsync` call for an
already-installed model id returns `Installed` immediately without any network or staging activity,
leaving a prior successful install completely untouched regardless of what the second call
supplies; a canceled download discards its staging directory and propagates
`OperationCanceledException` completely unchanged, never reclassified as a failure outcome; a
download/install failure is classified into `HttpError`, `NetworkBlocked`, `IoFailure`, or the
generic `Failed` fallback exactly as documented in `speech-model-downloader.md`; with no
`DownloadMirror` configured every file resolves and fetches from its own declared URI completely
unchanged, and with one configured every file resolves beneath the mirror, with its configured
authentication applied - by the `HttpModelDownloadClient` instance constructed with it - to every
request that instance issues; `HttpModelDownloadClient` genuinely
downloads exact bytes with monotonically increasing progress from a WireMock.Net-stubbed server,
throwing `HttpRequestException` for a non-2xx response, and sending a preemptive `Authorization`
header (Basic or Bearer) only when a mirror is supplied at construction; every tunable-parameter descriptor
rejects an internally inconsistent
range/option-set/default at construction; `SpeechModelParameterDiagnostics` throws
`ArgumentException` for a value invalid for a parameter a model declares while silently ignoring
(with only an `Info` diagnostic) a supplied key naming a parameter the model does not declare; and
`SpeechModelCatalog` correctly reports `NotDownloaded`, `Downloading`, `Downloaded`, and
`FailedOrCorrupt` for an injected fake model as a download is requested, in progress, completes, or
fails, using injected fakes for deterministic state-resolution coverage, and with models registered
through `AddModels` appearing in its enumeration; and the reference-voice units install only the
allow-listed pack entries safely, keep a failed or canceled upgrade's previous install, refuse
(without blocking) to uninstall a pack while its install is in flight, never touch the user voice
folder, and list voices merged in the documented precedence with emotion clips exposed as mood
variants of a voice rather than as extra voices. The concrete
`IRecognitionModel`/`ISynthesisModel` implementations shipped by this repository are not part of
this subsystem; they are verified by the sibling SpeechSherpa library (see _SpeechSherpa
ModelManagementSubsystem Verification_).
