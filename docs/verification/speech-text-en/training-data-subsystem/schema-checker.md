### SchemaChecker Verification

#### Verification Approach

`SchemaChecker` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/SchemaCheckerTests.cs`. Each test builds the
small valid data set of the dataset fixture, loads it with the real `TextEnDataSet`, changes one cell or one file, and
runs the checker with a real `CheckReport`, so the real `SpanValidator` is part of the test and each test shows one rule
failing for its own reason. The tests of the row counts (`ReadingCounts`) are in the same file.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder

#### Acceptance Criteria

The valid fixture gives no finding. Each of these gives a finding that names the file and line: a repeated, malformed or
lemma-mismatched reading identifier; an unknown part-of-speech hint or status; an empty or repeated description; a
phonetic character in a description; a repeated word identifier or a mapping to an unknown reading; an unmapped reading
(but not a reading that an extension decision added); a form of an unknown reading; a reading without a form; an invalid
decision, an addition of an unknown reading and a deferral with readings; a repeated row identifier across files; a
split column that differs from its file; a row with an unknown reading, a lemma that differs from its reading's, or an
unknown source; non-integer offsets; a span that is off by one, inside a longer word, splits a surrogate pair or lies
beyond the sentence; a harvested or generated row without a provenance record (decided by its source, so a synthetic row
renamed `hind-x` is found), a record without a row and a repeated record; a source, identifier and file that disagree
(the independent holdout source outside its file, another source inside it, and an identifier family that does not match
the source), while every identifier family of the committed data with its matching source gives no finding; and a stale,
missing, unknown or repeated gap row. The counts are computed per reading, the needed rows are
never negative, the count table has a header and one line per reading, and the summary lines are added as information.

#### Test Scenarios

##### Inventory

**Tests**: `SchemaChecker_Run_ReadingIdGrammar_ReportsFinding`,
`SchemaChecker_Run_ReadingLemmaNotIdPrefix_ReportsFinding`, `SchemaChecker_Run_DuplicateReadingId_ReportsFinding`,
`SchemaChecker_Run_BadPosTokenAndStatus_ReportsFindings`, `SchemaChecker_Run_DuplicateDescription_ReportsFinding`,
`SchemaChecker_Run_IpaSymbolInDescription_ReportsFinding`

##### Referential integrity

**Tests**: `SchemaChecker_Run_DuplicateWordId_ReportsFinding`,
`SchemaChecker_Run_MappingToUnknownReading_ReportsFinding`, `SchemaChecker_Run_UnmappedReading_ReportsFinding`,
`SchemaChecker_Run_ExtensionAddedReadingWithoutMapping_ReportsNothing`,
`SchemaChecker_Run_FormOfUnknownReading_ReportsFinding`, `SchemaChecker_Run_ReadingWithoutForm_ReportsFinding`,
`SchemaChecker_Run_InvalidDecision_ReportsFinding`, `SchemaChecker_Run_AddDecisionWithUnknownReading_ReportsFinding`,
`SchemaChecker_Run_DeferDecisionWithReadings_ReportsFinding`

##### Rows

**Tests**: `SchemaChecker_Run_ValidFixture_ReportsNoFindings`,
`SchemaChecker_Run_DuplicateIdAcrossFiles_ReportsFinding`, `SchemaChecker_Run_WrongSplitColumn_ReportsFinding`,
`SchemaChecker_Run_UnknownReading_ReportsFinding`, `SchemaChecker_Run_LemmaMismatch_ReportsFinding`,
`SchemaChecker_Run_UnknownSource_ReportsFinding`, `SchemaChecker_Run_NonIntegerOffsets_ReportsFinding`,
`SchemaChecker_Run_SpanOffByOne_ReportsFinding`, `SchemaChecker_Run_SpanInsideLongerWord_ReportsFinding`,
`SchemaChecker_Run_SpanSplitsSurrogatePair_ReportsFinding`, `SchemaChecker_Run_SpanBeyondSentence_ReportsFinding`

##### Provenance identifiers

**Tests**: `SchemaChecker_Run_HarvestRowWithoutProvenance_ReportsFinding`,
`SchemaChecker_Run_SyntheticRowWithoutProvenance_ReportsFinding`,
`SchemaChecker_Run_ProvenanceWithoutRow_ReportsFinding`, `SchemaChecker_Run_DuplicateProvenanceEntry_ReportsFinding`,
`SchemaChecker_Run_SyntheticRowRenamedToHoldoutId_ReportsIdAndMissingProvenance`,
`SchemaChecker_Run_HoldoutIndependentRowWithSyntheticSource_ReportsFindings`

##### Source consistency

**Tests**: `SchemaChecker_Run_SyntheticRowRenamedToHoldoutId_ReportsIdAndMissingProvenance`,
`SchemaChecker_Run_IndependentHoldoutSourceInTrain_ReportsFinding`,
`SchemaChecker_Run_HoldoutIndependentRowWithSyntheticSource_ReportsFindings`,
`SchemaChecker_Run_HarvestRowWithGeneratedId_ReportsFinding`,
`SchemaChecker_Run_SyntheticRowIdWithoutSynPrefix_ReportsFinding`,
`SchemaChecker_Run_IndependentHoldoutRowIdWithoutHindPrefix_ReportsFinding`,
`SchemaChecker_Run_AllIdentifierFamiliesWithMatchingSources_ReportsNoFindings` (the positive control)

##### Reading counts

**Tests**: `ReadingCounts_Build_ValidFixture_CountsRowsPerReading`, `ReadingCount_Needs_AreClampedAtZero`,
`ReadingCounts_WriteTable_ValidFixture_WritesHeaderAndOneLinePerReading`,
`SchemaChecker_Run_StaleGapsRow_ReportsFinding`, `SchemaChecker_Run_GapsRowMissing_ReportsFinding`,
`SchemaChecker_Run_GapsRowUnknownOrDuplicate_ReportsFindings`, `SchemaChecker_Run_ValidFixture_AddsSummaryInfo`
