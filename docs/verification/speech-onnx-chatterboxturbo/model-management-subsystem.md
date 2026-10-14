## ModelManagementSubsystem Verification

### Verification Approach

The SpeechOnnxChatterboxTurbo ModelManagementSubsystem is verified by automated tests in
`test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/ModelManagementSubsystem/`. The model and its
options are in `OnnxChatterboxTurboEnglishSynthesisModelTests.cs`, the registration extension in
`SpeechModelCatalogChatterboxTurboExtensionsTests.cs` (owned by the extension unit document, including the
test of the catalog-store voice default), the tokenizer in `ByteLevelBpeTokenizerTests.cs`
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
`OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_NullDirectory_ThrowsArgumentNullException`,
`OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_EmptyDirectory_ThrowsArgumentException`,
`OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_MissingFile_ThrowsFileNotFound` and
`OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_InvalidGraph_DisposesSessionsAndThrows`

With no voices `CreateBackend` throws `InvalidOperationException`, with a null directory path
`ArgumentNullException`, with an empty one `ArgumentException`, with a missing file `FileNotFoundException`
before any session is created, and with an invalid graph `InvalidOperationException` after disposing the sessions
it created.

#### Tokenizer output matches the reference ids

**Tests**: `ByteLevelBpeTokenizer_Encode_GoldenText_MatchesReferenceIds` and
`ByteLevelBpeTokenizer_GoldenCases_ContainManyCases` (see `byte-level-bpe-tokenizer.md`)

The embedded tokenizer reproduces the ids of the reference tokenizer for the golden texts, so the language model
receives exactly the ids it was trained with.

#### Reference voices are normalized

**Tests**: `ChatterboxTurboReferenceVoice_FromSamples_Mono24k_KeepsSamples`,
`ChatterboxTurboReferenceVoice_FromSamples_Stereo48k_IsDownMixedAndResampled`,
`ChatterboxTurboReferenceVoice_FromSamples_TooShort_Throws` and
`ChatterboxTurboReferenceVoice_FromSamples_TooLong_IsTrimmed` (see `chatterbox-turbo-reference-voice.md`)

Every voice, whatever its source rate and channel count, is mono 24 kHz, at least 1 second and at most 30 seconds.

#### Options validate on assignment

**Tests**: `ChatterboxTurboModelOptions_VariantSetters_UndefinedValue_ThrowsArgumentOutOfRangeException`,
`ChatterboxTurboModelOptions_EndOfSentenceTagPolicy_UndefinedValue_ThrowsArgumentOutOfRangeException`,
`ChatterboxTurboModelOptions_Properties_OutOfRangeValues_ThrowArgumentOutOfRangeException` and
`ChatterboxTurboModelOptions_ReferenceVoices_Null_ThrowsArgumentNullException`

Undefined variants or end-of-sentence policy and out-of-range numbers throw `ArgumentOutOfRangeException`; a null voice
list throws `ArgumentNullException`.

#### Variant selection chooses the id, the files and the loaded graphs

**Tests**: `OnnxChatterboxTurboEnglishSynthesisModel_Variants_HaveDistinctIdsAndCompleteFileSets` and
`OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_AuxiliaryVariants_LoadMatchingGraphFiles`

Each combination has its own id and file set, and backend creation loads exactly the selected graph files.

#### Variants and voices compose through the registration extension

**Test**: `SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_VariantsAndVoices_AreComposed`

The cross-unit scenario: a real `SpeechModelCatalog` receives the model through `AddChatterboxTurboModels` with a
non-default variant pair and explicit voices, and the registered model carries that variant's id, its eight-file
descriptor and the supplied voice choice.
