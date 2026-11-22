## RecognitionSubsystem Verification

### Verification Approach

The SpeechOnnxNemotronStt RecognitionSubsystem is verified through deterministic unit tests in
`test/DemaConsulting.Speech.Onnx.NemotronStt.Tests` that use managed fakes (`Fakes.cs`) in place of
the ONNX-backed encoder and prediction networks, so no test depends on model files:

- **`SilenceRunLimiter`**: truncation, speech preservation, adaptive threshold, and streaming
  equivalence - see *SpeechOnnxNemotronStt SilenceRunLimiter Verification*
- **`DitherNoise`**: deterministic, resettable, amplitude-matched noise - see *DitherNoise
  Verification*
- **`NemotronFeatureExtractor`**: reference log-mel values of the NeMo front end, frame count,
  streaming emission, chunked equals one-shot, and flush behavior - see *SpeechOnnxNemotronStt
  NemotronFeatureExtractor Verification*
- **`NemotronEncoder`**: ONNX encoder wiring and cache feedback over a synthetic model - see
  *SpeechOnnxNemotronStt NemotronEncoder Verification*
- **`NemotronRnntGreedyDecoder`**: greedy search over a scripted network - see *SpeechOnnxNemotronStt
  NemotronRnntGreedyDecoder Verification*
- **`NemotronRnntNetwork`**: ONNX prediction and joint wiring and the configurable, validated blank
  penalty over synthetic models - see *SpeechOnnxNemotronStt NemotronRnntNetwork Verification*
- **`NemotronEndpointDetector`**: the three endpoint rules, disabled rules, padding and removed
  silence accounting, and validation, driven directly with frame and silence events - see
  *SpeechOnnxNemotronStt NemotronEndpointDetector Verification*
- **`OnnxNemotronRecognitionEngine`**: chunking, provisional and final results, decode-clock
  endpointing without model reset, the maximum-length split, closing punctuation, silence
  padding, reset, and disposal - see *SpeechOnnxNemotronStt OnnxNemotronRecognitionEngine
  Verification*

The subsystem-level requirements are verified by the engine tests, which drive the whole chain
(limiter, dither, feature extractor, encoder chunking, decoder, endpoint detector, and vocabulary)
through the public `IRecognitionBackend` surface; only the encoder and the prediction network are
faked. Two engine fakes make the endpoint and padding behavior observable: a seven-frame encoder
(like the real model) that reports the loudest input feature of each output frame, and a network
that emits a word on each quiet-to-loud transition, so all-zero feature padding would produce an
extra word while silence padding does not.

The ONNX Runtime-backed `NemotronEncoder` and `NemotronRnntNetwork` are exercised in CI against
tiny synthetic ONNX models that honor the real tensor contracts, and end to end with the real
model by the real-model system tests described in *SpeechOnnxNemotronStt System Verification*.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline, on every target framework
- **Project**: `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
- **Test data**: `TestData/nemotron-features-golden.tsv` (reference feature values, provenance in
  `TestData/NOTICE.md`), copied beside the test assembly
- **Isolation**: in-memory tests; no network, no model files, and no audio hardware

### Acceptance Criteria

A SpeechOnnxNemotronStt RecognitionSubsystem test run passes when every unit suite passes:

- the limiter truncates long quiet runs without dropping speech;
- the feature extractor matches the reference values within 1e-3, produces `N / 160 + 1` frames,
  emits frames while streaming, and is chunk-invariant;
- the decoder honors blank, the symbol cap, and the configured blank penalty, and rejects an
  invalid penalty;
- the endpoint detector fires each rule exactly at its configured duration, never on padding
  alone, and counts removed silence;
- the engine reports provisional and final text, finalizes after 1200 ms without new words
  without resetting the model, reports one final per paused utterance even within one block,
  splits at the maximum utterance length between words, drops closing punctuation that would open
  a segment, never reports text for silence alone, pads only with silence features, and resets
  and disposes correctly.

### Test Scenarios

The subsystem requirements map to these engine-level scenarios; each unit's own verification
document lists its detailed scenarios (`onnx-nemotron-recognition-engine.md`,
`nemotron-endpoint-detector.md`, `nemotron-feature-extractor.md`, `nemotron-encoder.md`,
`nemotron-rnnt-greedy-decoder.md`, `nemotron-rnnt-network.md`, `silence-run-limiter.md`, and
`dither-noise.md`).

#### Endpoint after speech

**Tests**: `OnnxNemotronRecognitionEngine_TryDecode_SilenceAfterSpeech_FinalizesWithoutModelReset`,
`OnnxNemotronRecognitionEngine_TryDecode_ShortPauseBelowRule_KeepsOneUtterance`,
`OnnxNemotronRecognitionEngine_TryDecode_SteadyAudioWithoutNewTokens_FinalizesAfterTrailingBlanks`

Speech followed by 3 s of silence is finalized; a 0.6 s pause does not split an utterance; steady
audio without new words is finalized as a pause.

#### Long silence without speech

**Tests**: `TryDecode_OnlySilence_NeverEndpoints`,
`OnnxNemotronRecognitionEngine_TryFlush_SilenceOnly_ReportsNoText`

Five seconds of silence, beyond the 2400 ms rule, report nothing and never reset the model.

#### Maximum utterance length

**Tests**: `OnnxNemotronRecognitionEngine_TryDecode_ContinuousTokensBeyondMaximum_SplitsUtterance`,
`OnnxNemotronRecognitionEngine_TryDecode_MaximumLengthSplit_KeepsWordsWhole`

Continuous words beyond a shortened maximum are split into several finals, every word kept once
and whole.

#### Endpoint configuration

**Tests**: `OnnxNemotronRecognitionEngine_TryDecode_CustomEndpointRules_FinalizesAtConfiguredSilence`,
`OnnxNemotronRecognitionEngine_Constructor_InvalidOptions_ThrowsArgumentOutOfRangeException`

A 400 ms rule finalizes where the default does not; negative values are rejected.

#### Endpoint keeps the model state

**Tests**: `OnnxNemotronRecognitionEngine_TryDecode_SilenceAfterSpeech_FinalizesWithoutModelReset`,
`OnnxNemotronRecognitionEngine_TryDecode_SpeechAfterEndpoint_KeepsEveryToken`

No encoder or decoder reset happens at an endpoint, and each utterance's word appears exactly once
in its own final.

#### One final per utterance within one block

**Test**: `OnnxNemotronRecognitionEngine_TryDecode_PausedSpeechInOneBlock_ReportsOneFinalPerUtterance`

#### Closing punctuation after an endpoint

**Tests**: `OnnxNemotronRecognitionEngine_TryFlush_PunctuationAfterEndpoint_ReportsNoPunctuationOnlyFinal`,
`OnnxNemotronRecognitionEngine_TryFlush_PunctuationBeforeNextWords_StartsSegmentWithWord`

#### Silence never produces a result

**Tests**: `TryDecode_OnlySilence_NeverEndpoints`,
`OnnxNemotronRecognitionEngine_TryFlush_SilenceOnly_ReportsNoText`

#### Padding with silence features

**Tests**: `OnnxNemotronRecognitionEngine_TryFlush_PartialChunk_PadsWithSilenceFeatures`,
`OnnxNemotronRecognitionEngine_TryFlush_SpeechThenSilence_AddsNoWordsFromPadding`,
`TryDecode_QuietAfterShortSpeech_ReportsBeforeEndpoint`, `TryDecode_CustomLimiterCap_PadsAtThatCap`

#### Feature extraction, decoding, and the blank penalty

**Tests**: `NemotronFeatureExtractor_Extract_GoldenSignals_MatchNeMoReference`,
`NemotronFeatureExtractor_Flush_InputLength_ProducesNeMoFrameCount`,
`NemotronFeatureExtractor_Accept_StreamingInput_EmitsFramesWithoutFlush`,
`Decode_Tokens_AreCollectedAndAdvanced`, `SelectToken_BlankWithinPenalty_PrefersNonBlank`,
`NemotronRnntNetwork_Constructor_NegativeOrNonFiniteBlankPenalty_ThrowsArgumentOutOfRangeException`

See the unit documents for the complete lists.
