### ModelSelection Verification

#### Verification Approach

`ModelSelection` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/ModelSelectionTests.cs`. The tests train on the
small trainable corpus of the training fixture (18 hash bits, six epochs) with the real trainer and read the epoch
reports, the result and the two model images the unit returns. The temperature test recomputes the loss on the
development rows independently of the unit, from the loaded model's scores, the runtime's candidate score and the
framework's own `Math.Exp` and `Math.Log`, and requires that no other value of the grid, near or far from the chosen
one, gives a smaller loss. Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder; edits that remove development rows are
  made to that copy

#### Acceptance Criteria

- **Selection**: the kept epoch of the fixture is the first epoch with the greatest number of natural development rows
  decided correctly, recorded together with the 8 natural and 8 total development rows; with no natural development rows
  the choice is made on all development rows; with no development rows the first epoch is kept at temperature 1; the
  temperature stored in the compressed image is the chosen one, and no other grid value (a step of 1 to 1,000 divided by
  20) gives a smaller development loss.
- **Images**: the same corpus and settings give equal uncompressed images and equal results; the compressed and the
  uncompressed image hold the same weights, the loaded model carries the trainer version and the result holds the
  SHA-256 hash of the uncompressed image. The counts of the rows used and skipped are verified through the training
  record by the subsystem test `TrainingSubsystem_Train_UnalignedRow_SkippedAndCountedInRecord`.
- **Settings validation**: zero epochs throws `InvalidDataException` and a null corpus throws `ArgumentNullException`.

#### Test Scenarios

##### Selection

**Tests**: `ModelSelection_Train_Fixture_KeepsTheEarliestEpochWithTheMostNaturalDevRowsCorrect`,
`ModelSelection_Train_NoNaturalDevRows_SelectsOnAllDevRows`,
`ModelSelection_Train_NoDevRows_KeepsFirstEpochAtTemperatureOne`,
`ModelSelection_Train_Fixture_TemperatureMinimizesTheLossOnTheSelectionRows`

Six epochs are reported; the selected epoch is the first whose natural-correct count equals the maximum. With the
development file reduced to two synthetic rows, the best total over all epochs is the recorded one; with the file
reduced to its header, epoch 1 is kept at temperature 1. The independently computed loss at the chosen step is no larger
than at steps 1, 5, 20, 100 and 1,000 and the two neighbors of the chosen step, and the loaded image has the chosen
temperature.

##### Images

**Test**: `ModelSelection_Train_SameInputsTwice_GivesIdenticalImages`

Two trainings of the same corpus and settings give equal uncompressed images and equal results. The uncompressed image
equals the decompression of the compressed one, the loaded model reports the trainer version and the weight count, and
the result's image hash is that of the uncompressed image.

##### Settings validation

**Test**: `ModelSelection_Train_InvalidSettings_ThrowsInvalidDataException`

A maximum of zero epochs throws `InvalidDataException`; a null corpus throws `ArgumentNullException`.
