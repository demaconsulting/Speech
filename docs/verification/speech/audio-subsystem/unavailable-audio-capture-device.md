### UnavailableAudioCaptureDevice

#### Verification Approach

Verified through direct unit tests in `UnavailableAudioDeviceTests.cs` against the real singleton;
nothing is mocked because the unit has no dependencies beyond `AudioDeviceUnavailableException`.

#### Test Environment

No additional setup beyond the standard test runner; no audio hardware is required.

#### Acceptance Criteria

Tests pass when the device reports `IsAvailable` as `false`, reports zero for both channel count
and sample rate, and throws `AudioDeviceUnavailableException` from both `Start` and `Stop`.

#### Test Scenarios

##### Availability Is Reported False

**Test**: `UnavailableAudioCaptureDevice_IsAvailable_Read_ReturnsFalse`

##### Format Is Reported As Zero

**Test**: `UnavailableAudioCaptureDevice_CaptureFormat_Read_ReturnsZeroRateAndChannelCount`

##### Start Always Throws

**Test**: `UnavailableAudioCaptureDevice_Start_Always_ThrowsAudioDeviceUnavailableException`

##### Stop Always Throws

**Test**: `UnavailableAudioCaptureDevice_Stop_Always_ThrowsAudioDeviceUnavailableException`

##### Frame Event Subscription Is Safe

**Test**: `UnavailableAudioCaptureDevice_FrameCapturedSubscription_Always_DoesNotThrow`

Subscribing and unsubscribing a `FrameCaptured` handler is a no-op that never throws.
