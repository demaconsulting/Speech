## SpeechCli ModelCommandsSubsystem Verification

### Verification Approach

The ModelCommandsSubsystem is verified through deterministic unit tests against a fake
`ICliModelCatalog` (`FakeCliModelCatalog`, backed by an in-memory dictionary of
`SpeechModelDescriptor`s built from `FakeSpeechModel`), which lets every catalog outcome —
a known or unknown model id, an already-downloaded or missing model, a successful or failed
download, a progress report, a store exception — be produced on demand with no real
`SpeechModelCatalog`, network access, or downloaded model. A second, smaller layer of tests
exercises `SpeechModelCatalogAdapter`/`CliModelCatalogFactory` themselves against a real
`SpeechModelCatalog` rooted at an isolated temporary directory, proving the seam really does
forward to the library correctly, still without any network access (every model in this
repository's known-model list starts `NotDownloaded`, and no test triggers an actual download).
Out-of-process integration tests in `IntegrationTests.cs` invoke the built tool as a child
process for the argument-parsing and clean-error paths that matter most from an operator's
perspective (an unknown model id, a missing required argument), each against an isolated, empty
`--models-dir` so no test ever touches the real per-user model store.

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
  (hand-written fake implementing `ISpeechModel`, since the role-specific
  `IRecognitionModel`/`ISynthesisModel` interfaces restrict implementation to the library's own
  `InternalsVisibleTo` assemblies)

### Test Scenarios

#### ListModelsCommand

**Tests**: `ListModelsCommand_Run_NoFilters_ListsEveryModelAsTable`,
`ListModelsCommand_Run_RoleFilterTts_ListsOnlySynthesisModels`,
`ListModelsCommand_Run_StateFilterDownloaded_ListsOnlyDownloadedModels`,
`ListModelsCommand_Run_StateFilterMissing_ListsOnlyMissingModels`,
`ListModelsCommand_Run_FormatJson_EmitsJsonArray`,
`ListModelsCommand_Run_EmptyCatalog_PrintsNoModelsMessage`,
`ListModelsCommand_Run_InvalidRoleValue_ThrowsArgumentException`,
`ListModelsCommand_Run_InvalidFormatValue_ThrowsArgumentException`,
`ListModelsCommand_Run_UnsupportedFlag_ThrowsArgumentException`,
`ListModelsCommand_Run_RoleFlagMissingValue_ThrowsArgumentException`,
`SpeechCli_ListModelsCommand_Invoked_ListsKnownModelsAsTable`,
`SpeechCli_ListModelsCommandWithJsonFormat_Invoked_EmitsJsonArray`

**Scenario/Expected**: No filter lists every catalog model as an aligned table with the Id,
Display Name, Role, State, and License columns; `--role tts`/`--state downloaded`/`missing` each
filter to exactly the matching subset; `--format json` emits a JSON array whose objects contain
the same fields; an empty result (empty catalog or an over-restrictive filter) prints an
explanatory message instead of a header-only table; an unrecognized `--role`/`--format` value, an
unsupported flag, or a flag missing its value are all rejected with `ArgumentException` rather
than a crash or silent misbehavior; both the table and JSON out-of-process forms are also proven
against the built tool.

**Requirement coverage**: `SpeechCli-ModelCommands-ListModels`.

#### ModelInfoCommand

**Tests**: `ModelInfoCommand_Run_KnownModel_PrintsIdentityAndState`,
`ModelInfoCommand_Run_NumericParameter_PrintsBounds`,
`ModelInfoCommand_Run_ChoiceParameter_PrintsOptions`,
`ModelInfoCommand_Run_BooleanParameter_PrintsDefault`,
`ModelInfoCommand_Run_UnknownModelId_ThrowsArgumentException`,
`ModelInfoCommand_Run_MissingModelIdArgument_ThrowsArgumentException`,
`ModelInfoCommand_Run_ExtraArgument_ThrowsArgumentException`,
`SpeechCli_ModelInfoCommandWithUnknownId_Invoked_ReturnsCleanError`,
`SpeechCli_ModelInfoCommandWithMissingArgument_Invoked_ReturnsCleanError`

**Scenario/Expected**: A known model id prints its identity, state, license name/URL, and audio-
tag support; a `NumericParameter` prints its min/max/step/default; a `ChoiceParameter` prints its
option list and default; a `BooleanParameter` prints its default; an unknown model id throws
`ArgumentException` whose message names the id and suggests `list-models`, both in-process and
(as a clean, non-zero-exit-code error rather than a stack trace) out-of-process; a missing or
extra positional argument is likewise rejected with `ArgumentException`.

**Requirement coverage**: `SpeechCli-ModelCommands-ModelInfo`.

#### DownloadCommand

**Tests**: `DownloadCommand_RunAsync_SingleModel_Succeeds_ReportsInstalled`,
`DownloadCommand_RunAsync_MultipleModels_DownloadsEachSequentially`,
`DownloadCommand_RunAsync_ChecksumMismatch_ReportsErrorAndContinues`,
`DownloadCommand_RunAsync_UnknownModelId_ReportsErrorAndContinues`,
`DownloadCommand_RunAsync_ForceOnDownloadedModel_UninstallsFirst`,
`DownloadCommand_RunAsync_ForceOnMissingModel_DoesNotUninstall`,
`DownloadCommand_RunAsync_ReportsProgress_WritesPercentLine`,
`DownloadCommand_RunAsync_NoModelIds_ThrowsArgumentException`,
`DownloadCommand_RunAsync_UnsupportedFlag_ThrowsArgumentException`,
`SpeechCli_DownloadCommandWithUnknownId_Invoked_ReturnsNonZeroExitCode`,
`SpeechCli_DownloadCommandWithMissingArgument_Invoked_ReturnsCleanError`

**Scenario/Expected**: A single requested model downloads and reports installed; multiple
requested models download strictly in the order given, never interleaved; a checksum-mismatch
outcome or an unknown model id is reported as a failure for that model only, and the remaining
requested models are still attempted; `--force` against an already-`Downloaded` model uninstalls
it first, then downloads; `--force` against a not-yet-downloaded model never calls uninstall; a
progress report is forwarded to a visible percent-complete line; no `<modelId>` argument or an
unsupported flag is rejected with `ArgumentException`; both the unknown-id failure path and the
missing-argument path are also proven, cleanly and non-zero-exit, against the built tool.

**Requirement coverage**: `SpeechCli-ModelCommands-Download`.

#### DownloadCommand Cancellation

**Tests**: `DownloadCommand_RunAsync_CanceledBeforeStart_ReportsErrorAndStopsBatch`,
`DownloadCommand_RunAsync_Canceled_ReportsErrorAndStopsBatch`

**Scenario/Expected**: A token already canceled before the batch starts is reported and no model
is attempted; separately, the cancellation token is canceled only after the first requested
model's download has genuinely started and reported progress, proving the in-progress model's
cancellation is reported and no further requested model is attempted - cancellation stops the
whole batch rather than being treated as an ordinary per-model failure, whether it lands before
or during a model's download.

**Requirement coverage**: `SpeechCli-ModelCommands-DownloadCancellation`.

#### UninstallCommand

**Tests**: `UninstallCommand_Run_InstalledModel_UninstallsAndReportsSuccess`,
`UninstallCommand_Run_NeverInstalledModel_IsSafeNoOp`,
`UninstallCommand_Run_StoreThrows_ThrowsInvalidOperationException`,
`UninstallCommand_Run_MissingModelIdArgument_ThrowsArgumentException`,
`UninstallCommand_Run_ExtraArgument_ThrowsArgumentException`,
`SpeechCli_UninstallCommand_Invoked_NeverInstalledModel_ExitsCleanly`

**Scenario/Expected**: An installed model is uninstalled and reported successful; a never-
installed model is treated the same way (safe, successful no-op), never as an error; a
`SpeechModelStoreException` thrown by the store is caught and re-thrown as
`InvalidOperationException` so the CLI's clean-error convention applies; a missing or extra
positional argument is rejected with `ArgumentException`; the never-installed-model path exits
`0` against the built tool.

**Requirement coverage**: `SpeechCli-ModelCommands-Uninstall`.

#### CleanCommand

**Tests**: `CleanCommand_Run_KnownModel_CleansUpAndReportsSuccess`,
`CleanCommand_Run_MissingModelIdArgument_ThrowsArgumentException`,
`CleanCommand_Run_ExtraArgument_ThrowsArgumentException`,
`SpeechCli_CleanCommand_Invoked_NothingToCleanUp_ExitsCleanly`

**Scenario/Expected**: A model with nothing (or something) to clean up is reported successful
either way, since `CleanUpLeftovers` never throws; a missing or extra positional argument is
rejected with `ArgumentException`; the nothing-to-clean-up path exits `0` against the built tool.

**Requirement coverage**: `SpeechCli-ModelCommands-Clean`.

#### Seam Delegation and Null Guards

**Tests**: `ListModelsCommand_Run_NullContext_ThrowsArgumentNullException`,
`ListModelsCommand_Run_NullCatalog_ThrowsArgumentNullException`,
`ModelInfoCommand_Run_NullContext_ThrowsArgumentNullException`,
`ModelInfoCommand_Run_NullCatalog_ThrowsArgumentNullException`,
`DownloadCommand_RunAsync_NullContext_ThrowsArgumentNullException`,
`DownloadCommand_RunAsync_NullCatalog_ThrowsArgumentNullException`,
`UninstallCommand_Run_NullContext_ThrowsArgumentNullException`,
`UninstallCommand_Run_NullCatalog_ThrowsArgumentNullException`,
`CleanCommand_Run_NullContext_ThrowsArgumentNullException`,
`CleanCommand_Run_NullCatalog_ThrowsArgumentNullException`,
`SpeechModelCatalogAdapter_Enumerate_ReturnsCompiledInKnownModels`,
`SpeechModelCatalogAdapter_UninstallAndCleanUpLeftovers_NeverInstalledModel_DoNotThrow`,
`SpeechModelCatalogAdapter_DownloadAsync_UnknownModelId_ThrowsArgumentException`,
`CliModelCatalogFactory_Create_WithModelsDir_UsesGivenRoot`,
`CliModelCatalogFactory_Create_NullContext_ThrowsArgumentNullException`

**Scenario/Expected**: Every command rejects a `null` context or catalog immediately with
`ArgumentNullException`; `SpeechModelCatalogAdapter.Enumerate()` returns the library's real,
compiled-in known-model list; `Uninstall`/`CleanUpLeftovers` against a real, empty store never
throw; `DownloadAsync` against a real catalog throws `ArgumentException` for an id absent from
the known-model list; `CliModelCatalogFactory.Create` honors `--models-dir` and rejects a `null`
context.

**Requirement coverage**: `SpeechCli-ModelCommands-LibraryDelegation`.

### Requirements Coverage

- **`SpeechCli-ModelCommands-ListModels`**: see _ListModelsCommand_ above
- **`SpeechCli-ModelCommands-ModelInfo`**: see _ModelInfoCommand_ above
- **`SpeechCli-ModelCommands-Download`**: see _DownloadCommand_ above
- **`SpeechCli-ModelCommands-DownloadCancellation`**: see _DownloadCommand Cancellation_ above
- **`SpeechCli-ModelCommands-Uninstall`**: see _UninstallCommand_ above
- **`SpeechCli-ModelCommands-Clean`**: see _CleanCommand_ above
- **`SpeechCli-ModelCommands-LibraryDelegation`**: see _Seam Delegation and Null Guards_ above

### Acceptance Criteria

A ModelCommandsSubsystem test run passes when: `list-models` lists and filters the catalog
correctly in both table and JSON form; `model-info` prints a known model's full identity and
parameter surface and cleanly rejects an unknown id; `download` installs every requested model it
can, isolates and reports any single model's failure without abandoning the rest of the batch,
honors `--force`'s uninstall-then-redownload semantics, and stops the whole batch (rather than
continuing) on a genuine cancellation; `uninstall` and `clean` both treat "nothing to do" as a
successful no-op; and every command rejects a `null` context or catalog and forwards correctly to
a real, isolated `SpeechModelCatalog`/`SpeechModelStore` with no network access.

## Manual / Build-Time Verification

The following are verified manually for this pass, rather than by an automated test, because a
real model download requires network access unavailable in CI:

- `dotnet run --project src/DemaConsulting.Speech.Cli -- list-models` against an isolated
  `--models-dir` lists the real compiled-in known models with sane column output
- `dotnet run --project src/DemaConsulting.Speech.Cli -- model-info <a-real-known-model-id>`
  prints a real model's full identity and parameter surface without requiring it to be downloaded
