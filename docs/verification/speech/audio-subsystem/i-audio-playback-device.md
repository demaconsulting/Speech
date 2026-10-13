### IAudioPlaybackDevice

#### Verification Approach

`IAudioPlaybackDevice` is verified indirectly through both shipped implementations:
`UnavailableAudioPlaybackDevice` proves the honest-unavailable path, and `PortAudioPlaybackDevice`
proves queue-based playback behavior, format disclosure, and first-use fault behavior.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The contract is considered verified when the unavailable implementation reports `false`
availability, `0` channel count/sample rate, `0` pending sample count, and operational
exceptions correctly, and the real PortAudio-backed implementation queues samples, zero-fills
underruns, reports the resolved channel count/sample rate (or `0` when unresolved), reports
pending sample count honestly as it drains, and surfaces native first-use failures correctly.

#### Test Scenarios

The scenarios below are verified through the two implementations; the detailed arrange/expect
steps are in the unit verification documents named for each.

##### Availability flag

**Tests**: `UnavailableAudioPlaybackDevice_IsAvailable_Read_ReturnsFalse`,
`PortAudioPlaybackDevice_IsAvailable_NoResolvableDevice_ReturnsFalse`; **Expected**: `false` without throwing.

##### Start

**Tests**: `UnavailableAudioPlaybackDevice_Start_Always_ThrowsAudioDeviceUnavailableException`,
`PortAudioPlaybackDevice_Start_OpenFails_ThrowsAudioDeviceUnavailableException`,
`PortAudioPlaybackDevice_Start_EnvironmentRefreshedAfterConstruction_ThrowsAudioDeviceUnavailableException`;
**Expected**: `AudioDeviceUnavailableException` when unavailable, failing to open, or stale.

##### Stop

**Test**: `UnavailableAudioPlaybackDevice_Stop_Always_ThrowsAudioDeviceUnavailableException`;
**Expected**: `AudioDeviceUnavailableException` when unavailable (stop failures of the real device are
verified in the PortAudioPlaybackDevice document).

##### Write

**Tests**: `UnavailableAudioPlaybackDevice_Write_Always_ThrowsAudioDeviceUnavailableException`,
`PortAudioPlaybackDevice_Write_StreamRequestsSamples_DrainsQueueAndZeroFills`; **Expected**:
`AudioDeviceUnavailableException` when unavailable; queued samples are drained then zero-filled on
the real device.

##### Playback format

**Tests**: the three `*_PlaybackFormat_*` tests named in the requirements; **Expected**: resolved
values on a resolved device, `0` and `0` otherwise.

##### Pending sample count

**Tests**: `PortAudioPlaybackDevice_PendingSampleCount_WriteDrainAndStop_TracksQueueDepth`,
`UnavailableAudioPlaybackDevice_PendingSampleCount_Read_ReturnsZero`; **Expected**: tracks the queue depth
on the real device and is `0` when unavailable.

See also the AudioSubsystem-level scenarios "Playback Device: Selection, Availability, and Queued
Playback", "Playback Device: Reported Playback Format", "Playback Device: Pending Sample Drain
Tracking", and "Mockable Contracts: Device and Probe Substitution", plus the
_UnavailableAudioPlaybackDevice_ unit verification document
(`docs/verification/speech/audio-subsystem/unavailable-audio-playback-device.md`) for the
honest-unavailable behavior.
