## RecognitionSubsystem Verification

### Verification Approach

The SpeechSherpa RecognitionSubsystem contains a single unit, `SherpaOnnxRecognitionEngine`, the
real, native-backed implementation of the Speech library's internal `IRecognitionBackend` seam.
It is verified directly against the real native sherpa-onnx runtime and real, already-installed
streaming Zipformer and Nemotron models, through `SherpaOnnxRecognitionEngineTests` (post-endpoint
warm-up-replay bookkeeping) and `SherpaOnnxRecognitionEngineAccuracyTests` (real-speech Word Error
Rate, session-end `Reset()`, and `TryFlush()` recovery). Mocking native inference would provide no
useful proof for a unit whose entire purpose is turning real audio into real text through the
native runtime, so no fake backend is used here; all policy above the seam is verified in the
Speech library against a fake backend (see _Speech RecognitionSubsystem Verification_).

These tests self-skip (rather than fail) when their target model is not installed in the running
environment, so their evidence is only produced on a machine where the models are present.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Sherpa.Tests`
- **Dependencies**: The real native sherpa-onnx runtime and the real, already-installed streaming
  Zipformer and Nemotron models (self-skipping when absent); no network access and no physical
  audio hardware
- **Fixture**: `test/DemaConsulting.Speech.Sherpa.Tests/TestData/crossing-the-bar-16k-mono.wav`

### Unit-Level Test Scenarios

See the unit's own verification document for its detailed test scenarios:
`sherpa-onnx-recognition-engine.md`.

### Acceptance Criteria

A SpeechSherpa RecognitionSubsystem test run passes when every `SherpaOnnxRecognitionEngineTests`
and `SherpaOnnxRecognitionEngineAccuracyTests` scenario either passes or self-skips because its
target model is not installed, and, on a machine where the models are installed, the
post-endpoint warm-up-replay bookkeeping, real-speech Word Error Rate tolerance, session-end
`Reset()` isolation, and `TryFlush()` trailing-audio recovery criteria defined in
`sherpa-onnx-recognition-engine.md` are all met.
