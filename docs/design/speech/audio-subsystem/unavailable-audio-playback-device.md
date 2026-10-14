### UnavailableAudioPlaybackDevice

**Purpose**: Provide a safe, always-obtainable `IAudioPlaybackDevice` fallback for use when no
real playback backend is available.

**Data Model**: No instance fields. Exposes a single static `Instance` singleton; the
constructor is private. `IsAvailable` always returns `false`, and `ChannelCount`/`SampleRate`,
mirroring `UnavailableAudioCaptureDevice`, always return `0` - reporting a plausible-looking
format would mislead a consumer into preparing a conversion for audio that will never be
played. `PendingSampleCount` always returns `0`.

**Key Methods**:

- **Start()** / **Stop()** / **Write(IReadOnlyList&lt;float&gt;)**: Always throw
  `AudioDeviceUnavailableException`.

**Error Handling**: Same rationale as `UnavailableAudioCaptureDevice`.

**Dependencies**: `AudioDeviceUnavailableException`; implements `IAudioPlaybackDevice`.

**Callers**: `AudioDeviceFactory.CreatePlaybackDevice()` when PortAudio cannot initialize.
