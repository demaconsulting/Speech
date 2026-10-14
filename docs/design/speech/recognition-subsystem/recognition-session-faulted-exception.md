### RecognitionSessionFaultedException

**Purpose**: Give a `GetResultsAsync` consumer a distinct, typed signal that the
`IRecognitionSession` it is enumerating transitioned to `Faulted`, with the underlying cause
available as the inner exception.

**Data Model**: A sealed `Exception` subclass with no state beyond the standard `Message` and
`InnerException`. A session faults when an unrecoverable error occurs while `Starting`, `Running`,
or `Stopping` - for example the bound capture device being lost mid-session.

**Key Methods**:

- **RecognitionSessionFaultedException()**: Default constructor with a default message stating
  that the recognition session has faulted.
- **RecognitionSessionFaultedException(string message)**: Carries a message describing the fault.
- **RecognitionSessionFaultedException(string message, Exception innerException)**: Carries a
  message and the exception that caused the session to fault.

**Error Handling**: N/A - this type is itself an error signal and performs no detection or recovery.

**Dependencies**: None beyond the Base Class Library.

**Callers**: `RecognitionResultBuffer` throws it from `ReadAllAsync` once `Fault(...)` has been
called, wrapping the recorded cause; `RecognitionSession.GetResultsAsync` surfaces it to the
consumer.
