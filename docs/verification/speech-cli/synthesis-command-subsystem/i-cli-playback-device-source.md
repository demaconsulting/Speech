### ICliPlaybackDeviceSource Verification

#### Verification Approach

`ICliPlaybackDeviceSource` is an interface with no executable behavior of its own, so it is verified
through its implementations. The production implementation, `AudioDeviceFactoryPlaybackDeviceSource`,
is verified against a real `AudioDeviceFactory` (see its verification document). The test-only
implementation, `FakePlaybackDeviceSource`, resolves purely from an in-memory device list and is
what lets every `SpeakCommand` device-dispatch scenario run deterministically on any machine (see
the `SpeakCommand` verification document, in particular the unknown-device, no-device and
`--output-audio` scenarios).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test constructs its own source
- **Test doubles**: `FakePlaybackDeviceSource` and `FakeAudioPlaybackDeviceProbe`

#### Acceptance Criteria

The contract is satisfied when the production implementation exposes the factory's probe and
creates devices exactly as the factory does, and the fake lets `SpeakCommand` exercise device
validation, creation and unavailability without hardware.

#### Test Scenarios

##### AudioDeviceFactoryPlaybackDeviceSource_PlaybackProbe_ReturnsInjectedProbeIdentity

**Scenario**: The production implementation's probe is compared with the probe its factory was
built with.

**Expected**: They are the same instance.

##### AudioDeviceFactoryPlaybackDeviceSource_CreatePlaybackDevice_EmptyProbeDefault_ReturnsUnavailable

**Scenario**: A default device is requested when the probe lists no devices.

**Expected**: An unavailable device.

##### AudioDeviceFactoryPlaybackDeviceSource_CreatePlaybackDevice_UnknownNamedSelection_ReturnsUnavailable

**Scenario**: A device name the probe does not list is requested.

**Expected**: An unavailable device.

##### AudioDeviceFactoryPlaybackDeviceSource_CreatePlaybackDevice_MatchesFactoryResult

**Scenario**: A device is created for the default selection and for a listed named selection.

**Expected**: The result has the same type and availability as the factory's own result.

##### SpeakCommand_RunAsync_UnknownDevice_ThrowsArgumentException

**Scenario**: `speak` runs over the fake source with a device name its probe does not list.

**Expected**: `ArgumentException` naming the device, before any device is created.

##### SpeakCommand_RunAsync_NoPlaybackDeviceAvailable_ThrowsInvalidOperationException

**Scenario**: `speak` runs over a fake source that reports no device.

**Expected**: `InvalidOperationException` whose message names `--output-audio` as the alternative,
and the fake synthesizer's call count shows synthesis never started.
