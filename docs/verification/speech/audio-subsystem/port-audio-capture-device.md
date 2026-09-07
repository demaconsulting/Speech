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
resolvable device yields `IsAvailable = false`, preferred formats are honored when within device
capability, excessive preferred channel counts are clamped, omitted preferences fall back to the
device default, the reported channel count and sample rate match the requested open format (or
are zero when nothing was resolved), a preferred sample rate the host API confirms it can open is
honored, and a preferred sample rate the host API cannot open falls back to the device's default
sample rate with an Info-level diagnostic.

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

##### Construction: Preferred Format Within Capability Is Used

**Test**: `PortAudioCaptureDevice_Constructor_PreferredFormatWithinCapability_UsesPreferredFormat`

##### Construction: Excessive Preferred Channel Count Is Clamped

**Test**: `PortAudioCaptureDevice_Constructor_PreferredChannelCountExceedsCapability_ClampsAndReportsDiagnostic`

##### Construction: Omitted Preferred Format Uses Device Defaults

**Test**: `PortAudioCaptureDevice_Constructor_PreferredFormatOmitted_UsesDeviceDefaultFormat`

##### Capture Format: No Resolvable Device Returns Zero Rate and Channel Count

**Test**: `PortAudioCaptureDevice_CaptureFormat_NoResolvableDevice_ReturnsZeroRateAndChannelCount`

##### Construction: Unsupported Preferred Sample Rate Falls Back to Device Default

**Test**: `PortAudioCaptureDevice_Constructor_PreferredSampleRateUnsupported_FallsBackToDeviceDefaultSampleRateAndReportsDiagnostic`

##### Construction: Supported Preferred Sample Rate Is Used

**Test**: `PortAudioCaptureDevice_Constructor_PreferredSampleRateSupported_UsesPreferredFormat`
