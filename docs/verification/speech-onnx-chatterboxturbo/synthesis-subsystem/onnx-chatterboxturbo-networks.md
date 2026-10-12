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

Each of the four graph calls returns the documented data; the intra-op thread count is applied and
validated; sessions are disposed on dispose and on construction failure.

#### Test Scenarios

##### Encode binds named outputs

**Test**: `OnnxChatterboxTurboNetworks_Encode_NamedOutputs_ReturnsConditioning`

##### Encode binds unnamed outputs positionally

**Test**: `OnnxChatterboxTurboNetworks_Encode_UnnamedFourOutputs_BindsPositionally`

##### Embed returns table rows

**Test**: `OnnxChatterboxTurboNetworks_Embed_TokenIds_ReturnsTableRows`

**Test**: `OnnxChatterboxTurboNetworks_ReadFloats_HandlesFloat32AndFloat16`

##### Step reuses the cache and returns the last logits

**Test**: `OnnxChatterboxTurboNetworks_Step_TwoSteps_ReusesCacheAndReturnsLastLogits`

##### Decode returns the waveform

**Test**: `OnnxChatterboxTurboNetworks_Decode_SpeechTokens_ReturnsWaveform`

##### Thread count is applied and validated

**Test**: `OnnxChatterboxTurboNetworks_Constructor_ThreadCount_IsAppliedAndValidated`

##### An invalid language model disposes sessions

**Test**: `OnnxChatterboxTurboNetworks_Constructor_InvalidLanguageModel_DisposesSessionsAndThrows`

##### A missing graph disposes earlier sessions

**Test**: `OnnxChatterboxTurboNetworks_Constructor_MissingGraph_DisposesEarlierSessionsAndThrows`

##### Dispose disposes all sessions

**Test**: `OnnxChatterboxTurboNetworks_Dispose_DisposesAllSessions`
