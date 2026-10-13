## SpeechDemo DeviceSelectionSubsystem Verification

### Verification Approach

The DeviceSelectionSubsystem is verified at subsystem level by composing a real
`DeviceSelectionViewModel` over a real `AudioDeviceService` and an `AudioDeviceFactory` built with
substitute probes, proving the pickers show what the library's probes report and that a refresh
reaches the library's PortAudio environment. Unit-level behavior is verified in the unit
verification documents for AudioDeviceService and DeviceSelectionViewModel.

Automated tests do **not** claim proof that real microphones and speakers are enumerated
correctly; that depends on the library's PortAudio integration and remains a manual/local
verification activity on hardware-equipped machines.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own factory, adapter and view model
- **Test doubles**: NSubstitute fakes of `IAudioCaptureDeviceProbe` and
  `IAudioPlaybackDeviceProbe`, and a fake PortAudio API behind a `PortAudioEnvironment`. Where a test
  supplies only a capture probe, the playback probe is the library's default one over that fake
  environment; the fake throws `NotSupportedException` for everything except initialize and
  terminate, so that probe reports zero playback devices.

### Acceptance Criteria

A DeviceSelectionSubsystem test run passes when: the pickers list exactly what the library's
probes report and preselect the first device of each direction; probes that report nothing yield
empty pickers with an explanatory message and the library's system-default selections; and a
refresh through the panel makes the library re-scan its device table while a still-reported
selection is kept, and a refresh the library cannot complete leaves the lists and selections
untouched with the failure explained.

### Test Scenarios

#### DeviceSelectionSubsystem_Composed_ProbesReportDevices_PickersListThem

**Scenario**: The view model is composed over the real adapter and a factory whose probes report
two capture devices and one playback device.

**Expected**: Both pickers list exactly those devices.

#### DeviceSelectionSubsystem_Composed_ProbesReportDevices_PickersPreselectFirstOfEachDirection

**Scenario**: The view model is composed over the real adapter and a factory whose probes report
two capture devices and one playback device.

**Expected**: The first device of each direction is preselected, and the derived selections are the
library's name-based values for them.

#### DeviceSelectionSubsystem_Composed_ProbesReportNothing_PickersExplainAndDefault

**Scenario**: The view model is composed over the real adapter and a factory whose probes report
nothing.

**Expected**: Both pickers are empty with an explanatory status and both selections are the
library's system default.

#### DeviceSelectionSubsystem_Composed_Refresh_ReScansLibrary

**Scenario**: The composed panel is refreshed over a factory built on a fake PortAudio
environment.

**Expected**: The environment is terminated once and initialized twice (the initial initialization
plus the re-scan), and the capture and playback lists are unchanged.

#### DeviceSelectionSubsystem_Composed_Refresh_DeviceStillReported_KeepsSelection

**Scenario**: The composed panel is refreshed over a factory whose probe still reports both
capture devices, with the second capture device selected.

**Expected**: The second capture device stays selected.

#### DeviceSelectionSubsystem_Composed_RefreshFails_KeepsListsAndSelections

**Scenario**: The composed panel is refreshed over a fake PortAudio environment whose native
teardown throws.

**Expected**: The capture and playback lists and selections are unchanged.

#### DeviceSelectionSubsystem_Composed_RefreshFails_ExplainsInStatus

**Scenario**: The same failing refresh.

**Expected**: Both status texts carry the failure's message. The in-use refusal (`AudioDeviceInUseException`) takes the same
path in the view model but needs an open stream, which cannot be created without hardware, so
it is verified at unit level in the DeviceSelectionViewModel verification document.
