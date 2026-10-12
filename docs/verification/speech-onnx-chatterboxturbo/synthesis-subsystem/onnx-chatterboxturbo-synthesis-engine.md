### OnnxChatterboxTurboSynthesisEngine Verification

#### Verification Approach

Verified by `OnnxChatterboxTurboSynthesisEngineTests.cs` against a fake `IChatterboxTurboNetworks`
that records calls and replays scripted logits, so the algorithm is checked without ONNX Runtime.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: no network, no files

#### Acceptance Criteria

The prompt and decoder input are assembled as designed; generation is bounded and flagged; empty
text makes no network calls; voices are encoded once with fallback; mood markers select the mood clip
and never reach the tokenizer; a very large ratio is clamped; dispose and argument validation
behave as documented; a stuck token is capped at the configured run length, a cap of zero disables
the guard, and the stop token is never blocked.

#### Test Scenarios

##### Scripted tokens assemble prompt and decoder input

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_ScriptedTokens_AssemblesPromptAndDecoderInput`

##### A model that never stops is bounded and flagged

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_NeverStops_IsBoundedAndFlagged`

##### The proportional bound stops a runaway early

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_ProportionalBound_StopsRunawayEarly`

##### A huge ratio is clamped to the absolute cap

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_HugeRatio_ClampsToMaxNewTokens`

##### A stuck token is capped at the run length

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_StuckToken_IsCappedAtRunLength`

##### No run exceeds the cap anywhere

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_StuckToken_NeverExceedsCapAnywhere`

##### A cap of zero disables the guard

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_CapZero_AllowsUnboundedRepeats`

##### The stop token is never blocked

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_StopToken_IsNeverBlocked`

##### Empty text returns empty audio without network calls

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_EmptyText_ReturnsEmptyAudioWithoutNetworkCalls`

##### Voices are encoded once and fall back

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_Voices_AreEncodedOnceAndFallBack`

##### Mood markers select the mood clip and never reach the tokenizer

**Tests**: `OnnxChatterboxTurboSynthesisEngine_Generate_MoodMarker_UsesMoodClipOnceAndHidesMarker`,
`OnnxChatterboxTurboSynthesisEngine_Generate_MoodMissing_FallsBackToBaseClip`,
`OnnxChatterboxTurboSynthesisEngine_Generate_MarkerOnlyText_ReturnsEmptyAudio`,
`OnnxChatterboxTurboSynthesisEngine_Generate_MarkedSegments_GuardsApplyPerSegment`,
`OnnxChatterboxTurboSynthesisEngine_Generate_RenderedPlan_OrderedClipsAndNoMarkerInTokens`

##### Dispose disposes networks and rejects generation

**Test**: `OnnxChatterboxTurboSynthesisEngine_Dispose_DisposesNetworksAndRejectsGenerate`

##### Invalid constructor arguments throw

**Test**: `OnnxChatterboxTurboSynthesisEngine_Constructor_InvalidArguments_Throw`

##### Null text throws

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_NullText_ThrowsArgumentNullException`
