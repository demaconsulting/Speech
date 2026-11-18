### AveragedPerceptronTrainer

**Purpose**: Train the weights of the heteronym model: a candidate-restricted multi-class averaged perceptron over the
hashed features, in integer arithmetic only, so that the same data, settings and seed give the same weights on every
platform. The unit makes no choice on the development set; it reports the deployable weights after each epoch and
`ModelSelection` chooses.

**Data Model**:

- `AveragedPerceptronTrainer` (an internal sealed class): the corpus, the validated `TrainingSettings` and the bucket
  mask `2^hashBits - 1`. One instance trains once and is not thread-safe. The training state lives inside `Train`: the
  live weights (`int` per bucket), the time-weighted sums of the updates (`long` per bucket), the deployable weights
  (`short` per bucket, reused by every epoch) and the step counter
- `EpochWeights` (an internal read-only record struct): the epoch number from 1 and the deployable weights. The array
  is reused by the next epoch, so a receiver that wants to keep it copies it
- `ShortWeights` (an internal read-only record struct, in the same file): the deployable `short` weights as a
  `HeteronymClassifier.IWeightSource`, the weight source `ModelSelection` scores the kept epochs with. The live `int`
  weights are a private weight source of the trainer

**Learning.** For each training row the trainer scores the candidate readings of the row's form with the runtime's own
scoring and, when the best candidate is not the labeled one, adds one to the weights of the buckets the labeled
candidate's features select and subtracts one from those of the predicted candidate's, once per feature occurrence
(a feature hash that occurs twice is updated twice, as the scorer counts it twice). The scoring is
`HeteronymClassifier.ScoreCandidate` over the candidate's label and role salts, and the bucket of a feature is
`FeatureHashing.Bucket`, so the trainer selects exactly the buckets the analyzer will read. An exact tie keeps the
earlier candidate in the order of the rows of `forms.tsv`, the runtime's tie rule. Only rows that are aligned and
ambiguous are trained on. Only the candidates of the form take part, so a reading that is not a candidate is never
updated.

**Schedule.** The visiting order is built once: each used row appears once, and a natural row appears
`NaturalWeight` times (the repeats stand for the natural rows being worth more than generated ones). Every epoch
shuffles that order with `DeterministicRandom` (Fisher-Yates over SplitMix64, one generator seeded with the seed of the
settings and carried across epochs) and visits it. The step counter `T` advances once per visited row, including the
rows that need no update.

**Exact averaging.** The average of the weights after each of the `T` steps is computed exactly from the current weight
`W` and the time-weighted sum `U` of the updates (each update adds its delta times the step to `U`) as
`((T + 1) W - U) / T`. After an epoch the value is scaled by the settings' scale (64 by default), divided by `T` with
rounding half away from zero, clamped to 32,767 in magnitude (the model format cannot store -32768), and set to zero
when its magnitude is at most the prune threshold. All of it is 64-bit integer arithmetic, so no floating-point value
is involved and the result is identical on every platform. Buckets that were never touched stay 0.

**Overflow bound.** The arithmetic never wraps silently. With `T` the most steps training can take (the weighted rows
visited per epoch times the epochs) and `F` the most feature occurrences of one candidate, a step changes a bucket at
most `2F` times by one, so a live weight is at most `2FT`; `(T + 1) W - U` is the sum of `delta * (T + 1 - s)` over the
updates, so its magnitude is at most `F T (T + 1)`. The constructor requires `2 * scale * F * T * (T + 1) + T` to fit in
64 bits (this covers every intermediate value, the doubled numerator and the rounding term) and `2FT` to fit in 32 bits,
evaluating the bound in 128-bit integers, and otherwise throws `InvalidDataException` naming the scale, natural weight
and epochs to reduce. The training arithmetic is also `checked`, so a flaw in the derivation would throw rather than
wrap. The committed corpus and settings are far inside the bound, so the committed model is unchanged.

**Key Methods**:

- **AveragedPerceptronTrainer(corpus, settings)**: null arguments throw `ArgumentNullException`; the settings are
  validated and a setting out of range is an `InvalidDataException`, as are settings that fail the overflow bound for
  the corpus
- **Decide(inventory, row, weights, mask, scores)** (static): scores every candidate of a prepared row with a weight
  source, writes the integer scores and returns the position of the best candidate, a strictly greater score replacing
  the best so a tie keeps the earlier candidate. Used by the trainer itself and by model selection, so both decide as
  the runtime does
- **Train(onEpoch)**: trains `MaxEpochs` epochs and calls `onEpoch` with the deployable weights after each one

**Error Handling**: settings out of range and null arguments throw as above; a labeled reading that is not a candidate
of its row cannot happen for a loaded corpus (the corpus rejects it) and would throw `InvalidOperationException`, a
defect. The unit does no I/O.

**Dependencies**: `TrainingCorpus` (rows and inventory), `TrainingSettings` (from `TrainingRecord`),
`DeterministicRandom`, and from the HeteronymSubsystem `HeteronymClassifier` (`ScoreCandidate` and `IWeightSource`),
`FeatureHashing` (`Bucket`) and `ReadingInventory` (the salts and the candidate limit).

**Callers**: `ModelSelection` (training, `Decide` and `ShortWeights`) and the unit tests.
