### OnnxNemotronMultilingualRecognitionModel Verification

#### Verification Approach

Verified by deterministic unit tests that inspect the declared metadata and exercise
`CreateBackend` against scratch directories; no network access or real download is used. The
real installed model is exercised by the real-model system tests (see *SpeechOnnxNemotronStt
System Verification*).

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
(`OnnxNemotronMultilingualRecognitionModelTests.cs`), on every target framework; no network or
model files.

#### Acceptance Criteria

The model declares id `nemotron-3.5-asr-streaming-0.6b-onnx-int4`, the Recognition role, no audio tag support, mono
16000 Hz, the license name `OpenMDW-1.1`, and the license URL `https://openmdw.ai/license/1-1/`; downloads exactly one
file, the `nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1.zip` asset on the project's `models` release at its pinned
SHA-256; its bundle pins eleven files with well-formed SHA-256 checksums and their original HTTPS URIs; `InstallAsync`
throws `InvalidDataException` for a well-formed bundle whose files do not match their pins; exposes one `language`
choice parameter (`en-US` default, `en-GB`); `CreateBackend` throws for an empty directory, missing files, or a
vocabulary lacking the locale; and the engine-options overload throws `ArgumentNullException` for null options and
`ArgumentOutOfRangeException` for a negative blank penalty before reading any file. The license is the one the upstream
NVIDIA model card (nvidia/nemotron-3.5-asr-streaming-0.6b) names, verified by the project owner on the live Hugging Face
card; the MIT tag of the ONNX conversion repository the files come from covers only the conversion.

#### Test Scenarios

##### Metadata is declared

**Test**: `Metadata_IsDeclared`

##### The model downloads its pinned bundle from the models release

**Test**: `OnnxNemotronMultilingualRecognitionModel_DownloadDescriptor_IsModelsReleaseBundle`

##### The bundle pins the eleven files with their original sources

**Test**: `OnnxNemotronMultilingualRecognitionModel_BundleContents_PinsEveryVendorFile`

##### Installing a bundle whose files do not match their pins fails

**Test**: `OnnxNemotronMultilingualRecognitionModel_InstallAsync_BundleWithWrongFile_Throws`

The test stages a well-formed zip whose `model.json` declares every file at its compiled pin but whose
entries hold other bytes, and asserts that `InstallAsync` throws `InvalidDataException`.

##### Language parameter is a choice of en-US and en-GB

**Test**: `Parameters_LanguageChoice`

##### CreateBackend with an empty directory throws

**Test**: `CreateBackend_EmptyDirectory_Throws`

##### CreateBackend with missing files throws

**Test**: `CreateBackend_MissingFiles_Throws`

##### CreateBackend with a vocabulary lacking the locale throws

**Test**: `CreateBackend_VocabularyWithoutLocale_Throws`

##### The engine-options overload rejects an invalid blank penalty first

**Scenario**: a negative blank penalty with a model directory that does not exist.

**Expected**: `ArgumentOutOfRangeException`, before the directory is read.

**Test**:
`OnnxNemotronMultilingualRecognitionModel_CreateBackend_NegativeBlankPenalty_ThrowsArgumentOutOfRangeException`

##### The engine-options overload rejects null options

**Test**: `OnnxNemotronMultilingualRecognitionModel_CreateBackend_NullEngineOptions_ThrowsArgumentNullException`
