## ModelManagementSubsystem Verification

### Verification Approach

The SpeechOnnxChatterboxTurbo ModelManagementSubsystem is verified by automated unit tests in
`test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/ModelManagementSubsystem/`: the model,
options and registration extension in `OnnxChatterboxTurboEnglishSynthesisModelTests.cs`, the
tokenizer in `ByteLevelBpeTokenizerTests.cs` and the reference voice in
`ChatterboxTurboReferenceVoiceTests.cs`. Backend construction is verified against synthetic ONNX
graphs, so no real model is required.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Dependencies**: no network, no audio hardware, no real model files

### Test Scenarios

See the unit verification documents: `onnx-chatterboxturbo-english-synthesis-model.md`,
`byte-level-bpe-tokenizer.md` and `chatterbox-turbo-reference-voice.md`.

#### Registration Extension

**Test**: `SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_RegistersModelAndReturnsCatalog`

Verifies that the extension registers the one model with the supplied options and returns the same
catalog instance.

#### Registration Extension defaults the voice library to the catalog store

**Test**: `SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_DefaultsToCatalogStoreVoices`

Verifies that with no library in the options the model lists the voices of the catalog's store and the
caller's options are not modified.

### Acceptance Criteria

The subsystem is accepted when both registration tests pass and every unit suite passes: the model
metadata, descriptor, variants, voices (explicit, directory, user folder and pack, with duplicate and label
handling) and backend creation behave as documented, the tokenizer reproduces the reference ids, and
reference voices (with lazy and mood clips) are normalized and validated.
