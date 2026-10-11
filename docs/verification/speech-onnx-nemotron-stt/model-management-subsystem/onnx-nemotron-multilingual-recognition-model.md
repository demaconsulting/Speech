### OnnxNemotronMultilingualRecognitionModel Verification

#### Verification Approach

Verified by deterministic unit tests that inspect the declared metadata and exercise
`CreateBackend` against scratch directories; no network access or real download is used.

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
(`OnnxNemotronMultilingualRecognitionModelTests.cs`). One test uses the real installed model and
self-skips when it is absent.

#### Acceptance Criteria

The model declares id `nemotron-3.5-asr-streaming-0.6b-onnx-int4`, the Recognition role, no audio
tag support, mono 16000 Hz, and the license name and URL; lists eleven files with well-formed
SHA-256 checksums; exposes one `language` choice parameter (`en-US` default, `en-GB`); and
`CreateBackend` throws for an empty directory, missing files, or a vocabulary lacking the locale.
The license value is **UNVERIFIED** against the live upstream model card and must be confirmed
before release.

#### Test Scenarios

##### Metadata is declared

**Test**: `Metadata_IsDeclared`

##### Download descriptor lists eleven files

**Test**: `DownloadDescriptor_ListsElevenFiles`

##### Language parameter is a choice of en-US and en-GB

**Test**: `Parameters_LanguageChoice`

##### CreateBackend with an empty directory throws

**Test**: `CreateBackend_EmptyDirectory_Throws`

##### CreateBackend with missing files throws

**Test**: `CreateBackend_MissingFiles_Throws`

##### CreateBackend with a vocabulary lacking the locale throws

**Test**: `CreateBackend_VocabularyWithoutLocale_Throws`

##### CreateBackend with the real model transcribes (skipped when absent)

**Test**: `CreateBackend_RealModel_TranscribesWhenPresent`

This test is environment-dependent and is not linked to a requirement.
