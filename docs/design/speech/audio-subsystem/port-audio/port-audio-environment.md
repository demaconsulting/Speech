<!-- cspell:ignore ALSA portaudio -->
#### PortAudioEnvironment

**Purpose**: Cache PortAudio initialization state and apply the library's preferred-host policy.

**Data Model**: Holds an `IPortAudioApi` instance, the current `OSPlatform`, a `volatile` lazy
cached initialization result containing `IsInitialized` and `InitializationFailureMessage` that
is reassigned (rather than mutated) by `Refresh`, an internal `object` sync root, and an
active-stream registry (a dictionary keyed by owning device instance, valued by resolved device
name) that `Refresh` consults before tearing down the native runtime.

**Key Methods**:

- **ResolvePreferredHostApiType(OSPlatform)**: Maps Windows to `WASAPI`, Linux to `ALSA`, and
  macOS to `CoreAudio`, returning `null` for unsupported platforms.
- **TryResolvePreferredHostApi(...)**: Ensures PortAudio initialization has been attempted,
  resolves the preferred host API's runtime index, and returns the corresponding
  `PortAudioHostApiInfo`.
- **RegisterActiveStream(owner, deviceName)** / **UnregisterActiveStream(owner)**: Record or
  remove one device instance's active (started) stream under the sync root, keyed by reference
  identity, so a concurrent or later `Refresh` call knows whether it is safe to tear down the
  native runtime.
- **Refresh()**: Under the sync root, throws `AudioDeviceInUseException` naming the distinct
  in-use device(s) when the active-stream registry is non-empty, touching neither the runtime nor
  the cached initialization state; otherwise calls `IPortAudioApi.Terminate()` only when the
  runtime had previously initialized successfully, then always reassigns the cached
  initialization state to a new lazy value so the next access re-attempts initialization.
- **Shared**: Production singleton using the real `PortAudioApi` adapter.

**Error Handling**: Converts initialization failure into cached state instead of propagating the
native exception during composition. Preferred-host absence is represented as `false` from
`TryResolvePreferredHostApi(...)`. `Refresh()` throws `AudioDeviceInUseException` when any
registered stream is active, refusing the refresh entirely rather than tearing down a live
stream.

**Dependencies**: `IPortAudioApi`, `PortAudioHostApiType`, `PortAudioHostApiInfo`, and
`AudioDeviceInUseException`.

**Callers**: `AudioDeviceFactory`, `PortAudioCaptureDeviceProbe`, `PortAudioPlaybackDeviceProbe`,
`PortAudioCaptureDevice`, and `PortAudioPlaybackDevice`.
