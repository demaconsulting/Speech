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
- **Gate decision**: with thresholds of 6,902 basis points (generalization) and 9,520 (natural holdout), 168 of 242 and
  1,111 of 1,167 pass (the threshold itself being enough), 1,110 of 1,167 fails and the verdict names
  `holdout-natural` as the failing set, a set without rows fails, and a gate set that was not evaluated throws
  `InvalidDataException` naming it; for every set, the training rows included, the
  number of rows compared equals the number aligned with none mismatched and none suppressed, and the direct evaluation
  equals the end-to-end one.
- **Input validation**: a natural holdout evaluated with the provenance file deleted, or with the line of one row
  removed, throws `InvalidDataException` whose message names `harvest-provenance.tsv` or the set file and the row; the
  independent holdout (21 clusters) and the training rows (one cluster per row) evaluate without any provenance file;
  an unknown set name throws `InvalidDataException`; a clustering supplied by the caller decides how many clusters
  there are (one cluster, one per reading and one per row); the accuracy interval of rows supplied by the caller
  encloses the point estimate on the end-to-end path and is absent on the direct path and on a file set.
- **Overlay with and without the cue priors**: a generalization overlay of four rows in two families gives 4 rows, 3
  correct, 2 clusters and one cue-unsolvable row; the rows keep their order, no cue fires on the fixture (it has no
  adjective, past-tense or context-cue reading) and every model-only reading equals the shipped one; the slices are
  `all`, then `cue_unsolvable:no`, `cue_unsolvable:yes`, the two families and the two kinds in ordinal order; the slice
  `all` has 3 correct both ways, the accuracy 7,500 over 2 clusters, equal intervals and the cue effect interval
  (2, 0, 0, 0, not positive); a family slice has its own single cluster; an overlay whose row id repeats throws
  `InvalidDataException` naming the id.
- **Rows a cue prior acts on**: with a fixture whose `lead.noun` is the adjective reading, a regression overlay of four
  rows, two with a coordinated adjective or a degree adverb beside the target, marks exactly those two as cue-fired;
  their readings are the same with and without the priors, so the slice `all` counts 4 rows, 4 correct, 2 cue-fired
  and 0 changed, and each origin slice counts its own cue-fired row.
- **Cue-unsolvable check**: a generalization overlay whose marked rows include one with *yesterday* in the target's
  clause and one whose target lies inside a longer word throws `InvalidDataException` naming exactly those two rows,
  with `past-time cue` and `not one runtime token`, and not a marked row without a cue or an unmarked row with the same
  cue; with a third reading of `lead` that is a noun of another lemma only, a marked row whose target is tagged as a
  noun gets the noun-form prior without a change of reading (shipped and model-only both `lead.noun`, cue fired) and
  throws `InvalidDataException` naming it with `noun-form cue`, while the same row unmarked is evaluated.

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

**Tests**: `HeteronymEvaluator_DecideGate_EverySetAtThreshold_Passes`,
`HeteronymEvaluator_DecideGate_OneSetBelow_FailsNamingTheSet`,
`HeteronymEvaluator_DecideGate_NoRowsFailsAndMissingSetThrows`,
`HeteronymEvaluator_Evaluate_AllSets_TrainerAndAnalyzerAgreeOnEveryRow`,
`HeteronymEvaluator_EvaluateDirect_AgreesWithEndToEndOnTheFixture`

Hand-built evaluations of named gate sets, each with an accuracy interval, are decided with thresholds given per set.
The training rows (the set named `train`, used only for the parity check, with one unaligned row appended), the
development set and both holdout sets are evaluated and the parity counts are compared; the direct evaluation is
compared with the end-to-end one.

##### Threshold derivation and flips

**Tests**: `HeteronymEvaluator_DeriveGateThreshold_FiveSeeds_FloorOfMeanMinusTwoSd`,
`HeteronymEvaluator_DeriveGateThreshold_EverySeedPasses` (a theory over four cases),
`HeteronymEvaluator_ClassifyFlip_ClassifiesTheChange` (a theory over five cases)

The seed counts of the natural holdout (1,123, 1,114, 1,129, 1,132 and 1,129 of 1,167) give 9,520 basis points and those
of the independent holdout (1,882, 1,912, 1,914, 1,905 and 1,886 of 1,972) give 9,483, the values recorded by the seed
study; five equal counts give their own accuracy; counts so spread that the mean minus two standard deviations is below
zero give 0; and a single count is refused. For the regression and generalization seed counts and two widely spread
sets, every one of the five counts passes the threshold derived from them. A row that becomes right is `fixed`, one that
becomes wrong is `broken`, a change between two wrong readings is `changed`, and an unchanged or new row is no flip.

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

##### Overlay evaluation

**Tests**: `HeteronymEvaluator_EvaluateOverlay_NoCueFires_ReportsSlicesWithEqualModelOnly`,
`HeteronymEvaluator_EvaluateOverlay_CueFires_CountsFiredRowsApartFromChangedRows`,
`HeteronymEvaluator_EvaluateOverlay_CueUnsolvableViolation_ThrowsNamingRowsAndReasons`,
`HeteronymEvaluator_EvaluateOverlay_CueUnsolvableNounFormCueWithoutChange_ThrowsNamingTheCue`,
`HeteronymEvaluator_EvaluateOverlay_RepeatedId_ThrowsInvalidDataException`

The fixture model evaluates overlays written into the fixture folder (one row labeled against its trained pattern, so it
is wrong both ways), and the results, slices and messages listed in the acceptance criteria are compared. The default
fixture has no reading that a cue prior acts on; a second fixture makes `lead.noun` the adjective reading, so the
adjective cue acts on two rows, but the fixture model's own weights are larger than the prior and no reading changes.
A third fixture adds to `lead` a reading that is a noun of another lemma only, so the noun-form prior acts on a target
tagged as a noun without changing its reading.
A model-only reading that differs from the shipped one is therefore not produced by a fixture; the committed-data test
of the subsystem checks the record of the real overlays, whose cue rows do differ (the regression overlay has rows
whose reading a cue prior changes), and the evaluator's own consistency stop covers the rows on which no cue is
detected.
