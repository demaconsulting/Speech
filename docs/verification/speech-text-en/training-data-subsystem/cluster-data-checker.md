### ClusterDataChecker Verification

#### Verification Approach

`ClusterDataChecker` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/ClusterDataCheckerTests.cs`. Each test builds
the small valid data folder of the dataset fixture and, where needed, a small consistent set of word-cluster files: a
source list with one used book and one development book excluded from the corpus, one removal, a table of three words in
three clusters, and a record written by the real `ClusteringRecord` writer with the hashes of those files and the
current hash of the evaluation files (so a drift between the writer's and the check's field names fails the tests, and
the check reports no evaluation drift until a test edits an evaluation file). A test then breaks exactly one thing,
rewrites the record where only the rule under test should fail, loads the folder with the real `TextEnDataSet` and runs
the checker with a real `CheckReport`. Nothing is mocked. The check of the committed files and the check-data
integration are verified by subsystem tests (see *TrainingDataSubsystem Verification* and *ClusteringSubsystem
Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder

#### Acceptance Criteria

**Presence** (`Presence`). Consistent files give no finding and report the book counts and the table figures; a folder
without word-cluster files gives no finding and says the check was skipped; a folder with the table missing gives one
`clusters` finding located at the table.

**Sources** (`Sources`). A used row without a pin, without the public-domain statement, with a URL on the main web site,
over plain HTTP or of another e-book, with a year after 1929, an unknown status or group, an identifier out of order or
a blank title each give a `clusters` finding; a pin on an excluded book gives a finding.

**Leakage** (`Leakage`). When the source document of a development or natural-holdout row is the used book, exactly one
`leakage` finding is reported, located at the book's row.

**Removals** (`Removals`). A removal of a book that is not used, an unknown rule, a file that is not an evaluation file,
a paragraph or sentence that is not an integer, and rows out of order each give a finding.

**Table** (`Table`). A prefix path, a path that is not a bit string, a count below the record's minimum, a count with a
leading zero, words out of order, a word with a character outside the eligible set and more distinct paths than the
record's clusters each give a finding.

**Record** (`Record`). An edit of the source list, the removals or the table after the record was written gives exactly
one finding at the record, naming the stale hash field and the file; a record of another tokenizer gives one finding
naming both versions, and no tokenizer version gives no finding and an information line; book, word and path counts that
differ from the files are findings; an unparsable record, a record of schema 2 and a record without its objects are
findings.

**Evaluation drift** (`EvaluationDrift`). With the evaluation files as recorded, no finding and no drift line; after an
edit of a development row, still no finding, and exactly one information line that names `evaluationListingSha256`, the
current hash, and says it is not a failure.

#### Test Scenarios

##### Presence

**Tests**: `ClusterDataChecker_Run_ConsistentFiles_NoFindings`, `ClusterDataChecker_Run_NoFiles_IsSkipped`,
`ClusterDataChecker_Run_OneFileMissing_IsFinding`

##### Sources

**Tests**: `ClusterDataChecker_Run_InvalidSourceRow_IsFinding`, `ClusterDataChecker_Run_PinOnExcludedBook_IsFinding`

The theory changes one column of the used row and rewrites the record so the hashes stay current.

##### Leakage

**Test**: `ClusterDataChecker_Run_UsedDevelopmentOrHoldoutBook_IsLeakage`

The theory points the provenance of the development row and of the natural-holdout row at the used book.

##### Removals

**Tests**: `ClusterDataChecker_Run_InvalidRemoval_IsFinding`, `ClusterDataChecker_Run_UnsortedRemovals_IsFinding`

##### Table

**Test**: `ClusterDataChecker_Run_InvalidTable_IsFinding`

##### Record

**Tests**: `ClusterDataChecker_Run_FileChangedAfterInduction_RecordIsStale`,
`ClusterDataChecker_Run_TokenizerVersion_IsCompared`, `ClusterDataChecker_Run_RecordCountsDiffer_IsFinding`,
`ClusterDataChecker_Run_UnreadableRecord_IsFinding`

##### Evaluation drift

**Test**: `ClusterDataChecker_Run_EvaluationFilesChanged_InformsWithoutFinding`

The check runs once on the consistent files, then again after the sentence of the development row `syn-00003` changes;
the second run adds the drift information line and no finding.
