### OnnxChatterboxTurboSynthesisEngine Verification

#### Verification Approach

Verified by `OnnxChatterboxTurboSynthesisEngineTests.cs` against a fake `IChatterboxTurboNetworks`
that records calls and replays scripted logits, so the algorithm is checked without ONNX Runtime.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: no network, no files

#### Acceptance Criteria

The prompt and decoder input are assembled as designed; generation is bounded and flagged; empty
text makes no network calls; voices are encoded once with fallback; dispose and argument validation
behave as documented.

#### Test Scenarios

##### Scripted tokens assemble prompt and decoder input

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_ScriptedTokens_AssemblesPromptAndDecoderInput`

##### A model that never stops is bounded and flagged

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_NeverStops_IsBoundedAndFlagged`

##### The proportional bound stops a runaway early

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_ProportionalBound_StopsRunawayEarly`

##### Empty text returns empty audio without network calls

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_EmptyText_ReturnsEmptyAudioWithoutNetworkCalls`

##### Voices are encoded once and fall back

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_Voices_AreEncodedOnceAndFallBack`

##### Dispose disposes networks and rejects generation

**Test**: `OnnxChatterboxTurboSynthesisEngine_Dispose_DisposesNetworksAndRejectsGenerate`

##### Invalid constructor arguments throw

**Test**: `OnnxChatterboxTurboSynthesisEngine_Constructor_InvalidArguments_Throw`
