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
  `IAudioPlaybackDeviceProbe`, and a fake PortAudio API behind a `PortAudioEnvironment`

### Acceptance Criteria

A DeviceSelectionSubsystem test run passes when: the pickers list exactly what the library's
probes report and preselect the first device of each direction; probes that report nothing yield
empty pickers with an explanatory message and the library's system-default selections; and a
refresh through the panel makes the library re-scan its device table while a still-reported
selection is kept.

### Test Scenarios

#### DeviceSelectionSubsystem_Composed_ProbesReportDevices_PickersListThem

**Scenario**: The view model is composed over the real adapter and a factory whose probes report
two capture devices and one playback device.

**Expected**: Both pickers list exactly those devices, the first of each is preselected, and the
derived selections are the library's name-based values for them.

**Requirement coverage**: `SpeechDemo-DeviceSelection-PanelListsLibraryDevices`.

#### DeviceSelectionSubsystem_Composed_ProbesReportNothing_PickersExplainAndDefault

**Scenario**: The view model is composed over the real adapter and a factory whose probes report
nothing.

**Expected**: Both pickers are empty with an explanatory status and both selections are the
library's system default.

**Requirement coverage**: `SpeechDemo-DeviceSelection-EmptyPanelExplained`.

#### DeviceSelectionSubsystem_Composed_Refresh_ReScansLibraryAndKeepsSelection

**Scenario**: The composed panel is refreshed over a factory built on a fake PortAudio
environment, with the second capture device selected.

**Expected**: The environment is terminated once and initialized twice (the initial initialization
plus the re-scan), and the second capture device stays selected.

**Requirement coverage**: `SpeechDemo-DeviceSelection-RefreshReachesLibrary`.
