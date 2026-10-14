## ModelManagementSubsystem Verification

### Verification Approach

The ModelManagementSubsystem is verified through deterministic unit tests against a fake
`IModelDownloadClient` for `SpeechModelStore`'s replace/install-state logic and
`SpeechModelDownloader`'s fetch/verification/failure-state orchestration, plus a real
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

**Expected**: Content is staged, verified and switched into `current/`; a checksum mismatch
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

#### Downloads are serialized per model id and concurrent across models

**Stimulus**: Two concurrent `DownloadAsync` calls for the same model id, then for two different model ids.

**Expected**: Same-id calls run one after the other (a call canceled while waiting never reaches the
client); different-id calls run concurrently with no pool limit.

**Tests**: `SpeechModelDownloader_DownloadAsync_TwoConcurrentRequestsForSameModelId_AreSerialized`,
`SpeechModelDownloader_DownloadAsync_CanceledWhileQueued_ThrowsWithoutReachingClient`,
`SpeechModelDownloader_DownloadAsync_TwoConcurrentRequestsForDifferentModels_RunConcurrently`

#### Catalog tracks download state

**Stimulus**: Enumerate and query the catalog before and during a download.

**Expected**: `NotDownloaded` before, `Downloading` while in flight.

**Tests**: `SpeechModelCatalog_Enumerate_NeverDownloaded_ReportsNotDownloaded`,
`SpeechModelCatalog_GetState_DownloadInFlight_ReportsDownloading`

#### Uninstall leaves live staging alone

**Stimulus**: `SpeechModelStore.Uninstall` while another operation has staging in progress.

**Expected**: The install is removed, replaced directories are cleaned and live staging survives.

**Tests**: `SpeechModelStore_Uninstall_LiveStagingDirectory_IsLeftAlone`,
`SpeechModelStore_Uninstall_ReplacedDirectories_AreRemoved`

#### Failed replacement preserves the prior install

**Stimulus**: `SpeechModelStore.CompleteInstall` over an existing install where switching the staged
content into place fails.

**Expected**: The prior install is restored and still reads as installed; a momentary not-installed
state during the switch is permitted but never persists after a failure.

**Tests**: `SpeechModelStore_CompleteInstall_ExistingCurrent_ReplacesContentAndCleansUpOldDirectory`,
`SpeechModelStore_CompleteInstall_SecondMoveFails_RollsBackPreviousInstall`

#### Reference voice pack install, upgrade and uninstall

**Stimulus**: `ReferenceVoiceLibrary.InstallPackAsync` for a synthetic archive, then an upgrade from
voices-only to emotions, then `UninstallPackAsync`, including while an install is in flight.

**Expected**: Only allow-listed entries are installed; a failed or canceled upgrade keeps the previous
install; uninstall keeps the user folder and, while an install is in flight, waits for it and then removes
the pack (or throws `OperationCanceledException` if canceled while waiting).

**Tests**: `ReferenceVoiceLibrary_InstallPack_Success_InstallsVoicesAndUpdatesState`,
`ReferenceVoiceLibrary_InstallPack_UpgradeFromVoicesOnly_AddsEmotions`,
`ReferenceVoiceLibrary_InstallPack_FailedUpgrade_KeepsPreviousInstall`,
`ReferenceVoiceLibrary_UninstallPackAsync_RemovesPackKeepsUserFolder`,
`ReferenceVoiceLibrary_UninstallPackAsync_WhileInstalling_WaitsThenRemoves`,
`ReferenceVoiceLibrary_UninstallPackAsync_CanceledWhileWaiting_LeavesInstallIntact`,
`ReferenceVoiceLibrary_UninstallPackAsync_AlreadyCanceled_DoesNothingAndReleasesLock`

#### Reference voice merge

**Stimulus**: `ReferenceVoiceLibrary.EnumerateVoices` with caller, user and pack voices.

**Expected**: Voices appear in precedence order and the first id wins.

**Test**: `ReferenceVoiceLibrary_EnumerateVoices_MergesInPrecedenceOrderFirstIdWins`

#### Real HTTP download

**Stimulus**: `HttpModelDownloadClient` against the in-process WireMock.Net server.

**Expected**: Exact bytes with monotonically increasing progress; non-2xx throws
`HttpRequestException`.

**Tests**: `HttpModelDownloadClient_DownloadAsync_StubbedServer_DownloadsExactBytesWithProgress`,
`HttpModelDownloadClient_DownloadAsync_NonSuccessResponse_ThrowsHttpRequestException`. Further
scenarios are listed in `http-model-download-client.md`.

#### Failure classification

**Stimulus**: A download through the fake client that fails with an HTTP error, a network-level
failure, a local I/O failure or an unexpected exception.

**Expected**: The result is classified as `HttpError`, `NetworkBlocked`, `IoFailure` or the generic
`Failed` fallback; a checksum mismatch is `ChecksumMismatch`; cancellation propagates unchanged.

**Tests**: `SpeechModelDownloader_DownloadAsync_HttpRequestExceptionWithStatusCode_ReportsHttpError`,
`SpeechModelDownloader_DownloadAsync_HttpRequestExceptionWrappingSocketException_ReportsNetworkBlocked`,
`SpeechModelDownloader_DownloadAsync_IoException_ReportsIoFailure`,
`SpeechModelDownloader_DownloadAsync_ChecksumMismatch_ReportsChecksumMismatchAndInstallsNothing`

#### Parameter diagnostics

**Stimulus**: A `parameterValues` bag containing an invalid value, or an unknown key, is validated
against a model's declared parameters.

**Expected**: An invalid value throws `ArgumentException`; an unknown key is reported through an
`Info` diagnostic and otherwise ignored.

**Tests**: `SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterOutOfRange_Throws`,
`SpeechModelParameterDiagnostics_ValidateAndReport_UnrecognizedParameterId_ReportsInfoAndDoesNotThrow`,
and, for null arguments, the four
`SpeechModelParameterDiagnostics_ValidateAndReport_Null{ModelId,DeclaredParameters,Diagnostics,Category}_ThrowsArgumentNullException`
tests (`Speech-Models-TunableParameterArgumentValidation`)

#### Catalog registration

**Stimulus**: Models are registered through `SpeechModelCatalog.AddModels` and the catalog is
enumerated.

**Expected**: The registered models appear in the enumeration, and `AddModels` returns the same
catalog instance.

**Tests**: `SpeechModelCatalog_AddModels_Called_AppearsInEnumerate`,
`SpeechModelCatalog_AddModels_ChainedCall_ReturnsSameInstance`

#### Store reports installed only with `current/` and a valid manifest

**Stimulus**: `SpeechModelStore.IsInstalled` for no install, `current/` without a manifest, and a
malformed, empty or unreadable manifest.

**Expected**: `false` in every case, never an exception.

**Tests**: `SpeechModelStore_IsInstalled_NoInstall_ReturnsFalse`,
`SpeechModelStore_IsInstalled_CurrentWithoutManifest_ReturnsFalse`,
`SpeechModelStore_IsInstalled_ManifestIsMalformedJson_ReturnsFalse`,
`SpeechModelStore_CompleteInstall_ManifestWriteFails_ThrowsAndReadsAsNotInstalled`

#### Storage root is configurable

**Stimulus**: Construct a store with no options, and with a root path override.

**Expected**: The default root is under `LocalApplicationData`; an override is used verbatim; a null,
empty or whitespace-only override means "no override" and the default root is used.

**Tests**: `SpeechModelStore_Constructor_NoOptions_ResolvesUnderLocalApplicationData`,
`SpeechModelStore_Constructor_RootPathOverride_UsesOverrideVerbatim`,
`SpeechModelStore_Constructor_NullOrEmptyRootPathOverride_UsesDefaultRoot`

#### Failed uninstall is signaled and never reads as installed

**Stimulus**: Uninstall while a file handle is open in `current/`, or with an undeletable manifest.

**Expected**: `SpeechModelStoreException` is thrown; after a failed uninstall that removed the
manifest the model reads as not installed (Windows-only open-handle scenario).

**Tests**: `SpeechModelStore_Uninstall_OpenFileHandleInCurrent_ThrowsSpeechModelStoreException`,
`SpeechModelStore_Uninstall_ManifestDeleteFails_ThrowsSpeechModelStoreException`

#### Download descriptors validate themselves

**Stimulus**: Build download files and descriptors with an HTTP URI, a bad checksum, an escaping
install path, an empty list or duplicate install paths.

**Expected**: `ArgumentException` (or `ArgumentNullException`) at construction.

**Tests**: `SpeechModelDownloadFile_Constructor_HttpUri_ThrowsArgumentException`,
`SpeechModelDownloadFile_Constructor_InvalidChecksum_ThrowsArgumentException`,
`SpeechModelDownloadDescriptor_Constructor_EmptyFileList_ThrowsArgumentException`,
`SpeechModelDownloadDescriptor_Constructor_DuplicateInstallPaths_ThrowsArgumentException`

#### Progress is reported

**Stimulus**: A download through the fake client with an `IProgress` sink; progress fractions with a
known, unknown and zero total.

**Expected**: Increasing progress is reported; the fraction is computed, `null` or one respectively.

**Tests**: `SpeechModelDownloader_DownloadAsync_ValidPayload_InstallsModelAndReportsProgress`,
`SpeechModelDownloadProgress_FractionComplete_KnownTotal_ComputesFraction`,
`SpeechModelDownloadProgress_FractionComplete_UnknownTotal_ReturnsNull`,
`SpeechModelDownloadProgress_FractionComplete_ZeroTotalBytes_ReturnsOne`

#### Mirror authentication is sent only to the mirror

**Stimulus**: `HttpModelDownloadClient` with a bearer or credential mirror, against the WireMock.Net server.

**Expected**: The matching `Authorization` header is sent to the mirror, none outside it, and none on a
redirected request.

**Tests**: `HttpModelDownloadClient_DownloadAsync_BearerTokenMirror_SendsBearerAuthorizationHeader`,
`HttpModelDownloadClient_DownloadAsync_CredentialsMirror_SendsBasicAuthorizationHeader`,
`HttpModelDownloadClient_DownloadAsync_NonMirrorUriWithMirrorConfigured_SendsNoAuthorizationHeader`,
`HttpModelDownloadClient_DownloadAsync_MirrorRedirect_StripsAuthorizationHeaderOnRedirectedRequest`

#### Timeouts are classified as network-blocked

**Stimulus**: The built-in client's own timeout, and a non-timeout cancellation from an injected client.

**Expected**: `NetworkBlocked` for the internal timeout; `Failed` for an unrelated cancellation exception.

**Tests**: `SpeechModelDownloader_DownloadAsync_HttpModelDownloadClientInternalTimeout_ReportsNetworkBlocked`,
`SpeechModelDownloader_DownloadAsync_HttpModelDownloadClientNonTimeoutCancellation_ReportsFailed`

#### Catalog states and store exposure

**Stimulus**: A catalog download that succeeds, one that fails its checksum, and a read of `Store`.

**Expected**: `Downloaded` after success; `FailedOrCorrupt` after a failure; `Store` is the catalog's own store,
so a host resolves an installed model's directory and composes a recognizer or synthesizer through the same
catalog instance (`Speech-Models-CatalogStoreComposition`); a mirror passed to the catalog constructor is used.

**Tests**: `SpeechModelCatalog_DownloadAsync_ValidModel_ReportsDownloadedAfterCompletion`,
`SpeechModelCatalog_DownloadAsync_ChecksumMismatch_ReportsFailedOrCorrupt`,
`SpeechModelCatalog_Store_Always_ReturnsInternalStoreInstance`,
`SpeechModelCatalog_DownloadAsync_MirrorConfigured_ForwardsEffectiveUriThroughPublicConstructor`

#### Model contract declarations and hooks

**Stimulus**: Fake recognition and synthesis models declaring parameters, audio formats and defaults.

**Expected**: Declarations are exposed through the contract; default hooks leave staging and text unchanged.

**Tests**: `ISpeechModel_Parameters_DeclaresEveryDescriptorKind`,
`ISpeechModel_AudioTagSupportAndDownloadDescriptor_AreExposed`,
`IRecognitionModel_AudioFormat_DeclaredByModel_IsExposed`,
`ISynthesisModel_PreferredAudioFormat_DeclaredByModel_IsExposed`,
`ISpeechModel_InstallAsync_DefaultImplementation_CompletesWithoutModifyingStagingDirectory`,
`ISpeechModel_NormalizeText_DefaultImplementation_ReturnsInputUnchanged`

### Requirement Coverage Summary

Each subsystem requirement is verified at its own level by the tests named in
`docs/reqstream/speech/model-management-subsystem.yaml`; the list maps requirement groups to the
scenarios above, and every unit requirement is covered by the unit verification documents.

- `Speech-Models-AtomicInstall`, `-InstallStateQuery`, `-Uninstall`, `-LeftoverCleanup`: Download, verify and install;
  Failed replacement preserves the prior install; Store reports installed only with `current/` and a valid manifest;
  Failed uninstall is signaled; Uninstall leaves live staging alone
- `Speech-Models-ConfigurableRoot`: Storage root is configurable
- `Speech-Models-DownloadOrchestration`, `-PerModelDownloadSerialization`, `-CrossModelDownloadConcurrency`,
  `-HonestFailureStates`, `-PriorInstallProtected`: Download, verify and install; Repeat install is a fast path;
  Downloads are serialized per model id and concurrent across models
- `Speech-Models-RicherFailureClassification`, `-FailureErrorPopulated`, `-CallerCancellationNotAFailure`,
  `-DownloadFailureDiagnostics`: Failure classification; Timeouts are classified as network-blocked
- `Speech-Models-DownloadDescriptors`, `-DownloadProgress`, `-MockableHttpSeam`: Download descriptors validate
  themselves; Progress is reported; Real HTTP download
- `Speech-Models-MirrorRedirect`, `-MirrorAuthentication`, `-MirrorAuthenticationRedirectStripping`,
  `-MirrorAuthenticationTransportPolicy`: Mirror redirection; Mirror authentication is sent only to the mirror
- `Speech-Models-ContractDeclaration`, `-ContractParameterDeclaration`, `-ContractAudioTagAndDownloadDeclaration`,
  `-ContractLicenseDeclaration`, `-ContractHooks`, `-ContractRoleMembers`: Model contract declarations and hooks
- `Speech-Models-TunableParameters`, `-TunableParameterDescriptorValidation`, `-TunableParameterValueValidation`,
  `-TunableParameterArgumentValidation`, `-TunableParameterUnrecognizedIdTolerance`: Parameter diagnostics
- `Speech-Models-CatalogEnumeration`, `-CatalogStateReporting`, `-CatalogStoreComposition`: Catalog registration;
  Catalog tracks download state; Catalog states and store exposure
- `Speech-Models-ReferenceVoice*`: Reference voice pack install, upgrade and uninstall; Reference voice merge

### Acceptance Criteria

A ModelManagementSubsystem test run passes when all of the following hold:

- `SpeechModelStore` never reports a model installed unless both `current/` and a valid manifest are
  present, and a failed replacement leaves the prior install usable.
- A second `DownloadAsync` call for an already-installed model id returns `Installed` immediately
  without any network or staging activity, leaving a prior successful install completely untouched
  regardless of what the second call supplies.
- A canceled download discards its staging directory and propagates `OperationCanceledException`
  completely unchanged, never reclassified as a failure outcome.
- A download/install failure is classified into `HttpError`, `NetworkBlocked`, `IoFailure`, or the
  generic `Failed` fallback exactly as documented in `speech-model-downloader.md`.
- With no `DownloadMirror` configured every file resolves and fetches from its own declared URI
  completely unchanged; with one configured every file resolves beneath the mirror, with its
  configured authentication applied - by the `HttpModelDownloadClient` instance constructed with
  it - to every request that instance issues.
- `HttpModelDownloadClient` genuinely downloads exact bytes with monotonically increasing progress
  from a WireMock.Net-stubbed server, throws `HttpRequestException` for a non-2xx response, and
  sends a preemptive `Authorization` header (Basic or Bearer) only when a mirror is supplied at
  construction.
- Every tunable-parameter descriptor rejects an internally inconsistent range/option-set/default at
  construction.
- `SpeechModelParameterDiagnostics` throws `ArgumentException` for a value invalid for a parameter
  a model declares, and for a supplied key naming a parameter the model does not declare it ignores
  the key but reports it through an `Info` diagnostic.
- `SpeechModelCatalog` correctly reports `NotDownloaded`, `Downloading`, `Downloaded`, and
  `FailedOrCorrupt` for an injected fake model as a download is requested, in progress, completes,
  or fails, and models registered through `AddModels` appear in its enumeration.
- The reference-voice units install only the allow-listed pack entries safely, keep a failed or
  canceled upgrade's previous install, make an uninstall that arrives during an in-flight install
  wait for it and then remove the pack, never touch the user voice folder, and list voices merged in
  the documented precedence with emotion clips exposed as mood variants of a voice rather than as
  extra voices.

The concrete
`IRecognitionModel`/`ISynthesisModel` implementations shipped by this repository are not part of
this subsystem; they are verified by the sibling SpeechSherpa library (see _SpeechSherpa
ModelManagementSubsystem Verification_).
