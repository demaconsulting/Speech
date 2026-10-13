### OnnxChatterboxTurboSynthesisEngine Verification

#### Verification Approach

Verified by `OnnxChatterboxTurboSynthesisEngineTests.cs` against a fake `IChatterboxTurboNetworks`
that records calls and replays scripted logits, so the algorithm is checked without ONNX Runtime.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: no network I/O and no files; the fake networks record calls and replay scripted logits
- **Mood-marker setup**: the mood scenarios build segment text led by the internal marker characters
  (as `ChatterboxTurboCapabilityProfile` would write them) and give the voice a distinct mood clip, so
  the clip passed to the fake encoder shows which clip conditioned the segment

#### Acceptance Criteria

The prompt and decoder input are assembled as designed; generation is bounded and flagged; empty
text makes no network calls; voices are encoded once, each with its own clip, and an unknown speaker uses
the first voice; mood markers select the mood clip and never reach the tokenizer; a very large ratio is
clamped; the speed argument is ignored; dispose and argument validation behave as documented, including a
dispose during generation that stops the generation and waits for it; a stuck token is capped at the
configured run length, a cap of zero disables the guard, and a capped run still ends at the stop token
without being flagged truncated.

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

##### A run that reaches the cap still ends at the stop token

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_StopAfterCappedRun_EndsWithoutTruncation`

Scripted tokens: two identical tokens then the stop token, with a cap of two. Generation ends at the stop
token and is not flagged truncated. That the selector never blocks the stop token is verified at unit level in
`chatterbox-turbo-token-selector.md`.

##### Empty text returns empty audio without network calls

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_EmptyText_ReturnsEmptyAudioWithoutNetworkCalls`

##### Voices are encoded once and fall back

**Tests**: `OnnxChatterboxTurboSynthesisEngine_Generate_Voices_AreEncodedOnceAndFallBack` (voices with
distinct clips are encoded once each, in first-use order) and
`OnnxChatterboxTurboSynthesisEngine_Generate_UnknownSpeaker_UsesFirstVoiceClip`

##### Mood markers select the mood clip and never reach the tokenizer

**Tests**: `OnnxChatterboxTurboSynthesisEngine_Generate_MoodMarker_UsesMoodClipOnceAndHidesMarker`,
`OnnxChatterboxTurboSynthesisEngine_Generate_MoodMissing_FallsBackToBaseClip`,
`OnnxChatterboxTurboSynthesisEngine_Generate_MarkerOnlyText_ReturnsEmptyAudio`,
`OnnxChatterboxTurboSynthesisEngine_Generate_MarkedSegments_GuardsApplyPerSegment`,
`OnnxChatterboxTurboSynthesisEngine_Generate_RenderedPlan_OrderedClipsAndNoMarkerInTokens`

##### The engine reports a 24 kHz sample rate

**Test**: `OnnxChatterboxTurboSynthesisEngine_SampleRate_Is24Kilohertz`

##### The speed argument is ignored

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_Speed_IsIgnored`

Two calls with different speeds return audio of identical length and sample rate.

##### Dispose disposes networks and rejects generation

**Tests**: `OnnxChatterboxTurboSynthesisEngine_Dispose_DisposesNetworksAndRejectsGenerate` and
`OnnxChatterboxTurboSynthesisEngine_Dispose_DuringGeneration_StopsGenerationAndWaitsForIt`

The second test blocks the first language-model step, disposes from another thread, and checks that the
disposal waits for the generation, the generation stops at its next step with `ObjectDisposedException` and
the networks are disposed exactly once.

##### Invalid constructor arguments throw

**Test**: `OnnxChatterboxTurboSynthesisEngine_Constructor_InvalidArguments_Throw`

##### Null text throws

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_NullText_ThrowsArgumentNullException`
