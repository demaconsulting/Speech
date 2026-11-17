### HeteronymClassifier Verification

#### Verification Approach

`HeteronymClassifier` (with `HeteronymPrediction`) and `HeteronymModelProvider` (with `ModelLoadStatus`) are verified by
automated unit tests in `test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/HeteronymClassifierTests.cs`. The
scoring tests use the real inventory, the real feature extractor and the real model writer with hand-built weights: a
single weight on the bias feature of one reading (a bucket the test computes with the same salts the classifier uses)
makes the expected score and the expected softmax confidence exact, so the formulas are checked against closed-form
values, not against the implementation. The contexts are described with the real tokenizer and tagger through the
fixture builder (see *HeteronymSubsystem Verification* for that test-only dependency). The provider tests use a
model source fake that can serve no model, a corrupt file or a failing stream, and the real fixture model
for the success path. No fake of the classifier or the model is needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: every test builds its own model bytes, provider and source; the concurrency tests start their own tasks
  and compare with single-threaded results

#### Acceptance Criteria

With no weights at all the winner is the first candidate in the row order of the inventory and the confidence of two
candidates is exactly one half; the confidence equals the temperature-calibrated softmax of the top candidate for two
and for three candidates (to twelve decimals), and a higher temperature lowers it without changing the winner; a reading
that is not a candidate of the word never wins whatever its weights; a form with one candidate returns that reading with
confidence 1; the observer receives the running count of model-scored classifications only (never for a single-candidate
form); a model for
a newer inventory is rejected while an older or equal one is accepted; null arguments and a target outside the sentence
are rejected; concurrent callers obtain the single-threaded results; the embedded provider and the provider with no
source are not configured and have no model; a provider over a source reports that it is configured; concurrent
first requests all receive a classifier whose predictions equal those of a sequentially obtained one; an absent model
is reported as not shipped, a corrupt model or a
failing or unreadable source as invalid, and a model of a newer inventory as incompatible, each as a status value with
no classifier and never an exception. Whether a source is opened lazily, once, and whether a failure is cached rather
than retried are design choices (see the *HeteronymClassifier Design*) and are not asserted.

#### Test Scenarios

##### Decision and tie rule

**Tests**: `HeteronymClassifier_Classify_NoWeights_TieGoesToFirstCandidate`,
`HeteronymClassifier_Classify_NonCandidateWithHugeWeight_NeverWins`,
`HeteronymClassifier_Classify_ThreeCandidates_ConfidenceIsInRangeAndTopOne`

With an empty model, `record` in `I want to record it .` is `record.noun`, which is listed before `record.verb` in the
forms table, with a confidence of exactly 0.5. A bias weight of 32,767 for `read.past`, which is not a candidate of
`record`, changes nothing: the result is again a `record` reading at 0.5. For the first form of the inventory with three
candidates, a weight of 128 on the middle candidate makes it win with the confidence e^2 / (e^2 + 2), inside one third
to one.

##### Calibrated confidence

**Test**: `HeteronymClassifier_Classify_TwoCandidates64Apart_ConfidenceIsCalibratedSoftmax` (a theory over temperatures
1, 2 and 0.5)

A weight of 64 on the bias feature of `record.verb` at scale 64 gives the winner `record.verb` and the confidence
1/(1+e^-1) = 0.7310585786300049 at temperature 1, 0.6224593312018546 at temperature 2 and 0.8807970779778823 at
temperature 0.5, each equal to twelve decimals.

##### Single candidate and observer

**Tests**: `HeteronymClassifier_Classify_SingleCandidateForm_ReturnsConfidenceOne`,
`HeteronymClassifier_Classify_Observer_ReceivesRunningCount`,
`HeteronymClassifier_Classify_Observer_NotCalledForSingleCandidateForm`

The first single-candidate form of the inventory returns its only reading with confidence exactly 1. The observer (the
test seam that lets a caller act at an exact classification, for example to cancel) is not called for such a form, and
three classifications of an ambiguous form call it with 1, 2 and 3. That the single-candidate form is answered without
scoring is a design choice and is not inferred from the observer.

##### Construction and arguments

**Tests**: `HeteronymClassifier_Constructor_InventoryVersionCompatibility_IsChecked`,
`HeteronymClassifier_Constructor_NullArgument_ThrowsArgumentNullException`,
`HeteronymClassifier_Classify_TargetOutsideSentence_ThrowsArgumentOutOfRangeException`

A model written for the inventory revision plus one is rejected with an `InvalidDataException` whose message names the
newer inventory; models for revision 1 and for the current revision are accepted. A null inventory and a null model
throw `ArgumentNullException`. A target that is negative, equal to the sentence length or beyond it (and `int.MinValue`
and `int.MaxValue`) throws `ArgumentOutOfRangeException` naming `target`, for an ambiguous form and for a
single-candidate form alike (the theory runs both kinds).

##### Thread safety

**Test**: `HeteronymClassifier_Classify_ConcurrentCallers_GetSingleThreadedResults`

A classifier over the fixture model, four sentences with `record` and sixteen tasks of 200 rounds each classify the same
contexts; every prediction equals the single-threaded prediction.

##### Provider configuration and concurrent first requests

**Tests**: `HeteronymModelProvider_EmbeddedAndNone_AreNotConfiguredAndHaveNoModel`,
`HeteronymModelProvider_IsConfigured_ProviderWithSource_ReportsConfigured`,
`HeteronymModelProvider_GetClassifier_ConcurrentFirstCalls_GetClassifiersWithSequentialPredictions`

The embedded provider and the provider with no source are not configured, return no classifier and report `NotShipped`,
which is the state of this release. A provider over a source reports that it is configured. Thirty-two concurrent first
requests each receive a classifier with status `Loaded` that classifies both `record` words of
`I want to record a record.` exactly as a classifier from a separate, sequentially used provider does, over the default
inventory.

##### Provider statuses

**Tests**: `HeteronymModelProvider_GetClassifier_NoModel_ReportsNotShipped`,
`HeteronymModelProvider_GetClassifier_CorruptOrFailingSource_ReportsInvalid`,
`HeteronymModelProvider_GetClassifier_ModelOfNewerInventory_ReportsIncompatible`,
`HeteronymModelProvider_GetClassifier_ModelOfOtherFeatureSet_ReportsIncompatible`,
`HeteronymModelProvider_GetClassifier_ModelWithZeroFeatureSet_ReportsInvalid`,
`HeteronymModelProvider_GetClassifier_CorruptedChecksum_ReportsInvalid`,
`HeteronymModelProvider_GetClassifier_BadMagic_ReportsInvalid`

A source with no model gives `NotShipped` and no classifier, however often it is asked. A model with its last byte
changed, a source that throws `IOException` and one that throws `UnauthorizedAccessException` all give `Invalid` and no
classifier without throwing. A well-formed model written for a newer inventory gives `Incompatible` and no classifier,
and so does a well-formed model whose header names another feature-set version (2 and 65,535). A feature-set version of
zero, a changed checksum field and a changed magic byte give `Invalid`.
