### AudioDeviceFactoryCaptureDeviceSource Verification

#### Verification Approach

`AudioDeviceFactoryCaptureDeviceSource` is verified against a real `AudioDeviceFactory` composed over a fake capture
probe. With an injected probe, a selection the probe does not know resolves to the honestly unavailable device
whether or not PortAudio is initialized, so every assertion is deterministic on any machine. Resolving a known named
device to a usable device needs real hardware when it goes through the factory; that behavior is verified through the
in-memory `FakeCaptureDeviceSource` in the `ICliCaptureDeviceSource` contract scenario and manually (see
_ConversationCommandSubsystem Verification_).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own fake probe, factory, and source
- No audio hardware is required; the assertions hold whether or not hardware is present.

#### Acceptance Criteria

The unit passes when its capture probe enumerates the injected probe's devices, a named device the probe does not
know and the default selection with no devices both resolve to an unavailable device without throwing, and
construction without a factory is rejected.

#### Test Scenarios

##### AudioDeviceFactoryCaptureDeviceSource_CaptureProbe_ExposesInjectedProbe

**Scenario**: The source is built over a factory composed with a fake probe holding one device, and `CaptureProbe`
is read.

**Expected**: The very same probe instance is returned and enumerates the injected device.

##### AudioDeviceFactoryCaptureDeviceSource_CreateCaptureDevice_UnknownName_ReturnsUnavailableDevice

**Scenario**: A device is requested by a name the fake probe does not know.

**Expected**: A device whose `IsAvailable` is false is returned; nothing is thrown.

##### AudioDeviceFactoryCaptureDeviceSource_CreateCaptureDevice_DefaultWithNoDevices_ReturnsUnavailableDevice

**Scenario**: The default device (null selection) is requested and the fake probe holds no devices.

**Expected**: A device whose `IsAvailable` is false is returned; nothing is thrown.

##### AudioDeviceFactoryCaptureDeviceSource_NullFactory_ThrowsArgumentNullException

**Scenario**: The source is constructed with a null factory.

**Expected**: `ArgumentNullException`.
