### AudioDeviceFactoryPlaybackDeviceSource Verification

#### Verification Approach

`AudioDeviceFactoryPlaybackDeviceSource` is verified against a real, composed `AudioDeviceFactory`.
Each test compares the adapter's result with the factory's own result for the same call, which
proves the adapter is a pure pass-through. No test asserts whether real playback hardware is present,
because that varies by machine and is exactly the condition that must not affect the result.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test constructs its own factory and source; the results hold on machines with
  or without audio hardware
- **Test doubles**: None; the real `AudioDeviceFactory` is used deliberately

#### Acceptance Criteria

The unit passes when it exposes the factory's own probe, creates the same kind of device with the
same availability as the factory for both a default and a named selection, and rejects a missing
factory.

#### Test Scenarios

##### AudioDeviceFactoryPlaybackDeviceSource_PlaybackProbe_ForwardsToFactory

**Scenario**: The adapter's `PlaybackProbe` is compared with the factory's `PlaybackProbe`.

**Expected**: They are the same instance.

##### AudioDeviceFactoryPlaybackDeviceSource_CreatePlaybackDevice_ForwardsToFactory

**Scenario**: A theory creates a device for a null selection and for a named selection, through both
the adapter and the factory.

**Expected**: The two results have the same type and the same `IsAvailable` value.

##### AudioDeviceFactoryPlaybackDeviceSource_NullFactory_ThrowsArgumentNullException

**Scenario**: The adapter is constructed with a null factory.

**Expected**: `ArgumentNullException`.
