### SpeechModelContract Verification

#### Verification Approach

`ISpeechModel`, `IRecognitionModel`, and `ISynthesisModel` are verified through small
hand-written fake implementations (`FakeRecognitionModel`, `FakeSynthesisModel`) that prove the
contract's members are individually reachable and correctly typed through each role-marker
interface. `InstallAsync`'s no-op default is verified via `FakeSynthesisModel` (which never
overrides it); `FakeRecognitionModel`'s optional `useZipArchivePayload` constructor parameter
adds a zip-unpack override, verified directly at the unit level against a small, deterministic
in-memory zip archive built with `System.IO.Compression.ZipArchive`
(`FakeModelDescriptors.ZipArchiveDescriptor`/`ZipArchiveBytes`). The recognition role's engine
members added in Phase 3 are verified through `FakeRecognitionModel`, which builds a real managed
recognizer configuration without loading any native library or opening any file, so the contract
is provable with no downloaded model present. The synthesis role's engine, preferred-audio-format,
and capability-profile members added in Sub-phase 4b are verified the same way through
`FakeSynthesisModel`, which builds a real managed VITS `OfflineTtsConfig` without loading any
native library, exposes a concrete best-effort `PreferredAudioFormat`, and relies on
`ISynthesisModel.CapabilityProfile`'s default-hook implementation rather than overriding it,
proving the default resolves to `DefaultModelCapabilityProfile.Instance` with zero model-specific
code. `IRecognitionModel.NormalizeText(text, isFinal)`'s default hook (forwarding to
`ISpeechModel.NormalizeText(text)`) is verified directly against `FakeRecognitionModel`, which
does not override either method, proving the default resolves to the shared pass-through with no
model-specific code. The default `LicenseName`/`LicenseUrl` hooks are verified directly against
`FakeSynthesisModel`, which does not override either member, proving the defaults resolve to an
explicit `"Unknown"` name and a null URL rather than throwing or silently claiming a specific
license.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Fakes**: `test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/Fakes/`

#### Acceptance Criteria

A fake `IRecognitionModel` reports `Role == SpeechModelRole.Recognition` and a fake
`ISynthesisModel` reports `Role == SpeechModelRole.Synthesis`; `Parameters`,
`AudioTagSupport`, and `DownloadDescriptor` are reachable through the common `ISpeechModel`
contract regardless of which role-marker interface is used. The default `InstallAsync`
implementation completes without modifying a staging directory's contents at all; the default
`NormalizeText` implementation returns its input unchanged; a zip-archive-payload fake's
`InstallAsync` override extracts its declared archive's entries into the staging directory and
removes the archive file. A fake `IRecognitionModel` also exposes the mono engine input
`AudioFormat` it declares, builds an engine configuration whose feature rate and file paths are
resolved against a supplied installed-model directory, and rejects an empty directory. A fake
`ISynthesisModel` builds an engine configuration whose file paths are resolved against a supplied
installed-model directory, rejects an empty directory, exposes its best-effort
`PreferredAudioFormat`, and exposes the default `CapabilityProfile` hook. The default
`IRecognitionModel.NormalizeText(text, isFinal)` hook forwards to the shared
`ISpeechModel.NormalizeText(text)` pass-through, for both `isFinal` values. The default
`LicenseName`/`LicenseUrl` hooks report an explicit `"Unknown"` name and a null URL.

#### Test Scenarios

##### A fake recognition model reports the Recognition role

**Test**: `IRecognitionModel_Role_IsRecognition`

##### A fake synthesis model reports the Synthesis role

**Test**: `ISynthesisModel_Role_IsSynthesis`

##### A fake model's Parameters exposes every declared parameter descriptor kind

**Test**: `ISpeechModel_Parameters_DeclaresEveryDescriptorKind`

##### A fake model's AudioTagSupport and DownloadDescriptor are exposed through ISpeechModel

**Test**: `ISpeechModel_AudioTagSupportAndDownloadDescriptor_AreExposed`

##### The default InstallAsync implementation is a no-op that never modifies the staging directory

**Test**: `ISpeechModel_InstallAsync_DefaultImplementation_CompletesWithoutModifyingStagingDirectory`

##### The default NormalizeText implementation returns its input unchanged

**Test**: `ISpeechModel_NormalizeText_DefaultImplementation_ReturnsInputUnchanged`

##### A zip-archive-payload fake's InstallAsync override extracts entries and removes the archive

**Test**: `FakeRecognitionModel_InstallAsync_WithZipArchivePayload_ExtractsEntriesAndRemovesArchive`

##### A fake recognition model exposes its declared engine input AudioFormat

**Test**: `IRecognitionModel_AudioFormat_DeclaredByModel_IsExposed`

##### A fake recognition model builds an engine configuration resolved against its installed directory

**Test**: `IRecognitionModel_CreateEngineConfig_InstalledDirectory_ResolvesPathsAndSampleRate`

##### A fake recognition model rejects an empty installed-model directory

**Test**: `IRecognitionModel_CreateEngineConfig_EmptyDirectory_ThrowsArgumentException`

##### A fake synthesis model builds an engine configuration resolved against its installed directory

**Test**: `ISynthesisModel_CreateEngineConfig_InstalledDirectory_ResolvesPaths`

##### A fake synthesis model exposes its declared preferred audio format

**Test**: `ISynthesisModel_PreferredAudioFormat_DeclaredByModel_IsExposed`

##### A fake synthesis model rejects an empty installed-model directory

**Test**: `ISynthesisModel_CreateEngineConfig_EmptyDirectory_ThrowsArgumentException`

##### A fake synthesis model's default CapabilityProfile hook returns the shared default profile

**Test**: `ISynthesisModel_CapabilityProfile_DefaultImplementation_ReturnsDefaultProfile`

##### The default IRecognitionModel.NormalizeText hook forwards to ISpeechModel.NormalizeText

**Test**: `IRecognitionModel_NormalizeText_DefaultImplementation_ReturnsInputUnchanged`

##### The default LicenseName/LicenseUrl hooks report Unknown/null

**Test**: `ISpeechModel_LicenseName_DefaultImplementation_ReturnsUnknownAndNullLicenseUrl`
