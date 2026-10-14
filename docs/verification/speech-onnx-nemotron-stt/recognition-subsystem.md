## RecognitionSubsystem Verification

### Verification Approach

The SpeechOnnxNemotronStt RecognitionSubsystem is verified through deterministic unit tests in
`test/DemaConsulting.Speech.Onnx.NemotronStt.Tests` that use managed fakes (`Fakes.cs`) in place of
the ONNX-backed encoder and prediction networks, so no test depends on model files:

- **`SilenceRunLimiter`**: truncation, speech preservation, adaptive threshold, and streaming
  equivalence - see _SpeechOnnxNemotronStt SilenceRunLimiter Verification_
- **`DitherNoise`**: deterministic, resettable, amplitude-matched noise - see _DitherNoise
  Verification_
- **`NemotronFeatureExtractor`**: golden log-mel values, chunked equals one-shot, flush behavior -
  see _SpeechOnnxNemotronStt NemotronFeatureExtractor Verification_
- **`NemotronEncoder`**: ONNX encoder wiring and cache feedback over a synthetic model - see
  _SpeechOnnxNemotronStt NemotronEncoder Verification_
- **`NemotronRnntGreedyDecoder`**: greedy search over a scripted network - see _SpeechOnnxNemotronStt
  NemotronRnntGreedyDecoder Verification_
- **`NemotronRnntNetwork`**: ONNX prediction and joint wiring and the blank penalty over synthetic
  models - see _SpeechOnnxNemotronStt NemotronRnntNetwork Verification_
- **`OnnxNemotronRecognitionEngine`**: chunking, provisional and final results, endpointing,
  reset, and disposal - see _SpeechOnnxNemotronStt OnnxNemotronRecognitionEngine Verification_

The ONNX Runtime-backed `NemotronEncoder` and `NemotronRnntNetwork` are exercised in CI against
tiny synthetic ONNX models that honor the real tensor contracts, and end to end by the
self-skipping real-model test and the manual real-model run described in _SpeechOnnxNemotronStt
System Verification_.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
- **Isolation**: Pure in-memory tests; no network, files, or audio hardware

### Acceptance Criteria

A SpeechOnnxNemotronStt RecognitionSubsystem test run passes when every unit suite passes: the
limiter truncates long quiet runs without dropping speech, the feature extractor matches its
golden reference and is chunk-invariant, the decoder honors blank and the symbol cap, and the
engine reports provisional and final text, endpoints on input-clock quiet and on empty chunks,
and resets and disposes correctly.

### Test Scenarios

See each unit's own verification document: `onnx-nemotron-recognition-engine.md`,
`nemotron-feature-extractor.md`, `nemotron-encoder.md`, `nemotron-rnnt-greedy-decoder.md`,
`nemotron-rnnt-network.md`, `silence-run-limiter.md`, and `dither-noise.md`.
