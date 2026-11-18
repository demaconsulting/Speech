### ModelSelection Verification

#### Verification Approach

`ModelSelection` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/ModelSelectionTests.cs`. The tests train on the
small trainable corpus of the training fixture (18 hash bits, six epochs) with the real trainer and read the epoch
reports, the result and the two model images the unit returns. The temperature test recomputes the loss on the
development rows independently of the unit, from the loaded model's scores, the runtime's candidate score and the
framework's own `Math.Exp` and `Math.Log`, and requires that no other value of the grid, near or far from the chosen
one, gives a smaller loss. The image test loads the compressed image with the runtime's loader and checks its header.
Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder; edits that remove development rows are
  made to that copy

#### Acceptance Criteria

The kept epoch of the fixture is the first epoch with the greatest number of natural development rows decided
correctly, and that number is recorded together with the 8 natural and 8 total development rows; the chosen temperature
is a value of the grid (a step of 1 to 1,000 divided by 20) and is the temperature stored in the compressed image; no
other grid value gives a smaller development loss; the compressed and the uncompressed image hold the same weights and
the loaded model carries the current inventory version, the feature-set version, the trainer version, the hash of the
training data and the hash size, while the result holds the SHA-256 hash and the length of the uncompressed image; the
same corpus and settings give equal uncompressed images and equal results; with no natural development rows the choice
is made on all development rows (two rows, the best total of all epochs); with no development rows the first epoch is
kept at temperature 1; the inputs count 37 training rows, 34 used, 1 skipped as not aligned and 2 skipped as
single-candidate, 8 development rows all used, and carry the corpus hashes, the tagger version and the inventory
version; zero epochs throws `InvalidDataException` and a null corpus throws `ArgumentNullException`.

#### Test Scenarios

##### Epoch choice

**Tests**: `ModelSelection_Train_Fixture_KeepsTheEarliestEpochWithTheMostNaturalDevRowsCorrect`,
`ModelSelection_Train_NoNaturalDevRows_SelectsOnAllDevRows`,
`ModelSelection_Train_NoDevRows_KeepsFirstEpochAtTemperatureOne`

Six epochs are reported; the selected epoch is the first whose natural-correct count equals the maximum. With the
development file reduced to two synthetic rows, the best total over all epochs is the recorded one; with the file
reduced to its header, epoch 1 is kept.

##### Temperature choice

**Tests**: `ModelSelection_Train_Fixture_ChoosesAGridTemperatureAndStoresIt`,
`ModelSelection_Train_Fixture_TemperatureMinimizesTheLossOnTheSelectionRows`,
`ModelSelection_Train_NoDevRows_KeepsFirstEpochAtTemperatureOne`

The temperature step is within 1 to 1,000, the temperature is the step divided by 20 and the loaded image has that
temperature. The independently computed loss at the chosen step is no larger than at steps 1, 5, 20, 100 and 1,000 and
the two neighbors of the chosen step. Without development rows the temperature is 1.

##### Model images

**Test**: `ModelSelection_Train_Fixture_ImagesAreEquivalentAndCarryProvenance`

The uncompressed image equals the decompression of the compressed one, the loaded model reports the versions, the data
hash and the hash size, and the result's image hash and length are those of the uncompressed image.

##### Determinism

**Test**: `ModelSelection_Train_SameInputsTwice_GivesIdenticalImages`

Two trainings of the same corpus and settings give equal uncompressed images and equal results.

##### Input counts

**Test**: `ModelSelection_BuildInputs_Fixture_CountsUsedAndSkippedRows`

An unaligned row is appended to the training file; the counts are as listed in the acceptance criteria.

##### Settings validation

**Test**: `ModelSelection_Train_InvalidSettings_ThrowsInvalidDataException`

A maximum of zero epochs throws `InvalidDataException`; a null corpus throws `ArgumentNullException`.
