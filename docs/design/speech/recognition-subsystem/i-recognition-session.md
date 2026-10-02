### IRecognitionSession

**Purpose**: Define the Layer 5 contract for one streaming recognition session bound to one
capture device, so hosts and tests can depend on session lifecycle and result delivery without
depending on a specific inference backend.

**Data Model**: `IsAvailable` indicates whether this session is backed by a real, leased backend
rather than an unavailable fallback. `State` reports the session's current
`RecognitionSessionState` - the forward-only `Created -> Starting -> Running -> Stopping ->
Stopped -> Disposing -> Disposed` machine (with `Faulted` reachable from `Starting`, `Running`,
or `Stopping`); see the subsystem-level design doc's state diagram. `StateChanged` is raised, as
a `SessionStateChangedEventArgs` carrying `Previous` and `Current`, for every transition. The
adjacent `SpeechRecognitionResult` record carries the full recognized `Text` of the current
utterance plus an `IsFinal` flag distinguishing a provisional hypothesis from a committed one;
`Text` is never a delta, so a host can render it directly. The `SpeechRecognitionEvent` record
wraps one result as the value yielded by `GetResultsAsync`, mirroring the AudioSubsystem's
`AudioCaptureFrameEventArgs` pattern.

**Key Methods**:

- **StartAsync(CancellationToken cancellationToken = default)**: Transitions `Created ->
  Starting -> Running` and begins streaming recognition. Throws `InvalidOperationException` when
  called more than once on the same session, since a session is single-use by design - a host
  doing repeated, low-latency recognition should call `ISpeechRecognizerEngine.CreateSessionAsync`
  again for the next turn rather than attempting to restart a stopped session.
- **StopAsync(CancellationToken cancellationToken = default)**: Transitions `Running ->
  Stopping -> Stopped` and drains already-captured audio, so every result derived from audio
  accepted before the call is either delivered or accounted for (see the subsystem-level
  backpressure policy) before it returns - including the tail of an utterance that a streaming
  backend could not otherwise decode without audio it will now never receive: an implementation
  is expected to finalize and recover that trailing audio as one last final result rather than
  silently losing it. This guarantee is best-effort in the same way backend faults elsewhere are:
  if the backend itself faults while finalizing or resetting, the fault is reported rather than
  thrown and `StopAsync` still completes, but the trailing audio and/or the backend's clean state
  can no longer be guaranteed for that one call. Idempotent once stopped or faulted.
- **GetResultsAsync(CancellationToken cancellationToken = default)**: Returns an
  `IAsyncEnumerable<SpeechRecognitionEvent>` of ordered provisional and final results.
  Single-consumer: throws `InvalidOperationException` if called again while a previous
  enumeration of the same session is still active. A session fault during enumeration surfaces as
  `RecognitionSessionFaultedException` thrown from the enumerator, never silently.
- **DisposeAsync()**: Runs the full `Stopping -> Stopped` teardown first (if not already stopped
  or faulted), then transitions `Disposing -> Disposed` and releases the engine's lease so the
  owning `ISpeechRecognizerEngine` can create its next session. Idempotent.

**Error Handling**: Real implementations and the unavailable fallback both use
`SpeechRecognizerUnavailableException` when an operational call is invalid because no usable
session is available or the capture device fails at first use or mid-session; such a fault also
transitions `State` to `Faulted` and is wrapped in `RecognitionSessionFaultedException` for any
active `GetResultsAsync` consumer. Starting more than once, or consuming `GetResultsAsync` from
more than one concurrent caller, throws `InvalidOperationException`.

**Dependencies**: `RecognitionSessionState`, `SessionStateChangedEventArgs`,
`SpeechRecognitionResult`, `SpeechRecognitionEvent`, `SpeechRecognizerUnavailableException`,
`RecognitionSessionFaultedException`.

**Callers**: `ISpeechRecognizerEngine.CreateSessionAsync(...)` constructs implementations; hosts
that render a live transcript call `StartAsync`, consume `GetResultsAsync`, and call `StopAsync`.
