### OnnxChatterboxTurboEnglishSynthesisModel Verification

#### Verification Approach

Verified by `OnnxChatterboxTurboEnglishSynthesisModelTests.cs`. Backend creation uses synthetic ONNX
graphs written into a scratch install directory, and a static test hook confirms sessions are
disposed on failure.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: scratch directories; non-parallel collection for the session hook

#### Acceptance Criteria

Metadata is as declared; the default descriptor lists eight files with checksums; every variant
combination has a distinct id and complete file set; the voice choice reflects the supplied voices;
backend creation succeeds for a synthetic install and fails cleanly without voices, with a missing
file or with an invalid graph; options have safe defaults and validate.

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

##### Backend creation synthesizes from a synthetic install

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_SyntheticInstall_SynthesizesAudio`

##### Backend creation fails without voices

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_NoVoices_Throws`

##### Backend creation reports a missing file

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_MissingFile_ThrowsFileNotFound`

##### Backend creation disposes sessions for an invalid graph

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_InvalidGraph_DisposesSessionsAndThrows`

##### Options have safe defaults and validate

**Test**: `ChatterboxTurboModelOptions_Properties_HaveSafeDefaultsAndValidate`

##### Library voices merge after explicit and directory voices, first id wins

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_StoreVoices_MergedAfterExplicitAndDirectoryFirstIdWins`

##### A corrupt library voice does not fail construction

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_CorruptStoreVoice_DoesNotFailConstruction`

##### IncludeStoreVoices false ignores the library

**Test**: `OnnxChatterboxTurboEnglishSynthesisModel_Constructor_IncludeStoreVoicesFalse_IgnoresLibrary`

##### The catalog extension defaults to the catalog store's library

**Test**: `SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_DefaultsToCatalogStoreVoices`
