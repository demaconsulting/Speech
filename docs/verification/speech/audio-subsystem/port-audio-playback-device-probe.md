### PortAudioPlaybackDeviceProbe

#### Verification Approach

Verified through direct unit tests in `PortAudioPlaybackDeviceProbeTests.cs` using a fake
`IPortAudioApi` behind `PortAudioEnvironment`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

Tests pass when enumeration includes only preferred-host playback devices and degrades to an
empty list when the preferred host API or all playback devices are absent.

#### Test Scenarios

##### Enumeration: Mixed Host APIs Returns Only Preferred Output Devices

**Test**: `PortAudioPlaybackDeviceProbe_Enumerate_MixedHostApis_ReturnsOnlyPreferredOutputDevices`

##### Enumeration: Preferred Host API Missing Returns Empty List

**Test**: `PortAudioPlaybackDeviceProbe_Enumerate_PreferredHostApiMissing_ReturnsEmptyList`

##### Enumeration: No Devices Returns Empty List

**Test**: `PortAudioPlaybackDeviceProbe_Enumerate_NoDevices_ReturnsEmptyList`
