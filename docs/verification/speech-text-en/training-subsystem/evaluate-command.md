### EvaluateCommand Verification

#### Verification Approach

`EvaluateCommand` and the `Program` entry glue that dispatches to it are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/EvaluateCommandTests.cs`. The tests train a model
from the small trainable data folder of the training fixture through the real train command and then call the evaluate
command as a function through the command harness, with string writers for the output and error streams and temporary
model and record folders. The gate tests set the threshold in the record (and, for the interval condition, evaluate a
model with no weights, which always takes the first candidate exactly like the baseline), so that each condition can
fail alone. The holdout tests delete the holdout files and check that a development evaluation still succeeds. Nothing
is mocked. The evaluation of the real committed model, its gate and its committed record are not unit tests: they are
the subsystem tests `TrainingSubsystem_Evaluate_CommittedModelAndData_PassesGate` and
`TrainingSubsystem_Evaluate_CommittedRecord_IsCurrent` (see *SpeechTextEn TrainingSubsystem Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary base folder, data folder, model folder and record folder

#### Acceptance Criteria

`--help` prints the usage and exits 0; an unknown option, `--set` without a value, an unknown set, `--write` together
with `--check` and a missing model file exit 2 with a message on the error stream that names the problem (the model file
for a missing model); evaluating only the development set with both holdout files deleted exits 0, reports the eight
development rows and never prints `HOLDOUT`; evaluating the natural holdout prints `HOLDOUT EVALUATION`, a model
accuracy of 97.50% (39 of 40), a first-candidate baseline of 52.50% (21 of 40), an improvement over the
own-part-of-speech baseline of +45.00%, a parity line with 40 compared and none mismatched or suppressed, and the worst
reading (`lead.verb`, 20 of 21); `--gate` exits 0 with `GATE: PASS` for a threshold of 90.0% and says that the gate ran
on the natural holdout, exits 1 with `BELOW` and `GATE: FAIL` for a threshold of 99.0%, and exits 1 with
`NOT above zero` and `GATE: FAIL` for a model equal to its baseline even though a threshold of 0.1% is met; `--seeds`
prints a line for each evaluation seed; `--write` writes the six record files and `--check` then exits 0 with `OK`, and
exits 1 with a `STALE:` line for each file that was appended to or deleted; two written records are equal file for file;
`--gate` together with `--check` exits 0 only when the gate passes and the record is current, and exits 1, printing both
results, when the gate fails with a current record, when the gate passes with a stale record and when both fail;
`--gate` with `--write` exits 1 for a failing gate and still writes the six files; a harvested row without a source
document (the provenance file deleted, or the line of one row removed) exits 2 with a message on the error stream that
names `harvest-provenance.tsv` or the row (the development set included) and prints no `GATE:` verdict.

#### Test Scenarios

##### Usage errors

**Tests**: `EvaluateCommand_Run_Help_PrintsUsageAndExitsZero`,
`EvaluateCommand_Run_BadCommandLine_ExitsTwoNamingTheProblem` (a theory over an unknown option and an option without a
value), `EvaluateCommand_Run_UnknownSetOrWriteWithCheck_ExitsTwo`, `EvaluateCommand_Run_MissingModel_ExitsTwo`

Each command line is run and the exit code, the stream and the message are checked.

##### Holdout discipline

**Tests**: `EvaluateCommand_Run_DevSet_DoesNotReadHoldoutFiles`,
`EvaluateCommand_Run_HoldoutSet_AnnouncesAndPrintsTheFigures`

With the holdout files deleted the development evaluation succeeds and has no holdout line; the holdout evaluation
prints the announcement.

##### Report

**Test**: `EvaluateCommand_Run_HoldoutSet_AnnouncesAndPrintsTheFigures`

The printed figures are compared with the exact lines listed in the acceptance criteria.

##### Gate exit codes

**Tests**: `EvaluateCommand_Run_GateConditionsMet_ExitsZero`, `EvaluateCommand_Run_GateAccuracyBelowThreshold_ExitsOne`,
`EvaluateCommand_Run_GateLowerBoundNotPositive_ExitsOne`, `EvaluateCommand_Run_GateWithWrite_DecidesOnTheNaturalHoldout`

The threshold in the record is set to 900, 990 and 1 per thousand; the model of the last case is a model with no weights
given with `--model`. The gate decision is taken on the natural holdout also when the whole record is written in the
same run.

##### Seed study

**Test**: `EvaluateCommand_Run_Seeds_ReportsEachSeed`

`--seeds` prints `seed <n>: natural` for every evaluation seed of the record.

##### Record writing and checking

**Tests**: `EvaluateCommand_Run_WriteThenCheck_MatchesAndDetectsEditedRecord`,
`EvaluateCommand_Run_WriteTwice_ProducesIdenticalRecords`

Six files are written; the check passes; after the summary file is appended to and the gate file deleted the check
exits 1 naming both. Two records written separately are equal file for file.

##### Gate with record check

**Tests**: `EvaluateCommand_Run_GateFailsRecordCurrent_ExitsOne`, `EvaluateCommand_Run_GatePassesRecordStale_ExitsOne`,
`EvaluateCommand_Run_GatePassesRecordCurrent_ExitsZero`, `EvaluateCommand_Run_GateFailsRecordStale_ExitsOne`,
`EvaluateCommand_Run_GateFailsWithWrite_ExitsOneAndWritesRecord`

The record is written with a threshold of 990 (the gate fails) or 900 (it passes); the summary file is appended to for
the stale cases. `--gate --check` is run and the exit code and both result lines are compared; `--gate --write` with a
failing gate is run and the exit code and the six files are compared.

##### Missing provenance

**Tests**: `EvaluateCommand_Run_HarvestedRowWithoutProvenance_ExitsTwoNamingTheProblem` (a theory over the deleted
provenance file and a removed row line, with `--gate`),
`EvaluateCommand_Run_DevRowWithoutProvenance_ExitsTwoNamingTheRow`

After training, the provenance table is deleted or one line is removed. The exit code, the error text and the absence of
a `GATE:` line are compared. The committed data, where every harvested row has a document, is covered by the subsystem
tests `TrainingSubsystem_Evaluate_CommittedRecord_IsCurrent` (the cluster counts are part of the record) and
`TrainingSubsystem_Evaluate_CommittedModelAndData_PassesGate`.
