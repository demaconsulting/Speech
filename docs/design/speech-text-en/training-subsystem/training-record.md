### TrainingRecord

**Purpose**: Hold the settings that determine a trained model, what the training read and what it produced, and render
and parse them as the training record, `training.json`, in a deterministic text form the tool can verify byte for byte.
The settings are fixed before training starts and the gate settings are fixed before any holdout is evaluated; this unit
is where both are written down.

**Data Model** (all internal sealed records, immutable):

- `TrainingSettings`: `Seed` (the seed of the row-order shuffle), `HashBits` (16 to 24), `Scale` (the fixed-point scale
  of the integer weights), `PruneThreshold` (weights of at most this magnitude are dropped), `NaturalWeight` (the visits
  of each natural row per epoch), `MaxEpochs`, `TemperatureSteps` and `TemperatureDivisor` (the grid of candidate
  temperatures `k / divisor`). `TrainingSettings.Default` is the committed configuration: seed 1, 22 hash bits, scale
  64, prune threshold 2, natural weight 3, at most 12 epochs, a grid of 1,000 steps of 1/20. The prune threshold and the
  natural-row weight were chosen on the development set only, by a grid that was written down before it was run; the
  seed, the hash bits and the scale are fixed values of that grid
- `EvaluationSettings`: `Seeds` (the seeds of the multi-seed study, 1 to 5 by default, including the committed seed),
  `Resamples` (10,000), `ConfidencePercent` (95), `BootstrapSeed` (7) and the least pooled accuracy of the gate in
  per mille (930, that is 93.0%). `EvaluationSettings.Default` is the quality gate's definition, committed before the
  first holdout evaluation
- `TrainingInputs`: what the training read: the data hash, the features hash, the tagger version, the feature-set
  version, the inventory version, the trainer version, and for the training and the development file the number of rows,
  of rows used, of rows skipped because the target is not exactly one runtime token and of rows skipped because the form
  has one candidate
- `TrainingResult`: what the training produced: the number of epochs kept, the temperature step and the temperature
  stored, the natural and all development rows decided correctly with the number of rows of each, the calibration loss
  in millionths, the number of stored weights, the size of the uncompressed image and its SHA-256
- `TrainingRecord`: the four parts above, with the layout version `SchemaVersion` (1)

**The record layout.** `training.json` is one JSON object with a fixed key order and the members `schema`, `training`,
`evaluation` (with the array `seeds`), `inputs` and `result`, in the order of the records above. It is written with
two-space indentation, LF line ends and no byte order mark, and ends with a line feed. Numbers are integers; the one
non-integer, the temperature, is written as a string with the round-trip format and the invariant culture. It holds no
time stamp, host name or duration, so the same record always renders to the same bytes and a committed record is
comparable. The hashes are lower-case hexadecimal. The record is data of the repository, but is not a hand-edited file:
`train` writes it and `train --check` verifies it.

**Key Methods**:

- **TrainingSettings.Validate()** and **EvaluationSettings.Validate()**: check every value against its range (for the
  training settings, 16 to 24 hash bits, a scale of 1 to 1,000,000, a prune threshold up to 1,000, a natural weight and
  an epoch count from 1 up to 100, 1 to 100,000 temperature steps and a divisor of 1 to 1,000,000; for the evaluation
  settings, 1 to 100 seeds, 100 to 1,000,000 resamples, a confidence of 50 to 99 percent and a threshold of 1 to 1,000
  per mille); a value out of range is an `InvalidDataException` that names the setting
- **TrainingRecord.ToJson()**: renders the record as above
- **TrainingRecord.Parse(json)**: parses and validates a record. A null text throws `ArgumentNullException`. Malformed
  JSON, a missing or mistyped member, an unsupported schema version or a setting out of range is an
  `InvalidDataException` with a message that names the problem

**Error Handling**: every defect of a record is an `InvalidDataException`; the JSON, format and lookup exceptions of
the parser are translated into it, with the original as the inner exception. Nothing else is caught. The commands treat
the exception as an input error (or, for `train --check`, as a stale record).

**Dependencies**: the framework only (`System.Text.Json`, invariant-culture formatting).

**Callers**: `ModelSelection` (the inputs and result it builds), `AveragedPerceptronTrainer` (the training settings),
`HeteronymEvaluator` and `EvaluationRecord` (the evaluation settings), `TrainCommand` and `EvaluateCommand` (writing,
reading and verifying the record) and the unit tests.
