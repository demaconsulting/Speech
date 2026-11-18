### EvaluationRecord Verification

#### Verification Approach

`EvaluationRecord` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/EvaluationRecordTests.cs`. The tests render a
hand-built evaluation (one natural holdout set with its strata and two seeds) whose figures are written into the test as
exact text, so the expected file content is stated independently of the unit and any change of a column, a figure or a
format fails a test. The comparison tests write the files into a temporary folder and then edit, delete or leave them.
Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test has its own temporary folder, deleted on disposal

#### Acceptance Criteria

- **Files**: the summary file has its header line and one row with the exact integer counts and basis-point ratios of
  the sample (for example 1,100 of 1,167 as 9,426); the gate file holds the image hash, the thresholds, `accuracy_ok`
  and `interval_ok` and ends with the verdict `pass`, `fail` when either condition does not hold and `not evaluated`
  when there are no sets; the source, reading and confidence files list every group with its counts, its basis-point
  accuracy and the baseline count; the seeds file lists each seed and then the mean, the minimum and the maximum; basis
  points format as `93.12%`, `+2.46%` and `-0.05%` for the given sign option.
- **Deterministic**: the files of the same evaluation are equal on every rendering, in ordinal file-name order, with
  line feeds only and a final line feed; a written folder compares as current, and an appended and a deleted file are
  named.

#### Test Scenarios

##### Files

**Tests**: `EvaluationRecord_Render_Summary_HasExactFigures`, `EvaluationRecord_Render_Gate_HasThresholdsAndVerdict`,
`EvaluationRecord_Render_Gate_FailAndNotEvaluated`, `EvaluationRecord_Render_Groups_ListEveryGroup`,
`EvaluationRecord_Render_Seeds_HasRowsAndSpread`, `EvaluationRecord_FormatPercent_FormatsBasisPoints` (a theory over
three cases)

The summary, the source and reading files and the seeds file are compared with the expected text, and a confidence line
with its expected text. The sample gives the verdict `pass`; the sample with a lower bound that is not above zero and
too few correct gives `fail` with both conditions `no`; an evaluation without sets gives `not evaluated`.

##### Deterministic files and staleness

**Tests**: `EvaluationRecord_Render_IsDeterministicWithLineFeedsOnly`,
`EvaluationRecord_WriteThenCompare_DetectsEditedAndMissingFiles`

Two renderings have the same names in ordinal order and the same content, with no carriage return and a final line feed.
A written folder compares as current; after a file is appended to and another deleted, the two are named.
