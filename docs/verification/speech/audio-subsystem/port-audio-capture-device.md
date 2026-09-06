### PortAudioCaptureDevice

#### Verification Approach

Verified through direct unit tests in `PortAudioCaptureDeviceTests.cs` using fake
`IPortAudioApi` and `IPortAudioStream` implementations.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, plus NSubstitute for diagnostics
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

Tests pass when stale selections fall back to the host-API default capture device, capture-frame
delivery raises `FrameCaptured`, open failures surface `AudioDeviceUnavailableException`, no
resolvable device yields `IsAvailable = false`, and the reported channel count and sample rate
match the resolved device (or are zero when nothing was resolved).

#### Test Scenarios

##### Construction: Stale Selection Falls Back to Default Device

**Test**: `PortAudioCaptureDevice_Constructor_StaleSelection_FallsBackToDefaultDevice`

##### Start: Stream Captures Samples Raises FrameCaptured

**Test**: `PortAudioCaptureDevice_Start_StreamCapturesSamples_RaisesFrameCaptured`

##### Start: Open Fails Throws AudioDeviceUnavailableException

**Test**: `PortAudioCaptureDevice_Start_OpenFails_ThrowsAudioDeviceUnavailableException`

##### Availability: No Resolvable Device Returns False

**Test**: `PortAudioCaptureDevice_IsAvailable_NoResolvableDevice_ReturnsFalse`

##### Capture Format: Resolved Device Reflects the Resolved Device Format

**Test**: `PortAudioCaptureDevice_CaptureFormat_ResolvedDevice_ReflectsResolvedDeviceFormat`

##### Capture Format: No Resolvable Device Returns Zero Rate and Channel Count

**Test**: `PortAudioCaptureDevice_CaptureFormat_NoResolvableDevice_ReturnsZeroRateAndChannelCount`
