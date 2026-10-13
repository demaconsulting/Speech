### PortAudioPlaybackDevice

**Purpose**: Represent one real playback device resolved through PortAudio.

**Data Model**: Holds a `PortAudioEnvironment`, an `AudioDeviceSelection`, a diagnostics sink,
an optional preferred `AudioFormat`, and either resolved device metadata or `null` when no device
could be resolved.

`ChannelCount` and `SampleRate`, mirroring `PortAudioCaptureDevice`'s identical members, project
the values requested when the playback stream is opened: either the resolved device's own
default/full-capacity format, or the caller-preferred format with channel count clamped down to
device capability and sample rate negotiated against the device/host API's actual capability. They
report `0` when nothing was resolved. The resolved device metadata also captures the
environment's `Generation` immediately before the device-table query begins (not after
resolution succeeds), so `Start()` detects a `PortAudioEnvironment.Refresh()` that occurred at
any point during or after resolution. The stream is opened with a callback block size of
`FramesPerBuffer` (`0`, letting the host API choose).

A `ConcurrentQueue<float[]>` buffers whole sample blocks written by callers - one enqueue per
`Write` call rather than one per sample - until the PortAudio callback requests them. A
`_headBlock`/`_headOffset` cursor lets the callback drain queued blocks in bulk (via
`Array.Copy`) instead of dequeuing one sample at a time. A `long` `_pendingSampleCount` field,
updated with `Interlocked` because `Write` runs on caller threads while the PortAudio callback
runs on its own real-time thread, tracks how many enqueued samples the callback has not yet
dequeued. The callback fills one reused output buffer, so the array it returns is valid only until
its next invocation.

The `_headBlock`/`_headOffset` cursor requires no additional synchronization: it is touched from
`ProvideSamples` (the single real-time PortAudio callback thread, only while a stream is running)
and reset by `ClearQueuedSamples` (a caller thread) only after the native stream-stop call has
returned successfully, which blocks until the callback thread finished. `Start`/`Stop` are
serialized under the same lock. When the native stop fails the callback may still be running, so
the stop-failure path drains only the thread-safe queue and leaves the cursor alone; the next
`Start()` then discards that stale head block (before the replacement stream is opened) so a
restart never replays it, and removes its unconsumed remainder from `_pendingSampleCount` so the
counter never goes negative. Until that restart, `PendingSampleCount` after a failed stop may
still include the unconsumed remainder of the head block. `ClearQueuedSamples` subtracts each
dequeued block's length from `_pendingSampleCount` instead of resetting the counter, so a
concurrent `Write` (which counts a block before enqueueing it) can never leave the counter out of
step with the queue.
The queue is unbounded: audio written faster than the hardware renders it, or written before
`Start()`, is retained until drained or cleared by `Stop()`; audio written after `Stop()` is
likewise retained and played by the next `Start()`.

**Key Methods**:

- **PortAudioPlaybackDevice(...)**: Resolves either the named device or the preferred host API's
  default output device, negotiates any preferred sample-rate hint against the device/host API's
  actual capability via `IPortAudioApi.IsPlaybackFormatSupported`, honoring it only when
  confirmed openable and otherwise falling back to the device's own default sample rate with an
  Info-level diagnostic, and clamps any preferred channel count down to the device's maximum
  output-channel capability. Captures the environment's `Generation` before querying the device
  table and stores it alongside the resolved device metadata. The only exception is `ArgumentNullException` for a null
  environment: a fault raised by the native device-table queries is contained, reported as an
  Error-level diagnostic, and leaves the device unresolved (unavailable).
- **Start()**: Atomically validates the resolved device's captured `Generation` against the
  environment's current `Generation` and registers this instance as holding an active stream via
  `PortAudioEnvironment.TryRegisterActiveStream(...)`, all under one acquisition of the
  environment's lock, so a concurrent `AudioDeviceFactory.RefreshDevices()` can never land
  between the generation check and the registration. When the generation no longer matches (a
  refresh completed after this instance was resolved), `TryRegisterActiveStream` leaves this
  instance unregistered and returns `false`, and `Start()` throws
  `AudioDeviceUnavailableException` rather than opening a stream against possibly-stale
  device-table data. Otherwise it opens a playback-only PortAudio stream through `IPortAudioApi`
  and starts it, unregistering the already-successful registration if the native open fails. When
  the native stream fails to open or start, a guarded inner try/catch disposes the
  partially-opened stream (reporting, but never propagating, any exception the dispose itself
  raises) inside a `finally` that clears the stream reference and unregisters the active-stream
  entry, so a throwing `Dispose()` can never leave this device stuck registered as active; the
  original start failure is what is ultimately reported and thrown.
  Calling `Start()` on an already started device does nothing. Before opening the stream, `Start()`
  discards any stale head block left by an earlier failed native stop.
- **Write(IReadOnlyList&lt;float&gt;)**: Requires a non-null list (`ArgumentNullException`) and a
  resolved device (`AudioDeviceUnavailableException` otherwise); an empty list is ignored. Copies
  the interleaved samples into a new block, so the caller may reuse its buffer immediately, adds
  the block length to `_pendingSampleCount` and then enqueues the block. The count is added first
  so the callback can never subtract samples that were not yet counted.
- **Stop()**: Requires a resolved device (`AudioDeviceUnavailableException` otherwise). Stops and
  disposes the active playback stream and clears any queued stale audio, resetting
  `_pendingSampleCount` to zero, then unregisters this instance via
  `PortAudioEnvironment.UnregisterActiveStream(...)`. A device that was never started only clears
  the queue. When the native stop fails, the failure is reported and the stream is still
  disposed; the stop failure is then thrown wrapped, and a dispose fault is logged only. When the
  stop succeeds but the dispose fails, the dispose fault is thrown wrapped.
- **PendingSampleCount**: A `long` that reports `_pendingSampleCount` (never below zero), or `0`
  when no device was resolved, after `Stop()`, and before any write. `ProvideSamples` (the
  PortAudio callback) decrements it by exactly the number of samples it actually dequeued - never
  by a zero-fill shortfall - each time it runs, so the value genuinely reflects
  hardware-consumption progress rather than merely queue occupancy at enqueue time. A caller that
  must not cut playback off, such as `SynthesisSession`, polls it until it reaches zero before
  stopping the device.

**Error Handling**: When no device could be resolved, `IsAvailable` is `false` and operational
members throw `AudioDeviceUnavailableException`. Native stream-open or stream-stop failures are
wrapped in `AudioDeviceUnavailableException`, with the guarded-dispose/finally cleanup described
under `Start()` above ensuring the active-stream registration is always released even if the
cleanup-time `Dispose()` call itself throws. A stale `Generation` at `Start()` time is likewise
reported as `AudioDeviceUnavailableException`, directing the caller to create a fresh instance
via `AudioDeviceFactory` instead. A `RefreshDevices()` call made while this device's stream is
active is refused with `AudioDeviceInUseException` by `PortAudioEnvironment`, indirectly via
`AudioDeviceFactory`.

**Dependencies**: `PortAudioEnvironment`, `IPortAudioApi`, `IPortAudioStream`,
`AudioDeviceSelection`, `AudioFormat`, `IAudioPlaybackDevice`, `AudioDeviceUnavailableException`,
`ConcurrentQueue<float[]>`, and `ISpeechDiagnostics`.

**Callers**: `AudioDeviceFactory.CreatePlaybackDevice()`.
