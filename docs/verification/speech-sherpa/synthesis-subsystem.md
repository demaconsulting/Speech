## SynthesisSubsystem Verification

### Verification Approach

The SpeechSherpa SynthesisSubsystem contains a single unit, `SherpaOnnxSynthesisEngine`, the real,
native-backed implementation of the Speech library's internal `ISynthesisBackend` seam over
sherpa-onnx's offline text-to-speech API. Like the recognition backend, it can only be
meaningfully verified against the real native runtime and a real, installed synthesis model,
because mocking native inference would prove nothing about whether real text produces real audio.

**No automated test currently exists for this unit.** This is a pre-existing gap: the unit had no
test while it lived in the Speech library, and the move into SpeechSherpa carried that gap forward
unchanged rather than introducing it. All synthesis policy above the seam - Layer 2 rendering,
chunking, the session state machine, cancellation, and fault containment - remains fully verified
in the Speech library against a fake backend (see _Speech SynthesisSubsystem Verification_), and
each synthesis model's engine configuration is verified in _SpeechSherpa ModelManagementSubsystem
Verification_, so the uncovered surface is limited to the unit's own interop calls. Real
synthesis through this unit was proven only by one-time manual spikes recorded in the synthesis
models' own verification documents.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Sherpa.Tests`
- **Dependencies**: N/A - no automated test for this subsystem exists yet

### Unit-Level Test Scenarios

See the unit's own verification document: `sherpa-onnx-synthesis-engine.md`.

### Acceptance Criteria

No automated acceptance criterion is currently met by a test for this subsystem, and none is
claimed. Until a real-native test is added, the subsystem's verification status is: not covered
by any automated test (pre-existing gap carried forward by the package split), with real
synthesis evidenced only by the manual spikes recorded for
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel` and `SherpaOnnxKokoroEnglishSynthesisModel`.
