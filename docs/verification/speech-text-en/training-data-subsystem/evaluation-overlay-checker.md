### EvaluationOverlayChecker Verification

#### Verification Approach

`EvaluationOverlayChecker` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/EvaluationOverlayCheckerTests.cs`. Each test
builds the small valid data folder of the dataset fixture (which has two regression rows, one referring to a training
row and one to a golden file, and a generalization set with its header only), changes the overlay rows or writes a
generalization set, loads the folder with the real `TextEnDataSet` and runs the checker with a real `CheckReport`.
Nothing is mocked: the checker's only inputs are the loaded tables. The quotas of a populated set are passed in as a
small `GeneralizationShape`, except in the test of the default quotas. The resolution of the golden and test references
of the committed overlay, which the checker does not perform, is verified by a subsystem test (see
*TrainingDataSubsystem Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder (and, for the staged set, a folder beside
  it)

#### Acceptance Criteria

**Regression overlay** (`Regression`). The valid fixture gives no finding and reports two rows, one seen and one unseen.
A data-row reference that does not resolve, or whose sentence or license differs from the referenced row, a reference
into a holdout file, a row that repeats a holdout sentence, a seen mark that differs from the recomputed one in either
direction or is not yes or no, a golden row that repeats a training sentence but is marked unseen, an unknown origin, a
reference that does not fit its origin, a malformed identifier, a disallowed license, an invalid span and an unknown
reading each give an `overlay` finding.

**Generalization rows** (`GeneralizationRows`). An invalid kind, a family of another lemma group, a novel-vocabulary row
without cue forms, a cue form that is not in the sentence, is not lower case or is a form of the target, an invalid
cue-unsolvable mark, source or license, a sentence of fewer than 6 words and a sentence with a second form of the target
each give a finding; lemmas whose readings share a form are one group named by the smallest lemma.

**Novelty** (`Novelty`). A row that is new in its cue words and one that is new in its construction pass; a row that
repeats a training sentence or a regression sentence gives an N1 finding naming the match; a cue form that occurs with
the lemma in training gives an N2 finding naming the row, and a new cue form in the same row gives none; a construction
trigram that a training row has gives an N3 finding naming the trigram and the row.

**Staged reasons** (`StagedReasons`). For a staged file, each of the three novelty violations gives its fixed generic
reason, every finding is located in the staged file, and no finding names a data file or a training row.

**Shape** (`Shape`). An empty set gives no shape finding and an information line that the quotas apply later; a
populated set of two rows against the default quotas gives one finding for each quota it misses (families, family size,
both kinds, cue-unsolvable rows and their lemmas); a lemma group with rows for only one reading gives a finding.

#### Test Scenarios

##### Regression overlay

**Tests**: `EvaluationOverlayChecker_Run_ValidFixture_NoFindingsAndShapeSkipped`,
`EvaluationOverlayChecker_CheckRegression_DataRowMismatch_IsFinding`,
`EvaluationOverlayChecker_CheckRegression_HoldoutReferenceOrRepeat_IsFinding`,
`EvaluationOverlayChecker_CheckRegression_WrongSeenMark_IsFinding`,
`EvaluationOverlayChecker_CheckRegression_GoldenSentenceInTraining_MustBeSeen`,
`EvaluationOverlayChecker_CheckRegression_MalformedRow_IsFinding`

The data-row theory changes the reference, the sentence and the license of the row that refers to a training row; the
malformed-row theory changes one column of the golden row at a time.

##### Generalization rows

**Tests**: `EvaluationOverlayChecker_CheckGeneralization_MalformedColumn_IsFinding`,
`EvaluationOverlayChecker_CheckGeneralization_LengthOrSecondTarget_IsFinding`,
`EvaluationOverlayChecker_GroupOf_SharedForm_JoinsLemmas`

The group test adds a reading whose form is also a form of another lemma's reading and checks the group names, including
a lemma the inventory does not know.

##### Novelty

**Tests**: `EvaluationOverlayChecker_CheckGeneralization_NovelRows_PassWithSmallQuotas`,
`EvaluationOverlayChecker_CheckGeneralization_N1RepeatedSentence_IsFinding`,
`EvaluationOverlayChecker_CheckGeneralization_N2KnownCueForm_IsFinding`,
`EvaluationOverlayChecker_CheckGeneralization_N3KnownConstruction_IsFinding`

##### Staged reasons

**Test**: `EvaluationOverlayChecker_CheckGeneralization_StagedRows_GiveGenericReasonsOnly`

A staged file outside the data folder holds one row of each novelty violation and is checked with matches hidden.

##### Shape

**Tests**: `EvaluationOverlayChecker_Run_ValidFixture_NoFindingsAndShapeSkipped`,
`EvaluationOverlayChecker_CheckGeneralization_PopulatedSetBelowDefaultQuotas_ReportsEveryQuota`,
`EvaluationOverlayChecker_CheckGeneralization_OneReadingOfLemma_IsShapeFinding`
