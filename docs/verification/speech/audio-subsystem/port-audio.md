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

##### Seam: Capture and Playback Devices Interact Through Fake Streams

**Tests**: `PortAudioCaptureDevice_Start_StreamCapturesSamples_RaisesFrameCaptured`,
`PortAudioPlaybackDevice_Write_StreamRequestsSamples_DrainsQueueAndZeroFills`

##### Shared Singletons: Adapter and Environment are Reusable

**Tests**: `PortAudioApi_Instance_ReadTwice_ReturnsSameInstance`,
`PortAudioEnvironment_Shared_ReadTwice_ReturnsSameInstance`
