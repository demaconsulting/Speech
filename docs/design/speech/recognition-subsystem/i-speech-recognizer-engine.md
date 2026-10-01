### ISpeechRecognizerEngine

**Purpose**: Define the Layer 3 contract for one loaded, reusable speech-inference engine, so
hosts and tests can depend on "is recognition possible, and can I start a session?" without
depending on a specific inference backend.

**Data Model**: `IsAvailable` indicates whether the engine is backed by a loaded backend and can
create a session at all.

**Key Methods**:

- **CreateSessionAsync(IAudioCaptureDevice captureDevice, CancellationToken
  cancellationToken = default)**: Leases exclusive use of the loaded backend to a new
  `IRecognitionSession` bound to `captureDevice`, and returns a task that completes with that
  session. Never throws for an ordinary unavailable machine state - an unavailable engine returns
  `UnavailableRecognitionSession.Instance` instead - but throws (faulting the returned task)
  `RecognitionEngineBusyException` when another live session already holds the engine's lease,
  since the backend cannot usefully decode two concurrent streams and this type never silently
  queues a caller behind an unbounded wait. Precondition: `captureDevice` is non-null.
  Postcondition: the returned session is never null, and either owns the leased backend or is the
  shared unavailable instance.

**Error Handling**: `RecognitionEngineBusyException` is the one condition this contract
represents as an exception rather than a fallback value, because a concurrent lease attempt is a
caller-sequencing bug (create, use, and dispose one session before creating the next) rather than
an ordinary machine state. A null `captureDevice` throws (faulting the returned task)
`ArgumentNullException`.

**Dependencies**: `IAudioCaptureDevice` from the AudioSubsystem; `IRecognitionSession`,
`RecognitionEngineBusyException`.

**Callers**: `SpeechRecognizerFactory.LoadAsync(...)` constructs implementations; hosts call
`CreateSessionAsync` once per capture device they want to stream recognition from.
