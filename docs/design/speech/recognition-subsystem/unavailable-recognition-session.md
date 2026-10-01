### UnavailableRecognitionSession

**Purpose**: Provide a safe, always-obtainable `IRecognitionSession` fallback, returned by
`UnavailableSpeechRecognizerEngine.CreateSessionAsync`, for use when recognition is not possible
on the current machine.

**Data Model**: No instance state other than the never-invoked backing field for `StateChanged`.
Exposes a single static `Instance` singleton; the constructor is private. `IsAvailable` always
returns `false`; `State` always reports `RecognitionSessionState.Faulted`, honestly reflecting
that this session can never run.

**Key Methods**:

- **StartAsync()** / **StopAsync()**: Always throw `SpeechRecognizerUnavailableException`.
- **GetResultsAsync()**: Its enumeration always faults with
  `SpeechRecognizerUnavailableException` on the first `MoveNextAsync`.
- **DisposeAsync()**: A no-op that completes synchronously and never throws or invalidates
  `Instance`, so a host that wraps its session in a disposal scope runs unchanged on a machine
  without recognition.
- **StateChanged**: Never raised; subscription and unsubscription are safe no-ops.

**Error Handling**: Obtaining and holding the instance never throws. Only the operational members
throw (or fault), and only when actually invoked - a caller that checks `IsAvailable` first never
triggers them.

**Dependencies**: `SpeechRecognizerUnavailableException`; implements `IRecognitionSession`.

**Callers**: `UnavailableSpeechRecognizerEngine.CreateSessionAsync(...)` for every call.
