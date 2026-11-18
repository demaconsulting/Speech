### HeteronymEvaluator Verification

#### Verification Approach

`HeteronymEvaluator` (with its result records) is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/HeteronymEvaluatorTests.cs`. The interval and the
gate are verified on hand-built cluster statistics whose correct result is known without running the unit (identical
clusters give a degenerate interval at the exact difference, a model equal to its baseline gives a difference of zero),
with 2,000 resamples to keep the tests fast. The scoring is verified on the small trainable data folder of the training
fixture with a model trained by the real units: the fixture is built so that the model's decisions, the baselines and
the strata can be counted by hand (twenty verb and nineteen noun patterns decided and one noun pattern labeled as the
verb in the natural holdout, so 39 of 40 are right). The hints of the readings are chosen per test so that the
own-part-of-speech baseline is either never matching, which makes it the first candidate, or informative. The real
analyzer scores every row, so the parity tests compare two real code paths. Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test that needs data builds and deletes its own temporary data folder; the interval tests are
  pure functions

#### Acceptance Criteria

- **Metrics**: ratios convert to basis points rounded half away from zero (1/3 gives 3,333, -2/3 gives -6,667, 1/32
  gives 313, -1/32 gives -313) and a zero denominator gives 0; the natural holdout of the fixture has 40 rows, 40
  aligned, 40 covered, 39 correct and 21 correct for each of the three baselines, one source tally and 40 clusters; the
  readings are listed worst first (`lead.verb` 20 of 21, `lead.noun` 19 of 19); in the independent holdout the
  unaligned row is wrong, uncovered, not aligned and in the `none` confidence bucket.
- **Baselines**: an informative hint makes the own-part-of-speech baseline better than the first candidate; the majority
  baseline follows the training labels (11 correct when the noun is the training majority, against 10 for the first
  candidate).
- **Improvement interval**: twenty-five identical clusters give the interval 5,000 to 5,000 around 5,000 with a positive
  lower bound; a model equal to its baseline gives a difference and both bounds of 0 and a lower bound that is not
  above zero; a clearly better model gives the exact estimate (3,975 basis points) inside an interval whose lower bound
  is positive; one cluster gives an interval equal to its own difference; the same improvement as ten documents gives a
  wider interval than as two hundred independent rows with the same estimate; two natural rows moved to the same source
  document give 39 clusters instead of 40; the same seed gives the same interval and no clusters give the empty one.
- **Gate decision**: the gate passes at 930 of 1,000 and fails at 929 of 1,000, fails when the lower bound is not above
  zero however accurate the model is, and fails for a set without rows; for every set, the training rows included, the
  number of rows compared equals the number aligned with none mismatched and none suppressed, and the direct evaluation
  equals the end-to-end one.
- **Input validation**: a natural holdout evaluated with the provenance file deleted, or with the line of one row
  removed, throws `InvalidDataException` whose message names `harvest-provenance.tsv` or the set file and the row; the
  independent holdout (21 clusters) and the training rows (one cluster per row) evaluate without any provenance file;
  an unknown set name throws `InvalidDataException`; a clustering supplied by the caller decides how many clusters
  there are (one cluster, one per reading and one per row); the accuracy interval of rows supplied by the caller
  encloses the point estimate on the end-to-end path and is absent on the direct path and on a file set.

#### Test Scenarios

##### Metrics

**Tests**: `HeteronymEvaluator_Evaluate_NaturalHoldout_ComputesExactMetrics`,
`HeteronymEvaluator_Evaluate_NaturalHoldout_ListsReadingsWorstFirst`,
`HeteronymEvaluator_Evaluate_UnalignedRow_CountsWrongAndUncovered`,
`HeteronymEvaluator_RoundBasisPoints_RoundsHalfAwayFromZero` (a theory over four cases)

The fixture model is evaluated on the natural and the independent holdout and every count of the result record is
compared with the hand count.

##### Baselines

**Tests**: `HeteronymEvaluator_Evaluate_UposBaseline_UsesTheTagWhenTheHintIsUnique`,
`HeteronymEvaluator_Evaluate_MajorityBaseline_FollowsTheTrainingLabels`

The fixture is built with informative hints, and with six noun rows added to the training file.

##### Improvement interval

**Tests**: `HeteronymEvaluator_Bootstrap_IdenticalClusters_GiveExactDegenerateInterval`,
`HeteronymEvaluator_Bootstrap_ModelEqualsBaseline_LowerBoundIsNotPositive`,
`HeteronymEvaluator_Bootstrap_ModelClearlyBetter_HasPositiveLowerBoundAroundTheEstimate`,
`HeteronymEvaluator_Bootstrap_SingleCluster_IntervalEqualsTheEstimate`,
`HeteronymEvaluator_Bootstrap_RowsOfOneDocumentResampleTogether_SoTheIntervalIsWider`,
`HeteronymEvaluator_Evaluate_RowsOfOneDocument_FormOneCluster`,
`HeteronymEvaluator_Bootstrap_FixedSeed_IsRepeatableAndEmptyInputIsEmpty`

The cases are built from cluster statistics whose true difference is known. The interval is not compared with a bound
computed by another implementation: its width is asserted only relatively and by the degenerate cases, which have one
possible answer. One line of the provenance table of the fixture is edited to move a row to the document of another.

##### Gate decision and parity

**Tests**: `HeteronymEvaluator_DecideGate_AccuracyThresholdIsInclusive` (a theory over two cases),
`HeteronymEvaluator_DecideGate_LowerBoundNotPositive_Fails`, `HeteronymEvaluator_DecideGate_NoRows_Fails`,
`HeteronymEvaluator_Evaluate_AllSets_TrainerAndAnalyzerAgreeOnEveryRow`,
`HeteronymEvaluator_EvaluateDirect_AgreesWithEndToEndOnTheFixture`

Evaluations with a given number correct and an interval are decided with the default thresholds. The training rows (the
set named `train`, used only for the parity check, with one unaligned row appended), the development set and both
holdout sets are evaluated and the parity counts are compared; the direct evaluation is compared with the end-to-end
one.

##### Input validation

**Tests**: `HeteronymEvaluator_Evaluate_MissingProvenanceFile_ThrowsInvalidDataException`,
`HeteronymEvaluator_Evaluate_IndependentAndSyntheticRows_NeedNoProvenance`,
`HeteronymEvaluator_Evaluate_UnknownSet_ThrowsInvalidDataException`,
`HeteronymEvaluator_EvaluateRows_CustomClustering_GroupsRowsOfOneCluster`,
`HeteronymEvaluator_EvaluateRows_AccuracyInterval_EnclosesPointEstimate`

The provenance table of the fixture is deleted, and then restored without the line of one row, and the natural holdout
is evaluated each time; the sets of synthetic and independent rows are evaluated with the file deleted; a set named
`holdout-secret` is rejected. The rows of a file set are supplied to `EvaluateRows` grouped as one cluster, by reading
and one per row. The committed data is covered by the subsystem test
`TrainingSubsystem_Evaluate_CommittedModelAndData_PassesGate`.
