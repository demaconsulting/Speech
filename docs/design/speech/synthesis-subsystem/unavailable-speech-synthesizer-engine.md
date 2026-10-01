### UnavailableSpeechSynthesizerEngine

**Purpose**: Provide a safe, always-obtainable `ISpeechSynthesizerEngine` fallback for use when
synthesis is not possible on the current machine.

**Data Model**: No instance state. Exposes a single static `Instance` singleton; the constructor
is private, since the type carries no state and multiple instances would provide no value.
`IsAvailable` always returns `false`.

**Key Methods**:

- **CreateSessionAsync(device, cancellationToken)**: Always succeeds, returning the shared
  `UnavailableSynthesisSession.Instance` - binding a device to an already unavailable engine is
  itself an ordinary (if useless) composition, not an error, so only the returned session's
  operational members throw. Throws `ArgumentNullException` for a null `device`.
- **SpeakAsync(...)** / **SynthesizeAsync(...)**: Always throw
  `SpeechSynthesizerUnavailableException`.
- **DisposeAsync()**: A no-op that never throws and never invalidates `Instance`, so a host that
  wraps its engine in a disposal scope runs unchanged on a machine without synthesis.

**Error Handling**: Obtaining and holding the instance never throws. Only the operational members
throw, and only when actually invoked - a caller that checks `IsAvailable` first never triggers
them. This mirrors `UnavailableSpeechRecognizerEngine` exactly, so both subsystems degrade the
same recognizable way.

**Dependencies**: `SpeechSynthesizerUnavailableException`, `UnavailableSynthesisSession`;
implements `ISpeechSynthesizerEngine`.

**Callers**: `SpeechSynthesizerFactory.LoadAsync(...)` when the model is not installed, the
model's role is not synthesis, or the backend cannot be loaded.
