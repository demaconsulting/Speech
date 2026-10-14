### RecognitionEngineBusyException

**Purpose**: Give a caller a distinct, typed signal that `ISpeechRecognizerEngine.CreateSessionAsync`
failed because a previously created session's exclusivity lease is still held.

**Data Model**: A sealed `Exception` subclass with no state beyond the standard `Message` and
`InnerException`. Exactly one `IRecognitionSession` may be leased on an engine at a time, from
creation until its own `DisposeAsync` completes; the call fails fast with this exception rather
than queuing or awaiting the prior session's release, including while that session is still
tearing down.

**Key Methods**:

- **RecognitionEngineBusyException()**: Default constructor with a default message stating that
  the recognition engine already has a session leased.
- **RecognitionEngineBusyException(string message)**: Carries a message describing why the engine
  is busy.
- **RecognitionEngineBusyException(string message, Exception innerException)**: Carries a message
  and an inner exception describing the underlying cause.

**Error Handling**: N/A - this type is itself an error signal and performs no detection or recovery.

**Dependencies**: None beyond the Base Class Library.

**Callers**: `SpeechRecognizerEngine.CreateSessionAsync(...)` throws it when the lease is held.
