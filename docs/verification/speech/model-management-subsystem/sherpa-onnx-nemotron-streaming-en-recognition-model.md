### SherpaOnnxNemotronStreamingEnRecognitionModel Verification

#### Verification Approach

`SherpaOnnxNemotronStreamingEnRecognitionModel` is verified through deterministic unit tests
that never touch the network or the real ~442 MiB production download, deliberately mirroring
`SherpaOnnxZipformerEnRecognitionModelTests`'s shape since both models share the same
`OnlineModelConfig.Transducer` configuration surface: identity/metadata assertions (including
that `DisplayName` visibly names the NVIDIA Open Model License), a download-descriptor shape
check, engine-configuration field-wiring assertions, an explicit assertion that `ModelType` is
left unset, and an `InstallAsync` test against a small synthetic `.tar.bz2` fixture. Whether this
model needs a `NormalizeText` override was decided by a real (non-mocked) spike - synthesizing
audio with the shipped LibriTTS voice and feeding it through the real
`SherpaOnnxRecognitionEngine` - documented in this unit's XML remarks; the resulting pass-through
default is already covered generically by `SpeechModelContractTests`, so no dedicated
`NormalizeText` test exists for this class. `PostEndpointWarmupWindowMs` (`800`) is covered by a
dedicated assertion; the 800ms value itself, and the post-endpoint warm-up-replay mechanism it
configures, were validated against two real user-recorded WAV files fed through the real,
unmodified recognition pipeline with the real model (see the Real-Recording Evidence section
below and the RecognitionSubsystem verification chapter, which documents the shared engine-level
mechanism's own test coverage).

#### Real-Recording Evidence for the 800ms PostEndpointWarmupWindowMs Value

Documented in full in
`.agent-logs/planning-nemotron-endpoint-word-loss-warmup-replay-fix-9d4b71.md`:

- Both real recordings reproduced the underlying defect (dropped words across a false-positive
  mid-utterance endpoint reset) for this model before the fix.
- A window-size sweep (400/500/600/700/800/1000-1500ms) against both recordings found 600ms the
  minimum window that reliably recovered the dropped words, with no duplicated text observed at
  any tested size up to 1500ms for this model.
- 800ms was selected as the production value: comfortable margin above the 600ms minimum, well
  below where any issue was observed for this model.
- The endpoint-detection rule values (`Rule1MinTrailingSilence`/`Rule2MinTrailingSilence`/
  `Rule3MinUtteranceLength`) were left at their production values throughout - the fix does not
  depend on, and did not require, any change to endpoint sensitivity.

**Leading-silence regression, found and resolved (retry).** A later independent quality review
(`.agent-logs/quality-nemotron-endpoint-warmup-replay-fix-7a2f91.md`) found that the
then-unconditional replay mechanism corrupted the first genuinely recognized segment of a session
whenever an endpoint fired on leading silence before any speech occurred (`Recording.wav`'s
Nemotron transcript read `"B or not to be"` instead of the clean `"Or not to be"`). This was fixed
by gating replay eligibility on genuine recognized text having occurred since the last reset (see
`.agent-logs/planning-nemotron-endpoint-leading-silence-regression-fix-7a2f91.md` and the
RecognitionSubsystem design/verification chapters for the full mechanism and evidence). Both real
recordings were re-verified against the real, installed model after the fix: `Recording.wav`'s
Nemotron transcript is now `"Or not to be | That is the question"` (stray "B" gone), and
`capture-20260906-092843-2bdb9a.wav`'s Nemotron transcript remains `"To be or not to be | That is
the question"` (the "that is" recovery this 800ms value exists to preserve is unaffected).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test uses a unique scratch directory under `Path.GetTempPath()`; no test
  requires network access or the real, verified production archive

#### Acceptance Criteria

The model declares its stable `Id`/`Role`/empty `Parameters`/`AudioTagSupport = None`, and a
`DisplayName` that visibly names the NVIDIA Open Model License; its `DownloadDescriptor` names
exactly one HTTPS file (from `github.com`, never a bundled copy) with a well-formed SHA-256
checksum and a `.tar.bz2` relative install path; `SampleRate` reports `16000`;
`CreateEngineConfig` resolves the int8 encoder/decoder/joiner/tokens paths against the archive's
extracted top-level folder with `DecodingMethod = "greedy_search"` and `EnableEndpoint = 1`, and
deliberately leaves `ModelType` unset; an empty installed directory throws `ArgumentException`;
`InstallAsync` extracts a synthetic archive's entries and removes the archive file afterward.

#### Test Scenarios

##### The model declares its expected, stable catalog identity, visibly naming its license

**Test**: `SherpaOnnxNemotronStreamingEnRecognitionModel_Identity_DeclaresExpectedValues`

##### The model declares exactly one validated HTTPS archive download file

**Test**: `SherpaOnnxNemotronStreamingEnRecognitionModel_DownloadDescriptor_DeclaresSingleValidatedArchiveFile`

##### SampleRate reports the model's declared 16 kHz feature rate

**Test**: `SherpaOnnxNemotronStreamingEnRecognitionModel_SampleRate_Is16000`

##### CreateEngineConfig resolves the int8 files and feature configuration

**Test**: `SherpaOnnxNemotronStreamingEnRecognitionModel_CreateEngineConfig_ResolvesInt8FilesAndFeatureConfig`

##### CreateEngineConfig deliberately leaves ModelType unset

**Test**: `SherpaOnnxNemotronStreamingEnRecognitionModel_CreateEngineConfig_ModelTypeIsUnset`

##### CreateEngineConfig throws ArgumentException for an empty installed directory

**Test**: `SherpaOnnxNemotronStreamingEnRecognitionModel_CreateEngineConfig_EmptyDirectory_ThrowsArgumentException`

##### InstallAsync extracts a synthetic archive and removes it afterward

**Test**: `SherpaOnnxNemotronStreamingEnRecognitionModel_InstallAsync_SyntheticArchive_ExtractsFilesAndRemovesArchive`

##### PostEndpointWarmupWindowMs reports the empirically validated 800ms warm-up-replay window

**Test**: `SherpaOnnxNemotronStreamingEnRecognitionModel_PostEndpointWarmupWindowMs_Is800`
