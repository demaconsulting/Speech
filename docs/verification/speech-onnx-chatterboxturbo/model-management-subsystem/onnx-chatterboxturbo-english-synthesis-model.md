### OnnxChatterboxTurboEnglishSynthesisModel Verification

#### Verification Approach

Verified by `OnnxChatterboxTurboEnglishSynthesisModelTests.cs`, which also owns the verification of
`ChatterboxTurboModelOptions` (not a separate unit). Backend creation uses synthetic ONNX graphs written into a scratch
install directory, and a static session-factory seam substitutes a derived session that records its own disposal,
confirming no session is left open when backend creation fails. The fixed graphs (the fp16 language model and the q4
auxiliary graphs) are checked by file name, and by synthesizing through synthetic graphs of exactly those names; the
English language is a fixed property of the model (its display name is asserted) and the graphs' content is verified in
the networks unit. The bundle tests read the public `Bundles` and the internal `BundleContents` and `InstalledFiles`
(through `InternalsVisibleTo`); the wrong-file test stages well-formed zips whose `model.json` declares every file at
its compiled pin but whose entries hold other bytes. The installed-file tests assert that every file has a non-empty
64-character hexadecimal SHA-256 checksum. The
invalid-graph test observes disposal through the public `Dispose(bool)` override point of the derived session, so it
depends on no ONNX Runtime private state.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: scratch directories; non-parallel collection for the session-factory seam

#### Acceptance Criteria

Metadata (including the display name and the id `chatterbox-turbo-onnx-en`) is as declared; the model installs eight
files with checksums from two pinned component bundles (one language-model and one auxiliary bundle) on the project's
`models` release and downloads nothing else; both bundles pin their files with their original Hugging Face addresses
(provenance only); installing a bundle whose files do not match their pins throws `InvalidDataException`; the voice
choice reflects the
supplied voices; duplicate explicit or directory ids are rejected; library voices (user folder and installed
pack) follow explicit and directory voices, are loaded lazily, are skipped with an Info diagnostic when hidden
and are disambiguated with a `(user)` or `(pack)` label suffix; `IncludeStoreVoices = false` ignores the
library and the catalog extension defaults to the catalog store's library; the mood join silence option
reaches the capability profile; backend creation succeeds for a synthetic install and fails cleanly without
voices, with a missing file or with an invalid graph; options have safe defaults and validate.

#### Test Scenarios

##### Metadata describes a native-tag synthesis model

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Metadata_DescribesNativeTagSynthesisModel`

##### The model installs eight files

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_InstalledFiles_ListsEightFiles`

##### The model downloads its two pinned bundles from the models release

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_DownloadDescriptor_IsModelsReleaseBundle`

##### The model declares one language-model and one auxiliary bundle covering its files

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Bundles_DeclareOneLmAndOneAuxBundle`

##### Both component bundles pin their vendor files

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_BundleContents_PinsEveryVendorFile`

##### Installing a bundle whose files do not match their pins fails

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_InstallAsync_BundleWithWrongFile_Throws`

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

##### Duplicate explicit ids are rejected

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DuplicateExplicitIds_ThrowsArgumentException`

##### Duplicate directory voice ids are rejected

**Tests**:
`OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DirectoryVoiceDuplicatesExplicitId_ThrowsArgumentException`,
`OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DirectoryWavAndFlacSameStem_ThrowsArgumentException`

##### Repeated library label clashes get unique labels

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_RepeatedLabelClashes_AssignsUniqueLabels`

Several library voices sharing one display name receive numbered source suffixes, so every label in the voice
list is unique.

##### A damaged store clip fails at first use only

**Test**:
`OnnxChatterboxTurboEnglishSynthesisModel_Generate_CorruptStoreVoice_FailsAtFirstUseNamingFileAndOtherVoiceWorks`

The damaged voice's first use fails naming its file, while another voice still synthesizes.

##### Backend creation synthesizes from a synthetic install

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_SyntheticInstall_SynthesizesAudio`

Only synthetic graphs named like the fixed graphs (`language_model_fp16` and the `_q4` auxiliary graphs) are installed,
so the backend loads and synthesizes only when the model opens exactly those graphs.

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

**Tests**: `ChatterboxTurboModelOptions_EndOfSentenceTagPolicy_UndefinedValue_ThrowsArgumentOutOfRangeException`,
`ChatterboxTurboModelOptions_Properties_OutOfRangeValues_ThrowArgumentOutOfRangeException` and
`ChatterboxTurboModelOptions_ReferenceVoices_Null_ThrowsArgumentNullException`

The undefined-value test also asserts the previous value is kept.

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
