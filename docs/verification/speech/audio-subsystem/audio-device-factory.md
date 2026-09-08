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
format behavior, initialization failure degrades to the unavailable probes/devices, and device
creation is genuinely resolved consistently with whichever probe was injected (not silently
ignored in favor of an independent real-environment scan).

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

##### Creation: Injected Capture Probe Reporting No Devices Yields the Unavailable Fallback

**Test**: `AudioDeviceFactory_CreateCaptureDevice_InjectedProbeReportsNoDevices_ReturnsUnavailableDevice`

Proves an injected capture probe is genuinely consulted: even though the (fake) real
PortAudio environment has a resolvable device, an injected probe reporting zero known devices
causes `CreateCaptureDevice()` to return `UnavailableAudioCaptureDevice.Instance` rather than the
device the real environment would otherwise resolve independently.

##### Creation: Injected Capture Probe Not Knowing the Requested Device Yields the Unavailable Fallback

**Test**: `AudioDeviceFactory_CreateCaptureDevice_InjectedProbeDoesNotKnowRequestedDevice_ReturnsUnavailableDevice`

Proves a named selection not present in the injected probe's enumeration is rejected even though
the real environment could otherwise resolve a device by that name.

##### Creation: Injected Playback Probe Reporting No Devices Yields the Unavailable Fallback

**Test**: `AudioDeviceFactory_CreatePlaybackDevice_InjectedProbeReportsNoDevices_ReturnsUnavailableDevice`

##### Creation: Injected Playback Probe Knowing the Requested Device Returns the Real Device

**Test**: `AudioDeviceFactory_CreatePlaybackDevice_InjectedProbeKnowsRequestedDevice_ReturnsRealDevice`

Proves the fix does not regress the common case where the injected probe agrees with the real
environment: a selection the probe does know about still resolves to a real, available device.
