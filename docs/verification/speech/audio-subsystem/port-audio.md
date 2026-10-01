<!-- cspell:ignore ALSA portaudio -->
### PortAudio

#### Verification Approach

The PortAudio child subsystem is verified entirely through deterministic unit tests. Fake
`IPortAudioApi` and `IPortAudioStream` implementations drive preferred-host mapping,
initialization caching, and capture/playback stream behavior without loading real hardware in CI.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Dependencies**: No physical audio hardware required
- **Test doubles**: Fake `IPortAudioApi` and `IPortAudioStream` implementations

#### Acceptance Criteria

Tests pass when the subsystem's platform mapping, host-API resolution, initialization caching,
and mockable stream interactions behave as documented.

#### Test Scenarios

##### Environment: Platform Mapping and Host API Resolution

**Tests**: `PortAudioEnvironment_ResolvePreferredHostApiType_Windows_ReturnsWasapi`,
`PortAudioEnvironment_ResolvePreferredHostApiType_Linux_ReturnsAlsa`,
`PortAudioEnvironment_ResolvePreferredHostApiType_MacOs_ReturnsCoreAudio`,
`PortAudioEnvironment_ResolvePreferredHostApiType_UnsupportedPlatform_ReturnsNull`,
`PortAudioEnvironment_TryResolvePreferredHostApi_InitializedAndMapped_ReturnsHostApiInfo`,
`PortAudioEnvironment_TryResolvePreferredHostApi_PreferredHostApiMissing_ReturnsFalse`

##### Environment: Initialization Failure is Cached and Non-Throwing

**Test**: `PortAudioEnvironment_TryResolvePreferredHostApi_InitializationFails_ReturnsFalseAndCachesFailure`

##### Environment: Refresh Success and Refusal

**Tests**: `PortAudioEnvironment_Refresh_NoActiveStreams_ReinitializesAndReflectsNewOutcome`,
`PortAudioEnvironment_Refresh_ActiveStreamRegistered_ThrowsAudioDeviceInUseExceptionAndDoesNotTerminate`,
`PortAudioEnvironment_Refresh_NotPreviouslyInitialized_SkipsTerminateAndReinitializes`,
`PortAudioEnvironment_RegisterThenUnregisterActiveStream_Refresh_Succeeds`,
`PortAudioEnvironment_Refresh_OutcomeChanges_ReflectsNewInitializationResult`,
`PortAudioEnvironment_Refresh_TerminateThrows_PropagatesAndLeavesCachedStateUntouched`

Verifies that `Refresh()` terminates and reinitializes the native runtime (reflecting a new
initialization outcome, including a genuine fail-to-succeed transition) when no stream is active,
skips the terminate call when the runtime was never previously initialized, refuses the refresh
with `AudioDeviceInUseException` (without terminating) while any registered stream is active,
succeeds again once every previously registered stream has been unregistered, and propagates a
native `Terminate()` fault unchanged while leaving the previously cached initialization state
untouched.

##### Seam: Capture and Playback Devices Interact Through Fake Streams

**Tests**: `PortAudioCaptureDevice_Start_StreamCapturesSamples_RaisesFrameCaptured`,
`PortAudioPlaybackDevice_Write_StreamRequestsSamples_DrainsQueueAndZeroFills`

##### Shared Singletons: Adapter and Environment are Reusable

**Tests**: `PortAudioApi_Instance_ReadTwice_ReturnsSameInstance`,
`PortAudioEnvironment_Shared_ReadTwice_ReturnsSameInstance`
