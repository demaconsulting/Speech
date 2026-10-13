### SynthesisSession

**Purpose**: Chunk, render, and synthesize text into speech through a synthesis backend, bound to
exactly one playback device for the session's entire life, without ever performing synthesis or
inference work on the caller's UI thread, and without synthesizing ahead of playback across an
unbounded queue.

**Data Model**: Holds the owned `ISynthesisBackend`, the `IAudioPlaybackDevice` it plays through,
the `ISynthesisModel` supplying `NormalizeText`/`CapabilityProfile`/`ResolveSpeakerId`, an
optional `parameterValues` bag (the session's selected voice/tunable-parameter values, forwarded
unchanged from `SpeechSynthesizerEngine`), a diagnostics sink, a release callback for
the engine's exclusivity lease, and a `_syncRoot` lock guarding its `SynthesisSessionState`, its
current operation's `CancellationTokenSource`, its fault (if any), and disposal/lease-released
flags. `IsAvailable` is `true` unless disposed or `Faulted`.

The session never synthesizes ahead of playback: each segment is synthesized, then
(for `SpeakAsync`) immediately written to the playback device, before the next segment's
synthesis begins. This keeps the per-call state machine simple - exactly one backend call in
flight at a time - and playback of an early segment begins after only that segment has been
synthesized, without waiting for the whole plan. Synthesis of one segment never overlaps
synthesis of another.

Additional private state tracks native-call completion: `_operationTask` is the in-flight
operation's abandon-aware task; `_pendingNativeCompletion` is the raw completion of the most
recently started native call (a backend `Generate` or the playback device's `Start`), which
completes only when that call genuinely returns even if the dedicated worker abandoned it;
`_disposeTask` is the single-flight disposal operation shared by every `DisposeAsync` caller and
doubles as the disposed flag. The session does not own or dispose the backend; the creating
`SpeechSynthesizerEngine` does.

`DrainPollInterval` (15ms) and `DrainTailMargin` (40ms) are fixed constants governing
`WaitForPlaybackDrainAsync`'s post-loop wait in `GenerateAndOptionallyPlayAsync` (see below):
short enough that a `SpeakAsync` call returns promptly once the hardware is genuinely done, long
enough to avoid busy-spinning the thread pool re-reading a value only a real-time audio callback
thread can change.

**Key Methods**:

- **SpeakAsync(text, cancellationToken)** / **SynthesizeAsync(text, cancellationToken)**: Both
  validate `text` eagerly, then delegate to the shared `RunOperationAsync(text,
  playAfterSynthesis, cancellationToken)`. `StartOperation` validates the overlap rule and current
  state under the lock, sets `Starting` and registers the operation task there, then (after the lock
  is released, so `StateChanged` handlers never run under it) raises `Starting` and runs the operation,
  which transitions `Starting` → `Running`, runs
  `GenerateAndOptionallyPlayAsync`, then transitions `Stopping` → `Stopped` on success or
  cancellation, or to `Faulted` (reporting the fault via diagnostics) on any other exception. A
  second call while an operation is already `Starting`/`Running`/`Stopping` throws
  `InvalidOperationException`; a call while `Faulted` throws `SynthesisSessionFaultedException`
  wrapping the original fault; a call after disposal throws `ObjectDisposedException`. A
  cancellation that the native call honored promptly unwinds to `Stopped`. A cancellation
  that the native call did not honor within the dedicated worker's abandon timeout also returns
  to the caller promptly (as an `OperationCanceledException`), but moves the session to
  `Faulted` with a `TimeoutException` fault, because the abandoned call is still running against
  the shared backend; `_pendingNativeCompletion` is left set so `StopAsync`/`DisposeAsync` still
  wait for it. An `OperationCanceledException` thrown by the backend when no cancellation was
  requested is treated as an ordinary fault.
- **StopAsync(cancellationToken)**: Cancels the current operation's `CancellationTokenSource`
  under the lock, if one exists, awaits that tracked operation task, and then re-reads and awaits
  `_pendingNativeCompletion` (even when the operation task is already cleared by an abandon
  fault), so the returned task completes only once every native call the operation started has
  genuinely returned; a safe no-op when idle. The `cancellationToken` bounds only the caller's own
  wait: a canceled token makes the returned task throw, but the underlying cancel-and-await work
  still runs to completion for other callers and for `DisposeAsync`. A canceled operation whose
  native call stopped promptly unwinds through `Stopping` → `Stopped`; one whose native call
  was abandoned leaves the session `Faulted` (see Error Handling).
- **GenerateAndOptionallyPlayAsync(text, playAfterSynthesis, cancellationToken)**: Normalizes the
  text via `_model.NormalizeText`, parses it via `AudioTagParser.Parse`, renders it via
  `_model.CapabilityProfile.Render(spans, _model)` into a `SpeechPlan`, then synthesizes each
  `SpeechSegment` in turn via `GenerateSegmentAsync`. When `playAfterSynthesis` is `false`
  (`SynthesizeAsync`), simply collects every segment's `SynthesizedSpeech` into the returned list.
  When `true` (`SpeakAsync`), starts the playback device through `DedicatedWorker.Start` (so a
  stuck device-open call is bounded by the same cancel-then-abandon policy, and its raw
  completion is published as `_pendingNativeCompletion`), builds one `PlaybackAudioResampler` for
  the call, writes each segment's pre-silence/resampled audio/post-silence as it is produced, and
  once every segment has been written, awaits `WaitForPlaybackDrainAsync` before a `finally` block
  stops the device - reporting (but not rethrowing) a failure to stop, so the device is never left
  running regardless of whether the loop, the drain wait, or neither completed, faulted, or was
  canceled. When the device-start call was abandoned and is still running, the device stop is
  deferred to a background continuation that awaits the start call's genuine completion first
  (never running `Start` and `Stop` concurrently); that continuation becomes the pending native
  completion.
- **GenerateSegmentAsync(segment, cancellationToken)**: An empty-text segment (a rendered pause)
  skips the backend entirely and returns a pure-silence `SynthesizedSpeech` built directly from
  the segment's declared silence durations. Otherwise resolves speed/volume overrides against the
  model's declared numeric parameters via `SpeechParameterConventions`, resolves the speaker id
  for this session's selected voice by calling `_model.ResolveSpeakerId(_parameterValues)` once
  per segment (cheap and pure - re-evaluated fresh each call rather than cached for the whole
  session; deliberately independent of the per-segment `ParameterOverrides` speed/volume
  mechanism, which is Natural Language Audio Tag-scoped and transient, not a fit for a
  session-level voice selection), and calls `_backend.Generate(text, speedRatio, speakerId)`
  through `DedicatedWorker.Run` - so a non-cooperative native call is bounded by the
  cooperative-cancel-then-abandon policy rather than awaited indefinitely - applying any volume
  override as post-hoc amplitude scaling (backends have no native gain input),
  clamped to `[-1.0, 1.0]`. A speed or volume override whose resolved ratio is not a positive
  finite number is ignored.
- **WaitForPlaybackDrainAsync(cancellationToken)**: Polls `_device.PendingSampleCount` every
  `DrainPollInterval` (15ms) until it reaches zero - i.e. until the playback hardware has
  genuinely rendered every sample this operation wrote, not merely until every segment was handed
  off - then applies one further `DrainTailMargin` (40ms) wait before returning, to absorb any
  residual internal host buffering (e.g. PortAudio's own ring buffer) that has already left the
  managed queue but has not yet actually reached the speakers. Honors `cancellationToken`: a
  cancellation during either wait ends it immediately via `Task.Delay`'s own cancellation.
- **DisposeAsync()**: Starts one single-flight teardown (`_disposeTask`) shared by every caller:
  cancels any in-flight operation, awaits the operation task and then `_pendingNativeCompletion`
  (so an abandoned native call has genuinely returned), transitions `Disposing` → `Disposed`,
  and only then releases the owning engine's exclusivity lease exactly once (via the
  constructor-supplied release callback). Idempotent; concurrent callers all complete after the
  same teardown.

**Error Handling**: A fault raised synthesizing or playing a segment transitions the session to
`Faulted` and is reported through `ISpeechDiagnostics` before being rethrown to the caller of the
operation that faulted; every subsequent `SpeakAsync`/`SynthesizeAsync` call then throws
`SynthesisSessionFaultedException` wrapping that original fault, until the session is disposed. A
playback-device write failure still stops the device via the `finally` block before propagating.
A cancellation while `WaitForPlaybackDrainAsync` is polling ends the wait immediately via
`Task.Delay`'s own cancellation, while `finally` still stops the device; when no native call was
abandoned, the operation unwinds to `Stopped`, not `Faulted`, since cancellation is then an
ordinary outcome. An unavailable
playback device throws promptly rather than hanging. Calling an operational member after
disposal throws `ObjectDisposedException`; calling one while another is already in flight on the
same session throws `InvalidOperationException`.

**Dependencies**: `ISynthesisBackend` (whose `Generate` returns the plain-data `BackendAudio`
record: mono samples plus sample rate), `ISynthesisModel` (`NormalizeText`, `CapabilityProfile`,
`ResolveSpeakerId`), `SpeechPlan`, `SpeechSegment`, `SpeechParameterConventions`,
`PlaybackAudioResampler`, `SynthesizedSpeech`, `SynthesisSessionState`,
`SessionStateChangedEventArgs`, `SynthesisSessionFaultedException`, `DedicatedWorker` and
`DedicatedWorkerRun<T>`, `SpeechSynthesizerUnavailableException`, `AudioTagParser` from this
subsystem's other units, `IAudioPlaybackDevice` from the AudioSubsystem, and `ISpeechDiagnostics`
from the Diagnostics subsystem.

**Callers**: `SpeechSynthesizerEngine.CreateSessionAsync(device, cancellationToken)`.
