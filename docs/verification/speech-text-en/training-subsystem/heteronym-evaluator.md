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

Ratios convert to basis points rounded half away from zero (1/3 gives 3,333, 2/3 gives 6,667, -1/3 gives -3,333, 1/32
gives 313, -1/32 gives -313, 1/16 gives 625, 1,100/1,167 gives 9,426) and a zero denominator gives 0; twenty-five
identical clusters give the interval 5,000 to 5,000 around 5,000 with a positive lower bound; a model equal to its
baseline gives a difference, a lower bound and an upper bound of 0 and a lower bound that is not above zero; a clearly
better model gives the exact estimate (3,975 basis points) inside an interval whose lower bound is positive; one
cluster gives an interval equal to its own difference; the same improvement as ten documents gives a wider interval than
as two hundred independent rows with the same estimate; the same seed gives the same interval and no clusters give the
empty one; the gate passes at 930 of 1,000 and at 93 of 100 and fails at 929 of 1,000 and 92 of 100, fails when the
lower bound is not above zero however accurate the model is, and fails for a set without rows; the natural holdout of
the fixture has 40 rows, 40 aligned, 40 covered, 39 correct and 21 correct for each of the three baselines, one
source tally, 40 clusters, a positive lower bound and a difference of 4,500 basis points; the readings are listed worst
first (`lead.verb` 20 of 21, `lead.noun` 19 of 19); in the independent holdout the unaligned row is wrong, uncovered,
not aligned and in the `none` confidence bucket; an informative hint makes the own-part-of-speech baseline better than
the first candidate; the majority baseline follows the training labels (11 correct when the noun is the training
majority, against 10 for the first candidate); for every set the number of rows compared equals the number aligned
with none mismatched and none suppressed; the direct evaluation equals the end-to-end one; an unknown set name throws
`InvalidDataException`; two natural rows moved to the same source document give 39 clusters instead of 40; a natural
holdout evaluated with the provenance file deleted, or with the line of one row removed, throws `InvalidDataException`
whose message names `harvest-provenance.tsv` or the set file and the row; the independent holdout (21 clusters) and the
training rows (one cluster per row) evaluate without any provenance file.

#### Test Scenarios

##### Scoring metrics

**Tests**: `HeteronymEvaluator_Evaluate_NaturalHoldout_ComputesExactMetrics`,
`HeteronymEvaluator_Evaluate_NaturalHoldout_ListsReadingsWorstFirst`,
`HeteronymEvaluator_Evaluate_UnalignedRow_CountsWrongAndUncovered`

The fixture model is evaluated on the natural and the independent holdout and every count of the result record is
compared with the hand count.

##### Baselines

**Tests**: `HeteronymEvaluator_Evaluate_UposBaseline_UsesTheTagWhenTheHintIsUnique`,
`HeteronymEvaluator_Evaluate_MajorityBaseline_FollowsTheTrainingLabels`

The fixture is built with informative hints, and with six noun rows added to the training file; the baselines are
compared with the counts that follow.

##### Rounding

**Test**: `HeteronymEvaluator_RoundBasisPoints_RoundsHalfAwayFromZero` (a theory over eleven cases)

The cases include exact halves in both signs (1/32 and 1/16) and a zero denominator.

##### Interval

**Tests**: `HeteronymEvaluator_Bootstrap_IdenticalClusters_GiveExactDegenerateInterval`,
`HeteronymEvaluator_Bootstrap_ModelEqualsBaseline_LowerBoundIsNotPositive`,
`HeteronymEvaluator_Bootstrap_ModelClearlyBetter_HasPositiveLowerBoundAroundTheEstimate`,
`HeteronymEvaluator_Bootstrap_SingleCluster_IntervalEqualsTheEstimate`,
`HeteronymEvaluator_Bootstrap_RowsOfOneDocumentResampleTogether_SoTheIntervalIsWider`,
`HeteronymEvaluator_Bootstrap_FixedSeed_IsRepeatableAndEmptyInputIsEmpty`

The cases are built from cluster statistics whose true difference is known; the exact values are in the acceptance
criteria. The interval is not compared with a bound computed by another implementation: its width is asserted only
relatively (wider with clustering) and by the degenerate cases, which have one possible answer.

##### Provenance and clusters

**Tests**: `HeteronymEvaluator_Evaluate_RowsOfOneDocument_FormOneCluster`,
`HeteronymEvaluator_Evaluate_MissingProvenanceFile_ThrowsInvalidDataException`,
`HeteronymEvaluator_Evaluate_HarvestedRowWithoutProvenanceEntry_ThrowsInvalidDataException`,
`HeteronymEvaluator_Evaluate_IndependentAndSyntheticRows_NeedNoProvenance`

The provenance table of the fixture is edited (one row moved to the document of another, one line removed, the whole
file
deleted) and the natural holdout is evaluated; the cluster count and the exception message are compared. The sets of
synthetic and independent rows are evaluated with the file deleted. The committed data is covered by the subsystem tests
`TrainingSubsystem_Evaluate_CommittedRecord_IsCurrent` (the 266 natural clusters are part of the record) and
`TrainingSubsystem_Evaluate_CommittedModelAndData_PassesGate`.

##### Gate decision

**Tests**: `HeteronymEvaluator_DecideGate_AccuracyThresholdIsInclusive` (a theory over five cases),
`HeteronymEvaluator_DecideGate_LowerBoundNotPositive_Fails`, `HeteronymEvaluator_DecideGate_NoRows_Fails`

Evaluations with a given number correct and an interval are decided with the default thresholds; the accuracy
condition, the interval condition and the verdict are compared.

##### Parity

**Tests**: `HeteronymEvaluator_Evaluate_AllSets_TrainerAndAnalyzerAgreeOnEveryRow`,
`HeteronymEvaluator_Evaluate_TrainingRows_ParityHoldsAndUnalignedRowsAreReported`,
`HeteronymEvaluator_EvaluateDirect_AgreesWithEndToEndOnTheFixture`

The development set and both holdout sets are evaluated; the parity counts are compared, and the direct evaluation is
compared with the end-to-end one. The training rows can be evaluated too (the set named `train`, used only for the
parity check): the aligned rows are all compared and a row whose target is not a runtime token is counted as not
aligned.

##### Set validation

**Test**: `HeteronymEvaluator_Evaluate_UnknownSet_ThrowsInvalidDataException`

A set named `holdout-secret` is rejected.
