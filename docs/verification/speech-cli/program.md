## Program Verification

### Verification Approach

`Program` is verified in-process with the real `Context`, `CommandDispatch` and `Validation` types:
`RunAsync` is driven with contexts built from argument lists while the console output stream is
redirected to a writer the test inspects, and `Main` is driven with argument lists while the error
stream is redirected, so exit codes and error text are asserted without a child process. The
out-of-process integration tests in `IntegrationTests.cs` run the built tool the way an operator
would and assert on its console output and exit code. Subcommand behavior itself is verified in
each command subsystem; here only that the table dispatches to the real handlers is shown.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Console redirection is process-global, so every test restores the original writers
  in a `finally` block and the test classes run sequentially; dispatch tests use a temporary models
  directory that is removed afterward
- **Test doubles**: None; no network, audio hardware or installed model is required

### Acceptance Criteria

The unit passes when version output contains only the version; help (long, short and `-?` forms,
and no arguments) lists every global option and subcommand; only the highest-priority of
version, help and validation runs; a subcommand name reaches its real handler; expected errors
return exit code 1 with a one-line message, suppressed by `--silent`; and help never contains an
environment secret value.

### Test Scenarios

#### Program_Run_WithVersionFlag_DisplaysVersionOnly

**Scenario**: `RunAsync` receives `--version`.

**Expected**: The output contains the version and neither the banner nor the copyright line;
exit code 0.

#### Program_Run_WithShortVersionFlag_DisplaysVersion

**Scenario**: `RunAsync` receives `-v`.

**Expected**: The output contains the version and exit code is 0.

#### Program_Version_ReturnsNonEmptyString

**Scenario**: The `Version` property is read.

**Expected**: A non-empty, non-whitespace string.

#### Program_Run_WithHelpFlag_DisplaysUsageInformation

**Scenario**: `RunAsync` receives `--help`.

**Expected**: Usage, command list, global options (including `--version`, `--help`, `--models-dir`),
the masked bearer-token help line, and the name of every dispatch-table command; exit code 0.

#### Program_Run_WithShortHelpFlag_DisplaysUsage

**Scenario**: `RunAsync` receives `-h`.

**Expected**: Usage is displayed; exit code 0.

#### Program_Run_WithQuestionMarkFlag_DisplaysUsage

**Scenario**: `RunAsync` receives `-?`.

**Expected**: Usage is displayed; exit code 0.

#### Program_Run_NoArguments_DisplaysBannerAndUsage

**Scenario**: `RunAsync` receives no arguments.

**Expected**: The banner and the usage text are displayed, the context has no command, and the exit
code is 0.

#### Program_Run_Help_DocumentsVoicesOptionsAndConsentNote

**Scenario**: `RunAsync` receives `--help`.

**Expected**: The text names `--voices-dir`, `list-voices`, the `voices` usage summary, the
`--tts-param voice=<voiceId>` hint and the consent note.

#### Program_Run_Help_DocumentsMirrorEnvironmentVariables

**Scenario**: `RunAsync` receives `--help` with an empty fake environment.

**Expected**: The four `DEMASPEECH_MIRROR_*` variable names appear.

#### Program_Run_Help_DoesNotPrintEnvironmentSecretValues

**Scenario**: `RunAsync` receives `--help` with a fake environment holding a mirror URL and a bearer
token.

**Expected**: The token value does not appear in the output.

#### Program_Run_VersionHelpValidateTogether_HonorsPriorityOrder

**Scenario**: `RunAsync` receives `--validate --help --version`, then `--validate --help`.

**Expected**: The first prints the version only (no usage, no validation summary); the second prints
usage and no validation summary. The priority of the subcommand relative to these three flags has
no dedicated test.

#### Program_Run_WithValidateFlag_RunsValidation

**Scenario**: `RunAsync` receives `--validate`.

**Expected**: The validation summary (`Total Tests:`) is printed and the exit code is 0.

#### Program_Run_WithListModelsCommand_ListsModelsAndSucceeds

**Scenario**: `RunAsync` receives `--models-dir <temp> list-models`.

**Expected**: The real model-listing handler runs, output is produced and exit code is 0.

#### Program_Run_WithListDevicesCommand_CompletesWithSuccessExitCode

**Scenario**: `RunAsync` receives `list-devices`.

**Expected**: Completes with exit code 0.

#### Program_Run_WithDoctorCommand_ProducesReportWithoutThrowing

**Scenario**: `RunAsync` receives `--models-dir <temp> doctor`.

**Expected**: No exception and a non-empty report.

#### Program_Run_WithSpeakCommandAndNoModel_ThrowsArgumentException

**Scenario**: `RunAsync` receives `speak` with no model.

**Expected**: `ArgumentException` from the real `speak` handler, proving the dispatch wiring.

#### Program_Run_WithRecognizeCommandAndNoModel_ThrowsArgumentException

**Scenario**: `RunAsync` receives `recognize` with no model.

**Expected**: `ArgumentException` from the real `recognize` handler.

#### Program_Run_WithAskCommandAndNoModel_ThrowsArgumentException

**Scenario**: `RunAsync` receives `ask` with no model.

**Expected**: `ArgumentException` from the real `ask` handler.

#### Program_Run_WithNullContext_ThrowsArgumentNullException

**Scenario**: `RunAsync` receives a null context.

**Expected**: `ArgumentNullException`, surfaced through the returned task.

#### Program_Main_WithInvalidArgs_ReturnsNonZeroExitCode

**Scenario**: `Main` receives an unrecognized argument.

**Expected**: Exit code 1.

#### Program_Main_WithUnknownCommand_ReturnsNonZeroExitCodeAndReportsCommand

**Scenario**: `Main` receives an unknown subcommand name.

**Expected**: Exit code 1 and an error message containing the name.

#### Program_Main_WithInvalidArgsAndSilentFlag_SuppressesErrorOutput

**Scenario**: `Main` receives an unrecognized argument and `--silent`.

**Expected**: Exit code 1 and an empty error stream, proving the raw-argument `--silent` fallback
when no context could be built.

#### Program_Main_WithUnknownCommandAndSilentFlag_SuppressesErrorOutput

**Scenario**: `Main` receives an unknown subcommand name and `--silent`.

**Expected**: Exit code 1 and an empty error stream.

#### SpeechCli_UnknownOption_Provided_ReturnsCleanError

**Scenario**: The built tool is run as a child process with `--unknown`.

**Expected**: A non-zero exit code and an error message that names the argument.

#### SpeechCli_UnknownCommand_Provided_ReturnsCleanError

**Scenario**: The built tool is run as a child process with an unknown subcommand name.

**Expected**: A non-zero exit code and a clean error message, no stack trace.

#### SpeechCli_VersionFlag_Provided_OutputsVersion

**Scenario**: The built tool is run as a child process with `--version`.

**Expected**: The version is printed and the exit code is 0.

#### SpeechCli_HelpFlag_Provided_ListsAllSubcommands

**Scenario**: The built tool is run as a child process with `--help`.

**Expected**: Every subcommand is listed and the exit code is 0.

#### SpeechCli_NoArguments_Invoked_DisplaysBannerAndUsage

**Scenario**: The built tool is run as a child process with no arguments.

**Expected**: The banner and usage text are printed and the exit code is 0.

### Not Covered By A Dedicated Test

- An unexpected (non-`ArgumentException`, non-`InvalidOperationException`) exception is rethrown after
  `Unexpected error:` is printed; no test injects such a failure.
- The `Version` fallback chain (informational version, assembly version, `0.0.0`) is not exercised
  beyond the non-empty assertion.
- Subcommand dispatch has no test showing that a subcommand loses to `--help` or `--validate`.
