### UnavailableAudioCaptureDeviceProbe

#### Verification Approach

Verified through a direct unit test in `UnavailableAudioDeviceTests.cs` against the real
singleton; nothing is mocked.

#### Test Environment

No additional setup beyond the standard test runner.

#### Acceptance Criteria

The test passes when `Enumerate` returns an empty list and does not throw.

#### Test Scenarios

##### Enumerate Always Returns An Empty List

**Test**: `UnavailableAudioCaptureDeviceProbe_Enumerate_Always_ReturnsEmptyList`
