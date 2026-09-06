### AudioDeviceFactory

**Purpose**: Serve as the sole composition entry point for obtaining audio capture/playback
devices and their enumeration probes.

**Data Model**: `CaptureProbe` and `PlaybackProbe` hold the public probe implementations exposed
by the factory. A private `_environment` field tracks whether PortAudio initialized
successfully, and `_diagnostics` records structural selection and fallback events.

**Key Methods**:

- **AudioDeviceFactory(...)**: Accepts optional injected probes and a diagnostics sink, and an
  internal deterministic environment for tests. When the environment reports successful
  PortAudio initialization, default probes are the real PortAudio-backed implementations;
  otherwise they are the shared `Unavailable*` probes.
- **CreateCaptureDevice(...)**: Returns a real `PortAudioCaptureDevice` when PortAudio
  initialized successfully, otherwise `UnavailableAudioCaptureDevice.Instance`.
- **CreatePlaybackDevice(...)**: Returns a real `PortAudioPlaybackDevice` when PortAudio
  initialized successfully, otherwise `UnavailableAudioPlaybackDevice.Instance`.

**Error Handling**: No member throws during composition. PortAudio initialization failure is
reported through diagnostics and represented by unavailable fallback return values.

**Dependencies**: `PortAudioEnvironment`, `PortAudioCaptureDeviceProbe`,
`PortAudioPlaybackDeviceProbe`, `PortAudioCaptureDevice`, `PortAudioPlaybackDevice`, the
`Unavailable*` fallbacks, and `ISpeechDiagnostics`.

**Callers**: Hosts that need audio devices, and later speech-recognition/synthesis subsystems.
