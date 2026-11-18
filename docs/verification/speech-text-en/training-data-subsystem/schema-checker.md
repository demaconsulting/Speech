### SchemaChecker Verification

#### Verification Approach

`SchemaChecker` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/SchemaCheckerTests.cs`. Each test builds the
small valid data set of the dataset fixture, loads it with the real `TextEnDataSet` and runs the checker with a real
`CheckReport`, so the real `SpanValidator` is part of the test. The negative tests of one rule family put several
independent defects into one copy (each on its own row or cell) and assert that every defect has its own finding, so a
missing finding names the defect it belongs to. The tests of the row counts (`ReadingCounts`) are in the same file.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder

#### Acceptance Criteria

**Inventory** (`Inventory`). Each of these gives a finding: a repeated, malformed or lemma-mismatched reading
identifier; an unknown part-of-speech hint or status; an empty or repeated description; a phonetic character in a
description; a repeated word identifier or a mapping to an unknown reading; an unmapped reading (but not a reading that
an extension decision added); a form of an unknown reading; a reading without a form; an invalid decision, an addition
of an unknown reading and a deferral with readings.

**Rows** (`Rows`). The valid fixture gives no finding, with the informational summary lines. Each of these gives a
finding: a repeated row identifier across files; a split column that differs from its file; a row with an unknown
reading, a lemma that differs from its reading's, an unknown source or non-integer offsets; a span that is not a whole
word; a harvested or generated row without a provenance record (decided by its source, so a synthetic row renamed
`hind-x` is found), a record without a row and a repeated record; a source, identifier and file that disagree (the
independent holdout source outside its file, another source inside it, an identifier family that does not match the
source). Every identifier family of the committed data with its matching source gives no finding. The detailed
span rules are the unit tests of `SpanValidator`.

**Reading counts** (`ReadingCounts`). The counts are computed per reading, the needed rows are never negative, and a
stale, missing, unknown or repeated gap row is a finding.

#### Test Scenarios

##### Inventory

**Tests**: `SchemaChecker_Run_ReadingInventoryDefects_ReportsEachFinding`,
`SchemaChecker_Run_ReferentialDefects_ReportsEachFinding`,
`SchemaChecker_Run_ExtensionAddedReadingWithoutMapping_ReportsNothing`,
`SchemaChecker_Run_ExtensionDecisionDefects_ReportsEachFinding`

##### Rows

**Tests**: `SchemaChecker_Run_ValidFixture_ReportsNoFindings`, `SchemaChecker_Run_RowFieldDefects_ReportsEachFinding`,
`SchemaChecker_Run_ProvenanceDefects_ReportsEachFinding`,
`SchemaChecker_Run_SyntheticRowRenamedToHoldoutId_ReportsIdAndMissingProvenance`,
`SchemaChecker_Run_HoldoutIndependentRowWithSyntheticSource_ReportsFindings`,
`SchemaChecker_Run_IdentifierAndSourceMismatches_ReportsEachFinding`,
`SchemaChecker_Run_AllIdentifierFamiliesWithMatchingSources_ReportsNoFindings` (the positive control)

##### Reading counts

**Tests**: `ReadingCounts_Build_ValidFixture_CountsRowsPerReading`, `ReadingCount_Needs_AreClampedAtZero`,
`SchemaChecker_Run_GapTableDefects_ReportsEachFinding`
