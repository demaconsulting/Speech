## RecognitionSubsystem Verification

### Verification Approach

The SpeechOnnxNemotronStt RecognitionSubsystem is verified through deterministic unit tests in
`test/DemaConsulting.Speech.Onnx.NemotronStt.Tests` that use managed fakes (`Fakes.cs`) in place of
the ONNX-backed encoder and prediction networks, so no test depends on model files:

- **`SilenceRunLimiter` and `DitherNoise`**: truncation, speech preservation, adaptive threshold,
  streaming equivalence, and deterministic noise - see _SpeechOnnxNemotronStt SilenceRunLimiter
  Verification_
- **`NemotronFeatureExtractor`**: golden log-mel values, chunked equals one-shot, flush behavior -
  see _SpeechOnnxNemotronStt NemotronFeatureExtractor Verification_
- **`NemotronRnntGreedyDecoder`**: greedy search over a scripted network - see _SpeechOnnxNemotronStt
  NemotronRnntGreedyDecoder Verification_
- **`OnnxNemotronRecognitionEngine`**: chunking, provisional and final results, endpointing,
  reset, and disposal - see _SpeechOnnxNemotronStt OnnxNemotronRecognitionEngine Verification_

The ONNX Runtime-backed `NemotronEncoder` and `NemotronRnntNetwork` are exercised only by the
manual real-model run and the self-skipping real-model test described in _SpeechOnnxNemotronStt
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
`nemotron-feature-extractor.md`, `nemotron-rnnt-greedy-decoder.md`, and `silence-run-limiter.md`.
