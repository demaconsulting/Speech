# SpeechOnnxChatterboxTurbo System Verification Design

This document describes the system-level verification strategy for the SpeechOnnxChatterboxTurbo
library.

## Verification Approach

SpeechOnnxChatterboxTurbo is verified at system level through its public entry point, the
`AddChatterboxTurboModels()` extension method, exercised against a real Speech `SpeechModelCatalog`
as a host composes it, and through the synthetic-install end-to-end path: a model built from
tiny synthetic ONNX graphs written by the test project synthesizes audio through `CreateBackend`.

Below the system level, the model's identity, download descriptor, variants and voices, the
tokenizer (against golden ids generated from the real Hugging Face tokenizer), the reference-voice
normalization, the ONNX networks (against synthetic graphs), the generation loop (against fake
networks), the token selector and the tag-rendering capability profile are each verified
deterministically with no network access and no large model download.

Automated coverage **does not** extend to the intelligibility of speech produced by the real
production model or to audio hardware. Real-model quality is verified manually before release, with
the model files installed locally.

System and unit tests reside in `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: no network access, no audio hardware, no real model files
- **Isolation**: tests that create backends use fresh scratch directories; tests that observe ONNX
  sessions share a non-parallel collection because the observation hook is process-wide

## External Interface Simulation

Synthetic ONNX graphs (an encoder, an embedder, a language model with a key/value cache and a
decoder) are generated at test time by a small in-test ONNX writer. The generation loop is
additionally tested against a fake `IChatterboxTurboNetworks`.

## Acceptance Criteria

A system-level run passes when every scenario above passes and every subsystem suite passes with
no network access. The automated boundary is honest: real-model intelligibility is a manual
pre-release check.

## Test Scenarios

### AddChatterboxTurboModels registers the model and returns the catalog

**Test**: `SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_RegistersModelAndReturnsCatalog`

### The registered model declares native-tag synthesis metadata

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Metadata_DescribesNativeTagSynthesisModel`

### Tokenization matches the reference tokenizer

**Test**: `ByteLevelBpeTokenizer_Encode_GoldenText_MatchesReferenceIds`

### A synthetic install synthesizes audio

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_SyntheticInstall_SynthesizesAudio`

### Voice loading and selection

**Tests**: `ChatterboxTurboReferenceVoice_FromFile_StereoWav_LoadsVoice`,
`OnnxChatterboxTurboSynthesisEngine_Generate_Voices_AreEncodedOnceAndFallBack`

### Store voices (user folder and voice pack) are offered by default

**Tests**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_StoreVoices_MergedAfterExplicitAndDirectoryFirstIdWins`,
`SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_DefaultsToCatalogStoreVoices`

### Mood tags select the mood clip with a base-clip fallback

**Tests**: `ChatterboxTurboCapabilityProfile_Render_MoodTag_MarksFollowingTextWithMood`,
`OnnxChatterboxTurboSynthesisEngine_Generate_MoodMarker_UsesMoodClipOnceAndHidesMarker`,
`OnnxChatterboxTurboSynthesisEngine_Generate_MoodMissing_FallsBackToBaseClip`

### Tag rendering

**Tests**: `ChatterboxTurboCapabilityProfile_Render_MidSentenceLaugh_StaysInlineWithNativeToken`,
`ChatterboxTurboCapabilityProfile_Render_Sigh_BecomesSilenceSegment`

### Runaway generation is bounded

**Test**: `OnnxChatterboxTurboSynthesisEngine_Generate_NeverStops_IsBoundedAndFlagged`
