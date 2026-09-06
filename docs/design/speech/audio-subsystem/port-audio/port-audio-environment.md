<!-- cspell:ignore ALSA portaudio -->
#### PortAudioEnvironment

**Purpose**: Cache PortAudio initialization state and apply the library's preferred-host policy.

**Data Model**: Holds an `IPortAudioApi` instance, the current `OSPlatform`, and a lazy cached
initialization result containing `IsInitialized` and `InitializationFailureMessage`.

**Key Methods**:

- **ResolvePreferredHostApiType(OSPlatform)**: Maps Windows to `WASAPI`, Linux to `ALSA`, and
  macOS to `CoreAudio`, returning `null` for unsupported platforms.
- **TryResolvePreferredHostApi(...)**: Ensures PortAudio initialization has been attempted,
  resolves the preferred host API's runtime index, and returns the corresponding
  `PortAudioHostApiInfo`.
- **Shared**: Production singleton using the real `PortAudioApi` adapter.

**Error Handling**: Converts initialization failure into cached state instead of propagating the
native exception during composition. Preferred-host absence is represented as `false` from
`TryResolvePreferredHostApi(...)`.

**Dependencies**: `IPortAudioApi`, `PortAudioHostApiType`, and `PortAudioHostApiInfo`.

**Callers**: `AudioDeviceFactory`, `PortAudioCaptureDeviceProbe`, `PortAudioPlaybackDeviceProbe`,
`PortAudioCaptureDevice`, and `PortAudioPlaybackDevice`.
