### SherpaOnnxKokoroEnglishSynthesisModel Verification

#### Verification Approach

`SherpaOnnxKokoroEnglishSynthesisModel` is verified through two complementary layers:
deterministic unit tests that never touch the network or the real ~103 MiB production download
(identity/metadata assertions, a download-descriptor shape check, engine-configuration
field-wiring assertions against the `ISynthesisModel` internal `CreateEngineConfig` member, a
`CapabilityProfile` resolution check, an `InstallAsync` test against a small synthetic
`.tar.bz2` fixture, and `ResolveSpeakerId` resolution tests covering every declared voice plus
unknown-value/missing-key/null-bag fallback); and a one-time, real, personal spike performed
during this pass's development that downloaded the genuine production archive, loaded it through
the real native sherpa-onnx engine, and proved distinct, non-silent audio for two different
selected voices. The spike's scratch files (`.spike/`, the downloaded archive, and the spike test
project) were deleted after evidence capture and are not part of the repository; the evidence
below is this document's permanent record of that proof.

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

The model declares its stable `Id`/`DisplayName` (naming Apache-2.0)/`Role = Synthesis`/a single
`voice` `ChoiceParameter` with 11 options/`AudioTagSupport = None`; its `DownloadDescriptor` names
exactly one HTTPS file with a well-formed SHA-256 checksum and a `.tar.bz2` relative install
path; `CreateEngineConfig` resolves the Kokoro model/voices/tokens/data-dir paths against the
archive's extracted top-level folder with `LengthScale = 1.0f` and `Provider = "cpu"`; an empty
installed directory throws `ArgumentException`; `CapabilityProfile` resolves to the shared
`DefaultModelCapabilityProfile.Instance`; `InstallAsync` extracts a synthetic archive's entries
and removes the archive file afterward; `ResolveSpeakerId` resolves every one of the 11 declared
voices to its confirmed speaker id, and falls back to the default voice's id for an unknown
value, a missing key, or a `null` bag; and - proven once, empirically, outside the automated
suite - selecting two different voices against the same input sentence through the real
`SpeechSynthesizerFactory.Create` genuinely produces two different, non-silent audio outputs.

#### Test Scenarios

##### The model declares its expected, stable catalog identity and 11-voice parameter

**Test**: `SherpaOnnxKokoroEnglishSynthesisModel_Identity_DeclaresExpectedValues`

##### The model declares exactly one validated HTTPS archive download file

**Test**: `SherpaOnnxKokoroEnglishSynthesisModel_DownloadDescriptor_DeclaresSingleValidatedArchiveFile`

##### CreateEngineConfig resolves the Kokoro files and this model's own recommended configuration

**Test**: `SherpaOnnxKokoroEnglishSynthesisModel_CreateEngineConfig_ResolvesKokoroFilesAndConfig`

##### CreateEngineConfig throws ArgumentException for an empty installed directory

**Test**: `SherpaOnnxKokoroEnglishSynthesisModel_CreateEngineConfig_EmptyDirectory_ThrowsArgumentException`

##### CapabilityProfile resolves to the shared default profile

**Test**: `SherpaOnnxKokoroEnglishSynthesisModel_CapabilityProfile_IsDefaultProfile`

##### InstallAsync extracts a synthetic archive and removes it afterward

**Test**: `SherpaOnnxKokoroEnglishSynthesisModel_InstallAsync_SyntheticArchive_ExtractsFilesAndRemovesArchive`

##### ResolveSpeakerId resolves every one of the 11 declared voices to its confirmed id

**Test**: `SherpaOnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_KnownVoice_ReturnsConfirmedId`
(a `[Theory]` covering all 11 declared voice/speaker-id pairs)

##### ResolveSpeakerId falls back to the default voice's id for an unrecognized value

**Test**: `SherpaOnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_UnknownVoice_ReturnsDefaultId`

##### ResolveSpeakerId falls back to the default voice's id for a null value bag

**Test**: `SherpaOnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_NullBag_ReturnsDefaultId`

##### ResolveSpeakerId falls back to the default voice's id when the key is missing

**Test**: `SherpaOnnxKokoroEnglishSynthesisModel_ResolveSpeakerId_MissingKey_ReturnsDefaultId`

#### Real-Archive Spike Evidence (Distinct-Voice Proof)

This evidence was captured once, manually, during this pass's development, and is recorded here
because the scratch spike artifacts themselves were deleted afterward (per this repository's
convention of leaving no scratch artifacts in the working tree).

- **Archive downloaded**: `https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-int8-en-v0_19.tar.bz2`,
  103,248,205 bytes
- **SHA-256** (computed directly against the freshly downloaded bytes):
  `c9f0dd393615805b0bab050c340834d5e684e732aec91c0e860cd30e982c08bd`
- **License**: Apache License 2.0, read directly from the archive's own extracted `LICENSE` file
- **Extracted layout confirmed**: top-level folder `kokoro-int8-en-v0_19/` containing
  `model.int8.onnx` (134,186,977 bytes), `voices.bin` (5,755,904 bytes), `tokens.txt` (1,078
  bytes), `README.md` (135 bytes), `LICENSE`, and `espeak-ng-data/` (391 files)
- **Voice count and ordering confirmed three independent ways**: upstream
  `k2-fsa/sherpa-onnx`'s own `scripts/kokoro/v0.19/generate_voices_bin.py` generation script's
  hard-coded `id2speaker` map; byte arithmetic against the real `voices.bin`
  (`511 * 1 * 256 * 4 bytes * 11 voices = 5,755,904 bytes`, an exact match); and a live loaded
  `OfflineTts` instance reporting `NumSpeakers = 11`
- **Engine load confirmed**: a real `OfflineTts` instance built from exactly the
  `CreateEngineConfig` configuration above reported `SampleRate = 24000` and `NumSpeakers = 11`
- **Distinct-voice proof**: the same English sentence was synthesized twice through the real
  engine with two different speaker ids:
  - Speaker id 1 ("af_bella"): 65,486 samples generated, RMS amplitude ≈ 0.0543 - non-silent
  - Speaker id 9 ("bm_george"): 71,591 samples generated, RMS amplitude ≈ 0.0633 - non-silent
  - The two outputs differ in both sample count (duration) and RMS amplitude, and are not
    byte-identical, proving that selecting a different voice genuinely changes the synthesized
    output rather than the configuration field being silently ignored
- **Full pipeline confirmed**: the same distinction was independently reproduced end-to-end
  through the real `SpeechSynthesizerFactory.Create` with a `parameterValues` bag selecting each
  voice by name, `SherpaOnnxSpeechSynthesizer.GenerateSegment` calling
  `ISynthesisModel.ResolveSpeakerId` once per segment instead of a hard-coded `speakerId: 0`, and
  producing the same two distinct, non-silent outputs described above - not merely the isolated
  engine call
