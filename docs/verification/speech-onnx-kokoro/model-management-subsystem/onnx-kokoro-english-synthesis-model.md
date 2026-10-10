### OnnxKokoroEnglishSynthesisModel Verification

#### Verification Approach

`OnnxKokoroEnglishSynthesisModel` is verified by automated unit tests in
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests/ModelManagementSubsystem/OnnxKokoroEnglishSynthesisModelTests.cs`,
proving its declared catalog metadata, 30-file download descriptor shape, speaker-id resolution,
and `CreateBackend` wiring - the latter against a tiny, hand-built ONNX test fixture
(`TestData/fake-kokoro-model.onnx`, whose single node forwards its `style` input straight through
as the output) and synthetic per-voice style-vector files, never the real ~163 MiB production
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
`DownloadDescriptor` declares exactly 30 files (the ONNX model graph plus 29 voice style-vector
files), each with a well-formed 64-character hexadecimal SHA-256 checksum and a unique relative
install path, including `onnx/model_fp16.onnx` and a `voices/{voice}.bin` entry for every declared
voice; `PreferredAudioFormat` reports mono 24000 Hz; `CreateBackend` throws `ArgumentException`
for an empty installed directory; `ResolveSpeakerId` resolves every one of the 29 declared voices
to its declaration-order index, and falls back to the default voice's index (`0`) for an unknown
value, a missing key, or a `null` bag; and `CreateBackend`, given a valid installed directory
containing the fixture model and synthetic per-voice style files, constructs a working engine
whose `Generate` call for a given speaker id returns non-empty audio at
`OnnxKokoroSynthesisEngine.SampleRate` whose samples equal that speaker's own synthetic style
value - proving the requested voice's style vector is forwarded verbatim, not merely that some
styled audio is produced.

#### Test Scenarios

##### The model declares its expected, stable catalog identity and 29-voice parameter

**Test**: `OnnxKokoroEnglishSynthesisModel_Identity_DeclaresExpectedValues`

##### The model declares exactly 30 unique, validated download files

**Test**: `OnnxKokoroEnglishSynthesisModel_DownloadDescriptor_Declares30UniqueValidatedFiles`

##### PreferredAudioFormat reports the model's best-effort mono 24000 Hz hint

**Test**: `OnnxKokoroEnglishSynthesisModel_PreferredAudioFormat_IsMono24000`

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
`am_michael`, the sixth declared voice - proving the requested speaker id's own style vector is
selected and forwarded through the real ONNX Runtime inference call, not merely that some
non-empty audio is produced.

##### CreateBackend disposes the ONNX session and rethrows when a voice file is missing

**Test**: `OnnxKokoroEnglishSynthesisModel_CreateBackend_MissingVoiceFile_DisposesSessionAndThrows`

Using the test-only `OnnxKokoroEnglishSynthesisModel.OnSessionCreated` hook to capture the
`InferenceSession` `CreateBackend` creates from the tiny ONNX fixture, this test supplies a
directory with no `voices` subdirectory at all, so reading the first declared voice's style file
fails with `DirectoryNotFoundException`. It asserts the exception propagates unchanged and, via
reflection into `InferenceSession`'s own private `_disposed` field (the type exposes no public
equivalent of `SessionOptions`'s inherited `IsClosed`, since it derives directly from `object`
rather than `SafeHandle`), that the captured session was disposed rather than leaked - since no
`OnnxKokoroSynthesisEngine` is ever constructed on this failure path to take ownership of it.
