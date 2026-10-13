### AudioDeviceFactoryCaptureDeviceSource Verification

#### Verification Approach

`AudioDeviceFactoryCaptureDeviceSource` is verified as a pure pass-through by comparing its results with those of a
real `AudioDeviceFactory` for the same call. The tests deliberately do not assert whether capture hardware is
present, because that varies by machine and is exactly what the adapter must not change.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own `AudioDeviceFactory` and adapter
- No audio hardware is required; the assertions hold whether or not hardware is present.

#### Acceptance Criteria

The unit passes when its capture probe is the wrapped factory's probe, its device creation returns the same kind of
device with the same availability as the factory for both the default and a named selection, and construction
without a factory is rejected.

#### Test Scenarios

##### AudioDeviceFactoryCaptureDeviceSource_CaptureProbe_ForwardsToFactory

**Scenario**: The adapter wraps a factory and its `CaptureProbe` is read.

**Expected**: The adapter returns the very same probe instance as the factory.

##### AudioDeviceFactoryCaptureDeviceSource_CreateCaptureDevice_ForwardsToFactory

**Scenario**: A device is created through the adapter for a null selection and for a named selection, and the same
call is made on the factory.

**Expected**: The adapter's device has the same type and the same `IsAvailable` value as the factory's device in
both cases.

##### AudioDeviceFactoryCaptureDeviceSource_NullFactory_ThrowsArgumentNullException

**Scenario**: The adapter is constructed with a null factory.

**Expected**: `ArgumentNullException`.
