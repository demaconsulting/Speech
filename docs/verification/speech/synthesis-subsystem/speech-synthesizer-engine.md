### SpeechSynthesizerEngine

#### Verification Approach

`SpeechSynthesizerEngine` is verified with a deterministic fake
`ISynthesisBackend`/`ISynthesisBackendFactory` pair standing in for the real sherpa-onnx backend,
so no downloaded model and no platform-specific native speech-inference binary is ever needed. Its
exclusivity lease is verified by driving two sequential `CreateSessionAsync` calls - the second
before the first session is disposed, then again after - and asserting
`SynthesisEngineBusyException` in the first case and success in the second. Disposal ordering
(active leased session disposed before the owned backend) is verified by asserting the fake
backend's dispose call is observed only after the session's own teardown has run.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, no downloaded model, no native speech-inference
  runtime, and no physical audio hardware
- **Test doubles**: A fake `ISynthesisBackend`/`ISynthesisBackendFactory` pair and an NSubstitute
  `IAudioPlaybackDevice`

#### Acceptance Criteria

The unit is considered verified when `CreateSessionAsync` succeeds immediately with no lease
held, fails fast with `SynthesisEngineBusyException` while a lease is held, succeeds again once
the leased session is disposed, rejects a null device, always reports itself available, disposes
its backend exactly once across repeated `DisposeAsync` calls, and disposes an active leased
session before disposing the backend. The one-shot `SpeakAsync`/`SynthesizeAsync` convenience
overloads are verified to create and dispose a fresh session per call.

#### Test Scenarios

See the SynthesisSubsystem-level scenarios "Engine: Session Exclusivity and Lease Lifecycle" and
"Engine: One-Shot Convenience Overloads".
