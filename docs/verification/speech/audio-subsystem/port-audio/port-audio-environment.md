<!-- cspell:ignore ALSA portaudio -->
#### PortAudioEnvironment

##### Verification Approach

Verified through direct unit tests in `PortAudioEnvironmentTests.cs` using fake `IPortAudioApi`
implementations and through singleton access tests for the shared production environment.

##### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

##### Acceptance Criteria

Tests pass when supported-platform mappings are correct, unsupported platforms return `null`,
initialization failure is cached, preferred-host metadata resolves when the host API exists, a
refresh that actually proceeds advances the `Generation` counter by exactly one while a refresh
refused as in-use leaves it unchanged, and a concurrent first evaluation of the cached
initialization state and a concurrent `Refresh()` call are fully serialized rather than producing
an unbalanced `Initialize()`/`Terminate()` pair.

##### Test Scenarios

##### Platform Mapping: Supported and Unsupported Platforms

**Tests**: `PortAudioEnvironment_ResolvePreferredHostApiType_Windows_ReturnsWasapi`,
`PortAudioEnvironment_ResolvePreferredHostApiType_Linux_ReturnsAlsa`,
`PortAudioEnvironment_ResolvePreferredHostApiType_MacOs_ReturnsCoreAudio`,
`PortAudioEnvironment_ResolvePreferredHostApiType_UnsupportedPlatform_ReturnsNull`

##### Resolution: Preferred Host API Returns Metadata

**Test**: `PortAudioEnvironment_TryResolvePreferredHostApi_InitializedAndMapped_ReturnsHostApiInfo`

##### Resolution: Initialization Failure is Cached

**Test**: `PortAudioEnvironment_TryResolvePreferredHostApi_InitializationFails_ReturnsFalseAndCachesFailure`

##### Resolution: Missing Host API Returns False

**Test**: `PortAudioEnvironment_TryResolvePreferredHostApi_PreferredHostApiMissing_ReturnsFalse`

##### Shared Environment: Read Twice Returns Same Instance

**Test**: `PortAudioEnvironment_Shared_ReadTwice_ReturnsSameInstance`

##### Refresh: Reinitializes and Reflects New Outcome When No Stream Is Active

**Test**: `PortAudioEnvironment_Refresh_NoActiveStreams_ReinitializesAndReflectsNewOutcome`

##### Refresh: Refused With AudioDeviceInUseException While a Stream Is Active

**Test**: `PortAudioEnvironment_Refresh_ActiveStreamRegistered_ThrowsAudioDeviceInUseExceptionAndDoesNotTerminate`

##### Refresh: Skips Terminate When Never Previously Initialized

**Test**: `PortAudioEnvironment_Refresh_NotPreviouslyInitialized_SkipsTerminateAndReinitializes`

##### Refresh: Succeeds Once a Registered Stream Is Unregistered

**Test**: `PortAudioEnvironment_RegisterThenUnregisterActiveStream_Refresh_Succeeds`

##### Refresh: No Active Streams Increments Generation

**Test**: `PortAudioEnvironment_Refresh_NoActiveStreams_IncrementsGeneration`

##### Refresh: Refused Refresh Does Not Increment Generation

**Test**: `PortAudioEnvironment_Refresh_ActiveStreamRegistered_DoesNotIncrementGeneration`

##### IsInitialized: Concurrent Evaluation With Refresh Serializes and Preserves Initialize/Terminate Balance

**Test**: `PortAudioEnvironment_IsInitialized_ConcurrentWithRefresh_SerializesAndPreservesInitializeTerminateBalance`
