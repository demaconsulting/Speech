### CheckDataCommand Verification

#### Verification Approach

`CheckDataCommand` and the `Program` entry glue are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/CheckDataCommandTests.cs`. The tests run the
command with the real units over the small valid data folder of the dataset fixture, with string writers for the
standard output and error, an injected current directory and tool directory, and an injected environment lookup, so that
no test depends on the real environment variables or on the working directory of the test process.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder and passes its own arguments

#### Acceptance Criteria

A valid folder gives exit code 0 and skips the external check; a folder with a finding gives 1 and the line names the
check, the file and the line. An unknown option, an option without a value, a non-positive or non-numeric
`--max-findings`, a missing data folder, no data folder anywhere, a malformed data or evaluation path, a missing or
empty evaluation folder, an evaluation file without a sentence and a missing or unknown command give 2 with a message on
the error stream. `--help` prints the usage with exit code 0. The data folder comes from `--data` (also with an equals
sign) or is discovered from the current directory and then from the tool directory; the evaluation folder comes from
`--eval-sets` or the environment variable, the option winning; an overlap with an evaluation set gives exit code 1 and
an exact-match finding, and an evaluation folder inside the data folder is a finding; `--leakage-report` writes the
report file and is a usage error without evaluation sets or inside the data folder; `--max-findings` caps the printed
findings; `--counts` prints the per-reading table.

#### Test Scenarios

##### Exit codes

**Tests**: `CheckDataCommand_Run_ValidData_ExitsZeroAndSkipsExternalCheck`,
`CheckDataCommand_Run_Violation_ExitsOneAndNamesCheckFileAndLine`,
`CheckDataCommand_Run_UnknownOption_ExitsTwoWithMessage`, `CheckDataCommand_Run_OptionWithoutValue_ExitsTwo`,
`CheckDataCommand_Run_InvalidMaxFindings_ExitsTwo`, `CheckDataCommand_Run_DataFolderMissing_ExitsTwo`,
`CheckDataCommand_Run_NoDataFolderAnywhere_ExitsTwo`, `CheckDataCommand_Run_EvalSetsFolderMissing_ExitsTwo`,
`CheckDataCommand_Run_EvalSetsFolderEmpty_ExitsTwo`, `CheckDataCommand_Run_EvalSetsFileEmpty_ExitsTwo`,
`CheckDataCommand_Run_MalformedPath_ExitsTwo`, `Program_Run_MissingOrUnknownCommand_ExitsTwoWithUsage`

##### Options

**Tests**: `CheckDataCommand_Run_Help_PrintsUsageAndExitsZero`, `CheckDataCommand_Run_DataOptionWithEquals_IsAccepted`,
`CheckDataCommand_Run_NoDataOption_DiscoversFolderFromCurrentDirectory`,
`CheckDataCommand_Run_NoDataOption_DiscoversFolderFromToolDirectory`,
`CheckDataCommand_Run_EvalSetsFromEnvironment_RunsExternalCheck`,
`CheckDataCommand_Run_EvalSetsOptionAndEnvironment_OptionTakesPrecedence`,
`CheckDataCommand_Run_LeakageReportOption_WritesReportFile`,
`CheckDataCommand_Run_LeakageReportWithoutSetsOrInsideData_ExitsTwo`,
`CheckDataCommand_Run_MaxFindings_CapsPrintedFindings`, `CheckDataCommand_Run_CountsOption_PrintsPerReadingTable`,
`Program_Run_CheckDataHelp_PrintsCommandUsage`

##### Runs all checks

**Tests**: `CheckDataCommand_Run_ValidData_ExitsZeroAndSkipsExternalCheck`,
`CheckDataCommand_Run_Violation_ExitsOneAndNamesCheckFileAndLine`,
`CheckDataCommand_Run_EvalSetsOptionWithOverlap_ExitsOneAndReportsExactMatch`,
`CheckDataCommand_Run_EvalSetsInsideDataFolder_ExitsOneWithFinding`
