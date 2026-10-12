## AudioSubsystem Design

![AudioSubsystem Structure](AudioSubsystemView.svg)

### Overview

The AudioSubsystem defines the library's public audio capture/playback contracts, name-only
selection model, audio-format value type, honest unavailable fallbacks, and real
PortAudio-backed devices and probes. It contains the following direct units and one child
subsystem:

- **AudioFormat**: immutable validating value type for passing requested, preferred, and
  resolved audio-format intent around the library without leaking any backend-native type
- **WindowedSincLowpassFilter**: shared, dependency-free windowed-sinc Hamming FIR lowpass
  primitive used by both direction-specific resamplers' downsampling anti-aliasing stage
- **AudioDeviceDescription** and **AudioDeviceSelection**: immutable value types for device
  identity and persisted device preference
- **IAudioCaptureDevice** / **IAudioPlaybackDevice**: public device contracts
- **IAudioCaptureDeviceProbe** / **IAudioPlaybackDeviceProbe**: public device-enumeration
  contracts
- **AudioDeviceFactory**: composition root that exposes real PortAudio-backed defaults when the
  runtime initializes, otherwise honest unavailable fallbacks, and can re-scan the PortAudio
  device table on demand through `RefreshDevices()`
- **PortAudioCaptureDeviceProbe** / **PortAudioPlaybackDeviceProbe**: preferred-host filtered
  device enumeration implementations
- **PortAudioCaptureDevice** / **PortAudioPlaybackDevice**: real capture/playback implementations
- **UnavailableAudioCaptureDevice** / **UnavailableAudioPlaybackDevice** and their probes, plus
  **AudioDeviceUnavailableException**: honest fallback behavior when no real backend or no device
  is available
- **AudioDeviceInUseException**: refuses a device-table refresh while a capture/playback device
  created from the factory currently has an active stream - the one deliberate exception to this
  library's "never throws at composition" policy
- **WavFileAudioPlaybackDevice** / **WavFileAudioCaptureDevice**: file-backed
  `IAudioPlaybackDevice`/`IAudioCaptureDevice` implementations that write/read a mono, 16-bit PCM
  `.wav` file instead of using real hardware, for deterministic file-based synthesis/recognition
  by any host application
- **AudioFileReader** / **AudioClip** / **AudioResampler**: managed WAV and FLAC decoding into a
  floating-point clip, plus down-mixing and resampling, with no native decoder dependency
- **WavDecoder** / **FlacDecoder**: the internal container decoders behind `AudioFileReader`
- **PortAudio**: child subsystem containing the internal interop seam and the real
  PortAudioSharp2 adapter

### Interfaces

The subsystem exposes `IAudioCaptureDevice`, `IAudioPlaybackDevice`,
`IAudioCaptureDeviceProbe`, `IAudioPlaybackDeviceProbe`, `AudioFormat`,
`AudioDeviceDescription`, `AudioDeviceSelection`, `AudioDeviceFactory`,
`AudioDeviceUnavailableException`, `AudioDeviceInUseException`, `AudioFileReader`,
`AudioFileReadOptions`, `AudioClip`, `AudioResampler`, `AudioDeviceDirection`,
`AudioCaptureFrameEventArgs`, `UnavailableAudioCaptureDevice`, `UnavailableAudioPlaybackDevice`,
`UnavailableAudioCaptureDeviceProbe`, `UnavailableAudioPlaybackDeviceProbe`,
`WavFileAudioCaptureDevice` and `WavFileAudioPlaybackDevice` as its public API. The internal
`FlacBitReader`, `FlacCrc` and `FlacFrameDecoder` helpers support `FlacDecoder`. It consumes
`ISpeechDiagnostics` from the
Diagnostics subsystem to report structural selection, start/stop, and fallback facts without
ever exposing raw audio content. The ModelManagementSubsystem also consumes `AudioFormat` as a
narrow plain-data dependency for model format declarations, rather than duplicating another
format type there.

### Design

`AudioDeviceFactory` is the subsystem's composition root. It consults `PortAudioEnvironment` to
see whether PortAudio initialized successfully. When initialization succeeds, the factory exposes
real `PortAudioCaptureDeviceProbe` and `PortAudioPlaybackDeviceProbe` instances as defaults, and
creates real `PortAudioCaptureDevice` or `PortAudioPlaybackDevice` instances on request. When
initialization fails, the factory still composes safely and falls back to the shared
`Unavailable*` probes and devices.

`AudioFormat` is the subsystem's plain data value for carrying audio-format intent through that
composition boundary. At device-construction call sites it expresses a caller's preferred sample
rate and channel count, not a guarantee that the backend must expose them exactly.
`PortAudioCaptureDevice` and `PortAudioPlaybackDevice` request the preferred sample rate when one
is supplied, clamp the preferred channel count down to the resolved device's maximum capability,
and otherwise fall back to the device's own default sample rate and full channel capacity. The
device instance's own `SampleRate`/`ChannelCount` properties remain the resolved truth a caller
uses after construction.

The real probes and devices all rely on the same selection policy. `PortAudioEnvironment`
resolves one preferred host API for the current OS. Probe enumeration filters PortAudio's full
device catalog down to only devices belonging to that host API and to the requested direction.
Device construction first attempts an exact ordinal match on `AudioDeviceSelection.DeviceName`.
If that match fails, it falls back to the host API's default input or output device. If neither
is available, the real device still composes but reports `IsAvailable = false` and throws
`AudioDeviceUnavailableException` only when operational members are invoked.

A host application can force the PortAudio device table to be re-scanned - for example after a
user plugs in a headset - by calling `AudioDeviceFactory.RefreshDevices()`. This is the one
deliberate exception to this library's "never throws at composition" policy: refreshing requires
tearing down and reinitializing the native PortAudio runtime, which cannot safely happen while any
device created from the factory's environment currently has an open/started stream. When a stream
is active, the call is refused outright with `AudioDeviceInUseException` describing the in-use
device(s) only in its message (it exposes no structured device list), and neither the environment
nor the factory's probes are modified; the caller can stop the active stream(s) via
`IAudioCaptureDevice.Stop()`/`IAudioPlaybackDevice.Stop()` and retry. `PortAudioCaptureDevice.Start()`/`Stop()`
and `PortAudioPlaybackDevice.Start()`/`Stop()` register/unregister themselves with the environment
for exactly this purpose. Calling `Enumerate()` on `CaptureProbe`/`PlaybackProbe` alone, without
first calling `RefreshDevices()`, never detects newly attached or removed hardware. When the
refresh succeeds, `RefreshDevices()` re-evaluates `CaptureProbe`/`PlaybackProbe` against the new
initialization outcome, but only for a probe that was not explicitly injected at construction - an
explicitly injected probe is never replaced, so it will not reflect new hardware unless the
caller's own probe implementation handles that itself.

**Thread safety**: `PortAudioEnvironment.Refresh()` performs its active-stream refusal check and
runtime termination/reinitialization atomically under an internal lock, so a stream registered
before a `Refresh()` call began can never be torn down by it. `AudioDeviceFactory.RefreshDevices()`
itself, however, is not safe to call concurrently with another `RefreshDevices()` call or with a
concurrent read of `CaptureProbe`/`PlaybackProbe` on the same factory instance: the factory
re-assigns its own `_captureProbe`/`_playbackProbe` fields without a lock after delegating to the
environment, so a caller that refreshes and reads these properties from multiple threads without
its own external synchronization may observe a stale or torn-looking probe reference. A host
application driving `RefreshDevices()` from a UI or background thread should serialize its own
calls (for example, by routing every refresh through one thread or its own lock) rather than
relying on the factory to do so.

#### Unit Design Documents

Each of the following units is documented in its own design document under
`docs/design/speech/audio-subsystem/`:

- **WindowedSincLowpassFilter** (`windowed-sinc-lowpass-filter.md`): shared anti-aliasing FIR
  primitive for both direction-specific resamplers.
- **UnavailableAudioCaptureDevice** (`unavailable-audio-capture-device.md`) and
  **UnavailableAudioPlaybackDevice** (`unavailable-audio-playback-device.md`): honest unavailable
  device fallbacks.
- **UnavailableAudioCaptureDeviceProbe** (`unavailable-audio-capture-device-probe.md`) and
  **UnavailableAudioPlaybackDeviceProbe** (`unavailable-audio-playback-device-probe.md`): honest
  unavailable probe fallbacks that enumerate no devices.
- **AudioDeviceUnavailableException** (`audio-device-unavailable-exception.md`): signals misuse of
  an unavailable device.
- **AudioDeviceInUseException** (`audio-device-in-use-exception.md`): refuses a device-table
  refresh while a stream is active.
- **WavFileAudioPlaybackDevice** (`wav-file-audio-playback-device.md`) and
  **WavFileAudioCaptureDevice** (`wav-file-audio-capture-device.md`): file-backed playback and
  capture devices.
