<!-- cspell:ignore ALSA portaudio -->
#### PortAudioEnvironment

**Purpose**: Cache PortAudio initialization state and apply the library's preferred-host policy.

**Data Model**: Holds an `IPortAudioApi` instance, the current `OSPlatform`, a lazy cached
initialization result containing `IsInitialized` and `InitializationFailureMessage` that is
reassigned (rather than mutated) by `Refresh` and always read under the sync root, an internal
`object` sync root, an active-stream registry (a dictionary keyed by owning device instance,
valued by resolved device name) that `Refresh` consults before tearing down the native runtime,
and a `Generation` counter (`internal long`, read under the sync root) that `Refresh` advances by
exactly one every time it actually reinitializes the runtime.

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
  in-use device(s) when the active-stream registry is non-empty, touching neither the runtime,
  the cached initialization state, nor `Generation`; otherwise calls `IPortAudioApi.Terminate()`
  only when the runtime had previously initialized successfully, reassigns the cached
  initialization state to a new lazy value so the next access re-attempts initialization, and
  then increments `Generation` - all inside the same lock, so a refresh that proceeds always
  advances `Generation` exactly once and one that is refused never does.
- **IsInitialized** / **InitializationFailureMessage** / **Generation**: Each acquires the sync
  root before reading the underlying cached state, so a first (lazy) evaluation of the cached
  initialization result can never race a concurrent `Refresh()` call into an inconsistent outcome
  or an unbalanced `Initialize()`/`Terminate()` pair; every evaluation of the cached state is
  fully serialized with every `Refresh()` call under the one lock.
- **Shared**: Production singleton using the real `PortAudioApi` adapter.

**Error Handling**: Converts initialization failure into cached state instead of propagating the
native exception during composition. Preferred-host absence is represented as `false` from
`TryResolvePreferredHostApi(...)`. `Refresh()` throws `AudioDeviceInUseException` when any
registered stream is active, refusing the refresh entirely rather than tearing down a live
stream.

**Dependencies**: `IPortAudioApi`, `PortAudioHostApiType`, `PortAudioHostApiInfo`, and
`AudioDeviceInUseException`.

**Callers**: `AudioDeviceFactory`, `PortAudioCaptureDeviceProbe`, `PortAudioPlaybackDeviceProbe`,
`PortAudioCaptureDevice`, and `PortAudioPlaybackDevice` (the latter two also read `Generation` at
device-resolution time so `Start()` can detect a later refresh).
