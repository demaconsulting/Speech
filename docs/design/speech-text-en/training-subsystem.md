## TrainingSubsystem Design

![TrainingSubsystem Structure](TextEnTrainingSubsystemView.svg)

### Overview

The SpeechTextEn TrainingSubsystem is the `train` and `evaluate` commands of the Text.En trainer
(`tools/DemaConsulting.Speech.Text.En.Trainer`), the same console tool that hosts the `check-data` command of the
TrainingDataSubsystem. The tool is built and tested with the repository but is neither packaged nor shipped. The
subsystem trains the English heteronym model from the training and development data, verifies that a committed model is
what retraining produces, and measures a model on the development and holdout sets end to end, deciding the quality
gate.
Unlike the TrainingDataSubsystem it references the Text.En library: the library grants its internals to the trainer
assembly (and to the trainer's tests) with `InternalsVisibleTo`, so the trainer reuses the analyzer's own tokenization
adapter, token description, feature extractor, scoring and model format instead of a copy of them. It contains the
following units:

- **TrainingCorpus**: the loader of `train.tsv`, `dev.tsv`, `readings.tsv` and `forms.tsv` only, and the preparation of
  every row with the runtime's own units (with `LabeledRow`, `PreparedRow` and `RowPreparer`)
- **DeterministicRandom**: the SplitMix64 generator with unbiased draws and a Fisher-Yates shuffle
- **DeterministicMath**: the exponential and the natural logarithm from basic arithmetic only
- **AveragedPerceptronTrainer**: the candidate-restricted averaged perceptron in integer arithmetic (with
  `ShortWeights`)
- **ModelSelection**: the epoch and temperature choices on the development set, and the model images
- **TrainingRecord**: the training and evaluation settings, the inputs and the result, and the `training.json` text form
- **TrainCommand**: the command line entry of `train` and `train --check`
- **HeteronymEvaluator**: the end-to-end evaluation with baselines, strata, the clustered bootstrap, the gate decision
  and the parity check (with its result records)
- **EvaluationRecord**: the six deterministic tab-separated files of the evaluation record
- **EvaluateCommand**: the command line entry of `evaluate`

The data is a tool input, not a software item (see *SpeechTextEn TrainingDataSubsystem Design*).

### Interfaces

The subsystem exposes two command line interfaces and no public API: every type is internal. `train [options]` trains
and writes the model file and the training record; `train --check` verifies them; `evaluate [options]` evaluates,
decides the gate and writes or verifies the evaluation record. The options and the exit codes (0 success, 1 a stale
model or record or a failed gate, 2 a usage, input or I/O error) are described with *TrainCommand Design* and
*EvaluateCommand Design*; progress and results go to the standard output as lines that start with `train:` or
`evaluate:`, and usage and input errors to the standard error.

The subsystem consumes the .NET base class library, the TrainingDataSubsystem (`TsvTable`, `CheckReport`,
`TextEnDataSet` and `CheckDataCommand`: the strict table reader, the data folder discovery and the row header), the
internals of the Text.En library through `InternalsVisibleTo`, and the `DemaConsulting.PartOfSpeech` assembly, whose
tagger the library's adapter wraps. The classes live in the `DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem`
namespace. The committed locations that the commands derive from the data folder are
`src/DemaConsulting.Speech.Text.En/Resources/heteronyms.bin` (the model),
`tools/DemaConsulting.Speech.Text.En.Trainer/training.json` (the training record) and `docs/evaluation/text-en/` (the
evaluation record). The model is embedded in the package, so `train --check` and `evaluate --check` verify the shipped
files; continuous integration runs both on every operating system of the build matrix.

### Design

**Training flow.** `TrainCommand` finds the data folder and loads a `TrainingCorpus`. `ModelSelection` trains with
`AveragedPerceptronTrainer`, which reports the deployable weights after each epoch; `ModelSelection` scores each epoch
on the development rows, keeps the best, chooses the temperature and builds the compressed and the uncompressed model
image and the `TrainingResult`. `TrainCommand` writes the compressed model and the `TrainingRecord`. `train --check`
runs the same flow with the recorded settings and compares the outcome with the record and the committed model.

**Evaluation flow.** `EvaluateCommand` loads the model, the settings and the corpus, and has the `HeteronymEvaluator`
score each requested set through the real analyzer, compare it with the direct classifier path, and compute the
baselines, strata and bootstrap interval. It decides the gate with `HeteronymEvaluator.DecideGate`, runs the multi-seed
study with `ModelSelection`, and has `EvaluationRecord` write or verify the record.

**Unit collaboration.** `TrainingCorpus` is the only unit that opens a training file, `ModelSelection` the only one that
makes a development-set choice and builds a model, `HeteronymEvaluator` the only one that opens a holdout file, and
`TrainingRecord` the only one that knows the text form of the settings and the record. The dependency direction is
`TrainCommand` and `EvaluateCommand` to `ModelSelection`, `TrainingCorpus`, `TrainingRecord`, `HeteronymEvaluator` and
`EvaluationRecord`; `ModelSelection` to `AveragedPerceptronTrainer`, `DeterministicMath` and `TrainingCorpus`;
`AveragedPerceptronTrainer` to `DeterministicRandom` and `TrainingCorpus`; `HeteronymEvaluator` to `TrainingCorpus` and
`DeterministicRandom`; and `EvaluationRecord` to `HeteronymEvaluator`. There are no cycles.

**Parity with the runtime (decision).** A trained model is valid only while the training sees exactly what the analyzer
sees, so no step of the preparation or the scoring is a copy:

- row preparation goes through `PartOfSpeechAdapter.Analyze`, `PartOfSpeechAdapter.DescribeSentence` and
  `HeteronymFeatureExtractor.Extract`, the units the analyzer itself calls (`DescribeSentence` lives in the adapter,
  where the analyzer's reading pass also calls it, so the heteronym subsystem does not depend on the analysis subsystem)
- the bucket of a feature for a reading is `FeatureHashing.Bucket` and the score of a candidate is
  `HeteronymClassifier.ScoreCandidate`, each the single definition that the runtime classifier calls too; the tie rule,
  the earlier candidate in the order of `forms.tsv`, is the runtime's
- the model is written by `SparsePerceptronModelWriter` and read back by the strict loader
- the evaluation runs the real `EnglishTextAnalyzer` over the row's sentence with a `HeteronymModelProvider` over the
  model under test, and counts a row with no emitted reading as wrong; the direct path (the runtime classifier over the
  precomputed context) is compared with it row by row, and the **parity counts** (compared, mismatched, suppressed) are
  part of the record, so a drift between the trainer's features and the analyzer's shows as a mismatch

**Isolation of the holdout sets (decision).** The training corpus opens exactly four files by their names and never
lists the folder, so a holdout set cannot influence a model: that is a property of the code, not a convention. Every
choice (the epoch count, the temperature, and the pruning threshold and natural-row weight of the default settings) is
made on the development set. A holdout file is opened only by the evaluator for a set name a command passes, and the
command says so when it does. A set that has guided a choice no longer measures anything, so the gate is run only after
the configuration is fixed, and a change of the configuration after a holdout number has been seen is recorded as a new
gate run (see `RUN-LOG.md` of the trainer folder).

**Determinism and reproducibility (contract).** The same data, settings and seed give the same model bytes on every
platform, and a committed model can therefore be verified by retraining. The contract is kept by construction:

- training is single threaded, in integer arithmetic only, with exact averaging in 64-bit integers
- the order of the rows comes from `DeterministicRandom` (SplitMix64), never from the framework's generator
- the only floating-point computation that influences a choice, the calibration loss, uses `DeterministicMath`, never
  `Math.Exp` or `Math.Log`
- every sort is ordinal, every number is formatted with the invariant culture, and every file has LF line ends and no
  byte order mark
- the model that is hashed and compared is the **uncompressed** image, because the bytes of a Deflate stream depend on
  the runtime's compression implementation; the compressed file is the one that is committed, and `train --check`
  converts it back to compare it
- the training record keeps a hash of the feature stream (`featuresSha256`) next to the data hash, so a drift of the
  tagger or the extractor can be told apart from a change of the data when the check reports a stale model

The records hold no time stamp, host name or duration; durations are printed to the console only.

**The quality gate.** The gate is decided on the natural holdout with two conditions, both required: the pooled
end-to-end accuracy is at least a threshold (930 per mille by default, a row with no emitted reading counted as wrong),
and the lower bound of a two-sided percentile interval (95% by default) of the paired, document-clustered bootstrap of
the accuracy of the model minus the accuracy of the own-part-of-speech baseline is above zero, with the bounds compared
exactly. The independent holdout is reported but is not part of the gate. The settings are committed in the training
record before the first holdout evaluation, and are not changed by a result.

**Concurrency and cancellation.** None: both commands are single threaded, run to completion, and keep no state between
runs; the evaluator caches the sets it has prepared for the duration of one run only.
