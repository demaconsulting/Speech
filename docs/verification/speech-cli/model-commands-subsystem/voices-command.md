### VoicesCommand Verification

#### Verification Approach

The command is verified with a `FakeCliModelCatalog` that supplies packs and pack states, records
install and uninstall calls, can fail, throw a store exception or cancel, and reports progress, with a
capturing `Context` and a controlled `CancellationToken`. One out-of-process test in
`test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs` (outside this subsystem's review set) proves
`voices packs` against the built tool. No pack is downloaded. The never-installed uninstall against a
real store is verified in the `SpeechModelCatalogAdapter` verification document, and the mirror applied
to an install in the `CliModelCatalogFactory` verification document.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- `FakeCliModelCatalog`; no network

#### Acceptance Criteria

The command is verified when `packs` lists state, sizes and the consent note, `install` honors the
emotion options, reports progress and the consent note and gives a non-zero exit code on failure or
cancellation, `uninstall` forwards and reports, every Ctrl+C-driven cancellation is reported cleanly
while a cancellation not driven by the token propagates, and invalid arguments are an
`ArgumentException`.

#### Test Scenarios

##### VoicesCommand_RunAsync_Packs_ListsPackStateAndConsentNote

##### SpeechCli_VoicesPacksCommand_Invoked_ListsPacksAndConsentNote

**Scenario**: `voices packs` is run in-process and against the built tool.

**Expected**: Each pack's state and the consent note are printed.

##### VoicesCommand_RunAsync_Packs_ShowsDownloadAndInstalledSizes

**Scenario**: Packs with known sizes and installed variants are listed.

**Expected**: The download size, installed sizes without and with emotions, and the installed variant
are shown, with sizes labeled `MiB` and never `MB`.

##### VoicesCommand_RunAsync_PacksCanceled_ReportsErrorExitCode

**Scenario**: The token is canceled while packs are being listed.

**Expected**: A "canceled" error is reported with exit code `1` and no exception escapes.

##### VoicesCommand_RunAsync_PacksCanceledWithNoInstalledPacks_ReportsErrorExitCode

**Scenario**: The token is already canceled and no pack is installed, so no per-pack state query observes it.

**Expected**: A "canceled" error is reported with exit code `1` and no pack listing is printed.

##### VoicesCommand_RunAsync_Install_ReportsProgressAndConsentNote

**Scenario**: A pack is installed and progress is reported.

**Expected**: The install is forwarded to the catalog, progress lines are printed and the consent note
follows a successful install.

##### VoicesCommand_RunAsync_Install_DefaultIncludesEmotions

**Scenario**: `install` is run without `--no-emotions`.

**Expected**: Emotions are requested.

##### VoicesCommand_RunAsync_InstallNoEmotions_InstallsBaseVoicesOnly

**Scenario**: `install` is run with `--no-emotions`.

**Expected**: Only the base voices are requested.

##### VoicesCommand_RunAsync_InstallOverVoicesOnly_AnnouncesUpgrade

**Scenario**: A voices-only install exists and emotions are requested.

**Expected**: The command announces that the pack is removed and downloaded again.

##### VoicesCommand_RunAsync_InstallFails_ReportsErrorExitCode

##### VoicesCommand_RunAsync_InstallCanceled_ReportsErrorExitCode

**Scenario**: The install outcome is a failure (a checksum mismatch), or the token is canceled during the install.

**Expected**: An error with the reason is reported with exit code `1`; no success text or consent note is
printed and the pack stays not installed.

##### VoicesCommand_RunAsync_InstallCanceledDuringPreCheck_ReportsErrorExitCode

**Scenario**: The token is canceled during the emotions pre-check of `install`.

**Expected**: The cancellation is reported with a non-zero exit code.

##### VoicesCommand_RunAsync_InstallCanceledWithoutToken_Propagates

**Scenario**: The catalog throws an `OperationCanceledException` while the token is not canceled.

**Expected**: The exception propagates, because it is not an operator interruption.

##### ConsoleCancellation_Register_Raise_CancelsSourceRunsCallbackAndSuppressesTermination

**Scenario**: The console cancel signal is raised through the registration's internal seam.

**Expected**: The source is canceled, the callback runs and default termination is suppressed (a
`ConsoleCancellationTests` test).

##### VoicesCommand_RunAsync_Uninstall_ForwardsAndReports

**Scenario**: A pack is uninstalled.

**Expected**: The catalog's uninstall is called and success is reported.

##### VoicesCommand_RunAsync_UninstallStoreThrows_ThrowsInvalidOperationException

**Scenario**: The store throws a `SpeechModelStoreException` during uninstall.

**Expected**: It is rethrown as `InvalidOperationException`.

##### VoicesCommand_RunAsync_UninstallCanceled_ReportsErrorExitCode

**Scenario**: The token is canceled.

**Expected**: An error is reported with exit code `1`.

##### VoicesCommand_RunAsync_InstallUnknownPack_ThrowsArgumentException

**Scenario**: `install` is given an unknown pack id.

**Expected**: An `ArgumentException` is thrown before any output, and no install is attempted.

##### VoicesCommand_RunAsync_BadArguments_ThrowsArgumentException

**Scenario**: A theory over `""`, `bogus`, `install`, `uninstall`, `install voice-zero-1.0 extra` and
`packs extra`.

**Expected**: An `ArgumentException` is thrown for each: missing sub-action, unknown sub-action, missing
pack id and extra argument.

##### VoicesCommand_RunAsync_MissingSubAction_ReportsNoParameterName

##### VoicesCommand_RunAsync_UnknownSubAction_ReportsNoParameterName

**Scenario**: `voices` is run with no sub-action, or with an unknown one.

**Expected**: An `ArgumentException` whose parameter name is `null`.

##### VoicesCommand_RunAsync_NullArguments_Throw

**Scenario**: The command is called with a `null` context or catalog.

**Expected**: An `ArgumentNullException` is thrown. This is a null guard and carries no requirement.
