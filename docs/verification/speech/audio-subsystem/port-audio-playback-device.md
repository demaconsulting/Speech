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
`AudioDeviceUnavailableException`, no resolvable device yields `IsAvailable = false`, preferred
formats are honored when within device capability, excessive preferred channel counts are
clamped, omitted preferences fall back to the device default, the reported channel count and
sample rate match the requested open format (or are zero when nothing was resolved),
`PendingSampleCount` honestly tracks how many written samples the callback has genuinely
dequeued, a preferred sample rate the host API confirms it can open is honored, and a preferred
sample rate the host API cannot open falls back to the device's default sample rate with an
Info-level diagnostic.

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

##### Construction: Preferred Format Within Capability Is Used

**Test**: `PortAudioPlaybackDevice_Constructor_PreferredFormatWithinCapability_UsesPreferredFormat`

##### Construction: Excessive Preferred Channel Count Is Clamped

**Test**: `PortAudioPlaybackDevice_Constructor_PreferredChannelCountExceedsCapability_ClampsAndReportsDiagnostic`

##### Construction: Omitted Preferred Format Uses Device Defaults

**Test**: `PortAudioPlaybackDevice_Constructor_PreferredFormatOmitted_UsesDeviceDefaultFormat`

##### Playback Format: No Resolvable Device Returns Zero Rate and Channel Count

**Test**: `PortAudioPlaybackDevice_PlaybackFormat_NoResolvableDevice_ReturnsZeroRateAndChannelCount`

##### Pending Sample Count: Write, Drain, and Stop Track Queue Depth

**Test**: `PortAudioPlaybackDevice_PendingSampleCount_WriteDrainAndStop_TracksQueueDepth`

##### Construction: Unsupported Preferred Sample Rate Falls Back to Device Default

**Test**: `PortAudioPlaybackDevice_Constructor_PreferredSampleRateUnsupported_FallsBackToDeviceDefaultSampleRateAndReportsDiagnostic`

##### Construction: Supported Preferred Sample Rate Is Used

**Test**: `PortAudioPlaybackDevice_Constructor_PreferredSampleRateSupported_UsesPreferredFormat`

##### Write: Multiple Blocks of Varying Size Drain in Order

**Test**: `PortAudioPlaybackDevice_Write_MultipleBlocksOfVaryingSize_DrainsInOrder`

##### Write: Block Larger Than Request Drains Remainder on Next Call

**Test**: `PortAudioPlaybackDevice_Write_BlockLargerThanRequest_DrainsRemainderOnNextCall`

##### Write: Small Block Followed by Another Write Concatenates Across Boundary

**Test**: `PortAudioPlaybackDevice_Write_SmallBlockFollowedByAnotherWrite_ConcatenatesAcrossBoundary`

##### Stop: Partially Consumed Block Never Replays Stale Audio After Restart

**Test**: `PortAudioPlaybackDevice_Stop_PartiallyConsumedBlock_RestartNeverReplaysStaleAudio`
