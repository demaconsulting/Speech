### OnnxKokoroEnglishSynthesisModel Verification

#### Verification Approach

`OnnxKokoroEnglishSynthesisModel` is verified by automated unit tests in
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests/ModelManagementSubsystem/OnnxKokoroEnglishSynthesisModelTests.cs`,
proving its declared catalog metadata, its `models`-release bundle and the bundle's 30 pinned vendor files,
the rejection of a bundle whose extracted files do not match their pins, speaker-id resolution, and
`CreateBackend` wiring - the latter against a tiny, hand-built ONNX test fixture
(`TestData/fake-kokoro-model.onnx`, whose single node forwards its `style` input straight through
as the output) and synthetic per-voice style-vector files, never the real ~88 MiB production
model/voices download. Each declared voice's expected speaker id is reproduced directly in the
test class (`ExpectedVoiceOrder`), not reflected out of the production class, so a test failure
clearly shows which declared order assumption broke.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.Kokoro.Tests`
- **Isolation**: each test uses a unique scratch directory under `Path.GetTempPath()`, cleaned up
  on dispose; no test requires network access or the real, verified production download

#### Acceptance Criteria

The model declares its stable `Id`/`DisplayName`/`Role = Synthesis`/`AudioTagSupport = None`, and
`LicenseName = "Apache-2.0"` with its canonical `LicenseUrl`; its one voice `ChoiceParameter`
declares exactly 29 options in the confirmed order with `"af_heart"` as the default; its
`DownloadDescriptor` declares exactly one file, the `kokoro-en-v1.0-int8-b1.zip` asset on the project's
`models` release at its pinned SHA-256; the bundle's contents declare exactly 30 vendor files (the ONNX model
graph plus 29 voice style-vector files), each with its original HTTPS `onnx-community` address, a well-formed
64-character hexadecimal SHA-256 checksum and a unique relative install path, including
`onnx/model_quantized.onnx` and a `voices/{voice}.bin` entry for every declared voice; `InstallAsync` throws
`InvalidDataException` for a well-formed bundle whose extracted files do not match their pins;
`PreferredAudioFormat` reports mono 24000 Hz; the model's text analysis is one analyzer shared by all
instances whose
reading annotations differ for the noun and the verb of `record`; `CreateBackend` throws `ArgumentException`
for an empty installed directory; `ResolveSpeakerId` resolves every one of the 29 declared voices
to its declaration-order index, and falls back to the default voice's index (`0`) for an unknown
value, a missing key, or a `null` bag; and `CreateBackend`, given a valid installed directory
containing the fixture model and synthetic per-voice style files, constructs a working engine
whose `Generate` call for a given speaker id returns non-empty audio at
`OnnxKokoroSynthesisEngine.SampleRate` whose samples equal that speaker's own synthetic style
value - proving the requested voice's style vector is forwarded verbatim, not merely that some
styled audio is produced.

#### Test Scenarios

##### The model declares its expected, stable catalog identity

**Test**: `OnnxKokoroEnglishSynthesisModel_Identity_DeclaresExpectedValues`

##### The model declares its Apache-2.0 license

**Test**: `OnnxKokoroEnglishSynthesisModel_License_DeclaresApache2`

##### The model declares a 29-voice parameter with the expected default

**Test**: `OnnxKokoroEnglishSynthesisModel_VoiceParameter_Declares29VoicesWithDefault`

##### The model downloads its pinned bundle from the models release

**Test**: `OnnxKokoroEnglishSynthesisModel_DownloadDescriptor_IsModelsReleaseBundle`

##### The bundle holds the model graph and every voice file

**Test**: `OnnxKokoroEnglishSynthesisModel_BundleContents_DeclaresModelGraphAndEveryVoiceFile`

##### Every vendor file has its HTTPS source, a SHA-256 pin, and a unique install path

**Test**: `OnnxKokoroEnglishSynthesisModel_BundleContents_PinsEveryVendorFile`

##### Installing a bundle whose files do not match their pins fails

**Test**: `OnnxKokoroEnglishSynthesisModel_InstallAsync_BundleWithWrongFile_Throws`

The test stages a well-formed zip whose `model.json` declares every vendor file at its compiled pin but whose
entries hold other bytes, and asserts that `InstallAsync` throws `InvalidDataException`.

##### PreferredAudioFormat reports the model's best-effort mono 24000 Hz hint

**Test**: `OnnxKokoroEnglishSynthesisModel_PreferredAudioFormat_IsMono24000`

##### The model supplies one shared English text analysis that chooses the reading of a heteronym

**Test**: `OnnxKokoroEnglishSynthesisModel_TextAnalyzer_IsSharedAndClassifiesHeteronyms`

Two model instances return the same analyzer; analyzing `They record the song. The record broke.` gives two reading
annotations, one for each use of `record`, with different values (the verb and the noun).

##### ResolveSpeakerId resolves every one of the 29 declared voices to its declaration-order index

**Test**: `OnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_KnownVoice_ReturnsDeclarationOrderIndex`
(a `[Theory]` covering all 29 declared voice/index pairs)

##### ResolveSpeakerId falls back to the default voice's index for an unrecognized value

**Test**: `OnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_UnknownVoice_ReturnsDefaultId`

##### ResolveSpeakerId falls back to the default voice's index for a null value bag

**Test**: `OnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_NullBag_ReturnsDefaultId`

##### ResolveSpeakerId falls back to the default voice's index when the key is missing

**Test**: `OnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_MissingKey_ReturnsDefaultId`

##### CreateBackend throws ArgumentException for an empty installed directory

**Test**: `OnnxKokoroEnglishSynthesisModel_CreateBackend_EmptyDirectory_ThrowsArgumentException`

##### CreateBackend loads voices and Generate forwards the selected voice's own style verbatim

**Test**: `OnnxKokoroEnglishSynthesisModel_CreateBackend_ValidModelAndVoices_GeneratesUsingSelectedVoiceStyle`

Against a tiny, hand-built ONNX fixture whose single node forwards its `style` input straight
through as the output, and 29 synthetic per-voice `.bin` files each filled with that voice's own
declaration-order index, this test calls `CreateBackend` and then `Generate("cat", 1.0f,
speakerId: 5)`, asserting every returned sample equals `5.0f` - the style value belonging to
`af_kore`, the sixth declared voice - proving the requested speaker id's own style vector is
selected and forwarded through the real ONNX Runtime inference call, not merely that some
non-empty audio is produced.

##### CreateBackend disposes the ONNX session and rethrows when a voice file is missing

**Test**: `OnnxKokoroEnglishSynthesisModel_CreateBackend_MissingVoiceFile_DisposesSessionAndThrows`

Using the test-only `OnnxKokoroEnglishSynthesisModel.SessionFactory` seam to substitute a derived
`InferenceSession` that records its own disposal through the public `Dispose(bool)` override point, this
test supplies a directory with no `voices` subdirectory at all, so reading the first declared voice's
style file fails with `DirectoryNotFoundException`. It asserts the exception propagates unchanged and
that the single session opened for the test's scratch directory was disposed rather than leaked - since no
`OnnxKokoroSynthesisEngine` is ever constructed on this failure path to take ownership of it. No ONNX
Runtime private state is read and the disposed native session is never used.
