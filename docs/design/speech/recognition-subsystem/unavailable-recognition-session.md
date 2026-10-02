### UnavailableRecognitionSession

**Purpose**: Provide a safe, always-obtainable `IRecognitionSession` fallback, returned by
`UnavailableSpeechRecognizerEngine.CreateSessionAsync`, for use when recognition is not possible
on the current machine.

**Data Model**: No instance state other than the never-invoked backing field for `StateChanged`.
Exposes a single static `Instance` singleton; the constructor is private. `IsAvailable` always
returns `false`; `State` always reports `RecognitionSessionState.Created`, since this session
never runs and so never reaches any other state.

**Key Methods**:

- **StartAsync()**: Always throws `SpeechRecognizerUnavailableException`.
- **StopAsync()** / **DisposeAsync()**: Safe no-ops that complete synchronously; this session
  was never running and owns no engine, thread, or native resource, so there is nothing to stop
  or release, and disposal never throws or invalidates `Instance` - so a host that wraps its
  session in a disposal scope runs unchanged on a machine without recognition.
- **GetResultsAsync()**: Throws `SpeechRecognizerUnavailableException` synchronously, at the
  point of invocation - not deferred to the first `MoveNextAsync` - since this session has no
  real engine or capture device to stream results from.
- **StateChanged**: Never raised; subscription and unsubscription are safe no-ops.

**Error Handling**: Obtaining and holding the instance never throws. Only the operational members
throw (or fault), and only when actually invoked - a caller that checks `IsAvailable` first never
triggers them.

**Dependencies**: `SpeechRecognizerUnavailableException`; implements `IRecognitionSession`.

**Callers**: `UnavailableSpeechRecognizerEngine.CreateSessionAsync(...)` for every call.
