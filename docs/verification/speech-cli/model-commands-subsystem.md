## SpeechCli ModelCommandsSubsystem Verification

### Verification Approach

The ModelCommandsSubsystem is verified through deterministic unit tests against a fake
`ICliModelCatalog` (`FakeCliModelCatalog`, backed by an in-memory dictionary of
`SpeechModelDescriptor`s built from `FakeSpeechModel`), which lets every catalog outcome (a known or
unknown model id, an already-downloaded or missing model, a successful or failed download, a
progress report, a store exception) be produced on demand with no real `SpeechModelCatalog`, network
access, or downloaded model. A second, smaller layer of tests exercises `SpeechModelCatalogAdapter`
and `CliModelCatalogFactory` against a real `SpeechModelCatalog` rooted at an isolated temporary
directory, proving the seam really does forward to the library correctly, still without any network
access (every model in this repository's known-model list starts `NotDownloaded`, and no test
triggers an actual download).

The scenarios of each unit are documented in the unit's own verification document in the
`model-commands-subsystem/` folder:

- `ICliModelCatalog`, `SpeechModelCatalogAdapter`, `CliModelCatalogFactory`
- `DownloadProgressReporter`
- `ListModelsCommand`, `ModelInfoCommand`, `DownloadCommand`, `UninstallCommand`, `CleanCommand`
- `ListVoicesCommand`, `VoicesCommand`

**Out-of-process evidence**: the out-of-process tests that invoke the built tool as a child process
(unknown model id, missing required argument, never-installed uninstall, nothing-to-clean-up, JSON
and table listings, voice and pack listings) live in
`test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs`. That file is a single cross-cutting
file for the whole tool and is outside this subsystem's review set; the unit verification documents
name the specific `SpeechCli_*` tests they rely on. Each runs against an isolated, empty
`--models-dir`, so no test touches the real per-user model store.

Automated tests do **not** perform a real model download; that remains a manual verification
activity (see _Manual / Build-Time Verification_ below and in the system-level verification
document).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every unit test constructs its own `FakeCliModelCatalog`; every adapter and
  integration test uses a freshly created, uniquely named temporary directory as its
  `--models-dir`/`RootPathOverride`, deleted afterward
- **Test doubles**: `FakeCliModelCatalog` (hand-written fake implementing `ICliModelCatalog`,
  tracking `Uninstall`/`CleanUpLeftovers`/`Download` calls for assertion) and `FakeSpeechModel`
  (hand-written fake implementing `ISpeechModel`, used for descriptor-level scenarios; the
  role-specific `IRecognitionModel`/`ISynthesisModel` interfaces are public, so adapter tests may
  also implement them)

### Acceptance Criteria

A ModelCommandsSubsystem test run passes when every test in the unit verification documents passes
and:

- `list-models` lists and filters the catalog correctly in both table and JSON form
- `model-info` prints a known model's full identity and parameter surface and cleanly rejects an
  unknown id, a missing id and an extra argument
- `download` installs every requested model it can, isolates and reports any single model's failure
  without abandoning the rest of the batch, honors `--force`'s uninstall-then-redownload semantics,
  and stops the whole batch on a genuine cancellation
- `uninstall` and `clean` both treat "nothing to do" as a successful no-op
- `list-voices` lists voices (table and JSON) with the consent note, and `voices` lists, installs,
  and uninstalls voice packs, reporting failure or cancellation with a non-zero exit code
- Voice-pack installs include emotion variants by default and omit them with `--no-emotions`
- The real catalog composes the voice-cloning model with the voice library, enumerates
  `--voices-dir` voices, and does not fail when a `--voices-dir` file is undecodable
- A configured download mirror is threaded through to model downloads and voice-pack installs
- The catalog yields an engine and audio format for a model of the right role and refuses a model of
  the wrong role with an error that identifies the model
- Every command rejects a `null` context or catalog and forwards correctly to a real, isolated
  `SpeechModelCatalog`/`SpeechModelStore` with no network access

### Manual / Build-Time Verification

The following are verified manually, rather than by an automated test, because a real model
download requires network access unavailable in CI:

- `dotnet run --project src/DemaConsulting.Speech.Cli -- list-models` against an isolated
  `--models-dir` lists the real compiled-in known models with sane column output
- `dotnet run --project src/DemaConsulting.Speech.Cli -- voices install voice-zero-1.0` against a
  real or mirrored pack downloads and installs it, after which `list-voices` shows its voices
- `dotnet run --project src/DemaConsulting.Speech.Cli -- model-info <a-real-known-model-id>`
  prints a real model's full identity and parameter surface without requiring it to be downloaded
