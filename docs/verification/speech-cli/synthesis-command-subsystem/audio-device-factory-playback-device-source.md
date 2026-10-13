### AudioDeviceFactoryPlaybackDeviceSource Verification

#### Verification Approach

`AudioDeviceFactoryPlaybackDeviceSource` is verified against a real, composed `AudioDeviceFactory`
built with an injected stub playback probe, so the probe passthrough and the deterministic
unavailable-device outcomes (empty probe, unknown selection) hold on every machine. Where the
outcome depends on PortAudio initialization (a known device), the test compares the adapter's
result with the factory's own result for the same call; no test asserts whether real playback
hardware is present, because that varies by machine and must not affect the result.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test constructs its own factory and source; the results hold on machines with
  or without audio hardware
- **Test doubles**: `StubPlaybackProbe` (an in-memory device list); the real `AudioDeviceFactory`
  is used deliberately

#### Acceptance Criteria

The unit passes when it exposes the probe the factory was built with, creates an unavailable
device for an empty or unknown selection, creates the same kind of device with the same
availability as the factory for a default and a named selection, and rejects a missing factory.

#### Test Scenarios

##### AudioDeviceFactoryPlaybackDeviceSource_PlaybackProbe_ReturnsInjectedProbeIdentity

**Scenario**: The adapter's `PlaybackProbe` is compared with the stub probe given to the factory.

**Expected**: They are the same instance.

##### AudioDeviceFactoryPlaybackDeviceSource_CreatePlaybackDevice_EmptyProbeDefault_ReturnsUnavailable

**Scenario**: A default device is requested over a factory whose probe lists no devices.

**Expected**: The unavailable playback device.

##### AudioDeviceFactoryPlaybackDeviceSource_CreatePlaybackDevice_UnknownNamedSelection_ReturnsUnavailable

**Scenario**: A named device that the probe does not list is requested.

**Expected**: The unavailable playback device.

##### AudioDeviceFactoryPlaybackDeviceSource_CreatePlaybackDevice_MatchesFactoryResult

**Scenario**: A theory creates a device for a null selection and for a named selection that the
probe lists, through both the adapter and the factory.

**Expected**: The two results have the same type and the same `IsAvailable` value.

##### AudioDeviceFactoryPlaybackDeviceSource_NullFactory_ThrowsArgumentNullException

**Scenario**: The adapter is constructed with a null factory.

**Expected**: `ArgumentNullException`.
