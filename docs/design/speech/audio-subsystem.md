## AudioSubsystem Design

![AudioSubsystem Structure](AudioSubsystemView.svg)

### Overview

The AudioSubsystem defines the library's public audio capture/playback contracts, name-only
selection model, honest unavailable fallbacks, and real PortAudio-backed devices and probes. It
contains the following direct units and one child subsystem:

- **AudioDeviceDescription** and **AudioDeviceSelection**: immutable value types for device
  identity and persisted device preference
- **IAudioCaptureDevice** / **IAudioPlaybackDevice**: public device contracts
- **IAudioCaptureDeviceProbe** / **IAudioPlaybackDeviceProbe**: public device-enumeration
  contracts
- **AudioDeviceFactory**: composition root that exposes real PortAudio-backed defaults when the
  runtime initializes, otherwise honest unavailable fallbacks
- **PortAudioCaptureDeviceProbe** / **PortAudioPlaybackDeviceProbe**: preferred-host filtered
  device enumeration implementations
- **PortAudioCaptureDevice** / **PortAudioPlaybackDevice**: real capture/playback implementations
- **UnavailableAudioCaptureDevice** / **UnavailableAudioPlaybackDevice** and their probes, plus
  **AudioDeviceUnavailableException**: honest fallback behavior when no real backend or no device
  is available
- **PortAudio**: child subsystem containing the internal interop seam and the real
  PortAudioSharp2 adapter

### Interfaces

The subsystem exposes `IAudioCaptureDevice`, `IAudioPlaybackDevice`,
`IAudioCaptureDeviceProbe`, `IAudioPlaybackDeviceProbe`, `AudioDeviceDescription`,
`AudioDeviceSelection`, `AudioDeviceFactory`, and `AudioDeviceUnavailableException` as its
public API. It consumes `ISpeechDiagnostics` from the Diagnostics subsystem to report structural
selection, start/stop, and fallback facts without ever exposing raw audio content.

### Design

`AudioDeviceFactory` is the subsystem's composition root. It consults `PortAudioEnvironment` to
see whether PortAudio initialized successfully. When initialization succeeds, the factory exposes
real `PortAudioCaptureDeviceProbe` and `PortAudioPlaybackDeviceProbe` instances as defaults, and
creates real `PortAudioCaptureDevice` or `PortAudioPlaybackDevice` instances on request. When
initialization fails, the factory still composes safely and falls back to the shared
`Unavailable*` probes and devices.

The real probes and devices all rely on the same selection policy. `PortAudioEnvironment`
resolves one preferred host API for the current OS. Probe enumeration filters PortAudio's full
device catalog down to only devices belonging to that host API and to the requested direction.
Device construction first attempts an exact ordinal match on `AudioDeviceSelection.DeviceName`.
If that match fails, it falls back to the host API's default input or output device. If neither
is available, the real device still composes but reports `IsAvailable = false` and throws
`AudioDeviceUnavailableException` only when operational members are invoked.

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
