## CommandDispatch Verification

### Verification Approach

`CommandDispatch` is verified with unit tests over the real dispatch table. One test looks up
names with `Find` (including case and empty-name boundaries); one checks that every descriptor has
a unique name and a usage summary; one invokes every registered handler with a context built for
that command and asserts, for every command, that it either completes or reports a documented
operator error (`ArgumentException` or `InvalidOperationException`); and one asserts that a
synchronous command (`model-info`) and an asynchronous command (`download`) both surface their
`ArgumentException` to an awaiting caller. Which handler does what is verified in each command
subsystem; the table's validity is also checked at run time by the `--validate` self-test, see the
Validation verification.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: The handler test uses a temporary models directory that is removed afterward, and
  restores the console writers it redirects; the class runs in the sequential collection
- **Test doubles**: None

### Acceptance Criteria

The unit passes when the voice subcommands resolve through `Find` and near-miss names do not,
every handler in the table completes or reports only a documented operator error, a handler
fault surfaces to the caller for both synchronous and asynchronous commands, every descriptor has
a unique name and a usage summary, and an unknown name is not resolved so that the caller rejects
it.

### Test Scenarios

#### CommandDispatch_Find_VoiceCommands_AreRegistered

**Scenario**: `Find` is called with `list-voices` and with `voices`, then with `VOICES` and with an
empty name.

**Expected**: A descriptor with the matching name is returned for each of the first two; `null` for
the wrong-case and empty names.

#### CommandDispatch_Commands_EveryDescriptorHasNameAndUsageSummary

**Scenario**: Every descriptor in the table is inspected.

**Expected**: The table is not empty, names are unique, and each name and usage summary is non-blank.

#### CommandDispatch_Handlers_RunAndReportOnlyOperatorErrors

**Scenario**: Every descriptor's handler is invoked with a context for its own name (and a temporary
models directory) and awaited.

**Expected**: Each handler either completes or fails with `ArgumentException` or
`InvalidOperationException`; any other exception type fails the test. The outcome of every command is
asserted, none is skipped.

#### CommandDispatch_Handler_MissingArgument_SurfacesFaultForSyncAndAsyncCommands

**Scenario**: `model-info` (synchronous) and `download` (asynchronous) are invoked without a model id
and awaited.

**Expected**: Both surface an `ArgumentException`; neither fault is masked.

#### Program_Main_WithUnknownCommand_ReturnsNonZeroExitCodeAndReportsCommand

**Scenario**: `Main` receives a name that is not in the table, so `Find` returns no descriptor.

**Expected**: The name is rejected with exit code 1 and an error naming it, rather than reaching any
handler.

#### SpeechCli_UnknownCommand_Provided_ReturnsCleanError

**Scenario**: The built tool is run as a child process with a name that is not in the table.

**Expected**: A non-zero exit code and a clean error message.

#### Program_Run_WithHelpFlag_DisplaysUsageInformation

**Scenario**: `--help` is run and the usage text is compared with the table.

**Expected**: The name of every table entry appears in the usage text.

#### SpeechCli_HelpFlag_Provided_ListsAllSubcommands

**Scenario**: The built tool is run as a child process with `--help`.

**Expected**: Every subcommand is listed.

### Not Covered By A Dedicated Test

- `Find` with a null name (`ArgumentNullException`) is not asserted directly.
