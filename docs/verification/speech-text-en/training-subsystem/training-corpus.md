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

The corpus of the fixture holds 36 training rows and 8 development rows in file order, every training row aligned and
two of them without ambiguity; a corpus loaded with both holdout files deleted, or filled with garbage, equals the one
loaded with them present (same rows and the same two hashes); the data hash changes when `train.tsv` changes and not
when the readme changes, and is 64 hexadecimal characters (32 bytes); the features hash changes when a sentence
changes the features of a row while its label stays; a target that is only the beginning of a longer token gives a row
that is not aligned, has no features and is counted; an aligned ambiguous row has a tag, conjoined and shared features
and two candidates, and a single-candidate row is marked unambiguous; the part-of-speech hints are read from the
readings table; a label that is not a candidate of its form, a target that is not a form, each of the four missing
files, a span outside its sentence and a wrong header throw `InvalidDataException`, with a message that names the row,
the file or the line.

#### Test Scenarios

##### Input files and order

**Tests**: `TrainingCorpus_Load_Fixture_PreparesRowsInFileOrder`,
`TrainingCorpus_Load_HoldoutFilesMissingOrGarbage_GivesTheSameCorpus`

The fixture corpus has the training and development rows in file order. After the two holdout files are deleted, and
again after they are filled with text that is not a data file, loading gives the same hashes and the same row counts,
which shows that the loader never opens them.

##### Runtime alignment

**Tests**: `TrainingCorpus_Load_AlignedRows_HaveFeaturesAndUposAndAmbiguity`,
`TrainingCorpus_Load_TargetInsideLongerToken_IsNotAligned`

A `lead` row has a tag, features and two candidates; a `wind` row (one candidate) is marked unambiguous. A row whose
sentence is `The leader spoke softly.` with the target `lead` has no runtime token that is exactly the target: it is
not aligned, its feature lists are empty and exactly one such row is counted.

##### Part-of-speech hints

**Test**: `TrainingCorpus_Load_Fixture_ReadsPartOfSpeechHints`

The hints of `lead.verb` are `VERB` and `AUX` and those of `lead.noun` are `NOUN`.

##### Data validation

**Tests**: `TrainingCorpus_Load_LabelNotACandidate_ThrowsNamingTheRow`,
`TrainingCorpus_Load_TargetNotAForm_ThrowsNamingTheRow`, `TrainingCorpus_Load_MissingInputFile_ThrowsNamingTheFile` (a
theory over the four input files), `RowPreparer_ReadRows_SpanOutsideSentence_ThrowsNamingFileAndLine`,
`RowPreparer_ReadRows_WrongHeader_ThrowsInvalidDataException`

A row relabeled with the reading of another word is rejected with a message that contains the row identifier and
`not a candidate`; a row whose target is a word the inventory does not list is rejected with the identifier and
`not a form`; deleting `train.tsv`, `dev.tsv`, `readings.tsv` or `forms.tsv` is rejected with a message that names the
file; a span past the end of its sentence is rejected with the file name and `span`; a file with the wrong header is
rejected.

##### Input hashes

**Tests**: `TrainingCorpus_DataSha256_ChangesWithTrainingInputOnly`,
`TrainingCorpus_FeaturesSha256_ChangesWithTheFeaturesOfARow`

Appending to the readme leaves the data hash unchanged; changing a training row changes it. Changing the words around
the target of a row, with the same label, changes the features hash.
