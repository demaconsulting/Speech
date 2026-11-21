### EvaluationOverlay Verification

#### Verification Approach

`EvaluationOverlay` (with `OverlayRow`) is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/EvaluationOverlayTests.cs`. The tests write a
regression overlay and a generalization set into the data folder of the training fixture with the fixture's own
helpers, which compute the offsets from the target word, and load them with the unit; the expected sources, clusters
and slices are written into the tests as exact text. The refusal tests edit the written file. Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder

#### Acceptance Criteria

- **Rows**: a regression row loads with its id, reading and offsets (10 to 14 for *They will lead the team today.*),
  the source `regression/listening/unseen` (or `regression/data-row/seen`), the cluster `row:<id>`, its origin as the
  group, `-` as the kind, its seen mark, no cue-unsolvable mark and the slices `all`, `unseen` or `seen` and
  `origin:<origin>`; a generalization row loads with the source `generalization/<kind>/cue-unsolvable` or
  `cue-solvable`, the cluster `family:<family>`, its family as the group, its kind, `-` as the seen mark, its
  cue-unsolvable mark and the slices `all`, `kind:<kind>`, `cue_unsolvable:yes` or `no` and `family:<family>`; a file
  with its header only has no rows.
- **Refusal**: a missing regression file and an offset that is not an integer throw `InvalidDataException` naming
  `regression.tsv`, and a span beyond the sentence one naming the row; the names are `generalization` and `regression`
  in that order, with the files `generalization.tsv` and `regression.tsv`, `dev` is not an overlay, and its file name
  is refused with `ArgumentException`.

#### Test Scenarios

##### Rows

**Tests**: `EvaluationOverlay_Load_Regression_RowsCarryStrataSlicesAndRowClusters`,
`EvaluationOverlay_Load_Generalization_RowsCarryStrataSlicesAndFamilyClusters`,
`EvaluationOverlay_Load_HeaderOnly_HasNoRows`

Two rows of each overlay are written and every field of the loaded rows is compared; a generalization set without rows
loads as an empty list.

##### Refusal

**Tests**: `EvaluationOverlay_Load_MissingOrMalformed_ThrowsInvalidDataException`,
`EvaluationOverlay_Names_MapToFilesInOrdinalOrder`

The regression overlay is loaded before it exists, then with the start offset replaced by `ten`, then with the end
offset replaced by 99; the names, files and the refused name are checked.
