### SynthesisSessionFaultedException

**Purpose**: Give a caller a distinct, typed signal that an `ISynthesisSession` has reached its
terminal `Faulted` state and can no longer perform operations, carrying the original fault as its
inner exception.

**Data Model**: A sealed `Exception` subclass with no state beyond the standard `Message` and
`InnerException`. A session faults when an in-flight `SpeakAsync`/`SynthesizeAsync` operation fails
for a reason other than its own promptly-honored cancellation, including a native call that did
not stop cooperatively within the dedicated worker's abandon timeout and was detached. Once
faulted, the session is terminal.

**Key Methods**:

- **SynthesisSessionFaultedException()**: Default constructor with a default message stating that
  the synthesis session has faulted and can no longer perform operations.
- **SynthesisSessionFaultedException(string message)**: Carries a caller-supplied message.
- **SynthesisSessionFaultedException(string message, Exception innerException)**: Carries a message
  and the original fault that caused the session to fault; this is the form `SynthesisSession`
  uses.

**Error Handling**: N/A - this type is itself an error signal and performs no detection or recovery.
Callers are expected to dispose the faulted session and create a new one from the engine.

**Dependencies**: None beyond the Base Class Library.

**Callers**: `SynthesisSession` throws it from every `SpeakAsync`/`SynthesizeAsync` call made
while the session is `Faulted`, wrapping the original fault.
