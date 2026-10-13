# SpeechOnnxChatterboxTurbo System Verification Design

This document describes the system-level verification strategy for the SpeechOnnxChatterboxTurbo
library.

## Verification Approach

SpeechOnnxChatterboxTurbo is verified at system level by `SpeechOnnxChatterboxTurboTests`, which composes
the system as a host does: it registers the model with a real Speech `SpeechModelCatalog` through the
`AddChatterboxTurboModels()` extension method, loads it through `SpeechSynthesizerFactory.LoadAsync`, and
synthesizes text through the Speech library's real synthesis pipeline over a synthetic install: a model
built from tiny synthetic ONNX graphs written by the test project.

The synthetic encoder emits, as the conditioning prompt, the reference clip's peak amplitude times 1000, and
the synthetic decoder echoes its speech tokens as the waveform. The first three samples of every synthesized
segment are therefore the prompt tokens of the clip actually used (for example 250 for a clip of constant
level 0.25, 500 for 0.5) and the last three are the silence token, so the voice or mood clip selected by the
pipeline is observable at the system boundary without a real model. The synthetic language model never emits
the end-of-speech token, which makes the generation bound observable.

Below the system level, the model's identity, download descriptor, variants and voices, the
tokenizer (against golden ids generated from the real Hugging Face tokenizer), the reference-voice
normalization, the ONNX networks (against synthetic graphs), the generation loop (against fake
networks), the token selector and the tag-rendering capability profile are each verified
deterministically with no network access and no large model download.

Automated coverage **does not** extend to the intelligibility of speech produced by the real
production model or to audio hardware. Real-model quality is verified manually before release, with
the model files installed locally.

System tests reside in `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SpeechOnnxChatterboxTurboTests.cs`
and unit tests in the subsystem folders of the same project.

The only simulated external interface is ONNX Runtime's model content: synthetic ONNX graphs (an encoder, an
embedder, a language model with a key/value cache and a decoder) are generated at test time by a small
in-test ONNX writer, and the system tests replace the encoder with one whose prompt depends on the reference
clip, as described above. The generation loop is additionally unit tested against a fake
`IChatterboxTurboNetworks`. Reference clips are generated in memory or written as WAV files in a scratch
directory. The system tests share the non-parallel collection used by the other ONNX-session tests.

## Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: no network access, no audio hardware, no real model files
- **Isolation**: tests that create backends use fresh scratch directories; tests that observe ONNX
  sessions share a non-parallel collection because the observation hook is process-wide

## Acceptance Criteria

A system-level run passes when every scenario below passes and every subsystem suite passes with
no network access. The automated boundary is honest: real-model intelligibility is a manual
pre-release check. The registration extension's
`SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_DefaultsToCatalogStoreVoices`
is owned by the extension unit's verification document, not by a system scenario.

## Test Scenarios

### AddChatterboxTurboModels registers the model and returns the catalog

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_AddChatterboxTurboModels_RegistersModelAndReturnsCatalog`

Registers the model through the extension method on a real catalog; the same catalog is returned and the
one model is enumerable as a synthesis model.

### The registered model is a downloadable synthesis model with variants and voices

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_RegisteredModel_IsDownloadableWithVariantsAndVoices`

The model's role is synthesis, its download descriptor lists checksum-backed files, a non-default precision
variant has a different id, and the voice choice lists the supplied voices.

### Text is tokenized without a tokenizer download

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_EmbeddedTokenizer_SynthesizesWithoutTokenizerDownload`

The install holds only the four graphs and the download descriptor lists no tokenizer file, yet text is
synthesized. Exact token ids are verified against the reference tokenizer at unit level.

### A synthetic install synthesizes audio through the Speech pipeline

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_SyntheticInstall_SynthesizesThroughSpeechPipeline`

The factory returns an available engine and a sentence yields one 24 kHz segment of the expected length.

### Reference clips of any rate and channel count become voices

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_VoiceCloning_AcceptsAnyRateAndChannelCount`

A stereo 48 kHz in-memory clip and a stereo 44.1 kHz WAV file are normalized before conditioning, so both
reach the encoder at the same level. FLAC decoding is verified at unit level, not at system level.

### The selected voice's clip conditions the speech

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_VoiceConditioning_UsesSelectedVoiceClip`

The default and first voices are conditioned on their clip (prompt 250) and the second voice on its own
clip (prompt 500).

### Store voices are offered by default

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_StoreVoices_OfferedByDefaultAndSpoken`

A clip in the catalog store's user voice folder is listed after the explicit voice with no host code and is
spoken when selected.

### A damaged store clip does not block the model or other voices

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_StoreVoicesResilience_DamagedClipDoesNotBlockOtherVoices`

With a damaged clip in the user folder, registration and loading succeed, the damaged voice is listed and
the explicit voice is spoken.

### Mood tags select the mood clip with a base-clip fallback

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_MoodVoices_UseMoodClipWithBaseFallback`

Text after `[sad]` is conditioned on the voice's sad clip (prompt 500), plain text on the base clip (250),
and a voice without a sad clip uses its base clip (250).

### Mood markers are never spoken

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_MoodMarkerHidden_NeverSpokenOrSynthesized`

Text consisting only of caller-supplied marker characters yields no audio, a marker before text produces
the same audio as the text alone, and a mood tag adds no speech of its own.

### Tag rendering: silence, inline sound or omission

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_NativeTagRendering_SilenceInlineOrOmitted`

A sigh becomes a silent segment between two spoken segments, a native laugh stays inside one spoken
segment, and a tag with no usable sound is omitted without splitting the sentence. The exact tokens
rendered are verified at unit level.

### A trailing tag is kept with speech

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_TagPlacement_TrailingTagIsKeptWithSpeech`

Text that ends with a native tag (`That was really funny [laughs]`) yields a single spoken segment, so the tag
is not left as a segment of its own with no speech after it.

### Long text is split and every sentence is spoken

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_LongTextSegmentation_SplitsAndSpeaksEverySentence`

Ten sentences, well over the 200-character budget in total, yield several segments, each of the full synthetic
length, so no sentence is dropped.

### Runaway generation is bounded

**Test**: `SpeechOnnxChatterboxTurbo_SystemIntegration_RunawayProtection_BoundsGenerationWithoutException`

With a language model that never emits end-of-speech, every segment contains exactly the prompt, the
maximum number of generated tokens and the trailing silence, and no exception is thrown.
