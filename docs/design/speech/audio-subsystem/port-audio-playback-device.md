### PortAudioPlaybackDevice

**Purpose**: Represent one real playback device resolved through PortAudio.

**Data Model**: Holds a `PortAudioEnvironment`, an `AudioDeviceSelection`, a diagnostics sink,
an optional preferred `AudioFormat`, and either resolved device metadata or `null` when no device
could be resolved. `ChannelCount` and `SampleRate`, added in Sub-phase 4b mirroring
`PortAudioCaptureDevice`'s identical Phase 3 addition, project the values requested when the
playback stream is opened: either the resolved device's own default/full-capacity format, or the
caller-preferred format with channel count clamped down to device capability. They report `0`
when nothing was resolved. A `ConcurrentQueue<float>` buffers samples written by callers until
the PortAudio callback requests them, alongside a `long` `_pendingSampleCount` field updated
with `Interlocked` (because `Write` runs on caller threads while the PortAudio callback runs on
its own real-time thread) tracking how many enqueued samples the callback has not yet dequeued.

**Key Methods**:

- **PortAudioPlaybackDevice(...)**: Resolves either the named device or the preferred host API's
  default output device, applies any preferred sample-rate hint directly, and clamps any
  preferred channel count down to the device's maximum output-channel capability. Construction
  never throws.
- **Start()**: Opens a playback-only PortAudio stream through `IPortAudioApi` and starts it.
- **Write(IReadOnlyList&lt;float&gt;)**: Enqueues interleaved samples for the PortAudio callback to
  drain later and increments `_pendingSampleCount` by however many samples were enqueued.
- **Stop()**: Stops and disposes the active playback stream and clears any queued stale audio,
  resetting `_pendingSampleCount` to zero.
- **PendingSampleCount**: Reports `Interlocked.Read(ref _pendingSampleCount)`, or `0` when no
  device was resolved. `ProvideSamples` (the PortAudio callback) decrements it by exactly the
  number of samples it actually dequeued - never by a zero-fill shortfall - each time it runs, so
  the value genuinely reflects hardware-consumption progress rather than merely queue occupancy
  at enqueue time. Added to fix a bug where `SherpaOnnxSpeechSynthesizer.PlayStreamAsync` stopped
  the device (discarding whatever was still queued) as soon as every segment was enqueued, rather
  than once the hardware had actually rendered it, cutting audio off almost instantly.

**Error Handling**: When no device could be resolved, `IsAvailable` is `false` and operational
members throw `AudioDeviceUnavailableException`. Native stream-open or stream-stop failures are
wrapped in `AudioDeviceUnavailableException`.

**Dependencies**: `PortAudioEnvironment`, `IPortAudioApi`, `IPortAudioStream`,
`AudioDeviceSelection`, `AudioFormat`, `ConcurrentQueue<float>`, and `ISpeechDiagnostics`.

**Callers**: `AudioDeviceFactory.CreatePlaybackDevice()`.
