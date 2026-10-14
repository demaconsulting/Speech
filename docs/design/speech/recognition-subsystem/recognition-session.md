### RecognitionSession

**Purpose**: Stream a capture device's audio through format conversion into a recognition backend
and deliver the resulting provisional and final results, without ever performing recognition work
on the audio callback thread.

**Data Model**: Holds the leased `IRecognitionBackend` (owned by, and disposed by, the owning
`SpeechRecognizerEngine` - the session never disposes it, so the backend stays "hot"
across sessions), the `IAudioCaptureDevice` it streams from, the owning `IRecognitionModel` (used
only to normalize result text), an `AudioFrameResampler` configured at construction from the
device's reported format and the model's declared `AudioFormat.SampleRate`, a `DedicatedWorker`
that runs the pump loop, a `RecognitionResultBuffer`, a callback to release the engine's lease on
disposal, and a diagnostics sink. While running it also holds a bounded `Channel<float[]>` of
pending capture blocks and the pump `Task` draining it; both are created on start and cleared on
stop, guarded by a lock together with `State`. `IsAvailable` is always `true`, because this type
is only ever created after the backend loaded and the device reported itself available - every
unavailable case is represented by `UnavailableRecognitionSession` instead.

The pending-block queue holds `PendingFrameCapacity` (64) blocks and drops the oldest when full
(`BoundedChannelFullMode.DropOldest`). Recognition that has fallen behind live audio cannot be
caught up by queueing more of it, so bounding the backlog keeps both memory and latency flat
instead of letting them grow without limit. A finite `WavFileAudioCaptureDevice` source is the
exception: its channel uses `BoundedChannelFullMode.Wait` and the file device's `Start()` thread
blocks until the pump catches up, because a file is delivered faster than real time and must
be transcribed in totality. Likewise, teardown never cancels the pump of a file source, so the
abandon deadline cannot truncate a slow backend's final drain.

**Key Methods**:

- **StartAsync(CancellationToken)**: Transitions `Created -> Starting -> Running`. Creates the
  queue, subscribes to `FrameCaptured`, then starts the capture device. Subscribing before
  starting guarantees no captured block can be raised before there is a handler to enqueue it.
  Throws `InvalidOperationException` when called more than once (sessions are single-use).
  Precondition: not already started. Postcondition: capture is running and results will be
  buffered. If the device fails to start, transitions to `Faulted`, faults the result buffer, and
  throws `SpeechRecognizerUnavailableException` with the device's exception as `InnerException`.
- **StopAsync(CancellationToken)**: Transitions `Running -> Stopping -> Stopped` (idempotent once
  `Stopped`/`Disposing`/`Disposed`/`Faulted`). Unsubscribes, completes the queue, cancels the pump
  task's token (purely the `DedicatedWorker` abandon-timeout deadline - the pump loop itself never
  observes this token while draining), awaits the pump task, completes the result buffer, resets
  the backend, then stops the capture device. Draining the pump does two things in order: first
  it processes every already-queued block through the backend as normal, then - as its last
  action, still on the same pump thread - it drains any results the per-block cap left behind and
  flushes the backend (`TryFlush`), finalizing and
  delivering any trailing audio the backend had accepted but not yet decoded, for example the tail
  of an utterance released with no trailing silence, which a streaming backend cannot normally
  decode without more audio it will now never receive. Because of that flush, every result derived
  from audio captured before the call - including that trailing fragment - has been delivered (or
  accounted for per the backpressure policy) when `StopAsync` returns. The backend is then reset,
  discarding whatever the flush could not recover and any stale hypothesis left over from the
  session just stopped, so the next session the owning engine creates on the same "hot" backend
  always begins decoding from a clean start-of-utterance state, equivalent to a freshly constructed
  stream without the cost of reloading the model. The flush, the reset, and an abandoned pump
  thread are all best-effort: each is reported through `ISpeechDiagnostics` rather than thrown, so
  `StopAsync` still completes, but in those cases the trailing fragment may go undelivered and/or
  the backend's state cannot be guaranteed clean.
- **GetResultsAsync(CancellationToken)**: Single-consumer (`Interlocked.CompareExchange` guard,
  throws `InvalidOperationException` on re-entry); awaits `RecognitionResultBuffer.ReadAllAsync`
  and yields each result.
- **DisposeAsync()**: Runs the full `Stopping -> Stopped` teardown via `StopAsync` first (so a
  session disposed directly from `Running` still visits that teardown rather than skipping it),
  then transitions `Disposing -> Disposed` and invokes the release-lease callback so the owning
  engine's next `CreateSessionAsync` can succeed. Idempotent.
- **OnFrameCaptured(...)**: Runs on the audio callback thread. Checks `_device.IsAvailable`
  (faulting the session via `FaultSession` if it has gone unavailable mid-session), copies the
  block, and enqueues it; nothing else.
- **PumpLoop(...)**: Runs on the `DedicatedWorker` pump thread. Converts each block through
  `AudioFrameResampler`, feeds it to the backend, then polls the backend for results and writes
  them into the `RecognitionResultBuffer`. Each result's text is passed through the owning model's
  `IRecognitionModel.NormalizeText(text, isFinal)` before buffering, so a model whose raw output
  needs casing/contraction/punctuation restoration (for example `SpeechSherpa`'s
  `UppercaseTranscriptRestorer`) surfaces
  readable text to every consumer instead of the backend's raw output; bounded at
  `MaxResultsPerFrame` (32) results per block so a faulty backend cannot livelock the pump; any
  results beyond that cap stay in the backend and are drained on the next block or at stop. As its
  last action once the queue is drained and completed, drains the backend's remaining results
  (`TryDecode`, bounded at `MaxResultsAtFinalDrain` (1024)), then flushes the backend (`TryFlush`)
  and buffers the trailing final result the same way, before the queue-completion path returns.
- **FaultSession(Exception)**: Transitions to `Faulted` from `Starting`/`Running`/`Stopping`,
  faults the result buffer with the cause (surfacing it to `GetResultsAsync` as
  `RecognitionSessionFaultedException`), and cancels the pump token.

**Error Handling**: Every stage is contained: no failure escapes into the audio callback or the
caller of `StartAsync`/`StopAsync`/`DisposeAsync`, because an exception escaping the frame
handler would propagate into the native PortAudio callback and tear down the audio stream. A
failure enqueueing a block, or delivering a result, is caught and reported through
`ISpeechDiagnostics`, never rethrown, and the session continues. A failure of the recognition
backend itself (`AcceptSamples`, `TryDecode`, or a block conversion that precedes them) is
terminal by design: it is reported, the session transitions to `Faulted`, the result buffer is
faulted (so readers receive `RecognitionSessionFaultedException` after any already-buffered
results) and the pump stops, because a backend that has just thrown is in an undefined state and
repeating the failure for every later block would only hide it. Teardown (reset, device stop, lease
release) still runs on `StopAsync`/`DisposeAsync`, so the failure is surfaced as a faulted session
rather than a hang, and the host recovers by creating a new session. A capture device that
reported itself available but fails on `StartAsync`
or goes unavailable mid-session is surfaced as `SpeechRecognizerUnavailableException` (with the
device's exception as `InnerException` when applicable) and faults the session. A device
reporting a non-positive rate or channel count falls back to a pass-through conversion with a
warning rather than throwing at composition. Starting more than once, or consuming
`GetResultsAsync` from more than one concurrent caller, throws `InvalidOperationException`.

**Dependencies**: `IRecognitionBackend`, `AudioFrameResampler`, `RecognitionResultBuffer`,
`DedicatedWorker`, `RecognitionSessionState`, `SessionStateChangedEventArgs`,
`SpeechRecognitionResult`, `SpeechRecognitionEvent`, `SpeechRecognizerUnavailableException`,
`RecognitionSessionFaultedException`, `IRecognitionModel` (from the ModelManagementSubsystem, for
`NormalizeText`), `IAudioCaptureDevice` and `AudioCaptureFrameEventArgs` from the AudioSubsystem,
and `ISpeechDiagnostics` from the Diagnostics subsystem.

**Callers**: `SpeechRecognizerEngine.CreateSessionAsync(...)`.
