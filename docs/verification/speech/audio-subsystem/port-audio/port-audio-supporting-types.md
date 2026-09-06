<!-- cspell:ignore Alsa -->
#### PortAudio Supporting Types

##### Verification Approach

Verified indirectly through `PortAudioEnvironment` and the capture/playback device-probe tests,
each of which exercises host-API resolution and device-metadata conversion driven by these
supporting types through a fake `IPortAudioApi` implementation.

##### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

##### Acceptance Criteria

The supporting types are considered verified when they correctly carry host-API and device
metadata through preferred-host-API resolution and device enumeration, and when
`PortAudioHostApiType`'s platform-mapping constants resolve to the expected host API on each
supported platform.

##### Test Scenarios

##### Metadata: Host API Resolution Carries Device and Host-API Metadata

**Test**: `PortAudioEnvironment_TryResolvePreferredHostApi_InitializedAndMapped_ReturnsHostApiInfo`

##### Metadata: Device Enumeration Filters by Preferred Host API

**Tests**: `PortAudioCaptureDeviceProbe_Enumerate_MixedHostApis_ReturnsOnlyPreferredInputDevices`,
`PortAudioPlaybackDeviceProbe_Enumerate_MixedHostApis_ReturnsOnlyPreferredOutputDevices`

##### Constants: Platform Mapping Resolves the Expected Host API

**Tests**: `PortAudioEnvironment_ResolvePreferredHostApiType_Windows_ReturnsWasapi`,
`PortAudioEnvironment_ResolvePreferredHostApiType_Linux_ReturnsAlsa`,
`PortAudioEnvironment_ResolvePreferredHostApiType_MacOs_ReturnsCoreAudio`

##### Interop: Missing Host API Is Reported, Not Thrown

**Test**: `PortAudioEnvironment_TryResolvePreferredHostApi_PreferredHostApiMissing_ReturnsFalse`
