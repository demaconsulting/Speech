## SpeechCli DeviceCommandsSubsystem Verification

### Verification Approach

The DeviceCommandsSubsystem is verified at subsystem level by running the built tool as a child
process (`IntegrationTests.cs`) for the operator-visible outcomes of its three commands, and by the
headless-machine tests that run `list-devices` and `doctor` over fake probes that report no audio
backend at all. Unit-level behavior is verified in the unit verification documents for
ListDevicesCommand, DevicesTestCommand and DoctorCommand.

The out-of-process tests pass whether or not the host has audio hardware: `list-devices` exits
cleanly either way and `doctor` treats a missing audio backend as informational. They therefore
prove the graceful-degradation behavior on whatever machine runs them but do not prove device
discovery on real hardware. That, and real device creation for `devices test`, is verified manually
(see _Manual Verification_ in the unit documents), following the repository's convention for
hardware-dependent behavior.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own fakes and `Context`; `doctor` tests use a uniquely
  named temporary directory as the model store root. Tests that redirect the process-wide console
  belong to the `Sequential` xUnit collection.
- **Test doubles**: `FakeAudioCaptureDeviceProbe`/`FakeAudioPlaybackDeviceProbe` and, for `doctor`,
  `FakeCliModelCatalog` from the ModelCommandsSubsystem tests

### Acceptance Criteria

A DeviceCommandsSubsystem test run passes when `list-devices` and `doctor` both report a machine
with no audio devices as an informational outcome rather than an error, the built tool runs
`list-devices` and `doctor` to completion with the expected output, and `devices test` plays its tone on a known
playback device and rejects an unknown device name with a clean error.

### Test Scenarios

#### ListDevicesCommand_Run_NoDevices_PrintsNoDevicesMessage

**Scenario**: `list-devices` runs against probes that enumerate nothing.

**Expected**: "No devices found." is printed, not an error.

#### DoctorCommand_Run_UnavailablePortAudio_ReportsInformationalNoteNotFailure

**Scenario**: `doctor` runs against the library's unavailable-backend probes.

**Expected**: An informational PortAudio note, a `HEALTHY` summary and exit code `0`.

#### SpeechCli_ListDevicesCommand_Invoked_ExitsCleanly

**Scenario**: The built tool runs `list-devices`.

**Expected**: A clean exit with either a device table or "No devices found.".

#### SpeechCli_DoctorCommand_Invoked_ReportsHealthChecklist

**Scenario**: The built tool runs `doctor` against an isolated, writable `--models-dir`.

**Expected**: The checklist is printed and the run is healthy.

#### DevicesTestCommand_PlayTestTone_AvailableDevice_WritesToneAndReportsIt

**Scenario**: `devices test` plays its tone on a fake playback device.

**Expected**: The full tone is written to the device, the device is stopped, and the tone frequency and sample rate are
reported.

#### DevicesTestCommand_Run_UnknownPlaybackDevice_ThrowsArgumentException

**Scenario**: `devices test --device does-not-exist` runs against a factory over fake probes that do not know that name.

**Expected**: A clean `ArgumentException` naming the device, before any device is created.

#### SpeechCli_DevicesTestCommandWithUnknownDevice_Invoked_ReturnsCleanError

**Scenario**: The built tool runs `devices test` with an unknown device name.

**Expected**: A clean, non-zero-exit-code error rather than a stack trace.
