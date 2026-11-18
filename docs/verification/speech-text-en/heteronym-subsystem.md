## HeteronymSubsystem Verification

### Verification Approach

The SpeechTextEn HeteronymSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem` that need no network and no hardware. The subsystem-level
tests in `HeteronymSubsystemTests.cs` exercise the units together: the real inventory (loaded from the tables embedded
in the library), the real feature extractor, the real model writer and loader, the real classifier and the real model
provider. Apart from the embedded-model test, the tests make up the model: the test project builds a small one on
demand, so that what they check does not depend on what the shipped model happens to decide. The fixture builder
`Fakes/HeteronymFixtureModel.cs` describes a few labeled example
sentences exactly as the analyzer does, extracts their features, gives every feature of the gold reading a positive
weight and writes the result through the real writer; no binary fixture model is committed, so its bytes can never go
stale against the feature set. A model source fake (`Fakes/CountingModelSource.cs`) serves the bytes and can serve
nothing, a failing stream or a corrupt file. One subsystem test uses the shipped model instead: the embedded provider
loads the model embedded in the library.

**Test-only dependency on the AnalysisSubsystem.** To describe example sentences realistically the fixture builder
reaches the real part-of-speech tagger through `PartOfSpeechAdapter`, which is a type of the AnalysisSubsystem, and the
tokenization-parity test of the feature extractor calls the real tokenizer of the part-of-speech package. The
HeteronymSubsystem itself has no such dependency in production code: it receives only offsets, tag names and
confidences. The dependency exists only in the tests, because a realistic context needs the tagger's real tokens and
tags, and it is why the subsystem tests state the tagger as a real collaborator rather than a fake.

The units are verified in isolation by their own test files (see the unit verification documents); the subsystem tests
cover what only the combination can show: that a model supplied through a provider chooses a reading by context, that a
model is bound to reading ids rather than to table positions, and that every form of the real inventory classifies
within its own candidates. The accuracy of the shipped model is not verified here: that is the subject of the training
subsystem's committed-model tests and of the committed evaluation record (see *SpeechTextEn TrainingSubsystem
Verification*).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Files**: the golden feature vectors `TestData/heteronym-feature-vectors.tsv`, copied next to the test assembly; no
  test needs network access
- **Isolation**: every test builds its own model bytes, provider and source; the culture tests set the current
  culture and restore it in a `finally` block; the concurrency tests start their own tasks

### Acceptance Criteria

The subsystem tests pass when: a provider over a fixture model is configured, exposes the default inventory (its version
and form count) and yields a classifier whose status is `Loaded` and which chooses the verb reading for the verb use and
the noun reading for the noun use of the same spelling in one sentence, with a confidence above one half; the decisions
of a model do not change when the inventory lists the same readings in a different order, because the model binds to
reading ids; an empty model classifies every form of the real inventory in a sentence, with a prediction inside the
form's candidates and a confidence from 0 to 1, without error; and the embedded provider is configured, loads the
shipped model and, through the real tagger, chooses `wind.coil` for the verb and `wind.air` for the noun in
`Please wind the clock before the wind blows.`.

### Test Scenarios

#### Provider to classifier with a fixture model

**Test**: `HeteronymSubsystem_ProviderToClassifier_FixtureModel_ChoosesReadingByContext`

A provider over a source serving a fixture model trained on four example sentences reports that it is configured and
exposes the version and form count of the default inventory before any classifier has been requested (whether the
source is opened lazily is a design choice and is not asserted). It is then asked for its classifier (status
`Loaded`). The sentence `I want to record a record.` is described like the analyzer does (the tokens are `I`, `want`,
`to`, `record`, `a`, `record` and the period); the first `record` is classified as `record.verb` and the second as
`record.noun`, each with a confidence above 0.5.

#### Model bound to ids, not positions

**Test**: `HeteronymSubsystem_ModelBindsToReadingIds_NotTablePositions`

An inventory built from two hand-written tables that list `record.verb` before `record.noun` (the opposite of the
embedded order) and a classifier over it make the same decisions as the classifier over the embedded inventory, with the
same fixture model, for both words of the sentence: the chosen reading ids are equal.

#### Every inventory form classifies

**Test**: `HeteronymSubsystem_EveryInventoryForm_ClassifiesWithinItsCandidates`

For every form of the real inventory a sentence `We said {form} there.` is described with the real tagger and classified
by a classifier over an empty model (hash bits 18, no weights); each prediction names one of the form's candidates and
has a confidence from 0 to 1. This proves that every embedded form is found as a single token, that extraction works for
real tokens of all of them and that no form breaks the classifier.

#### Shipped model through the embedded provider

**Test**: `HeteronymSubsystem_EmbeddedProvider_ShippedModel_ChoosesReadingByContext`

The static embedded provider reports that it is configured and returns a classifier with the status `Loaded`. The
sentence `Please wind the clock before the wind blows.` is described like the analyzer does, and the classifier chooses
`wind.coil` for the first `wind` and `wind.air` for the second. This proves that the shipped model is embedded under the
resource name the provider asks for, that it loads and fits this build, and that it decides one clear pair of contexts;
it is not a measure of accuracy.

See also the unit documents `reading-inventory.md`, `heteronym-feature-extractor.md`, `sparse-perceptron-model.md` and
`heteronym-classifier.md`, and *SpeechTextEn System Verification* for the behavior through the public analyzer.
