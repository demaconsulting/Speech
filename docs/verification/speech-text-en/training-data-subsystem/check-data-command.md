### CheckDataCommand Verification

#### Verification Approach

`CheckDataCommand` and the `Program` entry glue are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/CheckDataCommandTests.cs`. The tests run the
command with the real units over the small valid data folder of the dataset fixture, with string writers for the
standard output and error, an injected current directory and tool directory, and an injected environment lookup, so no
test depends on the real environment variables or on the working directory of the test process.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder and passes its own arguments

#### Acceptance Criteria

**Exit codes** (`ExitCodes`). A valid folder gives 0 and skips the external check; a finding gives 1 and names the
check, file and line, as does an evaluation folder inside the data folder. A usage or input error (unknown option,
missing or invalid value, missing data folder, malformed path, missing or hollow evaluation folder, unknown command)
gives 2 with a message on the error stream.

**Options** (`Options`). `--help` prints the usage and the exit codes; the data folder comes from `--data` (also with an
equals sign) or is discovered from the current directory and then from the tool directory; the evaluation folder comes
from `--eval-sets` or the environment variable, the option winning; `--leakage-report` writes the report and is a usage
error without evaluation sets or inside the data folder; `--max-findings` caps the printed findings; `--counts` prints
the per-reading table.

**Staged generalization set** (`StagedGeneralization`). `--generalization` is parsed with a space and with an equals
sign; with a staged file outside the data folder that repeats a training sentence the command exits 1 with an `overlay`
finding located in the staged file that carries the generic reason, says which file it checked and names no training
row; a staged file that does not exist is a usage error with exit code 2.

**Corpus check** (`ClusterCheck`). With a consistent pair of corpus files the command exits 0 and reports the book
counts; after the pin of the used book is blanked it exits 1 with a `clusters` finding on that row of the source
list.

#### Test Scenarios

##### Exit codes

**Tests**: `CheckDataCommand_Run_ValidData_ExitsZeroAndSkipsExternalCheck`,
`CheckDataCommand_Run_Violation_ExitsOneAndNamesCheckFileAndLine`,
`CheckDataCommand_Run_EvalSetsInsideDataFolder_ExitsOneWithFinding`,
`CheckDataCommand_Run_UnknownOption_ExitsTwoWithMessage`, `CheckDataCommand_Run_OptionWithoutValue_ExitsTwo`,
`CheckDataCommand_Run_InvalidMaxFindings_ExitsTwo`, `CheckDataCommand_Run_DataFolderMissing_ExitsTwo`,
`CheckDataCommand_Run_EvalSetsFolderMissing_ExitsTwo`, `CheckDataCommand_Run_MalformedPath_ExitsTwo`,
`Program_Run_MissingOrUnknownCommand_ExitsTwoWithUsage`

The data-folder test covers a given and an undiscoverable folder, and the evaluation-folder test a missing folder, an
empty folder and an empty file, each in its own run.

##### Options

**Tests**: `Program_Run_CheckDataHelp_PrintsCommandUsage`, `CheckDataCommand_Run_DataOptionWithEquals_IsAccepted`,
`CheckDataCommand_Run_NoDataOption_DiscoversFolderFromCurrentDirectory`,
`CheckDataCommand_Run_EvalSetsFromEnvironment_RunsExternalCheck`,
`CheckDataCommand_Run_EvalSetsOptionAndEnvironment_OptionTakesPrecedence`,
`CheckDataCommand_Run_LeakageReportOption_WritesReportFile`,
`CheckDataCommand_Run_LeakageReportWithoutSetsOrInsideData_ExitsTwo`,
`CheckDataCommand_Run_MaxFindings_CapsPrintedFindings`, `CheckDataCommand_Run_CountsOption_PrintsPerReadingTable`

The discovery test runs once from a folder below the data's parent and once from an unrelated folder with the tool
directory below it.

##### Staged generalization set

**Tests**: `CheckDataCommand_Run_StagedGeneralization_ChecksStagedFileWithGenericReasons`,
`CheckDataCommand_Run_StagedGeneralizationMissing_ExitsTwo`, `CheckDataCommand_TryParse_GeneralizationOption_IsParsed`

##### Corpus check

**Test**: `CheckDataCommand_Run_ClusterFinding_ExitsOne`
