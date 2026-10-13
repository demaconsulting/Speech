### OnnxChatterboxTurboEnglishSynthesisModel Verification

#### Verification Approach

Verified by `OnnxChatterboxTurboEnglishSynthesisModelTests.cs`, which also owns the verification of
`ChatterboxTurboModelOptions` (not a separate unit). Backend creation uses synthetic ONNX
graphs written into a scratch install directory, and a static test hook confirms sessions are
disposed on failure and that none is created for a missing file. The language and the auxiliary-graph
variants are checked by id and file name; the English language is a fixed property of the model
(its display name is asserted) and the graphs' content is verified in the networks unit. The default
descriptor and variant tests assert that every file has a non-empty 64-character hexadecimal SHA-256
checksum. The invalid-graph test reads the private `InferenceSession._disposed` field by
reflection to confirm disposal; that read must be revisited whenever the ONNX Runtime package is upgraded,
because the field is an implementation detail of the library.

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

##### Every auxiliary variant loads the matching graph files

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_AuxiliaryVariants_LoadMatchingGraphFiles`

A theory over the Fp16 and Fp32 language models and the Q4, Fp16 and Fp32 auxiliary graphs: synthetic graphs
are written with the matching file-name suffix, and the session hook plus the descriptor paths confirm that
exactly the selected files were loaded.

##### Voice parameter reflects supplied voices

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Parameters_ReflectSuppliedVoices`

##### Voice id maps to index

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_ResolveSpeakerId_MapsVoiceIdToIndex`

##### An unknown or absent voice id selects the first voice

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_ResolveSpeakerId_UnknownOrAbsentVoice_FallsBackToFirstVoice`

##### A voice directory loads WAV and FLAC files

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_VoiceDirectory_LoadsWavAndFlacFiles`

##### A missing voice directory is rejected

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_MissingVoiceDirectory_ThrowsDirectoryNotFound`

##### A damaged voice file does not fail construction but fails at first use

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DamagedVoiceFile_DoesNotFailConstruction`

The test also builds a backend from a synthetic install and asserts that generating with the damaged voice
throws `InvalidDataException` naming the file.

##### Duplicate explicit or directory voice ids are rejected

**Tests**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DuplicateExplicitIds_ThrowsArgumentException`,
`OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DirectoryVoiceDuplicatesExplicitId_ThrowsArgumentException`
and `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DirectoryWavAndFlacSameStem_ThrowsArgumentException`

##### Backend creation synthesizes from a synthetic install

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_SyntheticInstall_SynthesizesAudio`

##### Backend creation fails without voices

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_NoVoices_Throws`

##### Backend creation rejects a null or empty model directory path

**Tests**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_NullDirectory_ThrowsArgumentNullException`
and `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_EmptyDirectory_ThrowsArgumentException`

##### Backend creation reports a missing file

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_MissingFile_ThrowsFileNotFound`

##### Backend creation disposes sessions for an invalid graph

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_InvalidGraph_DisposesSessionsAndThrows`

##### Options have safe defaults

**Tests**, one per default:
`ChatterboxTurboModelOptions_Defaults_LanguageModelVariantIsFp16AndAuxiliaryIsQ4`,
`ChatterboxTurboModelOptions_Defaults_MaxNewTokensIs1024`,
`ChatterboxTurboModelOptions_Defaults_MaxAudioTokensPerTextTokenIs25`,
`ChatterboxTurboModelOptions_Defaults_MaxConsecutiveSpeechTokenRepeatsIs12`,
`ChatterboxTurboModelOptions_Defaults_IntraOpThreadsIsHalfTheProcessorsBetweenOneAndSix` (which also
assigns 64 to prove the 1-to-6 range is a default and not a clamp),
`ChatterboxTurboModelOptions_Defaults_EndOfSentenceTagPolicyIsMoveBeforeFinalWord`,
`ChatterboxTurboModelOptions_Defaults_NoExplicitVoices`,
`ChatterboxTurboModelOptions_Defaults_MoodJoinSilenceIs150Milliseconds`,
`ChatterboxTurboModelOptions_Defaults_StoreVoicesAreIncluded` and
`ChatterboxTurboModelOptions_Defaults_NoDirectoryLibraryOrDiagnostics`.

##### Options validate on assignment

**Tests**: `ChatterboxTurboModelOptions_VariantSetters_UndefinedValue_ThrowsArgumentOutOfRangeException`,
`ChatterboxTurboModelOptions_EndOfSentenceTagPolicy_UndefinedValue_ThrowsArgumentOutOfRangeException`,
`ChatterboxTurboModelOptions_Properties_OutOfRangeValues_ThrowArgumentOutOfRangeException` and
`ChatterboxTurboModelOptions_ReferenceVoices_Null_ThrowsArgumentNullException`

The undefined-value tests also assert the previous value is kept.

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

The test that verifies this default is owned by the extension unit's verification document
(`speech-model-catalog-chatterbox-turbo-extensions.md`) and is linked here only as evidence for the
store-voice default; it is not repeated here.

##### A default library is used only when the options name none

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DefaultLibrary_UsedOnlyWhenOptionsNameNone`

The model-level counterpart: the default library supplied by the extension is used when the options name no
library, and an options-supplied library wins.
