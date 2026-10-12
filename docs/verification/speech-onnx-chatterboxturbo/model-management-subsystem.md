## ModelManagementSubsystem Verification

### Verification Approach

The SpeechOnnxChatterboxTurbo ModelManagementSubsystem is verified by automated unit tests in
`test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/ModelManagementSubsystem/`: the model,
options in `OnnxChatterboxTurboEnglishSynthesisModelTests.cs`, the registration extension in
`SpeechModelCatalogChatterboxTurboExtensionsTests.cs` (see its unit document), the
tokenizer in `ByteLevelBpeTokenizerTests.cs` and the reference voice in
`ChatterboxTurboReferenceVoiceTests.cs`. Backend construction is verified against synthetic ONNX
graphs, so no real model is required. `ChatterboxTurboModelOptions` is not a separate software unit: the
options are verified within the model unit document.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Dependencies**: no network, no audio hardware, no real model files

### Acceptance Criteria

The subsystem is accepted when every unit suite passes: the registration extension, the model
metadata, descriptor, variants, voices (explicit, directory, user folder and pack, with duplicate and label
handling) and backend creation behave as documented, the tokenizer reproduces the reference ids, and
reference voices (with lazy and mood clips) are normalized and validated.

### Test Scenarios

See the unit verification documents: `speech-model-catalog-chatterbox-turbo-extensions.md`,
`onnx-chatterboxturbo-english-synthesis-model.md`, `byte-level-bpe-tokenizer.md` and
`chatterbox-turbo-reference-voice.md`.
