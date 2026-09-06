### AudioDeviceFactory

#### Verification Approach

Verified through direct unit tests in `AudioDeviceFactoryTests.cs` using deterministic
`PortAudioEnvironment` instances. These tests prove the factory's non-throwing composition
behavior, real-default path, and initialization-failure fallback path without depending on real
hardware.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, plus NSubstitute
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

Tests pass when construction never throws, default probes are real PortAudio-backed probes when
initialization succeeds, injected probes are exposed exactly as supplied, optional preferred
formats are forwarded to the concrete devices, omitting the preference preserves device-native
format behavior, and initialization failure degrades to the unavailable probes/devices.

#### Test Scenarios

##### Construction: Custom Environment Never Throws

**Test**: `AudioDeviceFactory_Constructor_CustomEnvironment_DoesNotThrow`

##### Construction: PortAudio Initialized Exposes Real Probes

**Test**: `AudioDeviceFactory_Constructor_PortAudioInitialized_ExposesRealProbes`

##### Construction: PortAudio Initialization Failure Exposes Unavailable Probes

**Test**: `AudioDeviceFactory_Constructor_PortAudioInitializationFails_ExposesUnavailableProbes`

##### Construction: Injected Probes Are Exposed Exactly

**Test**: `AudioDeviceFactory_Constructor_InjectedProbes_ExposesInjectedProbes`

##### Creation: PortAudio Initialized Returns the Real Capture Device

**Test**: `AudioDeviceFactory_CreateCaptureDevice_PortAudioInitialized_ReturnsRealDevice`

##### Creation: Preferred Capture Format Is Forwarded

**Test**: `AudioDeviceFactory_CreateCaptureDevice_PreferredFormatSupplied_ForwardsPreferredFormat`

##### Creation: PortAudio Initialized Returns the Real Playback Device

**Test**: `AudioDeviceFactory_CreatePlaybackDevice_PortAudioInitialized_ReturnsRealDevice`

##### Creation: Preferred Playback Format Is Forwarded

**Test**: `AudioDeviceFactory_CreatePlaybackDevice_PreferredFormatSupplied_ForwardsPreferredFormat`

##### Creation: Omitting Preferred Format Preserves Device Defaults

**Test**: `AudioDeviceFactory_CreateDevices_PreferredFormatOmitted_PreservesDefaultFormatBehavior`

##### Creation: PortAudio Initialization Failure Returns Unavailable Devices

**Test**: `AudioDeviceFactory_CreateDevices_PortAudioInitializationFails_ReturnUnavailableDevices`
