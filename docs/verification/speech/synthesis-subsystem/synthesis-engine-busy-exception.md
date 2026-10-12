### SynthesisEngineBusyException

#### Verification Approach

The exception is verified through `SpeechSynthesizerEngine`, the only unit that throws it. A
deterministic fake `ISynthesisBackend`/`ISynthesisBackendFactory` pair and an NSubstitute
`IAudioPlaybackDevice` stand in for the real backend and playback hardware, so no downloaded model,
native speech-inference runtime, or audio hardware is needed. A first `CreateSessionAsync` call
takes the lease; a second call made before the first session is disposed is asserted to throw the
exception.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The unit is considered verified when `CreateSessionAsync` called while the engine's lease is held
by an undisposed session throws `SynthesisEngineBusyException` instead of queuing or blocking.

#### Test Scenarios

##### A second session request while the lease is held fails fast

**Test**: `SpeechSynthesizerEngine_CreateSessionAsync_LeaseAlreadyHeld_ThrowsSynthesisEngineBusyException`
