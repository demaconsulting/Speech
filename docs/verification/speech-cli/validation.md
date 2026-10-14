## Validation Verification

### Verification Approach

`Validation` has no test class of its own: it is exercised through the real `--validate` path.
In-process tests run `Program.RunAsync` and `Program.Main` with `--validate` and assert on the
printed summary, the results file and the exit code; the out-of-process integration tests run the
built tool and assert on the same. Because the self-test checks are written to pass on any machine,
a run reporting `Failed: 0` shows that all five checks (version, help, subcommand registration, audio-backend
probe, model-store root writable) passed on the host. The failure paths of the individual checks
cannot be provoked without injecting a fault, so they are covered by review of the per-check
exception handling rather than by a test; only the results-file failure paths have tests.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Results files are written to uniquely named temporary paths and removed afterward;
  console writers are redirected and restored; the self-test creates and deletes its own temporary
  directory and marker file
- **Dependencies**: No network, no installed model and no audio hardware are required. The
  audio-backend check enumerates whatever devices the host has, including none

### Acceptance Criteria

The unit passes when `--validate` completes with each of the five checks (version, help,
subcommand registration, audio backend, model store) reported passed and a `Total Tests:` /
`Passed:` / `Failed: 0` summary and exit code 0 on a clean host; writes a TRX file for a `.trx`
path and a JUnit file for a `.xml` path; honors `--depth` for the report heading; and reports an
unsupported results extension, and separately an unwritable results path, as an error with exit
code 1.

### Test Scenarios

#### Program_Run_WithValidateFlag_RunsValidation

**Scenario**: `RunAsync` receives `--validate`.

**Expected**: The summary containing `Total Tests:` is printed and the exit code is 0.

#### Program_Run_WithValidateFlag_ReportsVersionCheckPassed

**Scenario**: `RunAsync` receives `--validate`.

**Expected**: The output contains `✓ SpeechCli_VersionDisplay - Passed`.

#### Program_Run_WithValidateFlag_ReportsHelpCheckPassed

**Scenario**: `RunAsync` receives `--validate`.

**Expected**: The output contains `✓ SpeechCli_HelpDisplay - Passed`.

#### Program_Run_WithValidateFlag_ReportsSubcommandRegistrationCheckPassed

**Scenario**: `RunAsync` receives `--validate`.

**Expected**: The output contains `✓ SpeechCli_DispatchTableWellFormed - Passed`.

#### Program_Run_WithValidateFlag_ReportsAudioBackendCheckPassed

**Scenario**: `RunAsync` receives `--validate`, on a host with or without audio devices.

**Expected**: The output contains `✓ SpeechCli_AudioBackendProbe - Passed`.

#### Program_Run_WithValidateFlag_ReportsModelStoreCheckPassed

**Scenario**: `RunAsync` receives `--validate`.

**Expected**: The output contains `✓ SpeechCli_ModelStoreRootWritable - Passed`.

#### Program_Run_WithValidateFlag_PrintsSummaryTotals

**Scenario**: `RunAsync` receives `--validate`.

**Expected**: The summary shows `Total Tests: 5`, `Passed: 5` and `Failed: 0`; exit code 0.

#### SpeechCli_ValidateFlag_Provided_RunsValidation

**Scenario**: The built tool is run as a child process with `--validate`.

**Expected**: Exit code 0 and the summary shows `Total Tests:`, `Passed:` and `Failed: 0`, so every
check passed without any model or hardware requirement.

#### SpeechCli_ValidateWithTrxResults_Requested_GeneratesTrxFile

**Scenario**: The built tool is run with `--validate --results <path>.trx`.

**Expected**: Exit code 0 and a TRX results file containing a `TestRun` element.

#### Program_Main_ValidateWithXmlResults_WritesJUnitFile

**Scenario**: `Main` receives `--validate --results <path>.xml`.

**Expected**: Exit code 0 and a JUnit file containing a `testsuite` element.

#### SpeechCli_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel

**Scenario**: The built tool is run with `--validate --depth 3`.

**Expected**: Exit code 0 and the output contains a level-3 markdown heading (`###`).

#### Program_Main_ValidateWithUnsupportedResultsExtension_ReturnsNonZeroExitCode

**Scenario**: `Main` receives `--validate --results <path>.txt`.

**Expected**: Exit code 1, an error stream containing `Unsupported results file format`, and no file
written.

#### Program_Main_ValidateWithUnwritableResultsPath_ReturnsNonZeroExitCode

**Scenario**: `Main` receives `--validate --results` for a `.trx` path whose folder does not exist.

**Expected**: Exit code 1 and a non-empty error stream.

#### Program_Run_VersionHelpValidateTogether_HonorsPriorityOrder

**Scenario**: `--validate` is combined with `--help` and with `--version` (see the Program
verification for `Program_Run_VersionHelpValidateTogether_HonorsPriorityOrder` and
`Program_Run_HelpValidateTogether_HelpTakesPriority`).

**Expected**: Validation does not run (no `Total Tests:` line) when a higher-priority action is also
requested.

### Not Covered By A Dedicated Test

- The failure branch of each individual check (an exception, an empty or duplicate dispatch entry, a
  missing command in the help log, a read-only model-store root) is not provoked.
- Honoring `--models-dir` for the model-store check is not asserted; the default location is used by
  the tests.
- The inner version and help checks' isolation from ambient `DEMASPEECH_MIRROR_*` variables is not
  asserted with a polluted environment.
