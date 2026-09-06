### PortAudioCaptureDevice

**Purpose**: Represent one real capture device resolved through PortAudio.

**Data Model**: Holds a `PortAudioEnvironment`, an `AudioDeviceSelection`, a diagnostics sink,
an optional preferred `AudioFormat`, and either resolved device metadata or `null` when no
device could be resolved. `ChannelCount` and `SampleRate` project the values requested when the
capture stream is opened: either the resolved device's own default/full-capacity format, or the
caller-preferred format with channel count clamped down to device capability. They report `0`
when nothing was resolved. The active stream reference is synchronized so only one capture stream
can run at a time.

**Key Methods**:

- **PortAudioCaptureDevice(...)**: Resolves either the named device or the preferred host API's
  default input device, applies any preferred sample-rate hint directly, and clamps any
  preferred channel count down to the device's maximum input-channel capability. Construction
  never throws.
- **Start()**: Opens a capture-only PortAudio stream through `IPortAudioApi`, starts it, and
  forwards managed sample blocks through `FrameCaptured`.
- **Stop()**: Stops and disposes the active capture stream when one exists.

**Error Handling**: When no device could be resolved, `IsAvailable` is `false` and operational
members throw `AudioDeviceUnavailableException`. Native stream-open or stream-stop failures are
wrapped in `AudioDeviceUnavailableException` with the original exception as `InnerException`.

**Dependencies**: `PortAudioEnvironment`, `IPortAudioApi`, `IPortAudioStream`,
`AudioDeviceSelection`, `AudioFormat`, `AudioCaptureFrameEventArgs`, and `ISpeechDiagnostics`.

**Callers**: `AudioDeviceFactory.CreateCaptureDevice()`.
