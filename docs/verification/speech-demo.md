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
project. They compose the real shell with the library's fakes (catalog service, synthesis and
recognition sessions) so every system requirement is proven through the composed application
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

### Integration: Downloading a Listed Model Reports Progress and Installed Outcome

**Test**: `SpeechDemo_SystemIntegration_DownloadListedModel_ReportsProgressAndInstalledOutcome`

Verifies that, in a composed shell over a controlled catalog service, downloading a listed model
reports progress and ends with the row marked `Downloaded`, with no failure message. A controlled
service is used because automated tests must not depend on network access.

### Integration: A Failed Download Is Reported and Retryable

**Test**: `SpeechDemo_SystemIntegration_DownloadListedModel_FailureIsReportedAndRetryable`

Verifies that a failed download is shown as a failure message on the row without crashing, and
that the user can retry the download to success.

### Integration: Mirror Redirection at Launch and While Running

**Test**: `SpeechDemo_SystemIntegration_MirrorRedirection_AppliesAtLaunchAndWhileRunning`

Verifies that a mirror given at launch reaches the catalog service, and that changing or clearing
the mirror in the catalog panel while the application runs redirects subsequent downloads.

### Integration: Launch Options Configure the Store and Voices and Reject Bad Options

**Test**: `SpeechDemo_SystemIntegration_LaunchOptions_ConfigureStoreAndVoicesAndRejectBadOptions`

Verifies that the model store folder and user voices folder options take effect in the resulting
store and voice settings, and that an unrecognized or incomplete option is rejected with an
explained error. Rejection is verified at the option parser because the entry point starts the
graphical application.

### Integration: Reference Voice Pack Install, Remove and Voice Choice

**Test**: `SpeechDemo_SystemIntegration_ReferenceVoicePack_InstallRemoveAndChooseVoice`

Verifies that a user can install and remove the reference voice pack from the catalog panel, that
the synthesis voice choice refreshes immediately, and that a voice can be chosen.

### Integration: Chatterbox Selection Shows Consent and Attribution

**Test**: `SpeechDemo_SystemIntegration_ChatterboxSelected_ShowsConsentAndAttribution`

Verifies that selecting the Chatterbox Turbo model shows the voice consent note and the pack
attribution in the synthesis panel.

### Integration: Model Parameters Are Rendered and Forwarded to Synthesis

**Test**: `SpeechDemo_SystemIntegration_ModelParameters_RenderedAndForwardedToSynthesis`

Verifies that a selected installed model's declared parameters render with controls appropriate to
their kind and that adjusted values reach the model when synthesis loads.

### Integration: Text-to-Speech Speaks and Stops

**Test**: `SpeechDemo_SystemIntegration_TextToSpeech_SpeaksAndStops`

Verifies that entered text is spoken through the selected model, playback status is reported, and
the stop control ends playback.

### Integration: Speech-to-Text Streams Partial and Final Transcript Then Stops

**Test**: `SpeechDemo_SystemIntegration_SpeechToText_StreamsPartialAndFinalThenStops`

Verifies that a started recognition session shows the partial then committed final transcript and
that the user can stop it.

### Integration: Refreshing Every Panel Never Throws

**Test**: `SpeechDemo_SystemIntegration_RefreshAllPanels_NeverThrows`

Verifies that invoking every panel's refresh command on a fully composed demo — which is what a
user clicking the refresh buttons actually exercises — completes without faulting.
