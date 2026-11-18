## TrainingSubsystem Verification

### Verification Approach

The SpeechTextEn TrainingSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests` that need no network, no hardware and no evaluation corpus. The
subsystem-level tests in `TrainingSubsystemTests.cs` run the real `train` and `evaluate` commands, with their real
units, the real tagger and the real analyzer, over a small trainable data folder that the fixture
(`Fakes/TrainingFixture.cs`) writes in code into a temporary folder: a reading inventory with two readings of `lead`
(a verb listed first and a noun) and a one-candidate form `wind`, training rows that follow clear phrase patterns, eight
natural development rows, a natural holdout of forty rows (one of them labeled against its pattern, so the model is
wrong there by design), an independent holdout of twenty-one rows (one whose target lies inside a longer word) and the
provenance table that clusters the natural rows by document. No fixture table is checked in, so the test data needs no
review of its own. The command harness (`Fakes/CommandHarness.cs`) calls the commands as functions with string writers
for the output and error streams, so the tests observe only exit codes, written files, the loaded model and the
analyzer that uses it. Each unit is verified in isolation by its own test file (see the unit verification documents);
the subsystem tests verify that the units work together in the way the requirements state: a model trained on the
fixture loads into the runtime and classifies the trained patterns, the inputs decide the model and nothing else does,
a stale model is found, the trainer and the analyzer decide every row alike, and the evaluation figures, the gate and
the record follow from the model.

The identity of a model across operating systems is not asserted by one local run. The test
`TrainingSubsystem_Train_FixtureImage_MatchesPinnedHash` pins the SHA-256 hash of the uncompressed model trained from
the fixture; the build matrix of the continuous-integration pipeline runs that test on every supported operating
system, so a platform dependence of the tagger, the feature extractor or the training makes it fail on that system. The
evidence of identity across systems is therefore the matrix of passing runs, not any single run, and the compressed
bytes of a model are deliberately never compared, because they depend on the compression library. The same matrix runs
`train --check` and `evaluate --check` on the committed model, training record and evaluation record, which is the
evidence that the committed files are what every supported operating system produces; no local run can prove that.

The accuracy of the committed model is not asserted by the fixture tests, whose figures only prove the mechanics. It is
verified by the three committed-model tests below, which require the committed model to pass the quality gate and the
committed files to be current, and it is claimed only as far as the committed evaluation record
(`docs/evaluation/text-en/README.md`) states it, with the limits stated there.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline, on every operating system of the matrix
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: every test builds its own temporary data folder and model folder and deletes them afterwards; output
  is captured by string writers
- **Fixtures**: the training fixture and the command harness, defined in the test project
- **Not used** by the fixture tests: the real evaluation corpora and any committed model. Three tests, described under
  *Committed model and record*, deliberately run the commands against the committed repository files instead; they find
  the repository root above the test binaries and are skipped only when it is absent

### Acceptance Criteria

The subsystem tests pass when: training the fixture exits 0 and gives a model that the runtime loads, whose header
carries the inventory version, the feature-set version and the hash of the training data, and that makes the analyzer
choose `lead.verb` for a sentence of the verb pattern and `lead.noun` for one of the noun pattern; training the same
inputs twice gives identical uncompressed images and identical records, and the uncompressed fixture image has the
pinned hash; deleting a holdout file, filling the other with garbage and deleting the provenance table leaves the model
and the record byte for byte unchanged; a row whose target is not a token is skipped and counted in the record (one
unaligned, two single-candidate, thirty-four used of thirty-seven) while training still exits 0; a label that is not a
candidate of its form exits 2 naming the row and writes no model; a fresh model passes `train --check` and a changed
training row makes it exit 1 with a message that names the training data; the analyzer and the trainer decide every
aligned row of the development and both holdout sets alike (no mismatch, none suppressed); the evaluation of the holdout
sets gives the exact accuracies and baselines of the fixture (97.50% against 52.50% on the natural holdout); the gate
exits 0 for a threshold below the accuracy of the model and 1 for one above it; a written evaluation record passes
`evaluate --check` and an edited record file makes it exit 1 naming that file; and the entry point dispatches `train`
and `evaluate` and still reports an unknown command with exit code 2; and, over the committed repository files, the
committed model and training record are what `train --check` reproduces, the committed model passes the gate on the
natural holdout and the committed evaluation record is what `evaluate --check` reproduces.

### Test Scenarios

#### Train and load

**Test**: `TrainingSubsystem_Train_TinyFixture_ProducesLoadableModelThatClassifiesTrainingPatterns`

`train` runs with a model folder and 18 hash bits and exits 0. The model file is loaded by the runtime's own loader and
carries the current inventory version, the feature-set version and the hash of the training data. An analyzer built over
the fixture inventory and that model emits `lead.verb` for `lead` in `You will lead the class today.` and `lead.noun`
for `lead` in `The lead roof was very heavy.`. This is the evidence that the model is trained on, and loadable by, the
real runtime.

#### Repeatable training

**Tests**: `TrainingSubsystem_Train_SameInputsTwice_ProduceIdenticalUncompressedImagesAndRecords`,
`TrainingSubsystem_Train_FixtureImage_MatchesPinnedHash`

Two runs into separate folders give equal uncompressed images and equal record text. The uncompressed image of the
fixture model has the pinned SHA-256 hash. The pinned value is the only way a test can state a result that is the same
on another machine, so the test is the same on every operating system and the CI matrix supplies the cross-system
evidence, as described above.

#### Holdout isolation

**Test**: `TrainingSubsystem_Train_HoldoutFilesAbsentOrAltered_ImageUnchanged`

After a first training, one holdout file is deleted, the other replaced by text that is not a data file and the
provenance table the holdouts are clustered by deleted. Training again exits 0 and gives the same model file and the
same record bytes. That the evaluation of the development set reads no holdout file is verified by the unit tests of
the evaluate command.

#### Unaligned and single-candidate rows

**Test**: `TrainingSubsystem_Train_UnalignedRow_SkippedAndCountedInRecord`

A training row whose target is the beginning of a longer word (`leader`) is appended. Training exits 0 and the record
counts one row skipped as not aligned, two skipped because their word has a single candidate, thirty-four used and
thirty-seven in all.

#### Data error

**Test**: `TrainingSubsystem_Train_LabelNotACandidate_ExitsTwoNamingTheRow`

A development row is relabeled with a reading of another word. Training exits 2, the error stream names the row and no
model folder is created.

#### Staleness check

**Test**: `TrainingSubsystem_TrainCheck_FreshModelIsCurrentAndChangesMakeItStale`

A freshly trained model passes the check with exit code 0. One word of a training sentence is then changed; the check
exits 1 and its output says that the training data changed. The other causes of staleness (a development row, a recorded
setting, the tagger version, a replaced, corrupt or missing model file, a missing or invalid record and a model that is
equal but compressed differently) are verified one by one by the unit tests of the train command.

#### Parity of the trainer and the analyzer

**Test**: `TrainingSubsystem_Evaluate_TrainerAndAnalyzerPredictions_AgreeOnEveryFixtureRow`

`evaluate` runs on the development set and both holdout sets (`--set all`) and exits 0. For each of the three sets one
`parity:` line reports no mismatch and nothing suppressed.

#### Evaluation figures

**Test**: `TrainingSubsystem_Evaluate_FixtureSets_ComputesAccuracyBaselinesAndCoverage`

With part-of-speech hints that never match, so the own-part-of-speech baseline is the first candidate, the natural
holdout reports forty rows, all aligned and covered, a model accuracy of 97.50% (39 of 40) and 52.50% (21 of 40) for the
own-part-of-speech and the majority baselines, and the independent holdout reports twenty-one rows of which twenty are
aligned and covered and a model accuracy of 95.24% (20 of 21), the unaligned row counting as wrong.

#### Quality gate

**Test**: `TrainingSubsystem_Evaluate_Gate_ExitCodeFollowsThresholds`

The record of a trained model is edited to a gate threshold of 95.0% and then of 98.0%. The natural holdout is decided
with 97.5% accuracy, so `evaluate --gate` exits 0 for the first and 1 for the second.

#### Evaluation record

**Test**: `TrainingSubsystem_Evaluate_WriteThenCheck_MatchesAndDetectsEditedRecord`

`evaluate --write --record-dir` writes the record files; `evaluate --check` then exits 0. After one record file is
overwritten, the check exits 1 and its output names that file.

#### Program dispatch

**Test**: `TrainingSubsystem_Program_DispatchesTrainAndEvaluate`

The entry point runs `train --help` and `evaluate --help`, each exits 0 and prints its usage, and an unknown command
exits 2 with an error message that mentions `train`.

#### Committed model and record

**Tests**: `TrainingSubsystem_TrainCheck_CommittedModelAndRecord_AreCurrent`,
`TrainingSubsystem_Evaluate_CommittedModelAndData_PassesGate`,
`TrainingSubsystem_Evaluate_CommittedRecord_IsCurrent`

These tests run the real commands over the repository itself (the repository root is the data, model and record
location) and are skipped only when the root is not found. `train --check` retrains from the committed data with the
recorded settings and exits 0 only when the committed model file, the training record and the hashes of the data, the
feature stream and the image are what retraining produces; a stale or missing file exits 1 and the test fails with the
command output. `evaluate --gate` evaluates the committed model through the analyzer that embeds it on the natural
holdout and exits 0 only when the accuracy is at least 93.0% and the lower bound of the 95% interval of the improvement
over the own-part-of-speech baseline is above zero. `evaluate --check` recomputes the evaluation record and exits 0 only
when every committed record file is identical, byte for byte. The tests verify consistency and the gate, not the
correctness of the labels, and they do not show that the result is the same on another operating system (the CI matrix
does, by running them there). The headline figures (96.23% on the natural holdout against 90.66% for the part of speech
alone, and 95.44% on the independent holdout) are those of the committed record, with the limits stated there.

See also *SpeechTextEn System Verification* and the unit documents `training-corpus.md`, `deterministic-random.md`,
`deterministic-math.md`, `averaged-perceptron-trainer.md`, `model-selection.md`, `training-record.md`,
`train-command.md`, `heteronym-evaluator.md`, `evaluation-record.md` and `evaluate-command.md`.
