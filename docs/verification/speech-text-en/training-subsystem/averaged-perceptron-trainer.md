### AveragedPerceptronTrainer Verification

#### Verification Approach

`AveragedPerceptronTrainer` (with `ShortWeights`) is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/AveragedPerceptronTrainerTests.cs`. The tests train
on the small trainable corpus of the training fixture with one epoch and 18 hash bits, or with a handful of epochs, and
check the observable result: the weights reported after each epoch. The exactness test trains on a single row that the
initial weights get wrong, a case whose correct result is known without running the unit: one correction of size one,
averaged over one step and scaled by 64, gives weights of exactly plus and minus 64 and a total of zero. The other tests
use the runtime's own scoring (the shared candidate score of the classifier) to judge the learned weights, so they show
that the weights decide rows the way the runtime will. Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder

#### Acceptance Criteria

- **Learning**: after one row that the initial weights get wrong, the non-zero weights are exactly +64 and -64 and sum
  to zero; after eight epochs on the separable fixture every aligned ambiguous training row is decided for its label by
  the runtime's scoring; a natural-row weight of 3 gives other weights than 1; adding an unaligned row and a
  single-candidate row leaves the weights unchanged.
- **Repeatable**: the same seed and settings give identical weights after every epoch; a prune threshold of 1,000
  leaves every weight zero while a threshold of 0 leaves some non-zero.
- **Stable row order**: with the keyed order, the order of every epoch without the rows inserted at the top and the end
  of the training file equals the order before the insertion, while the positional order changes; the same seed gives
  the same order and weights twice, every epoch visits each row once and the natural row three times, the epochs
  differ, and another seed gives another order.
- **Settings validation**: a hash size of 40 bits, a natural-row weight of 0, an undefined row order and a null corpus
  are rejected before any training, as are settings whose worst case could overflow 64 bits (naming the settings),
  while the largest validated settings train without a wrapped weight.

#### Test Scenarios

##### Learning

**Tests**: `AveragedPerceptronTrainer_Train_SeparableFixture_DecidesEveryTrainingRow`,
`AveragedPerceptronTrainer_Train_SingleMistakenRow_GivesExactAveragedWeights`,
`AveragedPerceptronTrainer_Train_NaturalWeight_ChangesTheWeights`,
`AveragedPerceptronTrainer_Train_UnalignedAndSingleCandidateRows_AreIgnored`

The whole fixture is learned and judged with the runtime's scoring. One row labeled with the second candidate gives the
exact averaged weights. Two natural rows, one contradicting the synthetic patterns, give different weights under a
natural-row weight of 1 and of 3. The weights trained on the fixture equal those trained with an unaligned row and a
single-candidate row added.

##### Repeatability and pruning

**Tests**: `AveragedPerceptronTrainer_Train_SameSeedTwice_GivesIdenticalWeights`,
`AveragedPerceptronTrainer_Train_PruneThreshold_DropsSmallWeights`

Two trainings with seed 11 give equal weights after each of three epochs. With no pruning some weight is non-zero; with
a threshold above every weight all are zero.

##### Stable row order

**Tests**: `AveragedPerceptronTrainer_Train_KeyedOrder_InsertedRowsKeepRelativeOrderOfExistingRows`,
`AveragedPerceptronTrainer_Train_KeyedOrderSameSeed_IsDeterministic`

The visiting order of three epochs (seed 5, natural weight 2, one natural row) is taken before and after two rows are
inserted after the header of the training file and two natural rows are appended. With the keyed order each epoch
holds the six new visits, and without them it equals the order before the insertion; with the positional order the
first epoch's order of the existing rows changes. With seed 11 and natural weight 3 the keyed order and the trained
weights are the same twice, each epoch is a permutation of the visits (the natural row with repeats 0, 1 and 2), the
first two epochs differ, and seed 12 gives another first epoch.

##### Settings validation

**Tests**: `AveragedPerceptronTrainer_Constructor_InvalidSettings_ThrowsInvalidDataException`,
`AveragedPerceptronTrainer_Constructor_SettingsBeyondTheOverflowBound_ThrowsNamingTheSettings`,
`AveragedPerceptronTrainer_Train_ValidatedMaximumSettings_TrainsWithoutOverflow`

Out-of-range settings (including an undefined row order) and a null corpus are rejected by the constructor. With 200
natural rows, scale 1,000,000, natural weight 100 and 100 epochs the constructor throws a message naming the overflow
and the three settings, and safe settings on the same corpus are accepted. The largest validated settings train 100
epochs, and wherever a scale-1 run has a non-zero weight the scaled weight has the same sign.
