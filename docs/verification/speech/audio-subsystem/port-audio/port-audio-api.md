#### PortAudioApi

##### Verification Approach

Verified indirectly through the reusable singleton test and through higher-level tests that use
the `PortAudioEnvironment` and device abstractions consuming the `IPortAudioApi` seam.

##### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

##### Acceptance Criteria

The adapter is considered verified when the shared singleton loads and the higher-level seam
logic that depends on its contract behaves as documented.

##### Test Scenarios

##### Singleton: Read Twice Returns Same Instance

**Test**: `PortAudioApi_Instance_ReadTwice_ReturnsSameInstance`

##### Integration: Higher-Level Seam Logic Uses the Contract Successfully

**Tests**: `AudioDeviceFactory_Constructor_PortAudioInitialized_ExposesRealProbes`,
`AudioDeviceFactory_CreateCaptureDevice_PortAudioInitialized_ReturnsRealDevice`,
`AudioDeviceFactory_CreatePlaybackDevice_PortAudioInitialized_ReturnsRealDevice`
