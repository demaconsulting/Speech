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

### Test Scenarios

See the unit verification documents: `onnx-chatterboxturbo-synthesis-engine.md`,
`onnx-chatterboxturbo-networks.md`, `chatterbox-turbo-token-selector.md` and
`chatterbox-turbo-capability-profile.md`.

#### End-to-end synthesis through a synthetic install

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_SyntheticInstall_SynthesizesAudio`

#### Mood conditioning and generation guards

**Tests**: `OnnxChatterboxTurboSynthesisEngine_Generate_MoodMarker_UsesMoodClipOnceAndHidesMarker`,
`OnnxChatterboxTurboSynthesisEngine_Generate_NeverStops_IsBoundedAndFlagged` and
`OnnxChatterboxTurboSynthesisEngine_Generate_StuckToken_IsCappedAtRunLength`

### Acceptance Criteria

The subsystem is accepted when every unit suite passes: prompt assembly, bounding, run-length capping,
mood conditioning and voice caching in the engine; tensor contracts, thread configuration and disposal in the
networks; penalized selection that is safe for NaN and infinite logits; and tag and mood rendering and
segmentation in the capability profile.
