<!-- cspell:ignore ALSA portaudio -->
## AudioSubsystem Verification

### Verification Approach

The AudioSubsystem is verified primarily through deterministic unit tests that substitute fake
`IPortAudioApi` and `IPortAudioStream` implementations behind `PortAudioEnvironment`. This makes
preferred-host selection, name-based device matching, fallback to host-API defaults,
preferred-format forwarding and clamping, queue behavior, and native-fault degradation fully
testable without requiring physical audio hardware.

Automated coverage **does not** include true end-to-end hardware I/O. Actually opening a real
microphone or speaker and moving audio through it requires manual/local verification on a machine
with supported audio hardware.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services and no required physical audio hardware
- **Test doubles**: Fake `IPortAudioApi` and `IPortAudioStream` implementations plus
  NSubstitute diagnostics sinks where interaction verification is valuable

### Acceptance Criteria

An AudioSubsystem test run passes when:

- Preferred-host probe enumeration returns only devices from the selected host API
- Stale persisted names fall back to the host API's default capture/playback device
- Preferred-format hints are forwarded by the factory and resolved by PortAudio-backed devices
  without ever requesting more channels than a device exposes
- Real PortAudio-backed devices report `IsAvailable` honestly
- Capture frame delivery and playback queue draining work through the seam
- PortAudio initialization failures and stream-open failures degrade to the documented fallback
  behavior and `AudioDeviceUnavailableException`
- The automated verification boundary remains honest about the absence of hardware I/O coverage

### Test Scenarios

#### Preferred Host API: Capture Enumeration Filters Devices

**Tests**: `PortAudioCaptureDeviceProbe_Enumerate_MixedHostApis_ReturnsOnlyPreferredInputDevices`,
`PortAudioCaptureDeviceProbe_Enumerate_PreferredHostApiMissing_ReturnsEmptyList`,
`PortAudioCaptureDeviceProbe_Enumerate_NoDevices_ReturnsEmptyList`

Verifies that capture-device enumeration filters by the preferred host API and degrades to an
empty list when the host API or devices are absent.

#### Preferred Host API: Playback Enumeration Filters Devices

**Tests**: `PortAudioPlaybackDeviceProbe_Enumerate_MixedHostApis_ReturnsOnlyPreferredOutputDevices`,
`PortAudioPlaybackDeviceProbe_Enumerate_PreferredHostApiMissing_ReturnsEmptyList`,
`PortAudioPlaybackDeviceProbe_Enumerate_NoDevices_ReturnsEmptyList`

Verifies that playback-device enumeration filters by the preferred host API and degrades to an
empty list when the host API or devices are absent.

#### Audio Format Value: Validation and Convenience Construction

**Tests**: `AudioFormat_Constructor_ValidValues_ExposesProperties`,
`AudioFormat_Constructor_NonPositiveSampleRate_ThrowsArgumentOutOfRangeException`,
`AudioFormat_Constructor_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException`,
`AudioFormat_Mono_ValidSampleRate_ReturnsMonoFormat`,
`AudioFormat_Mono_NonPositiveSampleRate_ThrowsArgumentOutOfRangeException`,
`AudioFormat_ValueEquality_EquivalentValuesCompareByValue`

Verifies that the plain `AudioFormat` value rejects impossible rates/channel counts, exposes
accepted values unchanged, provides a concise mono helper, and compares by value.

#### Capture Device: Selection, Availability, and Stream Delivery

**Tests**: `PortAudioCaptureDevice_Constructor_StaleSelection_FallsBackToDefaultDevice`,
`PortAudioCaptureDevice_IsAvailable_NoResolvableDevice_ReturnsFalse`,
`PortAudioCaptureDevice_Start_StreamCapturesSamples_RaisesFrameCaptured`,
`PortAudioCaptureDevice_Start_OpenFails_ThrowsAudioDeviceUnavailableException`

Verifies capture-device default fallback, honest availability reporting, sample delivery through
`FrameCaptured`, and first-use failure wrapping.

#### Capture Device: Reported Capture Format

**Tests**: `PortAudioCaptureDevice_CaptureFormat_ResolvedDevice_ReflectsResolvedDeviceFormat`,
`PortAudioCaptureDevice_Constructor_PreferredFormatWithinCapability_UsesPreferredFormat`,
`PortAudioCaptureDevice_Constructor_PreferredChannelCountExceedsCapability_ClampsAndReportsDiagnostic`,
`PortAudioCaptureDevice_Constructor_PreferredFormatOmitted_UsesDeviceDefaultFormat`,
`PortAudioCaptureDevice_CaptureFormat_NoResolvableDevice_ReturnsZeroRateAndChannelCount`,
`UnavailableAudioCaptureDevice_CaptureFormat_Read_ReturnsZeroRateAndChannelCount`

Verifies that a resolved capture device reports the requested capture format actually opened on
the resolved device, honors an in-range preferred format, clamps an excessive preferred channel
count down to device capability, falls back to the device default when no preference is supplied,
and that both an unresolved real device and the shared unavailable fallback report zero for both
values rather than a plausible-looking default.

#### Playback Device: Selection, Availability, and Queued Playback

**Tests**: `PortAudioPlaybackDevice_Constructor_StaleSelection_FallsBackToDefaultDevice`,
`PortAudioPlaybackDevice_IsAvailable_NoResolvableDevice_ReturnsFalse`,
`PortAudioPlaybackDevice_Write_StreamRequestsSamples_DrainsQueueAndZeroFills`,
`PortAudioPlaybackDevice_Start_OpenFails_ThrowsAudioDeviceUnavailableException`

Verifies playback-device default fallback, honest availability reporting, ordered queue
consumption, underrun zero-fill, and first-use failure wrapping.

#### Playback Device: Reported Playback Format

**Tests**: `PortAudioPlaybackDevice_PlaybackFormat_ResolvedDevice_ReflectsResolvedDeviceFormat`,
`PortAudioPlaybackDevice_Constructor_PreferredFormatWithinCapability_UsesPreferredFormat`,
`PortAudioPlaybackDevice_Constructor_PreferredChannelCountExceedsCapability_ClampsAndReportsDiagnostic`,
`PortAudioPlaybackDevice_Constructor_PreferredFormatOmitted_UsesDeviceDefaultFormat`,
`PortAudioPlaybackDevice_PlaybackFormat_NoResolvableDevice_ReturnsZeroRateAndChannelCount`,
`UnavailableAudioPlaybackDevice_PlaybackFormat_Read_ReturnsZeroRateAndChannelCount`

Verifies that a resolved playback device reports the requested playback format actually opened on
the resolved device, honors an in-range preferred format, clamps an excessive preferred channel
count down to device capability, falls back to the device default when no preference is supplied,
and that both an unresolved real device and the shared unavailable fallback report zero for both
values rather than a plausible-looking default, mirroring the identical capture-direction
scenario above.

#### Playback Device: Pending Sample Drain Tracking

**Tests**: `PortAudioPlaybackDevice_PendingSampleCount_WriteDrainAndStop_TracksQueueDepth`,
`PortAudioPlaybackDevice_IsAvailable_NoResolvableDevice_ReturnsFalse`,
`UnavailableAudioPlaybackDevice_PendingSampleCount_Read_ReturnsZero`

Verifies that `PendingSampleCount` increases as samples are written, decreases by exactly the
number of samples a callback genuinely dequeues (never by a zero-fill shortfall), resets to zero
once `Stop` clears the queue, and honestly reports zero both for an unresolved real device and
the shared unavailable fallback - fixing a bug where `SherpaOnnxSpeechSynthesizer.PlayStreamAsync`
had no way to observe genuine hardware-drain progress and so stopped the device (discarding
whatever was still queued) as soon as every segment was enqueued, cutting audio off almost
instantly.

#### PortAudio Environment: Platform Mapping and Initialization Caching

**Tests**: `PortAudioEnvironment_ResolvePreferredHostApiType_Windows_ReturnsWasapi`,
`PortAudioEnvironment_ResolvePreferredHostApiType_Linux_ReturnsAlsa`,
`PortAudioEnvironment_ResolvePreferredHostApiType_MacOs_ReturnsCoreAudio`,
`PortAudioEnvironment_ResolvePreferredHostApiType_UnsupportedPlatform_ReturnsNull`,
`PortAudioEnvironment_TryResolvePreferredHostApi_InitializedAndMapped_ReturnsHostApiInfo`,
`PortAudioEnvironment_TryResolvePreferredHostApi_InitializationFails_ReturnsFalseAndCachesFailure`,
`PortAudioEnvironment_TryResolvePreferredHostApi_PreferredHostApiMissing_ReturnsFalse`

Verifies deterministic preferred-host mapping, runtime host-API metadata resolution, and cached
non-throwing initialization-failure behavior.

#### Factory: Real Defaults and Initialization Fallback

**Tests**: `AudioDeviceFactory_Constructor_CustomEnvironment_DoesNotThrow`,
`AudioDeviceFactory_Constructor_PortAudioInitialized_ExposesRealProbes`,
`AudioDeviceFactory_Constructor_PortAudioInitializationFails_ExposesUnavailableProbes`,
`AudioDeviceFactory_Constructor_InjectedProbes_ExposesInjectedProbes`,
`AudioDeviceFactory_CreateCaptureDevice_PortAudioInitialized_ReturnsRealDevice`,
`AudioDeviceFactory_CreateCaptureDevice_PreferredFormatSupplied_ForwardsPreferredFormat`,
`AudioDeviceFactory_CreatePlaybackDevice_PortAudioInitialized_ReturnsRealDevice`,
`AudioDeviceFactory_CreatePlaybackDevice_PreferredFormatSupplied_ForwardsPreferredFormat`,
`AudioDeviceFactory_CreateDevices_PreferredFormatOmitted_PreservesDefaultFormatBehavior`,
`AudioDeviceFactory_CreateDevices_PortAudioInitializationFails_ReturnUnavailableDevices`

Verifies the real default composition path, injection support, and graceful fallback when the
PortAudio runtime cannot initialize, while also proving that optional preferred-format hints are
forwarded to the concrete devices and that omitting the hint preserves the prior device-native
behavior.

#### Unavailable Fallbacks: Honest Degradation

**Tests**: `UnavailableAudioCaptureDevice_IsAvailable_Read_ReturnsFalse`,
`UnavailableAudioPlaybackDevice_IsAvailable_Read_ReturnsFalse`,
`UnavailableAudioCaptureDeviceProbe_Enumerate_Always_ReturnsEmptyList`,
`UnavailableAudioPlaybackDeviceProbe_Enumerate_Always_ReturnsEmptyList`

Verifies the shared unavailable fallbacks remain honest and usable as the last-resort
composition path.

#### AudioDeviceSelection: Name-Only Identity Resolution and Stale-Selection Fallback

**Tests**: `AudioDeviceSelection_Resolve_MatchingName_ReturnsMatchingDevice`,
`AudioDeviceSelection_Resolve_NoLongerPresent_ReturnsNull`,
`AudioDeviceSelection_Resolve_SystemDefault_ReturnsNull`,
`AudioDeviceSelection_Resolve_EmptyDeviceList_ReturnsNull`,
`AudioDeviceSelection_Resolve_NullDeviceList_ThrowsArgumentNullException`

Verifies `AudioDeviceSelection.Resolve`'s exact-name-only identity match against the current
device list (`Speech-Audio-NameOnlyIdentity`), and that a persisted selection naming a device no
longer present, an explicit system-default selection, or an empty device list all resolve to
`null` so the caller falls back to the host API's default device rather than resolving to a
different, unintended device (`Speech-Audio-SelectionFallback`).
