### PortAudioPlaybackDevice

#### Verification Approach

Verified through direct unit tests in `PortAudioPlaybackDeviceTests.cs` using fake
`IPortAudioApi` and `IPortAudioStream` implementations.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, plus NSubstitute for diagnostics
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

Tests pass when all of the following hold:

- Device resolution: an exact-name selection resolves that device, a stale selection falls back to
  the host-API default playback device, no resolvable device (or an unresolvable host API) yields
  `IsAvailable = false`, and a fault raised while querying the device table is contained as
  unavailability.
- Argument and unavailable-device handling: a null environment and a null `Write` list throw
  `ArgumentNullException`, and unresolved devices refuse `Start`, `Stop` and `Write` with
  `AudioDeviceUnavailableException`.
- Queueing: queued samples drain in order, `Write` copies the caller's buffer, empty writes are
  ignored, and underruns are zero-filled.
- Restart: a partially consumed block is never replayed after `Stop()` and a restart, including
  after a failed native stop, and the pending count stays exact.
- Formats: preferred formats are honored when within device capability, excessive preferred
  channel counts are clamped, omitted preferences fall back to the device default, a preferred
  sample rate the host API cannot open falls back to the device default with an Info-level
  diagnostic, and the reported channel count and sample rate match the requested open format (or
  are zero when nothing was resolved).
- `PendingSampleCount` tracks how many written samples the callback has genuinely dequeued and
  resets to zero after `Stop()`.
- Native faults: open, start and stop failures and a dispose fault after a successful stop are
  wrapped in `AudioDeviceUnavailableException`; a start failure disposes the opened stream; a
  dispose fault after a failed start or stop is reported but never replaces the primary failure;
  the active-stream registration is released in every case.
- Refresh: a device whose environment was refreshed after construction refuses to start with
  `AudioDeviceUnavailableException`, a device resolved after the refresh starts normally, and
  while a stream is active a refresh is refused.

#### Test Scenarios

The tests mark intent with one Arrange/Act/Assert comment per phase; each scenario below names
its expected outcome. Concurrency (a `Write` racing `Stop`) is not exercised by a threaded test,
because the tests deliberately avoid threads and sleeps; the race is instead excluded by design
(per-block subtraction in `ClearQueuedSamples`) and the sequential write-after-stop scenario below.

##### Construction: Device Query Fault Is Contained

**Test**: `PortAudioPlaybackDevice_Constructor_DeviceQueryFaults_ReportsUnavailableAndDiagnostic`

Construction does not throw when device enumeration faults; the device is unavailable and an
Error diagnostic names the fault.

##### Construction: Null Environment Is Rejected

**Test**: `PortAudioPlaybackDevice_Constructor_NullEnvironment_ThrowsArgumentNullException`

An `ArgumentNullException` is thrown.

##### Construction: Named Selection Present Resolves the Named Device

**Test**: `PortAudioPlaybackDevice_Constructor_NamedSelectionPresent_ResolvesNamedDevice`

The named (non-default) device and its format are resolved and the diagnostic names "selection"
as the basis.

##### Construction: Stale Selection Falls Back to Default Device

**Test**: `PortAudioPlaybackDevice_Constructor_StaleSelection_FallsBackToDefaultDevice`

The device is available and an Info diagnostic names "host API default" as the basis.

##### Construction: Host API Unresolvable

**Test**: `PortAudioPlaybackDevice_Constructor_HostApiUnresolvable_ReportsUnavailable`

`IsAvailable` is `false`.

##### Availability: No Resolvable Device Returns False

**Test**: `PortAudioPlaybackDevice_IsAvailable_NoResolvableDevice_ReturnsFalse`

`IsAvailable` is `false`, `Write` throws `AudioDeviceUnavailableException` and
`PendingSampleCount` is `0`.

##### Operations: No Resolvable Device Refuses Calls

**Test**: `PortAudioPlaybackDevice_Operations_NoResolvableDevice_ThrowAudioDeviceUnavailableException`

`Start`, `Stop` and `Write` throw `AudioDeviceUnavailableException`, a null `Write` throws
`ArgumentNullException`, and `PendingSampleCount` is `0`.

##### Write: Stream Requests Samples Drains Queue and Zero-Fills

**Test**: `PortAudioPlaybackDevice_Write_StreamRequestsSamples_DrainsQueueAndZeroFills`

Queued samples are returned in order and the shortfall of an oversized request is zero.

##### Write: Multiple Blocks of Varying Size Drain in Order

**Test**: `PortAudioPlaybackDevice_Write_MultipleBlocksOfVaryingSize_DrainsInOrder`

Blocks are delivered in write order and the pending count stays accurate.

##### Write: Block Larger Than Request Drains Remainder on Next Call

**Test**: `PortAudioPlaybackDevice_Write_BlockLargerThanRequest_DrainsRemainderOnNextCall`

The remainder of a partially consumed block is delivered by the next request.

##### Write: Small Block Followed by Another Write Concatenates Across Boundary

**Test**: `PortAudioPlaybackDevice_Write_SmallBlockFollowedByAnotherWrite_ConcatenatesAcrossBoundary`

One request spanning two blocks returns both in order.

##### Write: Empty and Non-Collection Lists

**Test**: `PortAudioPlaybackDevice_Write_EmptyAndNonCollectionLists_HandledCorrectly`

An empty write queues nothing; a list that is not an `ICollection<float>` is copied element by
element and drains intact.

##### Write: Caller Mutates Buffer After Write

**Test**: `PortAudioPlaybackDevice_Write_CallerMutatesBufferAfterWrite_QueuedAudioUnchanged`

The queued audio equals the samples at the time of the call.

##### Write: After Stop, Pending Count Matches Queued Samples

**Test**: `PortAudioPlaybackDevice_Write_AfterStop_PendingCountMatchesQueuedSamples`

Audio written after `Stop()` is retained and played after restart, and the pending count returns
to exactly `0` once drained.

##### Callback: Non-Positive Request Returns Empty

**Test**: `PortAudioPlaybackDevice_ProvideSamples_NonPositiveRequest_ReturnsEmptyAndKeepsQueue`

A zero-sample callback request returns an empty block and leaves queued samples pending.

##### Pending Sample Count: Write, Drain, and Stop Track Queue Depth

**Test**: `PortAudioPlaybackDevice_PendingSampleCount_WriteDrainAndStop_TracksQueueDepth`

The count rises on `Write`, falls only by samples actually dequeued (never by zero-fill), and is
`0` after `Stop()`.

##### Stop: Partially Consumed Block Never Replays Stale Audio After Restart

**Test**: `PortAudioPlaybackDevice_Stop_PartiallyConsumedBlock_RestartNeverReplaysStaleAudio`

After a successful stop and restart the first request returns silence.

##### Start: After a Failed Native Stop, No Stale Head Block Is Replayed

**Test**: `PortAudioPlaybackDevice_Start_AfterFailedNativeStop_DoesNotReplayStaleHeadBlock`

After a native stop failure and a restart, the first request returns silence, the pending count is
`0`, and a subsequent write is counted exactly (`2`) and delivered intact.

##### Playback Format: Resolved Device Reflects the Resolved Device Format

**Test**: `PortAudioPlaybackDevice_PlaybackFormat_ResolvedDevice_ReflectsResolvedDeviceFormat`

The reported channel count and sample rate equal the device's.

##### Playback Format: No Resolvable Device Returns Zero Rate and Channel Count

**Test**: `PortAudioPlaybackDevice_PlaybackFormat_NoResolvableDevice_ReturnsZeroRateAndChannelCount`

Both values are `0`.

##### Construction: Preferred Format Within Capability Is Used

**Test**: `PortAudioPlaybackDevice_Constructor_PreferredFormatWithinCapability_UsesPreferredFormat`

The preferred channel count and sample rate are requested.

##### Construction: Excessive Preferred Channel Count Is Clamped

**Test**: `PortAudioPlaybackDevice_Constructor_PreferredChannelCountExceedsCapability_ClampsAndReportsDiagnostic`

The device's maximum is requested and an Info diagnostic is reported.

##### Construction: Omitted Preferred Format Uses Device Defaults

**Test**: `PortAudioPlaybackDevice_Constructor_PreferredFormatOmitted_UsesDeviceDefaultFormat`

The device's default rate and full channel capacity are requested.

##### Construction: Unsupported Preferred Sample Rate Falls Back to Device Default

**Test**: `PortAudioPlaybackDevice_Constructor_PreferredSampleRateUnsupported_FallsBackToDeviceDefaultSampleRateAndReportsDiagnostic`

The device's default rate is used and an Info diagnostic is reported.

##### Construction: Supported Preferred Sample Rate Is Used

**Test**: `PortAudioPlaybackDevice_Constructor_PreferredSampleRateSupported_UsesPreferredFormat`

The preferred rate is requested.

##### Start: Already Started Opens the Resolved Format Once

**Test**: `PortAudioPlaybackDevice_Start_AlreadyStarted_OpensResolvedFormatOnce`

A second `Start` does nothing, and the stream was opened with the resolved channel count and
sample rate.

##### Start: Open Fails Throws AudioDeviceUnavailableException

**Test**: `PortAudioPlaybackDevice_Start_OpenFails_ThrowsAudioDeviceUnavailableException`

The failure is wrapped in `AudioDeviceUnavailableException`.

##### Start: Native Stream Start Fails Disposes the Stream and Releases the Registration

**Test**: `PortAudioPlaybackDevice_Start_NativeStreamStartFails_DisposesStreamAndReleasesRegistration`

The original failure is wrapped, the opened stream is disposed exactly once, and a later refresh
succeeds.

##### Start: Dispose Throws During Start-Failure Cleanup, Still Unregisters and Throws Original Failure

**Test**: `PortAudioPlaybackDevice_Start_DisposeThrowsDuringStartFailureCleanup_UnregistersActiveStreamAndThrowsOriginalStartFailure`

The original start failure is thrown, the dispose fault is reported, and the registration is
released.

##### Stop: Native Stop Failure Is Wrapped

**Test**: `PortAudioPlaybackDevice_Stop_NativeStopFails_ThrowsWrappedAndReleasesRegistration`

The stop failure is wrapped, the stream is still disposed, `PendingSampleCount` is `0`, and a
later refresh succeeds.

##### Stop: Native Stop and Dispose Both Fail

**Test**: `PortAudioPlaybackDevice_Stop_NativeStopAndDisposeBothFail_ThrowsStopFailure`

The stop failure is thrown and the dispose fault is reported with the "after a stop failure"
diagnostic.

##### Stop: Dispose Failure After Stop Is Wrapped

**Test**: `PortAudioPlaybackDevice_Stop_DisposeFailsAfterStop_ThrowsWrappedDisposeFailure`

The dispose failure is wrapped and the registration is still released.

##### Start: Registers an Active Stream With the Environment, Refresh Throws While Started

**Test**: `PortAudioPlaybackDevice_Start_RegistersActiveStreamWithEnvironment_RefreshThrowsWhileStarted`

`Refresh` throws `AudioDeviceInUseException` naming the device.

##### Stop: Unregisters the Active Stream, Refresh Succeeds After Stop

**Test**: `PortAudioPlaybackDevice_Stop_UnregistersActiveStream_RefreshSucceedsAfterStop`

`Refresh` completes without error.

##### Start: Environment Refreshed After Construction Throws AudioDeviceUnavailableException

**Test**: `PortAudioPlaybackDevice_Start_EnvironmentRefreshedAfterConstruction_ThrowsAudioDeviceUnavailableException`

`Start` throws and no stream is registered.

##### Start: Constructed After Refresh Starts Normally

**Test**: `PortAudioPlaybackDevice_Start_ConstructedAfterRefresh_StartsNormally`

`Start` completes without error.
