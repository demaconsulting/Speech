### PortAudioCaptureDevice

**Purpose**: Represent one real capture device resolved through PortAudio.

**Data Model**: Holds a `PortAudioEnvironment`, an `AudioDeviceSelection`, a diagnostics sink,
an optional preferred `AudioFormat`, and either resolved device metadata or `null` when no
device could be resolved. `ChannelCount` and `SampleRate` project the values requested when the
capture stream is opened: either the resolved device's own default/full-capacity format, or the
caller-preferred format with channel count clamped down to device capability and sample rate
negotiated against the device/host API's actual capability. They report `0` when nothing was
resolved. The resolved device metadata also captures the environment's `Generation` at the
moment resolution succeeded, so `Start()` can detect a later `PortAudioEnvironment.Refresh()`.
The active stream reference is synchronized so only one capture stream can run at a time.

**Key Methods**:

- **PortAudioCaptureDevice(...)**: Resolves either the named device or the preferred host API's
  default input device, negotiates any preferred sample-rate hint against the device/host API's
  actual capability via `IPortAudioApi.IsCaptureFormatSupported`, honoring it only when confirmed
  openable and otherwise falling back to the device's own default sample rate with an Info-level
  diagnostic, and clamps any preferred channel count down to the device's maximum input-channel
  capability. Captures the environment's current `Generation` alongside the resolved device
  metadata. Construction never throws.
- **Start()**: First compares the resolved device's captured `Generation` against the
  environment's current `Generation`, throwing `AudioDeviceUnavailableException` when they no
  longer match (a refresh completed after this instance was resolved) rather than opening a
  stream against possibly-stale device-table data. Otherwise opens a capture-only PortAudio
  stream through `IPortAudioApi`, starts it, and forwards managed sample blocks through
  `FrameCaptured`. Registers this instance with `PortAudioEnvironment.RegisterActiveStream(...)`
  before opening the native stream, and unregisters it if the open fails, so a concurrent
  `AudioDeviceFactory.RefreshDevices()` knows a stream is active. When the native stream fails to
  open or start, a guarded inner try/catch disposes the partially-opened stream (reporting, but
  never propagating, any exception the dispose itself raises) inside a `finally` that clears the
  stream reference and unregisters the active-stream entry, so a throwing `Dispose()` can never
  leave this device stuck registered as active; the original start failure is what is ultimately
  reported and thrown.
- **Stop()**: Stops and disposes the active capture stream when one exists, then unregisters this
  instance via `PortAudioEnvironment.UnregisterActiveStream(...)`.

**Error Handling**: When no device could be resolved, `IsAvailable` is `false` and operational
members throw `AudioDeviceUnavailableException`. Native stream-open or stream-stop failures are
wrapped in `AudioDeviceUnavailableException` with the original exception as `InnerException`,
with the guarded-dispose/finally cleanup described under `Start()` above ensuring the
active-stream registration is always released even if the cleanup-time `Dispose()` call itself
throws. A stale `Generation` at `Start()` time is likewise reported as
`AudioDeviceUnavailableException`, directing the caller to create a fresh instance via
`AudioDeviceFactory` instead. A `RefreshDevices()` call made while this device's stream is active
is refused with `AudioDeviceInUseException` by `PortAudioEnvironment`, indirectly via
`AudioDeviceFactory`.

**Dependencies**: `PortAudioEnvironment`, `IPortAudioApi`, `IPortAudioStream`,
`AudioDeviceSelection`, `AudioFormat`, `AudioCaptureFrameEventArgs`, and `ISpeechDiagnostics`.

**Callers**: `AudioDeviceFactory.CreateCaptureDevice()`.
