### RecognitionSession

This chapter covers the streaming session together with its companion Layer 3 engine and the
other units it exists to coordinate - the recognition-backend seam, the result buffer, the
dedicated pump-thread worker, and the audio-format converter - because none of them can be
reviewed meaningfully in isolation: the session's whole job is to move audio from the converter
into the backend on the right thread and buffer the results back out.

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
instead of letting them grow without limit.

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
  action, still on the same pump thread - it flushes the backend (`TryFlush`), finalizing and
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
  `MaxResultsPerFrame` (32) results per block so a faulty backend cannot livelock the pump. As its
  last action once the queue is drained and completed, flushes the backend (`TryFlush`) and
  buffers the trailing final result the same way, before the queue-completion path returns.
- **FaultSession(Exception)**: Transitions to `Faulted` from `Starting`/`Running`/`Stopping`,
  faults the result buffer with the cause (surfacing it to `GetResultsAsync` as
  `RecognitionSessionFaultedException`), and cancels the pump token.

**Error Handling**: Every stage is contained. A failure enqueueing a block, converting it,
running inference on it, or delivering a result is caught and reported through
`ISpeechDiagnostics`, never rethrown - an exception escaping the frame handler would propagate
into the native PortAudio callback and tear down the audio stream, and a single bad block must
never end the session. A capture device that reported itself available but fails on `StartAsync`
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

#### SpeechRecognizerEngine

**Purpose**: Implement the Layer 3 `ISpeechRecognizerEngine` contract against a loaded
`IRecognitionBackend`, enforcing the subsystem's single-session-at-a-time exclusivity (see the
subsystem-level design doc's "Engine exclusivity and lease behavior").

**Data Model**: Holds the loaded `IRecognitionBackend`, the owning `IRecognitionModel`, a
`SemaphoreSlim(1, 1)` lease, and a diagnostics sink; constructs a new `DedicatedWorker` per
session it creates. `IsAvailable` is always `true`, because this type is only ever created after the
backend loaded successfully - every unavailable case is represented by
`UnavailableSpeechRecognizerEngine` instead.

**Key Methods**:

- **CreateSessionAsync(IAudioCaptureDevice, CancellationToken)**: When the supplied device
  reports `IsAvailable` as `false` (no microphone or no working audio backend - an ordinary
  machine state, exactly like a model not being installed), reports a Warning diagnostic and
  returns `UnavailableRecognitionSession.Instance` without attempting the backend lease. Otherwise
  attempts `_lease.Wait(0, ...)` -
  a zero-timeout, fail-fast acquire with no queueing. On success, constructs and returns a new
  `RecognitionSession` wrapping the shared backend, device, model, resampler
  configuration, diagnostics sink, pump worker, and a release-lease callback that calls
  `_lease.Release()`. On failure (the lease is already held), throws
  `RecognitionEngineBusyException` without blocking.
- **Dispose()**: Disposes the owned backend and the lease semaphore. Only safe to call once no
  session still holds the lease.

**Error Handling**: `RecognitionEngineBusyException` is the only condition this type represents as
an exception; every other call either succeeds or is routed through the created session's own
error handling.

**Dependencies**: `IRecognitionBackend`, `RecognitionSession`, `DedicatedWorker`,
`RecognitionEngineBusyException`; implements `ISpeechRecognizerEngine`.

**Callers**: `SpeechRecognizerFactory.LoadAsync(...)` constructs it; hosts call
`CreateSessionAsync` once per capture device.

#### RecognitionResultBuffer

**Purpose**: Isolate a possibly-slow `GetResultsAsync` consumer from the pump thread with a
bounded, two-tier buffer (see the subsystem-level design doc's "Backpressure policy").

**Data Model**: One overwritable slot for the latest unconsumed provisional result, plus a
byte-capped (`MaxFinalResultBytes`, 64 KiB, measured via each final result's `Text` length) FIFO
queue of final results, backed by a `Channel`-based signal so `ReadAllAsync` can await new data
without polling.

**Key Methods**:

- **AddResult(SpeechRecognitionEvent)**: A provisional result overwrites the single pending
  provisional slot (an unconsumed provisional is superseded by the next one, never queued). A
  final result is enqueued into the FIFO; if enqueueing it would exceed `MaxFinalResultBytes`, the
  oldest queued final is evicted first (reported through `ISpeechDiagnostics` at `Warning` level)
  and only as a last resort.
- **ReadAllAsync(CancellationToken)**: An `IAsyncEnumerable<SpeechRecognitionEvent>` yielding the
  latest provisional and every queued final in the order they became ready, until `Complete()` is
  called, or throwing `RecognitionSessionFaultedException` once `Fault(...)` has been called.
- **Complete()**: Signals that no further results will be written; lets a draining
  `ReadAllAsync` finish normally once its backlog is consumed.
- **Fault(Exception)**: Records the cause and signals every current and future `ReadAllAsync`
  consumer to throw `RecognitionSessionFaultedException` wrapping it.

**Error Handling**: Eviction of an unconsumed final result is the one data-loss case this type
can produce, and it is always reported through `ISpeechDiagnostics` at `Warning` level rather than
silently dropped.

**Dependencies**: `SpeechRecognitionEvent`, `RecognitionSessionFaultedException`,
`ISpeechDiagnostics`.

**Callers**: `RecognitionSession`'s pump loop writes to it; `GetResultsAsync` reads from
it.

#### DedicatedWorker

**Purpose**: Run one blocking delegate (a model load, or a session's pump loop) on its own
dedicated, long-running thread rather than a pooled thread, and bound how long a caller ever
waits for it to react to cancellation (see the subsystem-level design doc's "Cancellation and
abandon policy").

**Data Model**: An optional `abandonTimeout` (default `DefaultAbandonTimeout`, 2 seconds), an
`ISpeechDiagnostics` sink, and a diagnostics category string, all supplied at construction.

**Key Methods**:

- **RunAsync(Func&lt;CancellationToken, Task&gt; or Func&lt;CancellationToken, T&gt; work,
  CancellationToken)**: Starts `work` on a dedicated thread created with
  `TaskCreationOptions.LongRunning`, forwards `CancellationToken` to it, and returns a `Task`/
  `Task<T>` representing the work. If the caller cancels, `work`'s own token is signaled first
  (cooperative cancellation); if `work` has not completed within `abandonTimeout` of that signal,
  the returned task completes faulted with `OperationCanceledException` without waiting further,
  and the abandonment is reported through the diagnostics sink at `Warning` level, including which
  diagnostics category the abandoned work belonged to.

**Error Handling**: An abandoned delegate is never forcibly terminated (the CLR provides no safe
way to do so) - it is left to run to completion or forever on its own thread, and only the
*caller's view* of it (the returned `Task`) is abandoned. A delegate that throws is surfaced
through the returned task exactly as `Task.Run` would.

**Dependencies**: `ISpeechDiagnostics`.

**Callers**: `SpeechRecognizerFactory.LoadAsync(...)` for model loading;
`RecognitionSession` for its pump loop (one new worker constructed per session by
`SpeechRecognizerEngine.CreateSessionAsync`).

#### IRecognitionBackend and IRecognitionBackendFactory

**Purpose**: Confine every speech-inference interop call behind one mockable boundary, so the
session's threading, conversion, and result-delivery logic is verifiable with pure managed
fakes - no downloaded model and no platform-specific native binary are ever required in CI. This
mirrors the `IPortAudioApi` seam used for audio interop and the `IModelDownloadClient` seam used
for downloads.

**Data Model**: N/A (interfaces only).

**Key Methods**:

- **IRecognitionBackend.AcceptSamples(ReadOnlySpan&lt;float&gt;)**: Buffers one block of mono
  samples, already at the model's required rate, into the current utterance. Never blocks on
  decoding; an empty block is a no-op.
- **IRecognitionBackend.TryDecode(out SpeechRecognitionResult?)**: Decodes as much buffered audio
  as possible and reports the next result, returning `false` when there is nothing new. Reporting
  "nothing new" instead of an empty result keeps the caller's event stream free of duplicates
  while silence is streaming. Callers may poll until it returns `false`.
- **IRecognitionBackend.TryFlush(out SpeechRecognitionResult?)**: Finalizes and decodes any
  buffered-but-undecoded audio - the tail of an utterance released with no trailing silence, which
  `TryDecode` alone cannot decode without future context that will now never arrive - and reports
  it as one last final result if it produced non-empty text, or `false` if there was nothing to
  recover. Called once, at session end, immediately before `Reset()`.
- **IRecognitionBackend.Reset()**: Discards a partially decoded utterance.
- **IRecognitionBackendFactory.Create(IRecognitionModel, string installedModelDirectory,
  parameterValues)**: Loads a backend for the model; the real implementation asks the model to
  construct its own backend via the internal `IRecognitionModel.CreateBackend`.

**Error Handling**: Implementations are not thread-safe by contract; the session calls them from
exactly one pump thread. Load failures surface as exceptions from `Create(...)`, which
`SpeechRecognizerFactory` converts into the honest unavailable fallback.

**Dependencies**: `SpeechRecognitionResult`; `IRecognitionModel` from the
ModelManagementSubsystem.

**Callers**: `RecognitionSession` (backend) and `SpeechRecognizerFactory` (factory).

#### DefaultRecognitionBackendFactory

**Purpose**: Provide the one real `IRecognitionBackendFactory` implementation, replacing the former
`SherpaOnnxRecognitionEngineFactory`. It holds no engine-specific knowledge at all: the design
makes each model's backing class responsible for constructing its own backend, so adding a
model, or an entire model-supplying extension package, never requires changing this factory.

**Data Model**: Stateless; no fields.

**Key Methods**:

- **Create(IRecognitionModel, string installedModelDirectory, IReadOnlyDictionary&lt;string,
  object&gt;? parameterValues)**: Validates its arguments and forwards to the model's own internal
  `IRecognitionModel.CreateBackend(installedModelDirectory, parameterValues)`, returning the
  loaded backend unchanged.

**Error Handling**: Throws `ArgumentNullException` for a null model and `ArgumentException` for a
null or empty directory. Any exception the model's `CreateBackend` raises - for example a missing
native runtime or unusable model files - propagates unchanged to `SpeechRecognizerFactory`, which
converts it into the honest unavailable fallback.

**Dependencies**: `IRecognitionModel` from the ModelManagementSubsystem.

**Callers**: `SpeechRecognizerFactory`'s public `LoadAsync(...)` overloads.

The real sherpa-onnx `IRecognitionBackend` implementation, `SherpaOnnxRecognitionEngine` -
including its post-endpoint warm-up replay mitigation - is no longer part of this subsystem: it
moved, together with the concrete sherpa-onnx models that construct it, to the sibling
`SpeechSherpa` system. See *SherpaOnnxRecognitionEngine* in *SpeechSherpa RecognitionSubsystem
Design*.

#### AudioFrameResampler

**Purpose**: Convert one block of interleaved, multi-channel capture audio into the mono audio at
the model-declared `AudioFormat.SampleRate` that a recognition engine requires. Keeping this
conversion in a pure,
dependency-free type makes it exhaustively testable with plain float arrays.

**Data Model**: Immutable: the source sample rate, source channel count, and target sample rate,
all validated as positive at construction. No per-block state, so one instance serves a whole
session and is safe to share across threads.

**Key Methods**:

- **Convert(ReadOnlySpan&lt;float&gt;)**: Downmixes then resamples. Downmixing first means the
  interpolation runs over one signal rather than once per channel, which is both cheaper and
  removes any possibility of channels drifting out of alignment. Returns an empty array when the
  input contains no complete frame. A single-channel source skips the downmix step entirely and
  resamples the input directly, since `Resample` already copies or allocates as needed and a
  downmix pass would only duplicate the input. For a multi-channel source, the downmixed
  intermediate is written into an `ArrayPool<float>`-rented buffer rather than a newly allocated
  array, since it is read only by `Resample` immediately afterward and then discarded. This only
  removes that redundant downmix-buffer allocation: the final resampled result is still allocated,
  and (when downsampling) so is the FIR lowpass kernel that `Resample` builds fresh on every call.
- **DownmixToMono(ReadOnlySpan&lt;float&gt;, int channelCount)**: Averages each frame's channels,
  accumulating in double precision so a high channel count cannot lose low-level detail to
  repeated single-precision rounding. A trailing partial frame is discarded, since it has no
  defined average. A single-channel input is copied unchanged. Averaging values in `[-1.0, 1.0]`
  stays in range, so no clipping step is needed. Delegates its per-frame averaging loop to the
  destination-buffer overload below.
- **DownmixToMono(ReadOnlySpan&lt;float&gt;, int channelCount, Span&lt;float&gt; destination)**:
  Writes the same averaged frames as the array-returning overload into a caller-supplied buffer,
  letting a caller (such as `Convert`) supply an `ArrayPool<float>`-rented buffer for a purely
  transient result instead of allocating a new array. Throws `ArgumentException` when
  `destination` is shorter than the number of complete input frames.
- **Resample(ReadOnlySpan&lt;float&gt;, int sourceSampleRate, int targetSampleRate)**: Produces
  `floor(length * target / source)` samples, each the linear blend of the two input samples
  straddling its position, with the final positions clamped to the last input sample. Equal rates
  copy the input unchanged, so the identity case introduces no error at all. When downsampling, a
  small Hamming-windowed sinc lowpass filter runs first to attenuate above-target-Nyquist energy
  before decimation. The output length is computed in 64-bit arithmetic so a long block at a high
  rate cannot overflow the intermediate product.

**Error Handling**: A non-positive sample rate or channel count throws
`ArgumentOutOfRangeException`, because no meaningful conversion exists for it. Empty,
single-sample, and rounds-to-empty conversions all return an empty or clamped result rather than
throwing, since a capture device may legitimately deliver a very short block.

**Dependencies**: `AudioSubsystem.WindowedSincLowpassFilter` for the downsampling anti-aliasing
lowpass stage, shared with `PlaybackAudioResampler`'s identical need in the synthesis subsystem;
otherwise none beyond the Base Class Library.

**Callers**: `RecognitionSession`.

#### Design Constraints

**Resampler quality is a deliberate, bounded trade-off.** Downmixing remains a straight
arithmetic mean and the upsampling path remains linear interpolation, keeping the implementation
small, reviewable, and dependency-free. The downsampling path now adds a small Hamming-windowed
sinc FIR lowpass filter immediately before decimation, closing the most harmful aliasing case
without turning this unit into a full polyphase DSP subsystem. Each captured block is still
converted independently, so interpolation restarts at every block boundary, but the new filter
removes much of the above-target-Nyquist energy that would otherwise fold into the speech band.
A later phase should revisit the design only if a real model's measured recognition accuracy on
non-native-rate hardware is shown to require something stronger.

**Recognition results are never reported through diagnostics.** Every diagnostic this subsystem
emits is a structural fact - composed, started, stopped, or a named fault - and never includes
recognized text, honoring the `ISpeechDiagnostics` contract that makes it safe for a host to wire
a diagnostics sink to a visible developer log.

**Core ships zero built-in recognition models.** `SpeechModelCatalog` starts with an empty
known-model list, and this subsystem names no concrete model or engine backend: a host populates
the catalog through `SpeechModelCatalog.AddModels` (or an extension method such as the sibling
`SpeechSherpa` system's `AddSherpaModels`). Within this repository the session is therefore proven
end to end against fake models and fake backends; real-model, real-native-runtime recognition
evidence lives with the concrete backend in *SpeechSherpa RecognitionSubsystem Design* and its
companion verification.
