## SynthesisSubsystem Verification

### Verification Approach

The SpeechSherpa SynthesisSubsystem contains a single unit, `SherpaOnnxSynthesisEngine`, the real,
native-backed implementation of the Speech library's public `ISynthesisBackend` seam over
sherpa-onnx's offline text-to-speech API. Like the recognition backend, it can only be
meaningfully verified against the real native runtime and a real, installed synthesis model,
because mocking native inference would prove nothing about whether real text produces real audio.
It is verified directly against the real native sherpa-onnx runtime and a real, installed
VITS/Piper synthesis model through `SherpaOnnxSynthesisEngineTests`, mirroring
`SherpaOnnxRecognitionEngineTests`; tests self-skip, rather than fail, when that model is not
installed in the running environment. All synthesis policy above the seam - Layer 2 rendering,
chunking, the session state machine, cancellation, and fault containment - remains fully verified
in the Speech library against a fake backend (see _Speech SynthesisSubsystem Verification_), and
each synthesis model's engine configuration is verified in _SpeechSherpa ModelManagementSubsystem
Verification_. Real synthesis through this unit is additionally evidenced by one-time manual
spikes recorded in the synthesis models' own verification documents.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Sherpa.Tests`
- **Dependencies**: The real native sherpa-onnx runtime and the real, already-installed VITS/Piper
  synthesis model (self-skipping when absent); no network access and no physical audio hardware

### Unit-Level Test Scenarios

See the unit's own verification document: `sherpa-onnx-synthesis-engine.md`.

### Acceptance Criteria

The subsystem's test run passes when every `SherpaOnnxSynthesisEngineTests` scenario either
passes or self-skips because its target model is not installed, and, on a machine where the model
is installed, the construction/sample-rate, generation, argument-validation, and disposal criteria
defined in `sherpa-onnx-synthesis-engine.md` are all met. Real synthesis is additionally evidenced
by the manual spikes recorded for `SherpaOnnxVitsLibriTtsEnglishSynthesisModel` and
`SherpaOnnxKokoroEnglishSynthesisModel`.
