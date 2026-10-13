### DownloadCommand Verification

#### Verification Approach

The command is verified with a `FakeCliModelCatalog` that can return any download outcome, report
progress and track `Uninstall` calls, with a capturing `Context` and, for cancellation, a controlled
`CancellationToken`. Two out-of-process tests in
`test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs` (outside this subsystem's review set) prove
the unknown-id and missing-argument paths against the built tool. No test performs a real download.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- `FakeCliModelCatalog` and `FakeSpeechModel`; no network

#### Acceptance Criteria

The command is verified when models download sequentially in the given order, a single failure is
reported without abandoning the batch, `--force` uninstalls only a downloaded model first, a
cancellation stops the whole batch, failures give a non-zero exit code, and invalid arguments are an
`ArgumentException`.

#### Test Scenarios

##### DownloadCommand_RunAsync_SingleModel_Succeeds_ReportsInstalled

**Scenario**: One known model is requested and the catalog reports `Installed`.

**Expected**: The model is reported installed.

##### DownloadCommand_RunAsync_MultipleModels_DownloadsEachSequentially

**Scenario**: Several models are requested.

**Expected**: They download strictly in the order given, never interleaved.

##### DownloadCommand_RunAsync_ChecksumMismatch_ReportsErrorAndContinues

**Scenario**: The first model's download reports a checksum mismatch.

**Expected**: The failure is reported for that model and the remaining models are still attempted.

##### DownloadCommand_RunAsync_UnknownModelId_ReportsErrorAndContinues

**Scenario**: One requested id is unknown to the catalog, so the download throws `ArgumentException`.

**Expected**: The failure is reported for that model only, nothing is uninstalled for it, and the
remaining models are still attempted.

##### SpeechCli_DownloadCommandWithUnknownId_Invoked_ReturnsNonZeroExitCode

**Scenario**: The built tool is run with an unknown model id.

**Expected**: A non-zero exit code.

##### DownloadCommand_RunAsync_ForceOnDownloadedModel_UninstallsFirst

**Scenario**: `--force` is given for a `Downloaded` model.

**Expected**: The model is uninstalled and then downloaded.

##### DownloadCommand_RunAsync_ForceOnMissingModel_DoesNotUninstall

**Scenario**: `--force` is given for a model that is not downloaded.

**Expected**: No uninstall call is made.

##### DownloadCommand_RunAsync_ReportsProgress_WritesPercentLine

**Scenario**: The catalog reports progress during a download.

**Expected**: A percent-complete line is written.

##### DownloadCommand_RunAsync_CanceledBeforeStart_ReportsErrorAndStopsBatch

**Scenario**: The token is already canceled when the batch starts.

**Expected**: The cancellation is reported, no model is attempted and the exit code is non-zero.

##### DownloadCommand_RunAsync_Canceled_ReportsErrorAndStopsBatch

**Scenario**: The token is canceled after the first model's download has started and reported progress.

**Expected**: The in-progress model's cancellation is reported and no further model is attempted.

##### ConsoleCancellation_Register_Raise_CancelsSourceRunsCallbackAndSuppressesTermination

##### ConsoleCancellation_Observe_Raise_RunsCallbackWithoutSuppressingTermination

**Scenario**: The console cancel signal is raised through the registration's internal seam.

**Expected**: `Register` cancels its source, runs its callback and suppresses termination; `Observe` only
runs its callback. These tests live in `ConsoleCancellationTests`.

##### DownloadCommand_RunAsync_NoModelIds_ThrowsArgumentException

##### DownloadCommand_RunAsync_UnsupportedFlag_ThrowsArgumentException

##### SpeechCli_DownloadCommandWithMissingArgument_Invoked_ReturnsCleanError

**Scenario**: No model id, or an unsupported flag, is given; and the built tool is run with no id.

**Expected**: An `ArgumentException` with parameter name `args`; out-of-process, a clean error and a
non-zero exit code.

##### DownloadCommand_RunAsync_NullContext_ThrowsArgumentNullException

##### DownloadCommand_RunAsync_NullCatalog_ThrowsArgumentNullException

**Scenario**: `RunAsync` is called with a `null` context or catalog.

**Expected**: An `ArgumentNullException` is thrown. These are null guards and carry no requirement.
