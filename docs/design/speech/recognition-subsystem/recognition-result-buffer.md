### RecognitionResultBuffer

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
