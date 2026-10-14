### ListDevicesCommand Verification

#### Verification Approach

`ListDevicesCommand` is verified through deterministic unit tests over hand-written fakes of the
library's public probe interfaces (`FakeAudioCaptureDeviceProbe`, `FakeAudioPlaybackDeviceProbe`),
which produce any enumeration outcome (no devices, one device, devices in both directions) with no
audio hardware. The healthy and invalid-direction paths are also run against the built tool as a
child process in `IntegrationTests.cs`; those tests pass whether or not the machine has audio
hardware, because the command exits cleanly in both cases.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test builds its own fake probes and `Context`. The tests redirect the
  process-wide console and therefore belong to the `Sequential` xUnit collection, so they never run
  in parallel with another test that redirects the console.
- **Test doubles**: the two fake probes above

#### Acceptance Criteria

The unit passes when it lists the devices of both probes by default and only those of the
requested direction under `--direction`, reports "No devices found." for an empty result, reports
each device with the name, channel count and sample rate the probe returned, rejects an invalid
`--direction`, a `--direction` with no value or followed by another option, an
unsupported flag and a missing context or probe, and the built tool lists devices cleanly.

#### Test Scenarios

##### ListDevicesCommand_Run_NoFilter_ListsBothDirections

**Scenario**: One capture and one playback device are enumerated and no flag is given.

**Expected**: The output contains both device names, exactly as the fakes returned them, and the
`Capture` and `Playback` direction names. The next scenario asserts the channel and sample-rate columns.

##### ListDevicesCommand_Run_Devices_ReportChannelCountAndSampleRate

**Scenario**: A capture device with 7 channels at 44123 Hz and a playback device with 5 channels at 96321 Hz are
enumerated. These values are distinctive so they cannot appear in the output by coincidence.

**Expected**: Each device's line contains that device's own channel count and sample rate.

##### ListDevicesCommand_Run_DirectionInput_ListsOnlyCaptureDevices

**Scenario**: `--direction input` is given with devices in both directions.

**Expected**: Only the capture device appears.

##### ListDevicesCommand_Run_DirectionOutput_ListsOnlyPlaybackDevices

**Scenario**: `--direction output` is given with devices in both directions.

**Expected**: Only the playback device appears.

##### ListDevicesCommand_Run_NoDevices_PrintsNoDevicesMessage

**Scenario**: Both probes enumerate nothing.

**Expected**: The output contains "No devices found." and the command does not throw.

##### ListDevicesCommand_Run_InvalidDirectionValue_ThrowsArgumentException

**Scenario**: `--direction sideways`.

**Expected**: `ArgumentException`.

##### ListDevicesCommand_Run_DirectionFlagMissingValue_ThrowsArgumentException

**Scenario**: `--direction` is the last argument.

**Expected**: `ArgumentException` whose message says `--direction requires a value`.

##### ListDevicesCommand_Run_DirectionFlagFollowedByOption_ThrowsArgumentException

**Scenario**: `--direction` is followed by another `--direction` token instead of a value.

**Expected**: `ArgumentException` whose message says `--direction requires a value`; the option token is not
swallowed as the value.

##### ListDevicesCommand_Run_UnsupportedFlag_ThrowsArgumentException

**Scenario**: An unsupported flag is given.

**Expected**: `ArgumentException`.

##### ListDevicesCommand_Run_NullContext_ThrowsArgumentNullException

**Scenario**: The context is null.

**Expected**: `ArgumentNullException`.

##### ListDevicesCommand_Run_NullCaptureProbe_ThrowsArgumentNullException

**Scenario**: The capture probe is null.

**Expected**: `ArgumentNullException`.

##### ListDevicesCommand_Run_NullPlaybackProbe_ThrowsArgumentNullException

**Scenario**: The playback probe is null.

**Expected**: `ArgumentNullException`.

##### SpeechCli_ListDevicesCommand_Invoked_ExitsCleanly

**Scenario**: The built tool runs `list-devices` as a child process.

**Expected**: Exit code `0` and either a device table or "No devices found.", whatever this machine
has.

##### SpeechCli_ListDevicesCommandWithInvalidDirection_Invoked_ReturnsCleanError

**Scenario**: The built tool runs `list-devices --direction bogus`.

**Expected**: A clean one-line error and a non-zero exit code, with no stack trace.

#### Manual Verification

Running `dotnet run --project src/DemaConsulting.Speech.Cli -- list-devices` on a development machine
with audio hardware shows the genuinely detected devices with sane column output. This is manual
because the automated tests cannot depend on the host's hardware.
