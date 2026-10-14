### UnavailableAudioCaptureDevice

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
