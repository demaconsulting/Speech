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
- **PortAudio**: child subsystem containing the internal interop seam and the real
  PortAudioSharp2 adapter

### Interfaces

The subsystem exposes `IAudioCaptureDevice`, `IAudioPlaybackDevice`,
`IAudioCaptureDeviceProbe`, `IAudioPlaybackDeviceProbe`, `AudioFormat`,
`AudioDeviceDescription`, `AudioDeviceSelection`, `AudioDeviceFactory`,
`AudioDeviceUnavailableException`, and `AudioDeviceInUseException` as its public API. It consumes
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

#### WindowedSincLowpassFilter

**Purpose**: Provide the shared, direction-agnostic windowed-sinc Hamming lowpass FIR primitive
that both `RecognitionSubsystem.AudioFrameResampler` (capture direction) and
`SynthesisSubsystem.PlaybackAudioResampler` (playback direction) need immediately before their
own decimation step, so the anti-aliasing DSP math is written and tested once instead of
duplicated byte-for-byte in two direction-specific resamplers.

**Data Model**: Stateless static class; no instance fields. Operates purely on `float` sample
arrays and kernel coefficients, with no knowledge of channels, capture, or playback.

**Key Methods**:

- **BuildLowpassKernel(double cutoffRatio, int tapCount)**: Builds a normalized Hamming-windowed
  sinc kernel whose coefficients sum to `1.0`, for a given cutoff ratio (a fraction of the source
  signal's Nyquist frequency) and an odd tap count.
- **ApplyLowpassFilter(ReadOnlySpan&lt;float&gt;, float[])**: Applies a symmetric FIR kernel to a
  mono signal, replicating the nearest endpoint sample at the edges so the output length always
  matches the input length exactly.
- **DownsamplingFilterTapCount**: The odd-numbered FIR tap count (`33`) both resamplers use for
  their anti-aliasing filter.

**Error Handling**: `BuildLowpassKernel` throws `ArgumentOutOfRangeException` when the cutoff
ratio lies outside `(0, 1]` or the tap count is non-positive, and `ArgumentException` when the
tap count is even, because the kernel is only well-defined when symmetric around one exact center
sample. `ApplyLowpassFilter` throws `ArgumentNullException` for a null kernel.

**Dependencies**: None beyond the Base Class Library.

**Callers**: `RecognitionSubsystem.AudioFrameResampler`, `SynthesisSubsystem.PlaybackAudioResampler`.

#### UnavailableAudioCaptureDevice

**Purpose**: Provide a safe, always-obtainable `IAudioCaptureDevice` fallback for use when no
real capture backend is available.

**Data Model**: No instance fields. Exposes a single static `Instance` singleton; the
constructor is private. `IsAvailable` always returns `false`, and `ChannelCount`/`SampleRate`
always return `0` - reporting a plausible-looking format would mislead a consumer into preparing
a conversion for audio that will never arrive.

**Key Methods**:

- **Start()** / **Stop()**: Always throw `AudioDeviceUnavailableException`.

**Error Handling**: Signals misuse of a known-unavailable device with
`AudioDeviceUnavailableException`.

**Dependencies**: `AudioDeviceUnavailableException`; implements `IAudioCaptureDevice`.

**Callers**: `AudioDeviceFactory.CreateCaptureDevice()` when PortAudio cannot initialize.

#### UnavailableAudioPlaybackDevice

**Purpose**: Provide a safe, always-obtainable `IAudioPlaybackDevice` fallback for use when no
real playback backend is available.

**Data Model**: No instance fields. Exposes a single static `Instance` singleton; the
constructor is private. `IsAvailable` always returns `false`, and `ChannelCount`/`SampleRate`,
added in Sub-phase 4b mirroring `UnavailableAudioCaptureDevice`'s identical Phase 3 addition,
always return `0` - reporting a plausible-looking format would mislead a consumer into preparing
a conversion for audio that will never be played.

**Key Methods**:

- **Start()** / **Stop()** / **Write(IReadOnlyList&lt;float&gt;)**: Always throw
  `AudioDeviceUnavailableException`.

**Error Handling**: Same rationale as `UnavailableAudioCaptureDevice`.

**Dependencies**: `AudioDeviceUnavailableException`; implements `IAudioPlaybackDevice`.

**Callers**: `AudioDeviceFactory.CreatePlaybackDevice()` when PortAudio cannot initialize.

#### UnavailableAudioCaptureDeviceProbe

**Purpose**: Provide a safe `IAudioCaptureDeviceProbe` fallback that reports no devices rather
than throwing.

**Data Model**: No instance fields. Exposes a single static `Instance` singleton.

**Key Methods**:

- **Enumerate()**: Always returns an empty list.

**Error Handling**: N/A - absence is represented as an empty list.

**Dependencies**: `AudioDeviceDescription`; implements `IAudioCaptureDeviceProbe`.

**Callers**: `AudioDeviceFactory` when PortAudio cannot initialize.

#### UnavailableAudioPlaybackDeviceProbe

**Purpose**: Provide a safe `IAudioPlaybackDeviceProbe` fallback that reports no devices rather
than throwing.

**Data Model**: No instance fields. Exposes a single static `Instance` singleton.

**Key Methods**:

- **Enumerate()**: Always returns an empty list.

**Error Handling**: N/A - absence is represented as an empty list.

**Dependencies**: `AudioDeviceDescription`; implements `IAudioPlaybackDeviceProbe`.

**Callers**: `AudioDeviceFactory` when PortAudio cannot initialize.

#### AudioDeviceUnavailableException

**Purpose**: Signal that an operational member of an unavailable audio device was invoked.

**Data Model**: No additional fields beyond the standard `Exception` base members.

**Key Methods**: Standard three-constructor exception pattern.

**Error Handling**: This type is itself the error-handling mechanism.

**Dependencies**: `Exception`.

**Callers**: The unavailable fallback devices and real PortAudio devices when first-use native
failures occur.

#### AudioDeviceInUseException

**Purpose**: Refuse a PortAudio device-table refresh while any capture/playback device created
from the environment currently has an active (open/started) stream.

**Data Model**: No additional fields beyond the standard `Exception` base members.

**Key Methods**: Standard three-constructor exception pattern (default, message, message with
inner exception), mirroring `AudioDeviceUnavailableException`.

**Error Handling**: This type is itself the error-handling mechanism, and is the one deliberate
exception to this library's "never throws at composition" policy. Refreshing PortAudio's device
table requires tearing down and reinitializing the native runtime, which would invalidate any
stream currently open against it; rather than silently tearing down a live stream, the refresh is
refused outright. Unlike `AudioDeviceUnavailableException`, this is caller-driven, recoverable
misuse - the caller can stop the active capture/playback session(s) via `IAudioCaptureDevice.Stop()`/
`IAudioPlaybackDevice.Stop()` and retry `RefreshDevices()` - not an honest composition-time
unavailability. It exposes no structured device list; any identifying detail is only ever present
as free-form text in the exception's message.

**Dependencies**: `Exception`.

**Callers**: `PortAudioEnvironment.Refresh()`, `AudioDeviceFactory.RefreshDevices()`.

#### WavFileAudioPlaybackDevice

See `docs/design/speech/audio-subsystem/wav-file-audio-playback-device.md` for full detail.
Writes synthesized speech to a `.wav` file as 16-bit PCM instead of rendering it to real playback
hardware, so any host application can capture synthesized audio deterministically. Implements
`IAudioPlaybackDevice`; always reports `IsAvailable = true` since file writes never depend on
real audio hardware.

#### WavFileAudioCaptureDevice

See `docs/design/speech/audio-subsystem/wav-file-audio-capture-device.md` for full detail. Reads
a mono, 16-bit PCM `.wav` file and delivers it as normalized capture frames instead of capturing
from real microphone hardware, so any host application can drive speech recognition from a
pre-recorded file deterministically. Implements `IAudioCaptureDevice` plus one additional public
member, `EndOfFileReached`, raised once the file has been fully consumed.
