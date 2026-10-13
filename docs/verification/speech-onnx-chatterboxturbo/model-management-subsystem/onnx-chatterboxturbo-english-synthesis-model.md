### OnnxChatterboxTurboEnglishSynthesisModel Verification

#### Verification Approach

Verified by `OnnxChatterboxTurboEnglishSynthesisModelTests.cs`, which also owns the verification of
`ChatterboxTurboModelOptions` (not a separate unit). Backend creation uses synthetic ONNX
graphs written into a scratch install directory, and a static test hook confirms sessions are
disposed on failure and that none is created for a missing file. The language and the auxiliary-graph
variants are checked by id and file name; the English language is a fixed property of the model
(its display name is asserted) and the graphs' content is verified in the networks unit.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: scratch directories; non-parallel collection for the session hook

#### Acceptance Criteria

Metadata (including the display name) is as declared; the default descriptor lists eight files with
checksums; every variant combination has a distinct id and complete file set; the voice choice reflects the
supplied voices; duplicate explicit or directory ids are rejected; library voices (user folder and installed
pack) follow explicit and directory voices, are loaded lazily, are skipped with an Info diagnostic when hidden
and are disambiguated with a `(user)` or `(pack)` label suffix; `IncludeStoreVoices = false` ignores the
library and the catalog extension defaults to the catalog store's library; the mood join silence option
reaches the capability profile; backend creation succeeds for a synthetic install and fails cleanly without
voices, with a missing file or with an invalid graph; options have safe defaults and validate.

#### Test Scenarios

##### Metadata describes a native-tag synthesis model

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Metadata_DescribesNativeTagSynthesisModel`

##### Default descriptor lists eight files

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_DownloadDescriptor_DefaultVariant_ListsEightFiles`

##### Variants have distinct ids and complete file sets

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Variants_HaveDistinctIdsAndCompleteFileSets`

##### The fp32 variant loads the fp32 graph

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_Fp32Variant_UsesFp32Graph`

##### Voice parameter reflects supplied voices

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Parameters_ReflectSuppliedVoices`

##### Voice id maps to index

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_ResolveSpeakerId_MapsVoiceIdToIndex`

##### A voice directory loads WAV and FLAC files

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_VoiceDirectory_LoadsWavAndFlacFiles`

##### A damaged voice file does not fail construction

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DamagedVoiceFile_DoesNotFailConstruction`

##### Duplicate explicit or directory voice ids are rejected

**Tests**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DuplicateExplicitIds_ThrowsArgumentException`,
`OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DirectoryVoiceDuplicatesExplicitId_ThrowsArgumentException`
and `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DirectoryWavAndFlacSameStem_ThrowsArgumentException`

##### Backend creation synthesizes from a synthetic install

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_SyntheticInstall_SynthesizesAudio`

##### Backend creation fails without voices

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_NoVoices_Throws`

##### Backend creation rejects an empty model directory path

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_EmptyDirectory_ThrowsArgumentException`

##### Backend creation reports a missing file

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_MissingFile_ThrowsFileNotFound`

##### Backend creation disposes sessions for an invalid graph

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_InvalidGraph_DisposesSessionsAndThrows`

##### Options have safe defaults and validate

**Tests**: `ChatterboxTurboModelOptions_Properties_HaveSafeDefaultsAndValidate`,
`ChatterboxTurboModelOptions_VariantSetters_UndefinedValue_ThrowsArgumentOutOfRangeException` and
`ChatterboxTurboModelOptions_ReferenceVoices_Null_ThrowsArgumentNullException`

The defaults asserted are Fp16 language model, Q4 auxiliary graphs, 1024 tokens, 25 tokens per text token, 12
repeats, a thread count from 1 to 6, the move-before-final-word policy, no voices, store voices included, no
directory, library or diagnostics, and a 150 ms join silence.

##### Library voices merge after explicit and directory voices, first id wins

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_StoreVoices_MergedAfterExplicitAndDirectoryFirstIdWins`

##### A hidden library voice is skipped with an Info diagnostic

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_LibraryVoiceDuplicate_IsSkippedWithInfoDiagnostic`

##### A clashing user voice label gets a (user) suffix

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_UserVoiceLabelClash_AddsUserSuffix`

##### Pack voices are listed and a clashing label gets a (pack) suffix

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_PackVoices_AreListedWithPackSuffixOnClash`

##### A corrupt library voice does not fail construction

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_CorruptStoreVoice_DoesNotFailConstruction`

##### IncludeStoreVoices false ignores the library

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_IncludeStoreVoicesFalse_IgnoresLibrary`

##### The mood join silence option reaches the capability profile

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CapabilityProfile_UsesMoodJoinSilenceOption`

##### The catalog extension defaults to the catalog store's library

**Test**: `SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_DefaultsToCatalogStoreVoices`
is verified in the extension unit's verification document and is linked here only as evidence for the
store-voice default; its owner is `speech-model-catalog-chatterbox-turbo-extensions.md`.

##### A default library is used only when the options name none

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DefaultLibrary_UsedOnlyWhenOptionsNameNone`

The model-level counterpart: the default library supplied by the extension is used when the options name no
library, and an options-supplied library wins.
