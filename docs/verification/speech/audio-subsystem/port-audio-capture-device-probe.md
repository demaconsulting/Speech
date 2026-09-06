### PortAudioCaptureDeviceProbe

#### Verification Approach

Verified through direct unit tests in `PortAudioCaptureDeviceProbeTests.cs` using a fake
`IPortAudioApi` behind `PortAudioEnvironment`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

Tests pass when enumeration includes only preferred-host capture devices and degrades to an empty
list when the preferred host API or all capture devices are absent.

#### Test Scenarios

##### Enumeration: Mixed Host APIs Returns Only Preferred Input Devices

**Test**: `PortAudioCaptureDeviceProbe_Enumerate_MixedHostApis_ReturnsOnlyPreferredInputDevices`

##### Enumeration: Preferred Host API Missing Returns Empty List

**Test**: `PortAudioCaptureDeviceProbe_Enumerate_PreferredHostApiMissing_ReturnsEmptyList`

##### Enumeration: No Devices Returns Empty List

**Test**: `PortAudioCaptureDeviceProbe_Enumerate_NoDevices_ReturnsEmptyList`
