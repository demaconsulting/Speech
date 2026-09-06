### SherpaOnnxZipformerEnRecognitionModel Verification

#### Verification Approach

`SherpaOnnxZipformerEnRecognitionModel` is verified through deterministic unit tests that never
touch the network or the real ~70 MiB production download: identity/metadata assertions, a
download-descriptor shape check (single HTTPS file, well-formed SHA-256 checksum, `.tar.bz2`
relative install path), engine-configuration field-wiring assertions against the `IRecognitionModel`
internal members, and an `InstallAsync` test against a small synthetic `.tar.bz2` fixture built
by the test project's own `TarBz2ArchiveFixtures` helper.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test uses a unique scratch directory under `Path.GetTempPath()`; no test
  requires network access or the real, verified production archive

#### Acceptance Criteria

The model declares its stable `Id`/`DisplayName`/`Role`/empty `Parameters`/`AudioTagSupport =
None`; its `DownloadDescriptor` names exactly one HTTPS file with a well-formed SHA-256 checksum
and a `.tar.bz2` relative install path; `SampleRate` reports `16000`; `CreateEngineConfig`
resolves the int8 encoder/decoder/joiner/tokens paths against the archive's extracted top-level
folder with `ModelType = "zipformer2"`, `DecodingMethod = "greedy_search"`, and
`EnableEndpoint = 1`; an empty installed directory throws `ArgumentException`; `InstallAsync`
extracts a synthetic archive's entries and removes the archive file afterward; `NormalizeText`
delegates final results to `UppercaseTranscriptRestorer.RestoreFinal` and provisional results to
`UppercaseTranscriptRestorer.RestoreProvisional`; `PostEndpointWarmupWindowMs` reports the
disabled `0` default (see Real-Recording Evidence below).

#### Real-Recording Evidence for Not Opting Into PostEndpointWarmupWindowMs

Documented in full in
`.agent-logs/planning-nemotron-endpoint-word-loss-warmup-replay-fix-9d4b71.md`: forcing the
post-endpoint warm-up-replay feature on for this model, against a real recording, produced
genuine duplicated text at every tested window from 500ms upward, and this model showed no
reproduction of the underlying word-loss defect the feature exists to fix on either real
recording available. `SherpaOnnxZipformerEnRecognitionModel_PostEndpointWarmupWindowMs_IsDisabledDefault`
is a regression guard against this deliberate non-enablement ever being accidentally reversed.

#### Test Scenarios

##### The model declares its expected, stable catalog identity

**Test**: `SherpaOnnxZipformerEnRecognitionModel_Identity_DeclaresExpectedValues`

##### The model declares exactly one validated HTTPS archive download file

**Test**: `SherpaOnnxZipformerEnRecognitionModel_DownloadDescriptor_DeclaresSingleValidatedArchiveFile`

##### AudioFormat reports the model's trained mono 16 kHz feature rate

**Test**: `SherpaOnnxZipformerEnRecognitionModel_AudioFormat_IsMono16000`

##### CreateEngineConfig resolves the int8 files and feature configuration

**Test**: `SherpaOnnxZipformerEnRecognitionModel_CreateEngineConfig_ResolvesInt8FilesAndFeatureConfig`

##### CreateEngineConfig throws ArgumentException for an empty installed directory

**Test**: `SherpaOnnxZipformerEnRecognitionModel_CreateEngineConfig_EmptyDirectory_ThrowsArgumentException`

##### InstallAsync extracts a synthetic archive and removes it afterward

**Test**: `SherpaOnnxZipformerEnRecognitionModel_InstallAsync_SyntheticArchive_ExtractsFilesAndRemovesArchive`

##### NormalizeText restores casing, contractions, and punctuation on final results

**Test**: `SherpaOnnxZipformerEnRecognitionModel_NormalizeText_Final_RestoresCasingContractionsAndPunctuation`

##### NormalizeText applies only cheap casing on provisional results

**Test**: `SherpaOnnxZipformerEnRecognitionModel_NormalizeText_Provisional_AppliesCheapCasingOnly`

##### PostEndpointWarmupWindowMs reports the disabled default, unchanged from before this fix

**Test**: `SherpaOnnxZipformerEnRecognitionModel_PostEndpointWarmupWindowMs_IsDisabledDefault`
