## TrainingSubsystem Verification

### Verification Approach

The SpeechTextEn TrainingSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests` that need no network, no hardware and no evaluation corpus. The
subsystem-level tests in `TrainingSubsystemTests.cs` run the real `train` and `evaluate` commands, with their real
units, the real tagger and the real analyzer, over a small trainable data folder that the fixture
(`Fakes/TrainingFixture.cs`) writes in code into a temporary folder: a reading inventory with two readings of `lead`
(a verb listed first and a noun) and a one-candidate form `wind`, training rows that follow clear phrase patterns, eight
natural development rows, a natural holdout of forty rows (one labeled against its pattern, so the model is wrong there
by design), an independent holdout of twenty-one rows (one whose target lies inside a longer word) and the provenance
table that clusters the natural rows by document. The command harness (`Fakes/CommandHarness.cs`) calls the commands as
functions with string writers, so the tests observe only exit codes, written files, the loaded model and the analyzer
that uses it. Each unit is verified in isolation by its own test file (see the unit verification documents); the
subsystem tests verify that the units work together as the requirements state.

The identity of a model across operating systems is not asserted by one local run. The test
`TrainingSubsystem_Train_FixtureImage_MatchesPinnedHash` pins the SHA-256 hash of the uncompressed fixture model, and
the build matrix of the continuous-integration pipeline runs it on every supported operating system, so a platform
dependence of the tagger, the feature extractor or the training makes it fail on that system. The compressed bytes of a
model are deliberately never compared, because they depend on the compression library. The same matrix runs the
committed-model tests below, which is the evidence that the committed files are what every supported operating system
produces.

The accuracy of the committed model is not asserted by the fixture tests, whose figures only prove the mechanics. It is
claimed only as far as the committed evaluation record (`docs/evaluation/text-en/README.md`) states it, with the limits
stated there.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline, on every operating system of the matrix
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: every test builds its own temporary data folder and model folder and deletes them afterwards; output
  is captured by string writers
- **Fixtures**: the training fixture and the command harness, defined in the test project
- **Committed files**: two tests, described under *Committed model and record*, run the commands against the committed
  repository files instead of the fixture; they find the repository root above the test binaries and are skipped only
  when it is absent

### Acceptance Criteria

- **Repeatable model** (`Training-RepeatableModel`): training the fixture exits 0 and gives a model that the runtime
  loads, with the inventory version, the feature-set version and the data hash in its header; training twice gives
  identical uncompressed images and records, and the uncompressed fixture image has the pinned hash; a fresh model
  passes `train --check`, and a changed training row makes it exit 1 naming the training data; the committed model and
  record are what `train --check` reproduces.
- **Command contract** (`Training-CommandContract`): the entry point dispatches `train` and `evaluate` and reports an
  unknown command with exit code 2; a label that is not a candidate of its form exits 2 naming the row and writes no
  model.
- **Holdout-free training** (`Training-HoldoutFreeTraining`): deleting a holdout file, filling the other with garbage
  and deleting the provenance table leaves the model and the record byte for byte unchanged.
- **Runtime-aligned training** (`Training-RuntimeAlignedTraining`): the trained model makes the analyzer choose
  `lead.verb` and `lead.noun` for the sentences of the trained patterns; a row whose target is not a token is skipped
  and counted in the record while training still exits 0; the analyzer and the trainer decide every aligned row of the
  development and both holdout sets alike.
- **Evaluation report** (`Training-EvaluationReport`): the evaluation of the holdout sets gives the exact accuracies
  and baselines of the fixture; the gate exits 0 for a threshold below the accuracy of the model and 1 for one above it;
  a written record passes `evaluate --check` and an edited record file makes it exit 1 naming that file; the committed
  model passes the gate and the committed record is what `evaluate --check` reproduces.

### Test Scenarios

#### Train and load

**Test**: `TrainingSubsystem_Train_TinyFixture_ProducesLoadableModelThatClassifiesTrainingPatterns`

`train` runs with a model folder and 18 hash bits and exits 0. The runtime's own loader loads the model, and an analyzer
built over the fixture inventory and that model emits `lead.verb` for `lead` in `You will lead the class today.` and
`lead.noun` in `The lead roof was very heavy.`.

#### Repeatable training

**Tests**: `TrainingSubsystem_Train_SameInputsTwice_ProduceIdenticalUncompressedImagesAndRecords`,
`TrainingSubsystem_Train_FixtureImage_MatchesPinnedHash`

Two runs into separate folders give equal uncompressed images and equal record text, and the uncompressed fixture image
has the pinned SHA-256 hash, which the CI matrix checks on every operating system.

#### Holdout isolation

**Test**: `TrainingSubsystem_Train_HoldoutFilesAbsentOrAltered_ImageUnchanged`

After a first training, one holdout file is deleted, the other replaced by text that is not a data file and the
provenance table deleted. Training again gives the same model file and the same record bytes. That the evaluation of
the development set reads no holdout file is verified by the unit tests of the evaluate command.

#### Unaligned and single-candidate rows

**Test**: `TrainingSubsystem_Train_UnalignedRow_SkippedAndCountedInRecord`

A training row whose target is the beginning of a longer word (`leader`) is appended. Training exits 0 and the record
counts one row skipped as not aligned, two as single-candidate, thirty-four used and thirty-seven in all.

#### Data error and entry point

**Tests**: `TrainingSubsystem_Train_LabelNotACandidate_ExitsTwoNamingTheRow`,
`TrainingSubsystem_Program_DispatchesTrainAndEvaluate`

A development row relabeled with a reading of another word makes training exit 2 naming the row and create no model
folder. The entry point runs `train --help` and `evaluate --help`, each exits 0 and prints its usage, and an unknown
command exits 2.

#### Staleness check

**Test**: `TrainingSubsystem_TrainCheck_FreshModelIsCurrentAndChangesMakeItStale`

A freshly trained model passes the check; after one word of a training sentence is changed the check exits 1 and says
that the training data changed. The other causes are verified by the unit tests of the train command.

#### Parity of the trainer and the analyzer

**Test**: `TrainingSubsystem_Evaluate_TrainerAndAnalyzerPredictions_AgreeOnEveryFixtureRow`

`evaluate --set all` exits 0 and for each of the three sets one `parity:` line reports no mismatch and nothing
suppressed.

#### Evaluation figures and quality gate

**Tests**: `TrainingSubsystem_Evaluate_FixtureSets_ComputesAccuracyBaselinesAndCoverage`,
`TrainingSubsystem_Evaluate_Gate_ExitCodeFollowsThresholds`

With part-of-speech hints that never match, so the own-part-of-speech baseline is the first candidate, the natural
holdout reports forty rows, all aligned and covered, a model accuracy of 97.50% (39 of 40) and 52.50% (21 of 40) for the
own-part-of-speech and the majority baselines, and the independent holdout reports twenty-one rows of which twenty are
aligned and a model accuracy of 95.24% (20 of 21), the unaligned row counting as wrong. The record of a trained model
is edited to a gate threshold of 95.0% and then of 98.0%, so `evaluate --gate` exits 0 and then 1.

#### Evaluation record

**Test**: `TrainingSubsystem_Evaluate_WriteThenCheck_MatchesAndDetectsEditedRecord`

`evaluate --write --record-dir` writes the record files and `evaluate --check` exits 0; after one record file is
overwritten, the check exits 1 and names that file.

#### Committed model and record

**Tests**: `TrainingSubsystem_TrainCheck_CommittedModelAndRecord_AreCurrent`,
`TrainingSubsystem_Evaluate_CommittedModelAndData_PassesGate`

These tests run the real commands over the repository itself and are skipped only when the root is not found.
`train --check` retrains from the committed data with the recorded settings and exits 0 only when the committed model
file, the training record and the hashes are what retraining produces. `evaluate --gate --check` evaluates the
committed model through the analyzer that embeds it and exits 0 only when the accuracy on the natural holdout is at
least 93.0%, the lower bound of the 95% interval of the improvement over the own-part-of-speech baseline is above zero,
and every committed evaluation record file is identical, byte for byte. The tests verify consistency and the gate, not
the correctness of the labels, and they do not show that the result is the same on another operating system (the CI
matrix does). The headline figures are those of the committed record, with the limits stated there.

See also *SpeechTextEn System Verification* and the unit documents `training-corpus.md`, `deterministic-random.md`,
`deterministic-math.md`, `averaged-perceptron-trainer.md`, `model-selection.md`, `training-record.md`,
`train-command.md`, `heteronym-evaluator.md`, `evaluation-record.md` and `evaluate-command.md`.
