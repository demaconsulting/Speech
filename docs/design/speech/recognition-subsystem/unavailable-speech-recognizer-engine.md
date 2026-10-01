### UnavailableSpeechRecognizerEngine

**Purpose**: Provide a safe, always-obtainable `ISpeechRecognizerEngine` fallback for use when
recognition is not possible on the current machine.

**Data Model**: No instance state. Exposes a single static `Instance` singleton; the constructor
is private, since the type carries no state and multiple instances would provide no value.
`IsAvailable` always returns `false`.

**Key Methods**:

- **CreateSessionAsync(IAudioCaptureDevice, CancellationToken)**: Throws `ArgumentNullException`
  synchronously if `device` is `null`, and throws `OperationCanceledException` synchronously if
  `cancellationToken` is already cancelled - both are programming/caller errors, not machine
  states. Otherwise never throws; returns a completed task holding
  `UnavailableRecognitionSession.Instance` regardless of the supplied device's own availability,
  so a caller that composes without checking `IsAvailable` first still receives a usable,
  honestly-unavailable session rather than an exception.

**Error Handling**: Obtaining and holding the instance never throws. The returned session is the
one place operational misuse surfaces, and only when actually invoked. This mirrors
`UnavailableAudioCaptureDevice` exactly, so both subsystems degrade the same recognizable way.

**Dependencies**: `UnavailableRecognitionSession`; implements `ISpeechRecognizerEngine`.

**Callers**: `SpeechRecognizerFactory.LoadAsync(...)` when the model is not installed, the
model's role is not recognition, or the backend cannot be loaded.
