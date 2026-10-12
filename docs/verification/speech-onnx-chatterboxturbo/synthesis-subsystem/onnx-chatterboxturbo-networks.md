### OnnxChatterboxTurboNetworks Verification

#### Verification Approach

Verified by `OnnxChatterboxTurboNetworksTests.cs` against synthetic ONNX graphs written by the test
project's `OnnxModelWriter` and `ChatterboxTurboGraphFixture`. A static session hook lets tests
observe sessions for thread configuration and disposal checks; these tests run in a non-parallel
collection.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: scratch directories; no network; no real model

#### Acceptance Criteria

Each of the four graph calls returns the documented data, including half-precision embeddings and logits;
invalid step arguments are rejected; the intra-op thread count is applied and validated; a missing graph
throws `FileNotFoundException`; sessions are disposed on dispose and on construction failure.

#### Test Scenarios

##### Encode binds named outputs

**Test**: `OnnxChatterboxTurboNetworks_Encode_NamedOutputs_ReturnsConditioning`

##### Encode binds unnamed outputs positionally

**Test**: `OnnxChatterboxTurboNetworks_Encode_UnnamedFourOutputs_BindsPositionally`

##### Embed returns table rows

**Test**: `OnnxChatterboxTurboNetworks_Embed_TokenIds_ReturnsTableRows`

##### ReadFloats handles float32 and float16

**Test**: `OnnxChatterboxTurboNetworks_ReadFloats_HandlesFloat32AndFloat16`

##### Step reuses the cache and returns the last logits

**Test**: `OnnxChatterboxTurboNetworks_Step_TwoSteps_ReusesCacheAndReturnsLastLogits`

##### Step converts half-precision embeddings and logits

**Test**: `OnnxChatterboxTurboNetworks_Step_HalfEmbeddingsAndLogits_AreConverted`

##### Step rejects invalid arguments

**Tests**: `OnnxChatterboxTurboNetworks_Step_WrongEmbeddingLength_ThrowsArgumentException` and
`OnnxChatterboxTurboNetworks_Step_AttentionShorterThanSequence_ThrowsArgumentOutOfRangeException`

##### Decode returns the waveform

**Test**: `OnnxChatterboxTurboNetworks_Decode_SpeechTokens_ReturnsWaveform`

##### Thread count is applied and validated

**Test**: `OnnxChatterboxTurboNetworks_Constructor_ThreadCount_IsAppliedAndValidated`

##### An invalid language model disposes sessions

**Test**: `OnnxChatterboxTurboNetworks_Constructor_InvalidLanguageModel_DisposesSessionsAndThrows`

##### Unrecognized encoder outputs are rejected

**Test**: `OnnxChatterboxTurboNetworks_Constructor_UnrecognizedEncoderOutputs_ThrowsInvalidOperationException`

##### A missing graph throws and disposes earlier sessions

**Test**: `OnnxChatterboxTurboNetworks_Constructor_MissingGraph_DisposesEarlierSessionsAndThrows`
(asserts `FileNotFoundException`)

##### Dispose disposes all sessions

**Test**: `OnnxChatterboxTurboNetworks_Dispose_DisposesAllSessions`
