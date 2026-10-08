## ModelManagementSubsystem Verification

### Verification Approach

The SpeechSherpa ModelManagementSubsystem is verified through deterministic unit tests that never
touch the network, the real multi-hundred-megabyte production downloads, or the native sherpa-onnx
runtime. Each of the four shipped models is verified for its identity and license metadata, its
download-descriptor shape, its engine-configuration field wiring through its internal static
`BuildEngineConfig` member, its installation behavior against a small synthetic `.tar.bz2`
fixture, and any model-specific hooks it overrides (`NormalizeText`, `PostEndpointWarmupWindowMs`,
`CapabilityProfile`, `ResolveSpeakerId`, tunable `Parameters`). The shared
`TarBz2ArchiveExtractor` and `UppercaseTranscriptRestorer` helpers are verified independently, and
the `AddSherpaModels()` extension method is verified against a real Speech `SpeechModelCatalog`.

The models' `CreateBackend` implementations, which construct the real native backends, are not
exercised here because doing so loads the native runtime; the real recognition backend is
verified in _SpeechSherpa RecognitionSubsystem Verification_ and the real synthesis backend's
coverage status is recorded in _SpeechSherpa SynthesisSubsystem Verification_. Where a model's
behavior was decided by a one-time real spike (for example the synthesis models' voice selection
or the Nemotron model's casing), that evidence is recorded in the model's own verification
document.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Sherpa.Tests`
- **Isolation**: Each test uses a unique scratch directory under `Path.GetTempPath()`; no test
  requires network access or a real, verified production archive
- **Fixtures**: Synthetic `.tar.bz2` archives built by the test project's own
  `TarBz2ArchiveFixtures` helper

### Unit-Level Test Scenarios

See each unit's own verification document for its detailed test scenarios:
`sherpa-onnx-zipformer-en-recognition-model.md`,
`sherpa-onnx-nemotron-streaming-en-recognition-model.md`,
`sherpa-onnx-vits-libritts-en-synthesis-model.md`, `sherpa-onnx-kokoro-en-synthesis-model.md`,
`tar-bz2-archive-extractor.md`, `uppercase-transcript-restorer.md`.

The `SpeechModelCatalogSherpaExtensions` unit is verified by the system-level scenarios
`AddSherpaModels_Called_ReturnsSameCatalogInstance` and
`AddSherpaModels_Called_RegistersExpectedFourModelsWithExpectedRoles`; see _SpeechSherpa System
Verification_.

### Acceptance Criteria

A SpeechSherpa ModelManagementSubsystem test run passes when: `AddSherpaModels()` returns the
same catalog instance and registers exactly the four shipped models with their expected roles;
every model declares its documented identity, license, and single-file HTTPS download descriptor
with a well-formed SHA-256 checksum and `.tar.bz2` relative install path; every model's
`BuildEngineConfig` resolves its documented files against the archive's extracted top-level
folder with its documented engine settings and throws `ArgumentException` for an empty installed
directory; every model's `InstallAsync` extracts a synthetic archive's entries and removes the
archive afterward; the Zipformer model delegates `NormalizeText` to `UppercaseTranscriptRestorer`
and leaves `PostEndpointWarmupWindowMs` disabled while the Nemotron model reports `800`; the
synthesis models declare their preferred audio formats, default capability profiles, and resolve
speaker ids with the documented fallbacks; `TarBz2ArchiveExtractor` extracts every entry and
rejects empty arguments; and `UppercaseTranscriptRestorer` produces the documented final and
provisional restorations.
