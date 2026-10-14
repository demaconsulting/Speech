### DevicesTestCommand Verification

#### Verification Approach

`DevicesTestCommand` is verified through deterministic unit tests. Argument parsing, name
resolution, availability checks and tone generation are pure or take injected values. The device
interaction logic is tested through the entry points that take an already resolved device:
`PlayTestTone` over a hand-written `IAudioPlaybackDevice` fake and `RecordTestClip` over a
hand-written `IAudioCaptureDevice` fake that raises frames on demand. The unknown-device path is
driven through the real `AudioDeviceFactory` composed over fake probes via its public constructor,
because that path fails before any device is created. Out-of-process tests in `IntegrationTests.cs`
run the built tool for the clean-error paths an operator sees.

Automated tests do **not** exercise real device creation: `AudioDeviceFactory`'s
`CreateCaptureDevice`/`CreatePlaybackDevice` depend on an internal PortAudio environment that is
independent of any injected probe, so that step cannot be deterministically faked. The
`RequireAvailable` checks that follow it are tested directly with fake devices, but the wiring from
`Run` through device creation into those checks is verified only manually.

Two timing facts shape the tests. `RecordTestClip` really waits for the duration it is given, so
its tests use a short duration. `PlayTestTone` accepts an optional total time limit that is counted
from before the device is started; the
stalled-device test injects a 1 ms limit, so the failure path is instant instead of waiting the
default 7 seconds (2-second tone plus 5-second margin). The default limit itself is therefore not
asserted by any automated test; it is a constant in the unit.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test builds its own fakes and `Context`. The tests capture and redirect the
  process-wide console output and belong to the `Sequential` xUnit collection, so they never run in
  parallel with another test that redirects the console.
- **Test doubles**: `FakeAudioCaptureDeviceProbe`/`FakeAudioPlaybackDeviceProbe`, a recording
  playback device (with configurable `PendingSampleCount` and `IsAvailable`) and a framing capture
  device (with configurable frames and a throw-on-start switch)

#### Acceptance Criteria

The unit passes when it defaults to the output direction and parses its flags; rejects bad
arguments, an unknown device name (either direction) and an unavailable device cleanly; generates a
tone of the expected length that stays within full scale, is identical on every channel and rejects
a non-positive sample rate; writes the whole tone and stops the device; fails instead of hanging
when the playback queue never drains or the device start alone exceeds the total limit; reports
capture statistics with invariant-culture durations; fails when capture delivers no samples; always
stops a capture device whose `Start` threw and a playback device whose `Start` or write threw; and
rejects a missing context, factory or device list.

#### Test Scenarios

##### DevicesTestCommand_ParseArguments_NoFlags_DefaultsToOutputDirection

**Scenario**: `test` is the only argument.

**Expected**: Direction is playback and no device name is set.

##### DevicesTestCommand_ParseArguments_DirectionInput_ParsesToCaptureDirection

**Scenario**: `--direction input`.

**Expected**: Direction is capture.

##### DevicesTestCommand_ParseArguments_DirectionOutput_ParsesToPlaybackDirection

**Scenario**: `--direction output`.

**Expected**: Direction is playback.

##### DevicesTestCommand_ParseArguments_DeviceFlag_ParsesDeviceName

**Scenario**: `--device <name>`.

**Expected**: The device name is carried unchanged.

##### DevicesTestCommand_ParseArguments_MissingSubAction_ThrowsArgumentException

**Scenario**: No arguments.

**Expected**: `ArgumentException`.

##### DevicesTestCommand_ParseArguments_UnknownSubAction_ThrowsArgumentException

**Scenario**: A first token other than `test`.

**Expected**: `ArgumentException`.

##### DevicesTestCommand_ParseArguments_InvalidDirectionValue_ThrowsArgumentException

**Scenario**: An unrecognized `--direction` value.

**Expected**: `ArgumentException`.

##### DevicesTestCommand_ParseArguments_UnsupportedFlag_ThrowsArgumentException

**Scenario**: An unsupported flag.

**Expected**: `ArgumentException`.

##### DevicesTestCommand_ParseArguments_DeviceFlagMissingValue_ThrowsArgumentException

**Scenario**: `--device` with no value.

**Expected**: `ArgumentException` whose message says `--device requires a value`.

##### DevicesTestCommand_ParseArguments_DirectionFlagMissingValue_ThrowsArgumentException

**Scenario**: `--direction` with no value.

**Expected**: `ArgumentException` whose message says `--direction requires a value`.

##### DevicesTestCommand_ParseArguments_DeviceFlagFollowedByOption_ThrowsArgumentException

**Scenario**: `--device --direction input`.

**Expected**: `ArgumentException`; the option token is not swallowed as the device name.

##### DevicesTestCommand_ParseArguments_DirectionFlagFollowedByOption_ThrowsArgumentException

**Scenario**: `--direction --device x`.

**Expected**: `ArgumentException`; the option token is not swallowed as the direction.

##### DevicesTestCommand_ResolveDeviceSelectionOrThrow_NullDeviceName_ReturnsNull

**Scenario**: No device name is requested.

**Expected**: `null`, meaning the platform default device.

##### DevicesTestCommand_ResolveDeviceSelectionOrThrow_KnownDeviceName_ReturnsSelection

**Scenario**: The requested name is among the enumerated devices.

**Expected**: A selection for that name.

##### DevicesTestCommand_ResolveDeviceSelectionOrThrow_UnknownDeviceName_ThrowsArgumentException

**Scenario**: The requested name is not among the enumerated devices.

**Expected**: `ArgumentException`.

##### DevicesTestCommand_ResolveDeviceSelectionOrThrow_NullKnownDevices_ThrowsArgumentNullException

**Scenario**: The known-device list is null.

**Expected**: `ArgumentNullException`.

##### DevicesTestCommand_GenerateToneSamples_ReturnsExpectedSampleCount

**Scenario**: 1000 Hz sample rate, 2 channels, 1 second.

**Expected**: Exactly 2000 samples.

##### DevicesTestCommand_GenerateToneSamples_AllSamplesWithinNormalizedRange

**Scenario**: A 0.5-second mono tone at 44.1 kHz.

**Expected**: Every sample lies within -1.0 to 1.0. The test asserts the full-scale bound, not the
0.2 amplitude itself.

##### DevicesTestCommand_GenerateToneSamples_IdenticalAcrossChannels

**Scenario**: A stereo tone.

**Expected**: In every frame both channels carry the same value.

##### DevicesTestCommand_GenerateToneSamples_NonPositiveSampleRate_ThrowsArgumentOutOfRangeException

**Scenario**: Sample rate `0`.

**Expected**: `ArgumentOutOfRangeException`.

##### DevicesTestCommand_GenerateToneSamples_NonPositiveOtherArguments_ThrowsArgumentOutOfRangeException

**Scenario**: A valid sample rate with a channel count of `0` or `-1`, a duration of `0` or `-1`, or a frequency of
`0` or `-440` (six cases).

**Expected**: `ArgumentOutOfRangeException` for each.

##### DevicesTestCommand_Run_UnknownPlaybackDevice_ThrowsArgumentException

**Scenario**: `--device` names a device the fake playback probe does not enumerate, run through a
real factory over fake probes.

**Expected**: `ArgumentException` naming the device.

##### DevicesTestCommand_Run_UnknownCaptureDevice_ThrowsArgumentException

**Scenario**: The same for `--direction input`.

**Expected**: `ArgumentException` naming the device.

##### DevicesTestCommand_Run_NullContext_ThrowsArgumentNullException

**Scenario**: The context is null.

**Expected**: `ArgumentNullException`.

##### DevicesTestCommand_Run_NullFactory_ThrowsArgumentNullException

**Scenario**: The factory is null.

**Expected**: `ArgumentNullException`.

##### DevicesTestCommand_RequireAvailable_UnavailablePlaybackDevice_ThrowsInvalidOperationException

**Scenario**: A fake playback device reports `IsAvailable == false`.

**Expected**: `InvalidOperationException` whose message names the playback device.

##### DevicesTestCommand_RequireAvailable_UnavailableCaptureDevice_ThrowsInvalidOperationException

**Scenario**: A fake capture device reports `IsAvailable == false`.

**Expected**: `InvalidOperationException` whose message names the capture device.

##### DevicesTestCommand_RequireAvailable_AvailableDevices_ReturnsSameDevice

**Scenario**: Available playback and capture devices are checked.

**Expected**: The same device instance is returned in each case.

##### DevicesTestCommand_PlayTestTone_AvailableDevice_WritesToneAndReportsIt

**Scenario**: A fake 48 kHz stereo playback device whose queue is empty.

**Expected**: The device is started and stopped, exactly `48000 * 2 * 2` samples are written, and
the report mentions the 440 Hz tone and the 48000 Hz sample rate. No model or catalog is involved.

##### DevicesTestCommand_PlayTestTone_DeviceNeverDrains_ThrowsInvalidOperationException

**Scenario**: A fake device whose `PendingSampleCount` never reaches zero, with a 1 ms drain limit.

**Expected**: `InvalidOperationException` ("did not finish...") and the device has been stopped,
proving the `finally` stop. The test returns immediately rather than waiting 7 seconds.

##### DevicesTestCommand_PlayTestTone_SlowStartExceedsTotalLimit_ThrowsInvalidOperationException

**Scenario**: A fake device whose `Start` blocks for 250 ms and whose queue then drains at once, with a 50 ms total
limit.

**Expected**: `InvalidOperationException` ("did not finish...") and the device has been stopped. This fails if the limit
is counted only from the drain wait, which is the regression it guards.

##### DevicesTestCommand_PlayTestTone_WriteBlocksPastLimit_ThrowsInvalidOperationException

**Scenario**: A fake device whose `Write` blocks until released by the test, with a 100 ms total limit.

**Expected**: `InvalidOperationException` ("did not finish...") and the device has been stopped, proving a blocking
`Write` cannot hang the command.

##### DevicesTestCommand_PlayTestTone_SlowWriteWithEmptyQueue_ThrowsInvalidOperationException

**Scenario**: A fake device whose `Write` sleeps 300 ms and whose queue is already empty, with a 50 ms total limit.

**Expected**: `InvalidOperationException` ("did not finish...") and the device has been stopped, proving success is
never reported after the limit has passed.

##### DevicesTestCommand_PlayTestTone_StartThrows_StillStopsDevice

**Scenario**: The fake playback device's `Start` throws.

**Expected**: The exception propagates and the device has been stopped.

##### DevicesTestCommand_PlayTestTone_WriteThrows_StillStopsDevice

**Scenario**: The fake playback device's write throws.

**Expected**: The exception propagates and the device has been stopped.

##### DevicesTestCommand_RecordTestClip_DeviceDeliversFrames_ReportsSampleCountAndPeak

**Scenario**: A fake capture device raises one frame of known samples.

**Expected**: The report contains the exact sample count and the peak amplitude of those samples.

##### DevicesTestCommand_RecordTestClip_DeviceDeliversNoFrames_ThrowsInvalidOperationException

**Scenario**: The fake capture device raises no frames.

**Expected**: `InvalidOperationException`, and the device has been stopped.

##### DevicesTestCommand_RecordTestClip_StartThrows_StopsDevice

**Scenario**: The fake capture device's `Start` throws.

**Expected**: The exception propagates and the device has been stopped, proving the stop in the
`finally` block. The frame unsubscription in the same block is not asserted separately.

##### DevicesTestCommand_RecordTestClip_CommaDecimalCulture_ReportsInvariantDuration

**Scenario**: The current culture is `de-DE` and a clip is recorded.

**Expected**: The duration is reported with a period decimal separator.

##### SpeechCli_DevicesCommandWithoutSubAction_Invoked_ReturnsCleanError

**Scenario**: The built tool runs `devices` with no sub-action.

**Expected**: A clean one-line error and a non-zero exit code.

##### SpeechCli_DevicesTestCommandWithUnknownDevice_Invoked_ReturnsCleanError

**Scenario**: The built tool runs `devices test --device does-not-exist-device`.

**Expected**: A clean error naming the device and a non-zero exit code. This passes with or without
audio hardware, because an unrecognized name fails before any device is created.

#### Manual Verification

Real device creation is verified manually on a machine with audio hardware:
`dotnet run --project src/DemaConsulting.Speech.Cli -- devices test` plays an audible tone on every
channel and `devices test --direction input` records and reports non-trivial capture statistics.
On a machine with no audio backend the same commands should fail with a clean message. This follows
the repository's convention for hardware-dependent behavior (see the
`Speech-AudioSubsystem-UnavailableFallbacks` verification document).
