### IAudioCaptureDeviceProbe

#### Verification Approach

`IAudioCaptureDeviceProbe` is verified indirectly through both shipped implementations:
`UnavailableAudioCaptureDeviceProbe` proves the honest-empty fallback path, and
`PortAudioCaptureDeviceProbe` proves preferred-host filtering and empty-enumeration degradation.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The contract is considered verified when both implementations return non-throwing enumerations and
the real implementation filters devices to the preferred host API.

#### Test Scenarios

See the AudioSubsystem-level scenarios "Preferred Host API: Capture Enumeration Filters Devices"
and "Unavailable Fallbacks: Honest Degradation".
