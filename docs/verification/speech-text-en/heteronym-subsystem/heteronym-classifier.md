### HeteronymClassifier Verification

#### Verification Approach

`HeteronymClassifier` (with `HeteronymPrediction`) and `HeteronymModelProvider` (with `ModelLoadStatus`) are verified by
automated unit tests in `test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/HeteronymClassifierTests.cs`. The
decision tests use the real inventory, the real feature extractor and the real model writer with hand-built weights: a
single weight on the bias feature of one reading makes the expected winner and the expected softmax confidence exact,
so the formulas are checked against closed-form values, not against the implementation. The contexts are described
with the real tokenizer and tagger through the fixture builder (see *HeteronymSubsystem Verification* for that
test-only dependency). The provider tests use a model source fake that can serve no model, a corrupt file or a failing
stream, and the real fixture model for the success path. No fake of the classifier or the model is needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: every test builds its own model bytes, provider and source; the concurrency tests start their own tasks
  and compare with single-threaded results

#### Acceptance Criteria

- **Decision**: with no weights the winner is the first candidate in the row order of the inventory with a confidence
  of exactly one half for two candidates; a reading that is not a candidate of the word never wins whatever its
  weights; the confidence equals the temperature-calibrated softmax of the top candidate for two and three candidates
  (to twelve decimals); a form with one candidate returns that reading with confidence 1; a target outside the
  sentence is rejected for both kinds of form
- **Adjective cue**: with a model that prefers the time reading of `minute` by a quarter of the cue's odds, a word
  tagged `ADJ` after a degree adverb or coordinated with an adjective gets `minute.tiny`, while the same model keeps
  `minute.time` when the word is tagged `NOUN`, when the word before it is a degree word tagged `ADJ` or when the
  coordinated word is not an adjective; a model that prefers the time reading by twice the cue's odds keeps it
- **Model compatibility**: a model for a newer inventory is rejected while an older or equal one is accepted; null
  arguments are rejected
- **Thread safety and observer**: concurrent callers obtain the single-threaded results; the observer receives the
  running count of model-scored classifications only
- **Model supply**: the embedded provider is configured and loads the embedded model, the provider with no source is
  not configured and has no model, concurrent first requests all receive a classifier whose predictions equal those
  of a sequentially obtained one, and an absent, corrupt, unreadable, newer-inventory or other-feature-set model is
  reported as a status value with no classifier and never as an exception. Whether a source is opened lazily, once, and
  whether a failure is cached rather than retried are design choices (see *HeteronymClassifier Design*) and are not
  asserted

#### Test Scenarios

##### Decision

**Tests**: `HeteronymClassifier_Classify_NoWeights_TieGoesToFirstCandidate`,
`HeteronymClassifier_Classify_NonCandidateWithHugeWeight_NeverWins`,
`HeteronymClassifier_Classify_TwoCandidates64Apart_ConfidenceIsCalibratedSoftmax`,
`HeteronymClassifier_Classify_ThreeCandidates_ConfidenceIsInRangeAndTopOne`,
`HeteronymClassifier_Classify_SingleCandidateForm_ReturnsConfidenceOne`,
`HeteronymClassifier_Classify_TargetOutsideSentence_ThrowsArgumentOutOfRangeException`

With an empty model, `record` in `I want to record it .` is `record.noun`, which is listed before `record.verb` in the
forms table, with a confidence of exactly 0.5. A bias weight of 32,767 for `read.past`, which is not a candidate of
`record`, changes nothing. A weight of 64 on the bias feature of `record.verb` at scale 64 and temperature 2 makes
`record.verb` win with the confidence 1/(1+e^-0.5). For the first form of the inventory with three candidates, a weight
of 128 on the middle candidate makes it win with the confidence e^2 / (e^2 + 2). The first single-candidate form
returns its only reading with confidence exactly 1. A target that is negative, equal to the sentence length or
`int.MaxValue` throws `ArgumentOutOfRangeException` naming `target` for an ambiguous form, and a target equal to the
length does so for a single-candidate form.

##### Adjective cue

**Tests**: `HeteronymClassifier_Classify_AdjectiveCue_DecidesAWeaklyContestedAdjective`,
`HeteronymClassifier_Classify_AdjectiveCueAgainstAConfidentModel_ModelWins`

The contexts are built with explicit tags. A bias weight of a quarter of the cue's odds (scale 64, temperature 1) for
`minute.time`: `very minute and clear` (`ADV ADJ CCONJ ADJ`), `so minute that`, `grey and minute` and
`minute and very clear` give `minute.tiny` with a confidence above one half; `wait a minute` (`NOUN`),
`the very minute he came` (`very` tagged `ADJ`, `minute` tagged `NOUN`), `very minute` with `minute` tagged `NOUN` and
`a minute and clear` with a noun after the conjunction keep `minute.time`. A bias of twice the cue's odds keeps
`minute.time` for `very minute` (`ADV ADJ`).

##### Model compatibility

**Tests**: `HeteronymClassifier_Constructor_InventoryVersionCompatibility_IsChecked`,
`HeteronymClassifier_Constructor_NullArgument_ThrowsArgumentNullException`

A model written for the inventory revision plus one is rejected with an `InvalidDataException` whose message names the
newer inventory; models for revision 1 and for the current revision are accepted. A null inventory and a null model
throw `ArgumentNullException`.

##### Thread safety and observer

**Tests**: `HeteronymClassifier_Classify_ConcurrentCallers_GetSingleThreadedResults`,
`HeteronymClassifier_Classify_Observer_ReceivesRunningCount`

A classifier over the fixture model, four sentences with `record` and sixteen tasks of 200 rounds each classify the
same contexts; every prediction equals the single-threaded prediction. The observer (the test seam that lets a caller
act at an exact classification, for example to cancel) is not called for a single-candidate form, and three
classifications of an ambiguous form call it with 1, 2 and 3.

##### Model supply

**Tests**: `HeteronymModelProvider_EmbeddedAndNone_EmbeddedLoadsAndNoneHasNoModel`,
`HeteronymModelProvider_GetClassifier_ConcurrentFirstCalls_GetClassifiersWithSequentialPredictions`,
`HeteronymModelProvider_GetClassifier_NoModel_ReportsNotShipped`,
`HeteronymModelProvider_GetClassifier_CorruptOrFailingSource_ReportsInvalid`,
`HeteronymModelProvider_GetClassifier_ModelOfNewerInventory_ReportsIncompatible`,
`HeteronymModelProvider_GetClassifier_ModelOfOtherFeatureSet_ReportsIncompatible`

The embedded provider is configured and returns a classifier with status `Loaded`, while the provider with no source is
not configured, returns no classifier and reports `NotShipped`. Thirty-two concurrent first requests each receive a
classifier with status `Loaded` that classifies both `record` words of `I want to record a record.` exactly as a
classifier from a separate, sequentially used provider does, and the provider exposes the default inventory. A source
with no model gives `NotShipped` and no classifier, however often it is asked. A model with its last byte changed, a
source that throws `IOException` and one that throws `UnauthorizedAccessException` all give `Invalid` and no
classifier without throwing. A well-formed model written for a newer inventory gives `Incompatible` and no classifier,
and so does a well-formed model whose header names another feature-set version (2 and 65,535).
