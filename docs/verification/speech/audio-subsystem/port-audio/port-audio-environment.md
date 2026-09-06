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
initialization failure is cached, and preferred-host metadata resolves when the host API exists.

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
