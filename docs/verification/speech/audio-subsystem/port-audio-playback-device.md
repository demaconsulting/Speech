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
dequeued, a preferred sample rate the host API confirms it can open is honored, a preferred
sample rate the host API cannot open falls back to the device's default sample rate with an
Info-level diagnostic, a native `Dispose()` fault during start-failure cleanup still releases the
active-stream registration and reports the original start failure rather than the dispose
failure, and a device whose environment was refreshed after construction refuses to start with
`AudioDeviceUnavailableException` while a device resolved after the refresh starts normally, a
fault raised while querying the device table is contained as unavailability, a failed native stop
or a dispose fault after a successful stop is wrapped in `AudioDeviceUnavailableException` while
still releasing the active-stream registration, and unresolved devices refuse operational calls.

#### Test Scenarios

The tests mark intent with one Arrange/Act/Assert comment per phase; each scenario below names
its expected outcome.

##### Construction: Device Query Fault Is Contained

**Test**: `PortAudioPlaybackDevice_Constructor_DeviceQueryFaults_ReportsUnavailableAndDiagnostic`

Construction does not throw when device enumeration faults; the device is unavailable and an
Error diagnostic names the fault.

##### Operations: No Resolvable Device Refuses Calls

**Test**: `PortAudioPlaybackDevice_Operations_NoResolvableDevice_ThrowAudioDeviceUnavailableException`

`Start`, `Stop` and `Write` throw `AudioDeviceUnavailableException`, a null `Write` throws
`ArgumentNullException`, and `PendingSampleCount` is `0`.

##### Write: Empty and Non-Collection Lists

**Test**: `PortAudioPlaybackDevice_Write_EmptyAndNonCollectionLists_HandledCorrectly`

An empty write queues nothing; a list that is not an `ICollection<float>` is copied element by
element and drains intact.

##### Start: Already Started Opens the Resolved Format Once

**Test**: `PortAudioPlaybackDevice_Start_AlreadyStarted_OpensResolvedFormatOnce`

A second `Start` does nothing, and the stream was opened with the resolved channel count and
sample rate.

##### Stop: Native Stop Failure Is Wrapped

**Test**: `PortAudioPlaybackDevice_Stop_NativeStopFails_ThrowsWrappedAndReleasesRegistration`

The stop failure is wrapped, the stream is still disposed, `PendingSampleCount` resets to `0`,
and a later refresh succeeds.

##### Stop: Dispose Failure After Stop Is Wrapped

**Test**: `PortAudioPlaybackDevice_Stop_DisposeFailsAfterStop_ThrowsWrappedDisposeFailure`

The dispose failure is wrapped and the registration is still released.

##### Callback: Non-Positive Request Returns Empty

**Test**: `PortAudioPlaybackDevice_ProvideSamples_NonPositiveRequest_ReturnsEmptyAndKeepsQueue`

A zero-sample callback request returns an empty block and leaves queued samples pending.

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

##### Start: Registers an Active Stream With the Environment, Refresh Throws While Started

**Test**: `PortAudioPlaybackDevice_Start_RegistersActiveStreamWithEnvironment_RefreshThrowsWhileStarted`

##### Stop: Unregisters the Active Stream, Refresh Succeeds After Stop

**Test**: `PortAudioPlaybackDevice_Stop_UnregistersActiveStream_RefreshSucceedsAfterStop`

##### Start: Dispose Throws During Start-Failure Cleanup, Still Unregisters and Throws Original Failure

**Test**: `PortAudioPlaybackDevice_Start_DisposeThrowsDuringStartFailureCleanup_UnregistersActiveStreamAndThrowsOriginalStartFailure`

##### Start: Environment Refreshed After Construction Throws AudioDeviceUnavailableException

**Test**: `PortAudioPlaybackDevice_Start_EnvironmentRefreshedAfterConstruction_ThrowsAudioDeviceUnavailableException`

##### Start: Constructed After Refresh Starts Normally

**Test**: `PortAudioPlaybackDevice_Start_ConstructedAfterRefresh_StartsNormally`
