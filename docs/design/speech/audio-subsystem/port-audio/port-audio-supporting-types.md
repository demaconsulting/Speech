<!-- cspell:ignore ALSA portaudio -->
#### PortAudio Supporting Types

**Purpose**: Provide the managed metadata records, stable host-API constants, and repository-local
P/Invoke bindings that `PortAudioApi` and `PortAudioEnvironment` depend on, without exposing
PortAudioSharp2 or native structs to higher-level logic.

**Data Model**:

- **PortAudioDeviceInfo**: Managed device metadata record carrying device name, host-API index,
  channel counts, default sample rate, and default low-latency values.
- **PortAudioHostApiInfo**: Managed host-API metadata record carrying the stable host-API type,
  host-API name, and host-API-scoped default input/output device indices.
- **PortAudioHostApiType**: Internal enum carrying the stable PortAudio constants used by the OS
  to preferred-host mapping policy.
- **PortAudioNativeMethods**: Internal static class declaring the native host-API functions absent
  from PortAudioSharp2 and the managed layout for `PaHostApiInfo`.

**Key Methods**:

- **PortAudioNativeMethods.Pa_GetHostApiCount()**: Returns the number of host APIs known to the
  loaded native PortAudio runtime.
- **PortAudioNativeMethods.Pa_GetHostApiInfo(...)**: Reads a native `PaHostApiInfo` structure for a
  given host-API index.
- **PortAudioNativeMethods.Pa_HostApiTypeIdToHostApiIndex(...)**: Resolves the runtime host-API
  index for a stable `PortAudioHostApiType` constant, returning a negative value when the host API
  is not present on the current machine.

**Error Handling**: The metadata records are plain data carriers with no validation of their own;
`PortAudioApi` is responsible for converting invalid native return values (negative counts, invalid
pointers) into managed exceptions before constructing these types. `PortAudioNativeMethods` itself
never throws — a missing host API is represented as a negative index, not an exception.

**Dependencies**: `PortAudioSharp2`'s native PortAudio library (loaded under the same
`"portaudio"` library name).

**Callers**: `PortAudioApi` (converts native/device structures into `PortAudioDeviceInfo` and
`PortAudioHostApiInfo`, and calls `PortAudioNativeMethods` to resolve host-API metadata) and
`PortAudioEnvironment` (uses `PortAudioHostApiType` in its OS-to-preferred-host-API mapping
policy).
