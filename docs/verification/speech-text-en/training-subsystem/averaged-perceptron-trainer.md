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

After one row that the initial weights get wrong, the non-zero weights are multiples of 64, include both +64 and -64 and
sum to zero, and the weights then decide that row for its label with the labeled candidate scoring higher; after eight
epochs on the separable fixture every aligned ambiguous training row is decided for its label; four epochs are reported
once each, numbered 1 to 4 in order; the same seed and settings give identical weights after every epoch; a prune
threshold of 1,000 leaves every weight zero while a threshold of 0 leaves some non-zero; a natural-row weight of 3 gives
other weights than 1 when the training data has natural rows; appending a row that is not aligned and a row with a
single-candidate word leaves the weights unchanged; a hash size of 40 bits and a natural-row weight of 0 throw
`InvalidDataException` and a null corpus throws `ArgumentNullException`.

#### Test Scenarios

##### Learning the labels

**Tests**: `AveragedPerceptronTrainer_Train_SingleMistakenRow_WeightsThenDecideTheLabel`,
`AveragedPerceptronTrainer_Train_SeparableFixture_DecidesEveryTrainingRow`

The corpus is one row labeled with the second candidate, which loses the initial tie, and then the whole fixture. The
weights are applied with the runtime's scoring; the decided candidate is the labeled one.

##### Exact weights

**Test**: `AveragedPerceptronTrainer_Train_SingleMistakenRow_GivesExactAveragedWeights`

The only non-zero weights are plus and minus 64, the weights sum to zero, and both signs occur.

##### Epoch reports and repeatability

**Tests**: `AveragedPerceptronTrainer_Train_ReportsEachEpochInOrder`,
`AveragedPerceptronTrainer_Train_SameSeedTwice_GivesIdenticalWeights`

The report callback receives epochs 1, 2, 3 and 4, once each. Two trainings with seed 11 give equal weights after each
of three epochs.

##### Pruning

**Test**: `AveragedPerceptronTrainer_Train_PruneThreshold_DropsSmallWeights`

With no pruning some weight is non-zero; with a threshold above every weight all are zero.

##### Natural-row weight

**Test**: `AveragedPerceptronTrainer_Train_NaturalWeight_ChangesTheWeights`

Two natural rows are appended, one contradicting the synthetic patterns. Weights learned with a natural-row weight of 1
and of 3 differ.

##### Ignored rows

**Test**: `AveragedPerceptronTrainer_Train_UnalignedAndSingleCandidateRows_AreIgnored`

The weights trained on the fixture equal those trained on the fixture with an unaligned row and a single-candidate row
added.

##### Settings validation

**Test**: `AveragedPerceptronTrainer_Constructor_InvalidSettings_ThrowsInvalidDataException`

Out-of-range settings and a null corpus are rejected by the constructor, before any training.
