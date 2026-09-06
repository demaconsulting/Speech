<!-- cspell:ignore ALSA portaudio -->
#### IPortAudioApi

**Purpose**: Define the internal seam that abstracts the subset of PortAudio functionality the
Speech library needs.

**Data Model**: N/A - this is a pure behavioral contract.

**Key Methods**:

- **Initialize()**: Initializes the PortAudio runtime.
- **HostApiCount / DeviceCount**: Expose runtime metadata counts.
- **FindHostApiIndex(...) / GetHostApiInfo(...) / GetDeviceInfo(...)**: Resolve stable host-API
  identifiers and per-device metadata.
- **OpenCaptureStream(...) / OpenPlaybackStream(...)**: Open mockable capture and playback
  streams using managed callbacks.

**Error Handling**: Implementations may throw on native/runtime faults. `PortAudioEnvironment`
and the higher-level devices convert those failures into non-throwing composition states or
`AudioDeviceUnavailableException` as appropriate.

**Dependencies**: `PortAudioHostApiType`, `PortAudioHostApiInfo`, `PortAudioDeviceInfo`, and
`IPortAudioStream`.

**Callers**: `PortAudioEnvironment`, the real probe implementations, and the real device
implementations.
