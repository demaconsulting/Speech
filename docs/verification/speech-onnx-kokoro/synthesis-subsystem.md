## SynthesisSubsystem Verification

### Verification Approach

The SpeechOnnxKokoro SynthesisSubsystem contains a single unit, `OnnxKokoroSynthesisEngine`, the
real, ONNX-Runtime-backed implementation of the Speech library's public `ISynthesisBackend` seam
over Kokoro v1.0's bare ONNX graph.

It is verified by automated unit tests in
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests/SynthesisSubsystem/OnnxKokoroSynthesisEngineTests.cs`
against a tiny, hand-built ONNX test fixture (`TestData/fake-kokoro-model.onnx`, whose single node
forwards its `style` input straight through as the output), covering construction argument
validation, the sample rate contract, the zero-token short-circuit, disposed-engine and
null-argument guards, disposal idempotency, and the accelerated-provider probe's successful-run
path. Voice-style-vector selection and the full phonemization-to-inference tensor wiring are
proven end-to-end by `OnnxKokoroEnglishSynthesisModelTests`'
`OnnxKokoroEnglishSynthesisModel_CreateBackend_ValidModelAndVoices_GeneratesUsingSelectedVoiceStyle`
test (via `CreateBackend`), reused here rather than duplicated, since that path is only reachable
through a synthesis model's own `CreateBackend` implementation (see _SpeechOnnxKokoro
ModelManagementSubsystem Verification_).

One additional test, `Generate_RealInstalledModel_ProducesNonEmptyAudio`, exercises the real
production Kokoro model end-to-end - through `OnnxKokoroEnglishSynthesisModel.CreateBackend` and
`ISynthesisBackend.Generate` together - producing real, non-empty 24000 Hz audio for known
English text. It skips (rather than fails) when the real model is not installed in the running
environment (checking every one of its 30 declared download files, not merely a non-empty
directory, so a partial/interrupted install still skips rather than failing), mirroring this
repository's established SpeechSherpa real-model-skip convention.

All synthesis policy above the seam - Layer 2 rendering, chunking, the session state machine,
cancellation, and fault containment - remains fully verified in the Speech library against a fake
backend (see _Speech SynthesisSubsystem Verification_), and this model's engine configuration is
verified in _SpeechOnnxKokoro ModelManagementSubsystem Verification_.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.Kokoro.Tests`
- **Dependencies**: No network access and no physical audio hardware for the fixture-based tests;
  one test additionally depends on the real, already-installed Kokoro model, self-skipping when
  it is not installed

### Unit-Level Test Scenarios

See the unit's own verification document: `onnx-kokoro-synthesis-engine.md`.

### Acceptance Criteria

This subsystem is accepted when: the engine's constructor throws `ArgumentNullException` for each
null required dependency and `ArgumentException` for an empty voice-styles dictionary;
`SampleRate` always reports 24000 Hz; `Generate` returns empty audio without running the ONNX
graph for zero-token text, throws `ArgumentNullException` for null text, and throws
`ObjectDisposedException` after disposal; a second `Dispose` call is a no-op; `RunProbeInference`
runs successfully against a runnable session; `CreateBackend`'s constructed engine forwards the
requested voice's own style vector verbatim (proven in
_SpeechOnnxKokoro ModelManagementSubsystem Verification_); and, when the real production model is
installed, the real engine produces non-empty 24000 Hz audio for known English text.
