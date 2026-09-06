### PortAudioPlaybackDevice

#### Verification Approach

Verified through direct unit tests in `PortAudioPlaybackDeviceTests.cs` using fake
`IPortAudioApi` and `IPortAudioStream` implementations.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, plus NSubstitute for diagnostics
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

Tests pass when stale selections fall back to the host-API default playback device, queued
samples drain in order and zero-fill underruns, open failures surface
`AudioDeviceUnavailableException`, no resolvable device yields `IsAvailable = false`, the
reported channel count and sample rate match the resolved device (or are zero when nothing was
resolved), and `PendingSampleCount` honestly tracks how many written samples the callback has
genuinely dequeued.

#### Test Scenarios

##### Construction: Stale Selection Falls Back to Default Device

**Test**: `PortAudioPlaybackDevice_Constructor_StaleSelection_FallsBackToDefaultDevice`

##### Write: Stream Requests Samples Drains Queue and Zero-Fills

**Test**: `PortAudioPlaybackDevice_Write_StreamRequestsSamples_DrainsQueueAndZeroFills`

##### Start: Open Fails Throws AudioDeviceUnavailableException

**Test**: `PortAudioPlaybackDevice_Start_OpenFails_ThrowsAudioDeviceUnavailableException`

##### Availability: No Resolvable Device Returns False

**Test**: `PortAudioPlaybackDevice_IsAvailable_NoResolvableDevice_ReturnsFalse`

##### Playback Format: Resolved Device Reflects the Resolved Device Format

**Test**: `PortAudioPlaybackDevice_PlaybackFormat_ResolvedDevice_ReflectsResolvedDeviceFormat`

##### Playback Format: No Resolvable Device Returns Zero Rate and Channel Count

**Test**: `PortAudioPlaybackDevice_PlaybackFormat_NoResolvableDevice_ReturnsZeroRateAndChannelCount`

##### Pending Sample Count: Write, Drain, and Stop Track Queue Depth

**Test**: `PortAudioPlaybackDevice_PendingSampleCount_WriteDrainAndStop_TracksQueueDepth`
