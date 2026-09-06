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

Automated coverage **does not** extend to driving the user interface, to real audio hardware, or
to a real, multi-hundred-megabyte model download. The library's compiled-in catalog now contains
four real, production models - `SherpaOnnxZipformerEnRecognitionModel`,
`SherpaOnnxNemotronStreamingEnRecognitionModel`, `SherpaOnnxVitsLibriTtsEnglishSynthesisModel`, and
`SherpaOnnxKokoroEnglishSynthesisModel` - covering both the recognition and synthesis roles, but
the full download/install lifecycle is still verified at the subsystem level against controlled
catalog data, never a real network fetch.

System tests reside in `SpeechDemoTests.cs` within the `DemaConsulting.Speech.Demo.Tests`
project, with the model-download outcome scenario additionally proven by
`ModelCatalogViewModelTests.cs` in the same project.

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

## System-Level Test Scenarios

### Integration: Real Library Entry Points Compose a Working Shell

**Test**: `SpeechDemo_SystemIntegration_RealLibraryEntryPoints_ComposesWorkingShell`

Verifies that composing the whole demo object graph over the real library produces a shell
offering this phase's four panels (Devices, Model Catalog, Text-to-Speech, Speech-to-Text),
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

Verifies that the catalog panel composed over the real library catalog lists exactly
`SpeechModelCatalog.KnownModels`' entries - the library's four real, production models spanning
both the recognition and synthesis roles - rather than reporting an empty catalog.

### Integration: Real Catalog Leaves the TTS and STT Panels Honestly Empty

**Test**: `SpeechDemo_SystemIntegration_RealCatalog_TtsAndSttPanelsReportEmptyStateHonestly`

Verifies that the synthesis and recognition panels, composed over the real library's device
factory and an isolated model catalog with a fresh, empty install store, both honestly report
having no *installed* model to choose from - known models exist in the compiled-in catalog, but
none are downloaded in a fresh store - rather than crashing or silently rendering blank.

### Integration: Downloading a Model Marks It Installed

**Test**: `ModelCatalogViewModel_DownloadAsync_Installed_MarksModelDownloaded`

Verifies that invoking the download command for a listed model against a catalog whose download
succeeds updates that row to report the `Downloaded` state, full progress, no further downloadable
action, and no failure message - proving a user can download a listed model and see the outcome
reported. This is verified against a controlled fake catalog service rather than a real network
download, consistent with SpeechDemo-Models-RealCatalogReporting's real-catalog enumeration proof.

### Integration: Refreshing Every Panel Never Throws

**Test**: `SpeechDemo_SystemIntegration_RefreshAllPanels_NeverThrows`

Verifies that invoking every panel's refresh command on a fully composed demo — which is what a
user clicking the refresh buttons actually exercises — completes without faulting.

## Acceptance Criteria

A SpeechDemo system-level test run passes when all scenarios above pass without unexpected
exceptions and when the automated verification boundary remains honest: composition and panel
state over the real library are claimed as automated coverage, while user-interface interaction,
physical audio I/O, and real model downloads are explicitly left to manual/local verification.
