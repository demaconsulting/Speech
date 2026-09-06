### SherpaOnnxVitsLibriTtsEnglishSynthesisModel Verification

#### Verification Approach

`SherpaOnnxVitsLibriTtsEnglishSynthesisModel` is verified through two complementary layers:
deterministic unit tests that never touch the network or the real ~78 MiB production download
(identity/metadata assertions, a download-descriptor shape check, engine-configuration
field-wiring assertions against the `ISynthesisModel` internal `CreateEngineConfig` member, a
`CapabilityProfile` resolution check, an `InstallAsync` test against a small synthetic
`.tar.bz2` fixture, and `ResolveSpeakerId` resolution tests covering valid values across the
declared range, out-of-range/non-numeric values, a null bag, and a missing key); and a one-time,
real, personal spike performed during this pass's development that downloaded the genuine
production archive, loaded it through the real native sherpa-onnx engine, and proved distinct,
non-silent audio for two different selected speakers. The spike's scratch files (`.spike/`, the
downloaded archive, and the spike test project) were deleted after evidence capture and are not
part of the repository; the evidence below is this document's permanent record of that proof.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each unit test uses a unique scratch directory under `Path.GetTempPath()`; no
  unit test requires network access or the real, verified production archive
- **Spike environment** (one-time, not part of the automated suite): a standalone console
  application referencing `org.k2fsa.sherpa.onnx` and `org.k2fsa.sherpa.onnx.runtime.win-x64`
  1.13.5 (the exact package versions this repository's `.csproj` references), run manually
  against the real downloaded archive on Windows x64

#### Acceptance Criteria

The model declares its stable `Id`/`DisplayName`/`Role = Synthesis`/a single `speaker`
`NumericParameter` (range `0`-`903`, default `0`, `IsInteger = true`)/`AudioTagSupport = None`;
its
`DownloadDescriptor` names exactly one HTTPS file with a well-formed SHA-256 checksum and a
`.tar.bz2` relative install path; `PreferredAudioFormat` reports mono `22050` Hz as a
best-effort pre-load hint; `CreateEngineConfig` resolves the VITS model/tokens/data-dir
paths against the archive's extracted top-level folder with `NoiseScale = 0.333f`,
`NoiseScaleW = 0.333f`, `LengthScale = 1.0f`, an unset `Lexicon`, and `Provider = "cpu"`; an empty
installed directory throws `ArgumentException`; `CapabilityProfile` resolves to the shared
`DefaultModelCapabilityProfile.Instance`; `InstallAsync` extracts a synthetic archive's entries
and removes the archive file afterward; `ResolveSpeakerId` resolves a valid numeric value
(including the boundaries `0` and `903`) to itself, resolves a boxed `int` the same way as a
boxed `double`, and falls back to the default speaker id (`0`) for an out-of-range value, a
non-numeric value, a missing key, or a `null` bag; and - proven once, empirically, outside the
automated suite - selecting two different speaker ids against the same input sentence through
the real `SpeechSynthesizerFactory.Create` genuinely produces two different, non-silent audio
outputs.

#### Test Scenarios

##### The model declares its expected, stable catalog identity

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_Identity_DeclaresExpectedValues`

##### The model declares exactly one validated HTTPS archive download file

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_DownloadDescriptor_DeclaresSingleValidatedArchiveFile`

##### The model declares exactly one numeric speaker parameter spanning its full range

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_Parameters_DeclaresSpeakerNumericParameter`

##### PreferredAudioFormat reports the model's best-effort mono 22050 Hz hint

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_PreferredAudioFormat_IsMono22050`

##### CreateEngineConfig resolves the VITS files and this model's own recommended configuration

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_CreateEngineConfig_ResolvesVitsFilesAndConfig`

##### CreateEngineConfig throws ArgumentException for an empty installed directory

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_CreateEngineConfig_EmptyDirectory_ThrowsArgumentException`

##### CapabilityProfile resolves to the shared default profile

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_CapabilityProfile_IsDefaultProfile`

##### InstallAsync extracts a synthetic archive and removes it afterward

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_InstallAsync_SyntheticArchive_ExtractsFilesAndRemovesArchive`

##### ResolveSpeakerId resolves valid numeric values across the declared range, including boundaries

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_ResolveSpeakerId_ValidValue_ReturnsThatValue`
(a `[Theory]` covering speakers 0, 1, 450, 902, and 903)

##### ResolveSpeakerId accepts a boxed int the same way as a boxed double

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_ResolveSpeakerId_BoxedInt_ReturnsThatValue`

##### ResolveSpeakerId falls back to the default id for an out-of-range value

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_ResolveSpeakerId_OutOfRangeValue_ReturnsDefaultId`
(a `[Theory]` covering -1, 904, and 100000)

##### ResolveSpeakerId falls back to the default id for a non-numeric value

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_ResolveSpeakerId_NonNumericValue_ReturnsDefaultId`

##### ResolveSpeakerId falls back to the default id for a null value bag

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_ResolveSpeakerId_NullBag_ReturnsDefaultId`

##### ResolveSpeakerId falls back to the default id when the key is missing

**Test**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel_ResolveSpeakerId_MissingKey_ReturnsDefaultId`

#### Real-Archive Spike Evidence (Distinct-Speaker Proof)

This evidence was captured once, manually, during this pass's development, and is recorded here
because the scratch spike artifacts themselves were deleted afterward (per this repository's
convention of leaving no scratch artifacts in the working tree).

- **Archive downloaded**: `https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/vits-piper-en_US-libritts_r-medium.tar.bz2`,
  82,038,311 bytes
- **SHA-256** (computed directly against the freshly downloaded bytes):
  `10dc268f3e371696d721486123e2705a9fc1faa113491979fde4d88dba1f1b1c`
- **License**: CC BY 4.0, read directly from the archive's own extracted `MODEL_CARD` file
- **Distinct-speaker proof**: the same English sentence ("The quick brown fox jumps over the lazy
  dog.") was synthesized twice through the real `SpeechSynthesizerFactory.Create` with a
  `parameterValues` bag selecting two different numeric speaker ids via the new `speaker`
  parameter:
  - Speaker id 0 (default): 47,104 samples generated, RMS amplitude ≈ 0.093208 - non-silent
  - Speaker id 450: 46,336 samples generated, RMS amplitude ≈ 0.125450 - non-silent
  - The two outputs differ in both sample count (duration) and RMS amplitude, and are not
    byte-identical, proving that selecting a different speaker genuinely changes the synthesized
    output rather than the configuration field being silently ignored
- **Full pipeline confirmed**: this distinction was produced end-to-end through the real
  `SpeechSynthesizerFactory.Create` with a `parameterValues` bag selecting each speaker by
  numeric index, `SherpaOnnxSpeechSynthesizer.GenerateSegment` calling
  `ISynthesisModel.ResolveSpeakerId` once per segment instead of a hard-coded `speakerId: 0`, and
  producing the two distinct, non-silent outputs described above - not merely an isolated engine
  call
