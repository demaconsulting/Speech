### TrainCommand Verification

#### Verification Approach

`TrainCommand` and the `Program` entry glue that dispatches to it are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/TrainCommandTests.cs`. The tests call the command as
a function through the command harness, with the real units, over the small trainable data folder of the training
fixture, with string writers for the standard output and error and a temporary model folder, so that no test depends on
the real data folder, the working directory of the test process or the committed locations. Each staleness case changes
exactly one thing in a trained model folder (a training row, one recorded value, the tagger version in the record, the
model file, the record) and checks the exit code and the line that names the cause. Nothing is mocked. The check of the
real committed model and record is not a unit test: it is the subsystem test
`TrainingSubsystem_TrainCheck_CommittedModelAndRecord_AreCurrent` (see *SpeechTextEn TrainingSubsystem Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary base folder, data folder and model folder

#### Acceptance Criteria

- **Usage and data errors**: `--help` prints the usage and exits 0; an unknown option, `--seed`, `--model-dir` or
  `--row-order` without a value, a value that is not a number, a row order other than `positional` or `keyed`, an
  out-of-range value, a data folder that does not exist, an experiment option (a number or the row order) without
  `--model-dir` and one combined with `--check` exit 2 with a message on the error stream that names the
  problem and write nothing (not the model folder, not the committed locations); a label that is not a candidate exits
  2 naming the row when checking.
- **Writes model and record**: training the fixture exits 0, writes a model the runtime loads and a record that parses,
  with the same hash size, the same weight count, the image hash and the kept epoch count printed; training again keeps
  the evaluation and gate settings of the existing record; `--row-order keyed` prints `row order keyed`, records the
  keyed order and the check of that folder passes, while training without the option records the positional order.
- **Check**: a fresh model passes the check with `OK`, and so does a model file that holds the same model with an
  uncompressed payload; a changed training row exits 1 with `STALE: the training data changed`, a changed recorded
  setting with `weights differ` and another recorded tagger version with `tagger changed`; a model file with other
  weights exits 1 with `committed model file differs`, a corrupt one with `not a valid model` and a missing one with
  `is missing`; a record that is not valid exits 1 with `not valid` and a missing record exits 1 naming the record.

#### Test Scenarios

##### Usage and data errors

**Tests**: `TrainCommand_Run_Help_PrintsUsageAndExitsZero`,
`TrainCommand_Run_BadCommandLine_ExitsTwoNamingTheProblem` (a theory over an unknown option, three options without a
value, a non-numeric value and an unknown row order), `TrainCommand_Run_ExperimentOptionMisuse_ExitsTwoAndWritesNothing`
(a theory over an out-of-range value, a numeric and a row-order experiment option without a model folder and each
combined with the check),
`TrainCommand_Run_MissingDataFolder_ExitsTwo`, `TrainCommand_Run_CheckWithDataError_ExitsTwo`

Each command line is run and the exit code, the stream and the message are checked; for the misuse cases the model
folder and the folders of the committed locations are checked not to exist. A training row is relabeled with a reading
of another word after a model was trained, and the check exits 2 with the row identifier on the error stream; training
with such a row is verified by the subsystem test `TrainingSubsystem_Train_LabelNotACandidate_ExitsTwoNamingTheRow`.

##### Writing the model and the record

**Tests**: `TrainCommand_Run_Fixture_WritesLoadableModelAndRecord`,
`TrainCommand_Run_ExistingRecord_KeepsItsEvaluationSettings`,
`TrainCommand_Run_RowOrderKeyed_RecordsTheOrderAndChecksWithIt`

The written model is loaded by the runtime's loader and the record parsed; their hash sizes and counts agree and the
output reports the image hash and the kept epochs. The gate threshold of the record is edited to 500 per mille, training
runs again and the parsed new record still holds 500. A model trained with `--row-order keyed` has the keyed order in
its record and passes the check of its folder, which retrains with the recorded order; a model trained without the
option in another folder has the positional order in its record.

##### Check

**Tests**: `TrainCommand_Run_CheckRecompressedButEqualModel_ExitsZero`,
`TrainCommand_Run_CheckAfterInputOrRecordChanged_ExitsOneNamingTheCause` (a theory over a training row, a recorded
setting and the recorded tagger version), `TrainCommand_Run_CheckAfterModelFileReplaced_ExitsOneNamingTheModelFile`,
`TrainCommand_Run_CheckMissingOrInvalidRecord_ExitsOne`

A freshly trained model passes, and so does a model file replaced by the uncompressed image of the same model. Each
cause is introduced into a trained model folder and the check is run; the exit code is 1 and the output contains the
line listed in the acceptance criteria. The tagger version is changed in the record, not in the tagger, so the test
shows that the check compares the recorded version with the running one. The model file is replaced by an empty model,
then by three bytes, then deleted.
