### ModelSelection

**Purpose**: Make every choice that is not a training step, on the development set only, and build the model image from
the choice: how many epochs to keep, which calibration temperature to store, and the two model files (compressed and
uncompressed). The unit is where training becomes a model, and it is the only place a choice is made.

**Data Model**:

- `TrainedModel` (an internal sealed record): the `TrainingInputs`, the `TrainingResult`, the **uncompressed image**,
  the **compressed image** and the development accuracy after each epoch (`EpochScore`)
- `EpochScore` (an internal read-only record struct): the epoch number, the natural development rows decided correctly
  and all development rows decided correctly
- `ModelSelection` (an internal static class, stateless and thread-safe): `TrainerVersion` is the constant 1, recorded
  in every model header and in the training record

*Epoch choice.* After every epoch the deployable weights (averaged, quantized and pruned, exactly what a model file
would hold) are scored on the development rows with the runtime's scoring. The kept epoch is the one that decides the
most **natural** (harvested) development rows correctly, and an exact tie keeps the **earlier** epoch. When the
development set has no natural row, all development rows are counted instead. Rows that are not aligned to the runtime
tokenization and rows of a one-candidate form carry no signal and are left out.

*Temperature choice.* The calibration temperature is the grid value `k / divisor` (`k` from 1 to the number of steps,
so by default 1/20 to 50 in steps of 1/20) that minimizes the negative log-likelihood of the labeled candidate over the
same rows as the epoch choice (the natural development rows, or all of them when there are none), a tie going to the
smaller `k`. The loss is computed from the integer scores with `DeterministicMath.Exp` and `DeterministicMath.Log`,
never `Math.Exp` or `Math.Log`, so the choice does not depend on a platform's math library; the divisor of a score
is the scale times the temperature. Only the chosen value, as a `float`, reaches the image. With no calibration rows
there is nothing to calibrate on and the temperature is 1.

*Images.* The kept weights are listed as sorted (bucket, weight) pairs of the non-zero buckets and compiled twice with
`SparsePerceptronModelWriter.Compile`: once with a Deflate payload (the file that is committed) and once without. Both
headers carry the inventory version, the feature-set version, the trainer version and the data hash. The **uncompressed
image** is the representation that is hashed (`ImageSha256`) and compared, because the bytes of a Deflate stream depend
on the runtime's compression implementation and could differ between operating systems for the same weights; the
weights themselves never do.

No holdout set is read here: the corpus does not contain one.

**Key Methods**:

- **Train(corpus, settings)**: validates the settings, trains with `AveragedPerceptronTrainer` while scoring the
  deployable weights of every epoch on the development rows, keeps a copy of the weights of the best epoch, chooses the
  temperature, builds both images and the `TrainingResult` (selected epochs, temperature step, the temperature, the
  development counts, the loss in millionths, the number of weights, the size and the SHA-256 of the uncompressed
  image) and returns the trained model. Null arguments throw `ArgumentNullException`; a setting out of range is an
  `InvalidDataException`
- **BuildInputs(corpus)**: the `TrainingInputs` of a corpus: the two hashes, the tagger, feature-set, inventory and
  trainer versions, and for the training and the development file the rows, the rows used (aligned and ambiguous), the
  rows skipped because they are not aligned and the rows skipped because the form has one candidate
- **ChooseTemperature** and **RowLoss** (private): the integer scores of every calibration row are computed once; for
  each `k` the loss is the sum over the rows of the logarithm of the sum of `exp((score - top) / divisor)` minus
  `(gold score - top) / divisor`, with the top score subtracted for stability

**Error Handling**: argument and settings errors as above. A writer or model-format failure is an `InvalidDataException`
of `SparsePerceptronModelWriter` and propagates. `InvalidOperationException` is thrown only when no epoch was trained,
which cannot happen with validated settings.

**Dependencies**: `AveragedPerceptronTrainer` and `ShortWeights`, `TrainingCorpus`, `TrainingRecord` (the settings,
inputs and result), `DeterministicMath`, and from the HeteronymSubsystem `SparsePerceptronModelWriter`,
`ReadingInventory` and `HeteronymFeatureExtractor` (the feature-set version).

**Callers**: `TrainCommand` (the model to write and the model to compare), `EvaluateCommand` (the retraining of the
multi-seed study) and the unit tests.
