### UnavailableSpeechRecognizer

**Purpose**: Provide a safe, always-obtainable `ISpeechRecognizer` fallback for use when
recognition is not possible on the current machine.

**Data Model**: No instance state other than the never-invoked backing field for
`ResultReceived`. Exposes a single static `Instance` singleton; the constructor is private, since
the type carries no state and multiple instances would provide no value. `IsAvailable` always
returns `false`.

**Key Methods**:

- **Start()** / **Stop()**: Always throw `SpeechRecognizerUnavailableException`.
- **Dispose()**: A no-op that never throws and never invalidates `Instance`, so a host that wraps
  its recognizer in a disposal scope runs unchanged on a machine without recognition.
- **ResultReceived**: Never raised; subscription and unsubscription are safe no-ops.

**Error Handling**: Obtaining and holding the instance never throws. Only the operational members
throw, and only when actually invoked - a caller that checks `IsAvailable` first never triggers
them. This mirrors `UnavailableAudioCaptureDevice` exactly, so both subsystems degrade the same
recognizable way.

**Dependencies**: `SpeechRecognizerUnavailableException`; implements `ISpeechRecognizer`.

**Callers**: `SpeechRecognizerFactory.Create(...)` when the model is not installed, the model's
role is not recognition, the capture device is unavailable, or the engine cannot be loaded.
