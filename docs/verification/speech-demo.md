# SpeechDemo System Verification Design

This document describes the system-level verification strategy for the SpeechDemo application.

## Verification Approach

SpeechDemo is verified through deterministic system-level integration tests that compose the
application exactly as its own composition root does — the real `AudioDeviceFactory`, the real
`SpeechModelCatalog` over an isolated store root, the real demo service adapters, and the real
view models — and then assert the resulting state. This proves the demo genuinely works against
the shipped library on whatever machine the test run happens to use, including a machine with no
audio backend and no installed model.

Only the Avalonia window itself is left out. A CI runner has no display, so starting a windowing
lifetime would be fragile, and it would add no evidence the view-model tests do not already
provide: the views contain no application logic, all bindings are compiled, and every value the
views show is produced by a view model that is verified directly.

Automated coverage **does not** extend to driving the user interface, to real audio hardware, or to
a real, multi-hundred-megabyte model download. The demo's catalog is populated through
SpeechSherpa's `AddSherpaModels()`, SpeechOnnxKokoro's `AddKokoroModels()`,
the SpeechOnnxNemotronStt `AddNemotronSttModels()`, and the SpeechOnnxChatterboxTurbo
`AddChatterboxTurboModels()` with real, production models covering both the recognition and
synthesis roles, but the full download/install lifecycle is still verified at the subsystem level
against controlled catalog data, never a real network fetch.

System tests reside in `SpeechDemoTests.cs` within the `DemaConsulting.Speech.Demo.Tests`
project. They compose the real shell over the library's real public entry points, and the
test project supplies its own fakes where a unit needs one (catalog service, synthesis and
recognition sessions), so every system requirement is proven through the composed application
rather than a single unit.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, no network access, and no guaranteed physical audio
  hardware
- **Isolation**: Each catalog-composing test roots its model store in a fresh directory under the
  test output folder, so no test ever reads or writes a developer's real installed-model store

## External Interface Simulation

The system tests use no simulation for the composition path: the real library types are
constructed. Where a machine-independent result is required, the tests use the library's own
public honest-unavailable probes (`UnavailableAudioCaptureDeviceProbe`,
`UnavailableAudioPlaybackDeviceProbe`), which is exactly what the library's audio factory itself
falls back to when the PortAudio runtime cannot initialize.

## Acceptance Criteria

A SpeechDemo system-level test run passes when all scenarios below pass without unexpected
exceptions and when the automated verification boundary remains honest: composition and panel
state over the real library are claimed as automated coverage, while user-interface interaction,
physical audio I/O, and real model downloads are explicitly left to manual/local verification.

## System-Level Test Scenarios

### Integration: Real Library Entry Points Compose a Working Shell

**Test**: `SpeechDemo_SystemIntegration_RealLibraryEntryPoints_ComposesWorkingShell`

Verifies that composing the whole demo object graph over the real library produces a shell
offering the four panels (Devices, Model Catalog, Text-to-Speech, Speech-to-Text),
opened on the first, and hosting the injected panel instances.

### Integration: Real Audio Backend Device Enumeration Never Throws

**Test**: `SpeechDemo_SystemIntegration_RealAudioBackend_DeviceEnumerationNeverThrows`

Verifies that building the device panel over the real audio factory succeeds regardless of
whether the machine running the tests has any audio hardware or a working PortAudio runtime.

### Integration: Unavailable Audio Backend Is Explained

**Test**: `SpeechDemo_SystemIntegration_UnavailableAudioBackend_DevicePanelExplainsAbsence`

Verifies that a machine whose audio backend is unavailable still produces a usable panel that
names the possible cause and reports the library's system-default selection, rather than a blank
picker a user would read as a bug.

### Integration: Real Model Catalog Reports Its Known Models

**Test**: `SpeechDemo_SystemIntegration_RealModelCatalog_ReportsKnownModels`

Verifies that the catalog panel composed over the real library catalog, populated through
SpeechSherpa's `AddSherpaModels()`, SpeechOnnxKokoro's `AddKokoroModels()`,
the SpeechOnnxNemotronStt `AddNemotronSttModels()`, and the SpeechOnnxChatterboxTurbo
`AddChatterboxTurboModels()`, lists exactly the real, production models those extension methods
register - spanning both the recognition and synthesis roles - rather than reporting an empty
catalog or only the Sherpa models.

The real multi-hundred-megabyte reference voice pack download is left to manual verification.

### Integration: Real Catalog Leaves the TTS and STT Panels Honestly Empty

**Test**: `SpeechDemo_SystemIntegration_RealCatalog_TtsAndSttPanelsReportEmptyStateHonestly`

Verifies that the synthesis and recognition panels, composed over the real library's device
factory and an isolated model catalog with a fresh, empty install store, both honestly report
having no *installed* model to choose from - known models are registered in the catalog, but
none are downloaded in a fresh store - rather than crashing or silently rendering blank.

### Integration: Downloading a Listed Model Shows the Installed Outcome

**Test**: `SpeechDemo_SystemIntegration_DownloadListedModel_ShowsInstalledOutcome`

Verifies that, in a composed shell over a controlled catalog service, downloading a listed model
ends with the row marked `Downloaded`, with no failure message. A controlled service is used
because automated tests must not depend on network access.

### Integration: Downloading a Listed Model Reports Progress

**Test**: `SpeechDemo_SystemIntegration_DownloadListedModel_ReportsProgress`

Verifies that progress reported by the catalog service during a download reaches the row.

### Integration: A Failed Download Is Reported

**Test**: `SpeechDemo_SystemIntegration_DownloadListedModel_FailureIsReported`

Verifies that a failed download is shown as a failure message on the row without crashing.

### Integration: A Failed Download Is Retryable

**Test**: `SpeechDemo_SystemIntegration_DownloadListedModel_FailedDownloadIsRetryable`

Verifies that the user can retry a failed download to success and that the earlier failure
message is cleared.

### Integration: An Empty Catalog Is Explained

**Test**: `SpeechDemo_SystemIntegration_EmptyCatalog_ModelPanelExplainsEmptiness`

Verifies that a caller-supplied catalog with no models shows the empty-catalog explanation.

### Integration: Mirror Redirection at Launch

**Test**: `SpeechDemo_SystemIntegration_MirrorRedirection_AppliesAtLaunch`

Verifies that a mirror given at launch reaches the downloader options and pre-populates the
catalog panel's mirror field.

### Integration: Mirror Redirection While Running

**Test**: `SpeechDemo_SystemIntegration_MirrorRedirection_AppliesWhileRunning`

Verifies that changing the mirror in the catalog panel while the application runs redirects
subsequent downloads and confirms success.

### Integration: Clearing the Mirror Reverts to Public URIs

**Test**: `SpeechDemo_SystemIntegration_MirrorRedirection_ClearingMirrorRevertsToPublicUris`

Verifies that clearing the mirror in the catalog panel applies no mirror to the service.

### Integration: Launch Options Relocate the Model Store

**Test**: `SpeechDemo_SystemIntegration_LaunchOptions_RelocateModelStore`

Verifies that the model store folder option takes effect in the resulting store options.

### Integration: Launch Options Record the Voices Folder

**Test**: `SpeechDemo_SystemIntegration_LaunchOptions_VoicesDirOptionRecordsVoicesFolder`

Verifies that the user voices folder option is recorded in the launch options.

### Integration: Launch Options Reject Bad Options With an Explanation

**Test**: `SpeechDemo_SystemIntegration_LaunchOptions_RejectBadOptionsWithExplanation`

Verifies that an unrecognized option, and an option missing its value, are rejected with an
explained error. Rejection is verified at the option parser because the entry point starts the
graphical application.

### Integration: Reference Voice Pack Install and Remove

**Test**: `SpeechDemo_SystemIntegration_ReferenceVoicePack_InstallAndRemoveFromCatalogPanel`

Verifies that a user can install and remove the reference voice pack from the catalog panel.

### Integration: Installed Voices Are Offered and the Choice Is Kept

**Test**: `SpeechDemo_SystemIntegration_ReferenceVoicePack_InstalledVoicesOfferedAndChoiceKept`

Verifies that the synthesis voice choice offers the newly installed voices immediately and that a
chosen voice is kept across a refresh.

### Integration: Catalog Panel Shows the Consent Note

**Test**: `SpeechDemo_SystemIntegration_ReferenceVoicePack_CatalogPanelShowsConsentNote`

Verifies that the catalog panel shows the voice consent note beside the pack list.

### Integration: Chatterbox Selection Shows the Consent Note

**Test**: `SpeechDemo_SystemIntegration_ChatterboxSelected_ShowsConsentNote`

Verifies that selecting the Chatterbox Turbo model shows the voice consent note in the synthesis
panel.

### Integration: Chatterbox Selection Shows the Pack Attribution

**Test**: `SpeechDemo_SystemIntegration_ChatterboxSelected_ShowsPackAttribution`

Verifies that selecting the Chatterbox Turbo model with the pack installed shows the pack
attribution in the synthesis panel.

### Integration: A Model Without Parameters Is Explained

**Test**: `SpeechDemo_SystemIntegration_ModelWithoutParameters_SettingsPanelExplainsNoParameters`

Verifies that the settings panel explains a selected model that declares no tunable parameters.

### Integration: Model Parameters Are Rendered and Forwarded to Synthesis

**Test**: `SpeechDemo_SystemIntegration_ModelParameters_RenderedAndForwardedToSynthesis`

Verifies that a selected installed model's declared parameters render with controls appropriate to
their kind and that adjusted values reach the model when synthesis loads.

### Integration: Text-to-Speech Speaks Entered Text

**Test**: `SpeechDemo_SystemIntegration_TextToSpeech_SpeaksEnteredText`

Verifies that entered text, including an inline audio tag, reaches the synthesis session unchanged.

### Integration: Text-to-Speech Reports Lifecycle Status

**Test**: `SpeechDemo_SystemIntegration_TextToSpeech_ReportsLifecycleStatus`

Verifies that playback status passes through synthesizing and playing to idle.

### Integration: Text-to-Speech Stop Ends the Utterance

**Test**: `SpeechDemo_SystemIntegration_TextToSpeech_StopEndsUtteranceWithExplanation`

Verifies that the stop control ends an in-flight utterance and the panel says it was stopped.

### Integration: Text-to-Speech Shows Example Audio Tags

**Test**: `SpeechDemo_SystemIntegration_TextToSpeech_ShowsExampleAudioTags`

Verifies that example audio tags are offered beside the text input.

### Integration: Text-to-Speech Explains a Missing Playback Device

**Test**: `SpeechDemo_SystemIntegration_TextToSpeech_NoPlaybackDeviceIsExplained`

Verifies that speaking with no playback device shows the explanation and speaks nothing.

### Integration: Text-to-Speech Explains an Unavailable Engine

**Test**: `SpeechDemo_SystemIntegration_TextToSpeech_UnavailableEngineIsExplained`

Verifies that speaking with an unavailable engine shows the explanation and speaks nothing.

### Integration: Speech-to-Text Starts Streaming

**Test**: `SpeechDemo_SystemIntegration_SpeechToText_StartsStreaming`

Verifies that starting listening begins streaming.

### Integration: Speech-to-Text Shows the Partial Result

**Test**: `SpeechDemo_SystemIntegration_SpeechToText_ShowsPartialResult`

Verifies that a provisional result is shown as the partial transcript and not committed.

### Integration: Speech-to-Text Commits the Final Result

**Test**: `SpeechDemo_SystemIntegration_SpeechToText_CommitsFinalResult`

Verifies that a final result is committed as a finished line and clears the partial.

### Integration: Speech-to-Text Stop Ends the Session

**Test**: `SpeechDemo_SystemIntegration_SpeechToText_StopEndsSession`

Verifies that stopping listening stops the session and returns the panel to idle.

### Integration: Speech-to-Text Explains a Missing Capture Device

**Test**: `SpeechDemo_SystemIntegration_SpeechToText_NoCaptureDeviceIsExplained`

Verifies that starting with no capture device shows the explanation.

### Integration: Speech-to-Text Explains an Unavailable Engine

**Test**: `SpeechDemo_SystemIntegration_SpeechToText_UnavailableEngineIsExplained`

Verifies that starting with an unavailable engine shows the explanation.

### Integration: Refreshing Every Panel Never Throws

**Test**: `SpeechDemo_SystemIntegration_RefreshAllPanels_NeverThrows`

Verifies that invoking every panel's refresh command on a fully composed demo — which is what a
user clicking the refresh buttons actually exercises — completes without faulting.
