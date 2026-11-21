### CorpusDataChecker Verification

#### Verification Approach

`CorpusDataChecker` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/CorpusDataCheckerTests.cs`. Each test builds
the small valid data folder of the dataset fixture and, where needed, a small consistent pair of corpus files: a source
list with one used book and one development book excluded from the corpus, and one removal. A test then breaks exactly
one thing, loads the folder with the real `TextEnDataSet` and runs the checker with a real `CheckReport`. Nothing is
mocked. The check-data integration is verified by subsystem tests (see *TrainingDataSubsystem Verification* and
*CorpusSubsystem Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder

#### Acceptance Criteria

**Presence** (`Presence`). Consistent files give no finding and report the book counts; a folder without corpus files
gives no finding and says the check was skipped; a folder with the removals missing gives one `corpus` finding located
at the removals.

**Sources** (`Sources`). A used row without a pin, without the public-domain statement, with a URL on the main web site,
over plain HTTP or of another e-book, with a year after 1929, an unknown status or group, an identifier out of order or
a blank title each give a `corpus` finding; a pin on an excluded book gives a finding.

**Leakage** (`Leakage`). When the source document of a development or natural-holdout row is the used book, exactly one
`leakage` finding is reported, located at the book's row.

**Removals** (`Removals`). A removal of a book that is not used, an unknown rule, a file that is not an evaluation file,
a paragraph or sentence that is not an integer, and rows out of order each give a finding.

#### Test Scenarios

##### Presence

**Tests**: `CorpusDataChecker_Run_ConsistentFiles_NoFindings`, `CorpusDataChecker_Run_NoFiles_IsSkipped`,
`CorpusDataChecker_Run_OneFileMissing_IsFinding`

##### Sources

**Tests**: `CorpusDataChecker_Run_InvalidSourceRow_IsFinding`, `CorpusDataChecker_Run_PinOnExcludedBook_IsFinding`

The theory changes one column of the used row.

##### Leakage

**Test**: `CorpusDataChecker_Run_UsedDevelopmentOrHoldoutBook_IsLeakage`

The theory points the provenance of the development row and of the natural-holdout row at the used book.

##### Removals

**Tests**: `CorpusDataChecker_Run_InvalidRemoval_IsFinding`, `CorpusDataChecker_Run_UnsortedRemovals_IsFinding`
