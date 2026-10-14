### RecognitionResultBuffer

**Purpose**: Isolate a possibly-slow `GetResultsAsync` consumer from the pump thread with a
two-tier buffer (see the subsystem-level design doc's "Backpressure policy").

**Data Model**: One overwritable slot for the latest unconsumed provisional result, plus an
unbounded FIFO queue of final results, with a completion-source signal so `ReadAllAsync` can
await new data without polling.

**Key Methods**:

- **AddResult(SpeechRecognitionEvent)**: A provisional result overwrites the single pending
  provisional slot (an unconsumed provisional is superseded by the next one, never queued). A
  final result is enqueued into the FIFO and also clears any unread provisional. Final results
  are never dropped.
- **ReadAllAsync(CancellationToken)**: An `IAsyncEnumerable<SpeechRecognitionEvent>` yielding the
  latest provisional and every queued final in the order they became ready, until `Complete()` is
  called, or throwing `RecognitionSessionFaultedException` once `Fault(...)` has been called.
- **Complete()**: Signals that no further results will be written; lets a draining
  `ReadAllAsync` finish normally once its backlog is consumed.
- **Fault(Exception)**: Records the cause and signals every current and future `ReadAllAsync`
  consumer to throw `RecognitionSessionFaultedException` wrapping it.

**Error Handling**: A null argument to `AddResult` or `Fault` throws `ArgumentNullException`. A
result added after `Complete()` or `Fault(...)` is silently ignored, so a late write from the pump
never throws. `Complete()` and `Fault(...)` never discard buffered results: `ReadAllAsync` first
delivers everything already buffered (queued finals, then any unread provisional) and only then
ends normally (completion) or throws `RecognitionSessionFaultedException` wrapping the cause
(fault). Only the first fault cause is kept. Canceling the token passed to `ReadAllAsync`
propagates `OperationCanceledException` to that reader only and leaves the buffer intact. The
buffer never drops a final result, and a provisional result is only replaced by a newer one.

**Dependencies**: `SpeechRecognitionEvent`, `RecognitionSessionFaultedException`.

**Callers**: `RecognitionSession`'s pump loop writes to it; `GetResultsAsync` reads from
it.
