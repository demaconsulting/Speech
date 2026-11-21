### EvaluateCommand Verification

#### Verification Approach

`EvaluateCommand` and the `Program` entry glue that dispatches to it are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/EvaluateCommandTests.cs`. The tests train a model
from the small trainable data folder of the training fixture through the real train command and then call the evaluate
command as a function through the command harness, with string writers for the output and error streams and temporary
model and record folders. The gate tests set the threshold in the record (and, for the interval condition, evaluate a
model with no weights, which always takes the first candidate exactly like the baseline), so that each condition can
fail alone. Nothing is mocked. The evaluation of the real committed model, its gate and its committed record are not
unit tests: they are the subsystem test `TrainingSubsystem_Evaluate_CommittedModelAndData_PassesGate` (see
*SpeechTextEn TrainingSubsystem Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary base folder, data folder, model folder and record folder

#### Acceptance Criteria

- **Usage errors**: `--help` prints the usage and exits 0; an unknown option, `--set` without a value, an unknown set,
  `--write` together with `--check`, a missing model file and a harvested row without a source document exit 2 with a
  message on the error stream that names the problem (the file, or the row, for the provenance) and, for the
  provenance, print no `GATE:` verdict.
- **Holdout discipline**: evaluating only the development set with both holdout files deleted exits 0, reports the
  eight development rows and never prints `HOLDOUT`; evaluating the natural holdout prints `HOLDOUT EVALUATION`, a
  model accuracy of 97.50% (39 of 40), a first-candidate baseline of 52.50%, an improvement over the
  own-part-of-speech baseline of +45.00%, a parity line with 40 compared and none mismatched or suppressed, and the
  worst reading (`lead.verb`, 20 of 21).
- **Gate exit codes**: `--gate` exits 1 with `BELOW` and `GATE: FAIL` for a threshold of 99.0%, and exits 1 with
  `NOT above zero` and `GATE: FAIL` for a model equal to its baseline even though a threshold of 0.1% is met; with
  `--write` it decides on the natural holdout (`GATE: PASS` at 96.0%) and writes the eight record files; with `--check`
  it exits 0 only when the gate passes and the record is current, and exits 1, printing both results, when the gate
  fails or the record is stale.
- **Record write and check**: `--write` writes the eight record files and `--check` then exits 0, and exits 1 with a
  `STALE:` line for each file that was appended to or deleted; `--seeds` prints a line for each evaluation seed.
- **Overlay sets**: `--set regression --set generalization` exits 0 without a holdout line and prints both overlays
  (2 and 3 rows), the cue-unsolvable check line and each slice with the figures with and without the cue priors (all
  correct, cue effect 0.00% with the interval [0.00%, 0.00%], no cue fired and none changed); a requested overlay
  whose file is missing exits 2 naming the file, an overlay with its header only is named and skipped with exit 0, and
  a cue-unsolvable row with a past-time word exits 2 naming the row and the cue; a record written after the overlays
  are added keeps the earlier record's summary, source, reading and confidence files as a byte-identical prefix
  followed by the `generalization` and `regression` lines, an identical gate file, the overlay rows file with its header
  and five rows, and checks as current; the record without overlays names the overlay files it left out.

#### Test Scenarios

##### Usage errors

**Tests**: `EvaluateCommand_Run_Help_PrintsUsageAndExitsZero`,
`EvaluateCommand_Run_BadCommandLine_ExitsTwoNamingTheProblem` (a theory over an unknown option and an option without a
value), `EvaluateCommand_Run_UnknownSetOrWriteWithCheck_ExitsTwo`, `EvaluateCommand_Run_MissingModel_ExitsTwo`,
`EvaluateCommand_Run_HarvestedRowWithoutProvenance_ExitsTwoNamingTheProblem`

Each command line is run and the exit code, the stream and the message are checked. For the provenance case one line of
the provenance table is removed after training and the evaluation of the natural holdout with `--gate` must exit 2
naming the row, with no `GATE:` line.

##### Holdout discipline

**Tests**: `EvaluateCommand_Run_DevSet_DoesNotReadHoldoutFiles`,
`EvaluateCommand_Run_HoldoutSet_AnnouncesAndPrintsTheFigures`

With the holdout files deleted the development evaluation succeeds and has no holdout line; the holdout evaluation
prints the announcement and the exact lines listed in the acceptance criteria.

##### Gate exit codes

**Tests**: `EvaluateCommand_Run_GateAccuracyBelowThreshold_ExitsOne`,
`EvaluateCommand_Run_GateLowerBoundNotPositive_ExitsOne`,
`EvaluateCommand_Run_GateWithWrite_DecidesOnTheNaturalHoldout`,
`EvaluateCommand_Run_GatePassesRecordCurrent_ExitsZero` (a theory over three cases)

The threshold in the record is set to 990 and 1 per thousand; the model of the second case is a model with no weights
given with `--model`. With `--write` the decision is taken on the natural holdout. The theory writes the record with a
threshold of 900 (the gate passes) or 990 (it fails), appends to the summary file for the stale case, runs
`--gate --check` and compares the exit code and both result lines: pass with a current record exits 0, fail with a
current record and pass with a stale record exit 1.

##### Record write, check and seeds

**Tests**: `EvaluateCommand_Run_WriteThenCheck_MatchesAndDetectsEditedRecord`,
`EvaluateCommand_Run_Seeds_ReportsEachSeed`

Eight files are written; the check passes; after the summary file is appended to and the gate file deleted the check
exits 1 naming both. `--seeds` prints `seed <n>: natural` for every evaluation seed of the record.

##### Overlay sets

**Tests**: `EvaluateCommand_Run_OverlaySets_ReportedWithAndWithoutCues`,
`EvaluateCommand_Run_OverlayMissingViolatedOrEmpty_ExitsTwoOrSkips`,
`EvaluateCommand_Run_WriteWithOverlays_AppendsAfterTheDataSets`

The fixture gets a regression overlay of two rows and a generalization set of three rows in two families, all of the
trained patterns, and the output lines listed in the acceptance criteria are checked. The second test runs the
command without a regression file, with a header-only generalization set and with a generalization set whose only row
is marked `cue_unsolvable=yes` and holds *yesterday*. The third test writes a record before and after the overlays are
written and compares the files.
