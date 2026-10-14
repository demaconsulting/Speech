### SynthesisEngineBusyException

**Purpose**: Give a caller a distinct, typed signal that `ISpeechSynthesizerEngine.CreateSessionAsync`
failed because the engine's single-session exclusivity lease is already held by another session.

**Data Model**: A sealed `Exception` subclass with no state beyond the standard `Message` and
`InnerException`. It is thrown instead of queuing or awaiting release because waiting would make
the call's latency depend on an unrelated session's teardown with no caller-visible way to bound
that wait; a caller that needs to wait implements its own retry or backoff.

**Key Methods**:

- **SynthesisEngineBusyException()**: Default constructor with a default message stating that the
  engine's exclusivity lease is already held by another session.
- **SynthesisEngineBusyException(string message)**: Carries a message describing which engine was
  busy.
- **SynthesisEngineBusyException(string message, Exception innerException)**: Carries a message and
  an inner exception describing the underlying cause.

**Error Handling**: N/A - this type is itself an error signal and performs no detection or recovery.

**Dependencies**: None beyond the Base Class Library.

**Callers**: `SpeechSynthesizerEngine.CreateSessionAsync(...)` throws it when the lease is held.
