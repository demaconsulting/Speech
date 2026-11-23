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
disposal, and a diagnostics sink. While running it also holds a `Channel<float[]>` of
pending capture blocks and the pump `Task` draining it; both are created on start and cleared on
stop, guarded by a lock together with `State`. `IsAvailable` is always `true`, because this type
is only ever created after the backend loaded and the device reported itself available - every
unavailable case is represented by `UnavailableRecognitionSession` instead.

The pending-block queue for a live device is lossless: an unbounded channel written with
`TryWrite`, so the audio callback never blocks and a recognizer that falls behind builds a backlog
instead of losing audio. (Dropping the oldest blocks, as an earlier bounded queue did, silently cut
words out of the middle and end of utterances when the decoder was briefly slow - for example at a
push-to-talk stop.) Memory stays bounded by a safety cap, `DefaultMaxPendingLiveAudio` (one minute
of the device's own interleaved audio, about 23 MB at 48 kHz stereo), tracked with an interlocked
count of queued samples. A block that would grow the backlog past the cap is discarded, counted,
and reported: the first such block of a session produces a warning diagnostic and teardown reports
the session's total, so nothing is ever dropped silently. A block is always accepted when nothing
else is queued. A recognizer a full minute behind real time is no longer producing useful live
results, so the cap only guards memory against a persistently too-slow recognizer. A finite
`WavFileAudioCaptureDevice` source instead uses a bounded channel (`FiniteSourcePendingFrameCapacity`,
64 blocks) with `BoundedChannelFullMode.Wait`: the file device's `Start()` thread blocks until the
pump catches up, because a file is delivered faster than real time and must be transcribed in
totality without buffering the whole file.

The session also tracks pump progress (an interlocked counter incremented after every block, every
final-drain decode, and the flush) and a discard flag set only by a canceled `StopAsync`.

**Key Methods**:

- **StartAsync(CancellationToken)**: Transitions `Created -> Starting -> Running`. Creates the
  queue, subscribes to `FrameCaptured`, then starts the capture device. Subscribing before
  starting guarantees no captured block can be raised before there is a handler to enqueue it.
  Throws `InvalidOperationException` when called more than once (sessions are single-use).
  Precondition: not already started. Postcondition: capture is running and results will be
  buffered. If the device fails to start, transitions to `Faulted`, faults the result buffer, and
  throws `SpeechRecognizerUnavailableException` with the device's exception as `InnerException`.
- **StopAsync(CancellationToken)**: Transitions `Running -> Stopping -> Stopped` (idempotent once
  `Stopped`/`Disposing`/`Disposed`/`Faulted`). The call itself only moves the state to `Stopping`
  and starts the shared background teardown, whose graceful order is: wait for an in-progress
  device start to return (a file source, or a pre-roll device replaying its buffer, delivers
  session audio from inside `Start()`, so a stop that races a file's start still recognizes the
  whole file); stop accepting captured audio (`StopAcceptingFrames`: unsubscribe from
  `FrameCaptured`, close the acceptance gate, wait for any callback already running to queue its
  block, then complete the queue - see `OnFrameCaptured`); drain every queued block through the
  backend, drain any results the per-block cap left behind,
  flush the backend (`TryFlush`) and buffer its final result, complete the result buffer, reset the
  backend, stop the capture device, and only then complete the returned task. Unsubscribing is
  deliberately not done under the state lock: a capture callback may itself be waiting for that
  lock (`FaultSession`), and a decorating device (`PreRollAudioCaptureDevice`) makes unsubscribing
  wait for a block it is already dispatching. Audio captured between the call and the unsubscribe
  is simply recognized as part of the session. The flush finalizes
  trailing audio the backend had accepted but not yet decoded, for example the tail of an utterance
  released with no trailing silence, which a streaming backend cannot normally decode without more
  audio it will now never receive. Because of that ordering, every result derived from audio
  captured before the call - including that trailing fragment - is buffered for `GetResultsAsync`
  when `StopAsync` returns. The backend is reset only after the flush, discarding whatever the flush
  could not recover, so the next session the owning engine creates on the same "hot" backend always
  begins decoding a clean utterance, equivalent to a freshly constructed stream without the cost of
  reloading the model. This is what makes a push-to-talk "resume" (a new session after a stop) start
  clean.

  A slow drain is never cut short. For a live source, teardown waits on the pump for as long as the
  pump keeps making progress, checking the progress counter once per `DedicatedWorker` abandon
  interval; only an interval with no progress at all (a backend call that never returns) cancels
  the pump token, which starts the worker's abandon deadline (the pump loop itself never observes
  this token). A file source's pump is never canceled. A fixed deadline measured from the stop -
  the earlier design - let a caller observe the stop as complete while the pump was still decoding
  queued audio, losing the final result.

  Cancellation is the only way a stop discards audio. A token already canceled when `StopAsync` is
  called sets the discard flag under the state lock before teardown starts, so not one more
  queued block is decoded; a token canceled while the caller is waiting sets it as the caller's
  wait ends. Either way the pump drops every remaining block without decoding it, skips the final
  drain and flush, and the pump token is canceled so a stuck backend call is abandoned promptly -
  for a file source as well as a live one, since the cancellation itself starts the dedicated
  worker's abandon deadline. A file source's queue is also completed at once, releasing a reader
  blocked on backpressure. A discarding stop waits for an in-progress device start for at most one
  abandon interval: if the start still has not returned, a warning is reported, teardown
  converges without it, and the device is stopped later, once its start finally returns (never
  concurrently with it; nothing waits for that, since a stuck start does not touch the shared
  backend);
  the caller's task completes as canceled, while the shared teardown (reset, device stop, state
  convergence, lease release on dispose) still completes for every caller. The reset, an abandoned
  pump thread, and a failed flush are best-effort: each is reported through `ISpeechDiagnostics`
  rather than thrown, so `StopAsync` still completes.
- **GetResultsAsync(CancellationToken)**: Single-consumer (`Interlocked.CompareExchange` guard,
  throws `InvalidOperationException` on re-entry); awaits `RecognitionResultBuffer.ReadAllAsync`
  and yields each result.
- **DisposeAsync()**: Runs the full `Stopping -> Stopped` teardown via `StopAsync` first (so a
  session disposed directly from `Running` still visits that teardown rather than skipping it),
  then transitions `Disposing -> Disposed` and invokes the release-lease callback so the owning
  engine's next `CreateSessionAsync` can succeed. Idempotent.
- **OnFrameCaptured(...)**: Runs on the audio callback thread. Checks `_device.IsAvailable`
  (faulting the session via `FaultSession` if it has gone unavailable mid-session), copies the
  block, and enqueues it - for a live source through the lossless, capped queue described above,
  for a file source with backpressure; nothing else. Because unsubscribing does not wait for a
  callback already running, the callback and teardown share a barrier: the callback counts itself
  in an in-flight counter and then checks an "accepting" flag; `StopAcceptingFrames` unsubscribes,
  clears the flag, and completes the queue only after the in-flight count reaches zero. Both steps
  are full fences, so a block whose callback was already running is still queued and recognized,
  and a block that arrives after the gate closed is dropped. The wait is bounded by the abandon
  interval; a callback still running after it (a host handler blocked in the diagnostics sink, for
  example) is reported as a warning, because a block it may still be delivering is then not
  recognized.
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
  When the discard flag is set it still reads the queue to its end (releasing a blocked file
  reader) but drops each block and skips the final drain and flush.
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

**Callers**: `SpeechRecognizerEngine.CreateSessionAsync(...)`. A push-to-talk host typically binds
successive sessions to one `PreRollAudioCaptureDevice` (see *PreRollAudioCaptureDevice*), which
delivers the audio captured just before each start as that session's first block.
