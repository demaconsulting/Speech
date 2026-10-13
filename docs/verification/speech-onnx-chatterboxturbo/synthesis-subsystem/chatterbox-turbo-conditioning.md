### ChatterboxTurboConditioning Verification

#### Verification Approach

The unit is a data record with no behavior, so it has no test class of its own. It is verified through the
tests of the units that produce and consume it: `OnnxChatterboxTurboNetworksTests.cs` (the encoder builds
it, the decoder consumes it) and `OnnxChatterboxTurboSynthesisEngineTests.cs` (the engine caches it and
reuses it per voice and mood). No mocking is needed; the networks tests use synthetic graphs and the engine
tests use a fake `IChatterboxTurboNetworks`.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: scratch directories for the synthetic graphs; no network I/O; no real model

#### Acceptance Criteria

The encoder output fields reach the record unchanged and the decoder accepts the record to produce a
waveform; one conditioning serves repeated utterances of the same voice and mood without a second encoder run.

#### Test Scenarios

##### The encoder fills the record from its outputs

**Test**: `OnnxChatterboxTurboNetworks_Encode_NamedOutputs_ReturnsConditioning`

The encoder's named outputs populate the conditioning embeddings, length, prompt tokens, speaker embeddings
and feature frames.

##### The decoder consumes the record

**Test**: `OnnxChatterboxTurboNetworks_Decode_SpeechTokens_ReturnsWaveform`

##### The record is reused per voice

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_Voices_AreEncodedOnceAndFallBack`

Each voice is encoded once however many utterances use it.

##### The record is reused per mood

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_MoodMarker_UsesMoodClipOnceAndHidesMarker`

A mood clip is encoded once and shares no cache entry with the base clip.
