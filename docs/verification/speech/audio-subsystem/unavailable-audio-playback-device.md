### UnavailableAudioPlaybackDevice

#### Verification Approach

Verified through direct unit tests in `UnavailableAudioDeviceTests.cs` against the real singleton;
nothing is mocked because the unit has no dependencies beyond `AudioDeviceUnavailableException`.

#### Test Environment

No additional setup beyond the standard test runner; no audio hardware is required.

#### Acceptance Criteria

Tests pass when the device reports `IsAvailable` as `false`, reports zero for channel count,
sample rate, and pending sample count, and throws `AudioDeviceUnavailableException` from `Start`,
`Stop`, and `Write`.

#### Test Scenarios

##### Availability Is Reported False

**Test**: `UnavailableAudioPlaybackDevice_IsAvailable_Read_ReturnsFalse`

##### Format Is Reported As Zero

**Test**: `UnavailableAudioPlaybackDevice_PlaybackFormat_Read_ReturnsZeroRateAndChannelCount`

##### Pending Sample Count Is Zero

**Test**: `UnavailableAudioPlaybackDevice_PendingSampleCount_Read_ReturnsZero`

##### Start Always Throws

**Test**: `UnavailableAudioPlaybackDevice_Start_Always_ThrowsAudioDeviceUnavailableException`

##### Stop Always Throws

**Test**: `UnavailableAudioPlaybackDevice_Stop_Always_ThrowsAudioDeviceUnavailableException`

##### Write Always Throws

**Test**: `UnavailableAudioPlaybackDevice_Write_Always_ThrowsAudioDeviceUnavailableException`
