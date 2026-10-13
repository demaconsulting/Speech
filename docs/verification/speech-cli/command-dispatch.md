## CommandDispatch Verification

### Verification Approach

`CommandDispatch` is verified with unit tests over the real dispatch table. One test looks up
names with `Find`; the other invokes every registered handler with a context built for that command
and checks that a task is always returned. Which handler does what is verified in each command
subsystem; the table's validity (unique names, non-empty summaries, non-null handlers) is
checked at run time by the `--validate` self-test, see the Validation verification.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: The handler test uses a temporary models directory that is removed afterward, and
  restores the console writers it redirects; the class runs in the sequential collection
- **Test doubles**: None

### Acceptance Criteria

The unit passes when the voice subcommands resolve through `Find`, every handler in the table is
non-null and yields a non-null task when invoked, and an unknown name is not resolved so that the
caller rejects it.

### Test Scenarios

#### CommandDispatch_Find_VoiceCommands_AreRegistered

**Scenario**: `Find` is called with `list-voices` and with `voices`.

**Expected**: A descriptor is returned for each.

#### CommandDispatch_Handlers_ReturnNonNullTasks

**Scenario**: Every descriptor's handler is invoked with a context for its own name (and a temporary
models directory), recording either a synchronous argument exception or the awaited outcome.

**Expected**: No handler is null and none returns a null task; the success or failure of the
command itself is irrelevant to this test.

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

- `Find` with a null name (`ArgumentNullException`) and its case-sensitive matching are not asserted
  directly.
- The synchronous-throw versus faulted-task difference between adapted and asynchronous handlers is
  tolerated by the handler test rather than asserted.
