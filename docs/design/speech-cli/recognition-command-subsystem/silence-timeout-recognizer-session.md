### SilenceTimeoutRecognizerSession

**Purpose**: End a microphone recognition session automatically when the speaker stops talking
(or never starts), by wrapping the session's result stream with an idle timeout that stops the
session when no result arrives in time.

**Data Model**: A stateless decorator. It holds the wrapped `IRecognitionSession`, an idle window
(the silence timeout), a start window (the grace period before the first result, defaulting to
the idle window when omitted) and a `TimeProvider` (the system clock when omitted). It raises a
`TimedOut` event after it has stopped the session. All bookkeeping for one enumeration lives in
the local variables of `GetResultsAsync`, so the type has no mutable shared state.

**Key Methods**:

- **Constructor**: Stores the collaborators. The wrapped session must not be null, and the idle
  window and any supplied start window must be greater than zero.
- **GetResultsAsync(cancellationToken)**: An asynchronous iterator over the wrapped session's
  results. Each loop iteration races the next result against a delay created from the
  `TimeProvider`, using `Task.WhenAny`. The delay of each iteration is bound to a cancellation
  source linked to the caller's token; that source is canceled and disposed as soon as the
  iteration ends (result won, timeout handled, fault or cancellation), so an idle timer that lost
  the race is released immediately and timers never accumulate across results. If the result wins,
  it is yielded and the next iteration arms the idle window. If the delay wins and no earlier
  timeout has fired on this enumeration,
  the decorator awaits the wrapped session's `StopAsync`, raises `TimedOut` once, and keeps
  looping rather than returning, so any trailing result that the stop drains still reaches the
  caller before the enumeration completes. The very first iteration uses the start window; every
  iteration from the first yielded result onward (partial or final) uses the idle window. A delay
  that ends because the token was canceled is not treated as a timeout, so the caller observes
  the cancellation instead of a spurious stop.

**Error Handling**: `ArgumentNullException` for a null session and `ArgumentOutOfRangeException`
for a non-positive idle or start window, both at construction. Cancellation of the token passed to
`GetResultsAsync` propagates as `OperationCanceledException`; the wrapped session keeps running,
matching the contract of its own result stream. Faults from the wrapped stream propagate
unchanged. The type is not disposable and needs no locks: the wrapped enumerator is released by the
iterator's own `await using`, and there is no background timer callback that could race a
concurrent disposal.

**Dependencies**: The library's `IRecognitionSession` and `SpeechRecognitionEvent`, and the BCL
`TimeProvider`. The idle delay uses `Task.Delay(TimeSpan, TimeProvider, CancellationToken)`, which
lets a test drive the whole idle, reset and timeout sequence with a fake `TimeProvider` and no
real wall-clock delay.

**Callers**: `RecognizeCommand` in microphone mode, and `AskCommand` for its listen phase.
