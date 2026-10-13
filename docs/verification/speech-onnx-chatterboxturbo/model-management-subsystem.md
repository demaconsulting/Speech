## ModelManagementSubsystem Verification

### Verification Approach

The SpeechOnnxChatterboxTurbo ModelManagementSubsystem is verified by automated tests in
`test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/ModelManagementSubsystem/`. The model and its
options are in `OnnxChatterboxTurboEnglishSynthesisModelTests.cs`, the registration extension in
`SpeechModelCatalogChatterboxTurboExtensionsTests.cs` (owned by the extension unit document, including
`AddChatterboxTurboModels_DefaultsToCatalogStoreVoices`), the tokenizer in `ByteLevelBpeTokenizerTests.cs`
and the reference voice in `ChatterboxTurboReferenceVoiceTests.cs`.
`ChatterboxTurboModelOptions` is not a separate software unit; its options are verified within the model unit
document.

Unit tests are the primary evidence. The subsystem scenarios below add the integration approach: the model,
the reference-voice type and the real Speech `ReferenceVoiceLibrary` over a scratch store are composed together
(the registration extension with a real `SpeechModelCatalog`, and `CreateBackend` over synthetic ONNX graphs
written by the test project). The mocking boundary is the ONNX graphs only (replaced by tiny synthetic graphs)
and the model store (a scratch directory); nothing else is mocked and no real model is required.

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
`chatterbox-turbo-reference-voice.md`. The subsystem scenarios below name the cross-unit behaviors.

#### Registration returns the same catalog

**Test**: `SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_RegistersModelAndReturnsCatalog`

Registering the model through the extension method returns the catalog it was called on, so calls chain.

#### Store voices merge after explicit voices and the first id wins

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_StoreVoices_MergedAfterExplicitAndDirectoryFirstIdWins`

Explicit voices, directory voices and library voices are listed in that order, and a library voice that repeats
an earlier id is skipped.

#### A corrupt library clip fails only when used

**Tests**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_CorruptStoreVoice_DoesNotFailConstruction` and
`ChatterboxTurboReferenceVoice_FromFileLazy_CorruptFile_FailsOnFirstUseNamingFile`

Construction and listing decode no clip; the damaged clip fails with its file named only when its voice is first used.

#### Backend creation fails when the install is unusable

**Tests**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_NoVoices_Throws`,
`OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_EmptyDirectory_ThrowsArgumentException`,
`OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_MissingFile_ThrowsFileNotFound` and
`OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_InvalidGraph_DisposesSessionsAndThrows`

With no voices `CreateBackend` throws `InvalidOperationException`, with an empty directory path
`ArgumentException`, with a missing file `FileNotFoundException`
before any session is created, and with an invalid graph `InvalidOperationException` after disposing the sessions
it created.
