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
  the sample (for example 1,100 of 1,167 as 9,426); the gate file holds the image and previous image hashes, the
  bootstrap settings and, for the gate set, its rows, clusters, correct rows, accuracy, interval bounds, threshold,
  flips and `pass`, and ends with the verdict `pass`, `fail` when the set is below its threshold and `not evaluated`
  when there are no sets; the source, reading and confidence files list every group with its counts, its basis-point
  accuracy and the baseline count; the seeds file lists each seed with every gate set's rows, correct rows and accuracy
  and then the mean, the minimum, the maximum and the threshold by the gate's rule; basis
  points format as `93.12%`, `+2.46%` and `-0.05%` for the given sign option.
- **Overlay files**: a report with a generalization overlay keeps the summary, source, reading and confidence files of
  the report without it as a byte-identical prefix, followed by the overlay's lines, and leaves the gate file of a gate
  on the natural holdout and the seeds file unchanged; the overlay rows file has its header and one exact line per row
  (with `-` where a mark does not apply) and the curated ablation file its header and one exact line per slice; without
  overlays both files have their header only.
- **Gate rows and flips**: a first write lists the three gate rows ordered by id with the previous reading equal to the
  current one and an empty flips file, and compares as current; after a record of an older model, a record of the
  current model and then one with a fourth row, the previous column and previous image stay those of the older model,
  the new row has `-` and the flips file lists the one row that became wrong as `broken`; a write of a new model image
  takes the committed current column as the previous one and the committed image as the previous image, lists one
  `fixed` and one `broken` row, counts them in the gate file and compares as current.
- **Deterministic**: the files of the same evaluation are equal on every rendering, in ordinal file-name order, with
  line feeds only and a final line feed; a written folder compares as current, and an appended and a deleted file are
  named.

#### Test Scenarios

##### Files

**Tests**: `EvaluationRecord_Render_Summary_HasExactFigures`, `EvaluationRecord_Render_Gate_HasPerSetFiguresAndVerdict`,
`EvaluationRecord_Render_Gate_FailAndNotEvaluated`, `EvaluationRecord_Render_Groups_ListEveryGroup`,
`EvaluationRecord_Render_Seeds_HasRowsAndSpread`, `EvaluationRecord_FormatPercent_FormatsBasisPoints` (a theory over
three cases)

The summary, the source and reading files and the seeds file are compared with the expected text, and a confidence line
with its expected text. The sample, gated on its natural holdout at 9,400 basis points, gives the verdict `pass`; the
sample with 1,000 correct gives `fail`; an evaluation without sets gives `not evaluated`.

##### Gate rows and flips

**Tests**: `EvaluationRecord_Write_FirstWrite_PreviousEqualsCurrentAndNoFlips`,
`EvaluationRecord_Write_SameModel_KeepsThePreviousColumn`, `EvaluationRecord_Write_NewModel_RotatesAndReportsFlips`

Each test writes records into a temporary folder in sequence and compares the gate rows, flips and gate files with the
expected text, so the rotation is checked against the folder's committed state as the command uses it.

##### Overlay files

**Test**: `EvaluationRecord_Render_Overlays_AppendedWithRowsAndCuratedAblation`

A hand-built generalization overlay of two rows in one family (the cue priors change one row) is added to the sample
report, and the files are compared with the sample's files and with the expected text.

##### Deterministic files and staleness

**Tests**: `EvaluationRecord_Render_IsDeterministicWithLineFeedsOnly`,
`EvaluationRecord_WriteThenCompare_DetectsEditedAndMissingFiles`

Two renderings have the same names in ordinal order and the same content, with no carriage return and a final line feed.
A written folder compares as current; after a file is appended to and another deleted, the two are named.
