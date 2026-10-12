### SpeechRecognizerEngine

#### Verification Approach

`SpeechRecognizerEngine` is verified with a deterministic fake `IRecognitionBackend`/
`IRecognitionBackendFactory` pair and NSubstitute capture devices and diagnostics sinks, so no
downloaded model, native speech-inference runtime, or audio hardware is needed. The single-session
lease is verified by driving sequential `CreateSessionAsync` calls: with no active session, while a
prior session is leased, while that prior session is still tearing down (its disposal held open by
a gated backend), and again after the prior session has fully completed its own disposal. Disposal
ordering is verified by disposing the engine with an active session and asserting the session is
disposed before the engine's own resources are released.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Dependencies**: No external services, no downloaded model, no native speech-inference runtime,
  and no physical audio hardware
- **Test doubles**: Fake `IRecognitionBackend`/`IRecognitionBackendFactory` implementations plus
  NSubstitute capture devices and diagnostics sinks

#### Acceptance Criteria

The unit is considered verified when `CreateSessionAsync` returns a session when none is active,
fails fast with `RecognitionEngineBusyException` while a prior session holds the lease (including
while that session is still disposing), returns a new session once the prior session has fully
disposed, and disposes an active session before releasing the engine's own resources.

#### Test Scenarios

See the RecognitionSubsystem-level scenario "Engine Exclusivity and Lease Behavior".

##### No active session returns a session

**Test**: `SpeechRecognizerEngine_CreateSessionAsync_NoActiveSession_ReturnsSession`

##### A new session is returned after the prior one fully disposed

**Test**: `SpeechRecognizerEngine_CreateSessionAsync_AfterPriorSessionFullyDisposed_ReturnsNewSession`

##### A leased engine fails fast

**Test**: `SpeechRecognizerEngine_CreateSessionAsync_SessionAlreadyLeased_ThrowsRecognitionEngineBusyException`

##### The lease is held while the prior session is disposing

**Test**: `SpeechRecognizerEngine_CreateSessionAsync_PriorSessionDisposing_ThrowsRecognitionEngineBusyException`

##### Disposal disposes the active session first

**Test**: `SpeechRecognizerEngine_DisposeAsync_WithActiveSession_DisposesSessionFirst`
