## SynthesisSubsystem Verification

### Verification Approach

The SpeechOnnxChatterboxTurbo SynthesisSubsystem is verified by automated unit tests in
`test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SynthesisSubsystem/`. The generation loop is
tested against a fake `IChatterboxTurboNetworks`; the ONNX networks are tested against synthetic
graphs written by the test project (an encoder, an embedder using Mod and Gather, a language model
using MatMul and Concat with a key/value cache, and a decoder using Cast); the token selector and
capability profile are pure functions tested directly. End-to-end construction through
`CreateBackend` is covered in the ModelManagementSubsystem verification.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Dependencies**: no network, no audio hardware, no real model files; tests observing ONNX
  sessions run in a non-parallel collection

### Acceptance Criteria

The subsystem is accepted when every unit suite passes: prompt assembly, bounding, run-length capping,
mood conditioning and voice caching in the engine; tensor contracts, thread configuration and disposal in the
networks; penalized selection that is safe for NaN and infinite logits; and tag and mood rendering and
segmentation in the capability profile.

### Test Scenarios

See the unit verification documents: `onnx-chatterboxturbo-synthesis-engine.md`,
`onnx-chatterboxturbo-networks.md`, `chatterbox-turbo-token-selector.md` and
`chatterbox-turbo-capability-profile.md`.

#### End-to-end synthesis through a synthetic install

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_SyntheticInstall_SynthesizesAudio`

Verifies that a model installed from tiny synthetic graphs builds a backend through `CreateBackend` and
returns audio at the model's 24 kHz output rate for a sentence of text.

#### Mood conditioning

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_MoodMarker_UsesMoodClipOnceAndHidesMarker`

Verifies that text led by a mood marker is conditioned on the voice's clip for that mood, the clip is
encoded once, and the marker never reaches the tokenizer or the generated speech.

#### Mood fallback to the base clip

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_MoodMissing_FallsBackToBaseClip`

Verifies that a mood the selected voice does not have is spoken with the voice's base clip rather than failing.

#### Voice fallback for an unknown speaker id

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_UnknownSpeaker_UsesFirstVoiceClip`

Verifies that an out-of-range speaker id is spoken with the first voice's clip.

#### Runaway generation is truncated, not failed

**Tests**: `OnnxChatterboxTurboSynthesisEngine_Generate_NeverStops_IsBoundedAndFlagged` and
`OnnxChatterboxTurboSynthesisEngine_Generate_ProportionalBound_StopsRunawayEarly`

Verifies that a language model that never emits the stop token yields audio after a bounded number of
steps, flagged as truncated, with no exception, and that the text-length-proportional bound stops it
before the absolute cap.

#### Repeated speech token is capped

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_StuckToken_IsCappedAtRunLength`

Verifies that an identical speech token repeated beyond the configured maximum is excluded from the next
selection so generation can progress.

#### Tag rendering and mood join silence

**Tests**: `ChatterboxTurboCapabilityProfile_Render_MidSentenceLaugh_StaysInlineWithNativeToken` and
`ChatterboxTurboCapabilityProfile_Render_MoodChange_AddsJoinSilenceOnlyAtChanges`

Verifies that a native tag stays inline with the speech around it and that the configured join silence
is added only where the mood changes.
