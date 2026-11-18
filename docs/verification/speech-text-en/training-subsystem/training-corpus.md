### TrainingCorpus Verification

#### Verification Approach

`TrainingCorpus` (with `LabeledRow`, `PreparedRow` and `RowPreparer`) is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/TrainingCorpusTests.cs`. The tests load the small
trainable data folder of the training fixture, written in code into a temporary folder, with the real tokenizer, tagger
and feature extractor of the Text.En library, so the alignment of a row with a runtime token is the real alignment.
Negative tests change exactly one thing in a fresh copy of the folder (one row, one file) and assert the exception
type and that its message names the row, the file or the line. Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder

#### Acceptance Criteria

- **Input files**: the corpus of the fixture holds 36 training rows and 8 development rows in file order; a corpus
  loaded with both holdout files deleted, or filled with garbage, equals the one loaded with them present (same rows
  and the same hashes); the data hash changes when `train.tsv` changes, not when the readme changes, and is 64
  hexadecimal characters (32 bytes).
- **Runtime alignment**: every training row of the fixture is aligned and two of them have no ambiguity; a target that
  is only the beginning of a longer token gives a row that is not aligned, has no features and is counted.
- **Data validation**: a label that is not a candidate of its form, a target that is not a form, a missing training
  file or readings table, a span outside its sentence, a start or end that is negative, overflowing or an empty span,
  and a wrong header throw `InvalidDataException`, with a message that names the row, the file or the line.

#### Test Scenarios

##### Input files and hash

**Tests**: `TrainingCorpus_Load_Fixture_PreparesRowsInFileOrder`,
`TrainingCorpus_Load_HoldoutFilesMissingOrGarbage_GivesTheSameCorpus`,
`TrainingCorpus_DataSha256_ChangesWithTrainingInputOnly`

The fixture corpus has the training and development rows in file order. After the two holdout files are deleted, and
again after they are filled with text that is not a data file, loading gives the same hash and the same row count,
which shows that the loader never opens them. Appending to the readme leaves the data hash unchanged; changing a
training row changes it.

##### Runtime alignment

**Tests**: `TrainingCorpus_Load_Fixture_PreparesRowsInFileOrder`,
`TrainingCorpus_Load_TargetInsideLongerToken_IsNotAligned`

A row whose sentence is `The leader spoke softly.` with the target `lead` has no runtime token that is exactly the
target: it is not aligned, its feature lists are empty and exactly one such row is counted.

##### Data validation

**Tests**: `TrainingCorpus_Load_LabelNotACandidate_ThrowsNamingTheRow`,
`TrainingCorpus_Load_TargetNotAForm_ThrowsNamingTheRow`, `TrainingCorpus_Load_MissingInputFile_ThrowsNamingTheFile` (a
theory over a training file and a readings table), `RowPreparer_ReadRows_SpanOutsideSentence_ThrowsNamingFileAndLine`,
`RowPreparer_ReadRows_InvalidOffsets_ThrowsInvalidDataExceptionNamingFileAndLine` (a theory over a negative start, an
overflowing end and an empty span), `RowPreparer_ReadRows_WrongHeader_ThrowsInvalidDataException`

A row relabeled with the reading of another word is rejected with a message that contains the row identifier and
`not a candidate`; a row whose target is a word the inventory does not list is rejected with the identifier and
`not a form`; a missing file is rejected with a message that names it; a span past the end of its sentence and each
invalid offset are rejected with the file name, the line (`dev.tsv:2:`) and `span`; a file with the wrong header is
rejected.
