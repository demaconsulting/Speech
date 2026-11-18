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

The summary file has its header line and one row with the exact integer counts and basis-point ratios of the sample (for
example 1,100 of 1,167 as 9,426); the gate file holds the image hash, the thresholds, `accuracy_ok` and `interval_ok`
and ends with the verdict `pass`, `fail` when either condition does not hold and `not evaluated` when there are no sets;
the source, reading and confidence files list every group with its counts, its basis-point accuracy and the baseline
count; the seeds file lists each seed and then the mean, the minimum and the maximum; the files of the same evaluation
are equal on every rendering, in ordinal file-name order, with line feeds only and a final line feed; a written folder
compares as current, and an appended, a deleted and a changed file are named; basis points format as `93.12%`, `0.00%`,
`+2.46%`, `2.46%`, `-0.05%` and `100.00%` for the given sign option.

#### Test Scenarios

##### Summary file

**Test**: `EvaluationRecord_Render_Summary_HasExactFigures`

The header and the one row are compared with the expected text.

##### Gate file

**Tests**: `EvaluationRecord_Render_Gate_HasThresholdsAndVerdict`, `EvaluationRecord_Render_Gate_FailAndNotEvaluated`

The sample gives `pass`; the sample with a lower bound that is not above zero and too few correct gives `fail` with
both conditions `no`; an evaluation without sets gives `not evaluated`.

##### Group files

**Test**: `EvaluationRecord_Render_Groups_ListEveryGroup`

The source and reading files are compared with the expected text and a confidence line with its expected text.

##### Seeds file

**Test**: `EvaluationRecord_Render_Seeds_HasRowsAndSpread`

The seeds file is compared with the expected text, which holds the two seeds and the mean, minimum and maximum.

##### Deterministic files

**Test**: `EvaluationRecord_Render_IsDeterministicWithLineFeedsOnly`

Two renderings have the same names in ordinal order and the same content, with no carriage return and a final line feed.

##### Staleness detection

**Tests**: `EvaluationRecord_WriteThenCompare_DetectsEditedAndMissingFiles`,
`EvaluationRecord_Compare_DifferentEvaluation_NamesTheChangedFiles`

A written folder compares as current. After a file is appended to and another deleted, the two are named. An evaluation
with another image hash names the gate file only.

##### Percent format

**Test**: `EvaluationRecord_FormatPercent_FormatsBasisPoints` (a theory over six cases)

The six cases are listed in the acceptance criteria.
