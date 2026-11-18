### TrainCommand Verification

#### Verification Approach

`TrainCommand` and the `Program` entry glue that dispatches to it are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/TrainCommandTests.cs`. The tests call the command as
a function through the command harness, with the real units, over the small trainable data folder of the training
fixture, with string writers for the standard output and error and a temporary model folder, so that no test depends on
the real data folder, the working directory of the test process or the committed locations. Each staleness test changes
exactly one thing in a trained model folder (a training row, a development row, one recorded value, the model file, the
record) and checks the exit code and the line that names the cause. The default-locations test lays the data folder out
under a temporary base folder like the repository, so the committed locations can be written and checked without
touching the real ones. Nothing is mocked. The check of the real committed model and record is not a unit test: it is
the subsystem test `TrainingSubsystem_TrainCheck_CommittedModelAndRecord_AreCurrent` (see *SpeechTextEn
TrainingSubsystem Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary base folder, data folder and model folder

#### Acceptance Criteria

`--help` prints the usage and exits 0; an unknown option, `--seed` or `--model-dir` without a value, a value that is not
a number, an out-of-range value (nothing written) and a data folder that does not exist exit 2 with a message on the
error stream that names the problem; an experiment option without `--model-dir` exits 2 and leaves the committed files
untouched, and one combined with `--check` exits 2; training the fixture exits 0, writes a model the runtime loads and a
record that parses, with the same hash size, the same weight count, the image hash and the kept epoch count printed;
training again keeps the evaluation and gate settings of the existing record; without `--model-dir` the model and the
record are written to the committed locations under the base folder and `--check` accepts them; a fresh model passes the
check, and so does a model file that holds the same model with an uncompressed payload; a changed training row exits 1
with `STALE: the training data changed` and `FAILED`; a changed development row exits 1; a changed recorded setting
exits 1 with `weights differ`; a model file with other weights exits 1 with `committed model file differs`; a corrupt
model file exits 1 with `not a valid model` and a missing one with `is missing`; a record that is not valid exits 1 with
`not valid` and a missing record exits 1 naming the record; a record that names another tagger version exits 1 with
`tagger changed`; a label that is not a candidate exits 2 naming the row, both when training (and no model is written)
and when checking.

#### Test Scenarios

##### Usage errors

**Tests**: `TrainCommand_Run_Help_PrintsUsageAndExitsZero`,
`TrainCommand_Run_BadCommandLine_ExitsTwoNamingTheProblem` (a theory over an unknown option and two options without a
value), `TrainCommand_Run_NonNumericExperimentValue_ExitsTwo`,
`TrainCommand_Run_ExperimentValueOutOfRange_ExitsTwoAndWritesNothing`, `TrainCommand_Run_MissingDataFolder_ExitsTwo`

Each command line is run and the exit code, the stream and the message are checked; for the out-of-range value the
model folder is checked not to exist.

##### Experiment isolation

**Tests**: `TrainCommand_Run_ExperimentOptionWithoutModelDir_ExitsTwoAndLeavesCommittedFilesUntouched`,
`TrainCommand_Run_ExperimentOptionWithCheck_ExitsTwo`

An experiment option is given without a model folder, and then with the check option. Both exit 2 with a message that
names `--model-dir` or `--check`; in the first case the folders of the committed locations are checked not to exist.

##### Writing the model and the record

**Tests**: `TrainCommand_Run_Fixture_WritesLoadableModelAndRecord`,
`TrainCommand_Run_DefaultLocations_WritesCommittedFilesThatCheckAccepts`

The written model is loaded by the runtime's loader and the record parsed; their hash sizes and counts agree and the
output reports the image hash and the kept epochs. With no model folder the files appear at the committed locations and
the check exits 0.

##### Keeping the gate settings

**Test**: `TrainCommand_Run_ExistingRecord_KeepsItsEvaluationSettings`

The gate threshold of the record is edited to 500 per mille, training runs again and the parsed new record still holds
500.

##### Current models

**Tests**: `TrainCommand_Run_CheckFreshModel_ExitsZero`, `TrainCommand_Run_CheckRecompressedButEqualModel_ExitsZero`

A freshly trained model passes with `OK`. A model file replaced by the uncompressed image of the same model also passes.

##### Stale models

**Tests**: `TrainCommand_Run_CheckAfterTrainingRowChanged_ExitsOneNamingTheData`,
`TrainCommand_Run_CheckAfterDevRowChanged_ExitsOne`,
`TrainCommand_Run_CheckAfterRecordedSettingChanged_ExitsOneNamingTheWeights`,
`TrainCommand_Run_CheckAfterModelFileReplaced_ExitsOneNamingTheModelFile`,
`TrainCommand_Run_CheckCorruptOrMissingModelFile_ExitsOne`, `TrainCommand_Run_CheckMissingOrInvalidRecord_ExitsOne`,
`TrainCommand_Run_CheckAfterTaggerVersionChanged_ExitsOneNamingTheTagger`

Each cause is introduced into a trained model folder and the check is run; the exit code is 1 and the output contains
the line listed in the acceptance criteria. The tagger version is changed in the record, not in the tagger, so the test
shows that the check compares the recorded version with the running one.

##### Data errors

**Tests**: `TrainCommand_Run_CheckWithDataError_ExitsTwo`, `TrainCommand_Run_LabelNotACandidate_ExitsTwoNamingTheRow`

A training row is relabeled with a reading of another word, once before training and once after a model was trained and
checked; both exit 2 and the error stream contains the row identifier; the first writes no model.
