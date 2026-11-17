### LeakageChecker Verification

#### Verification Approach

`LeakageChecker` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/LeakageCheckerTests.cs`. The internal tests use
the small valid data set of the dataset fixture with one sentence, document or template moved into a second file. The
external tests write evaluation sets (text files and tab-separated files) into a temporary folder outside the data
folder, with sentences that repeat, nearly repeat or only partly overlap a row. The real evaluation corpora are never
used in a test; the one-time comparison of the real corpora with the committed leakage report is an operator task
described in the data README. The helper functions of the sentence keys (`SentenceKeys`) are tested in the same file.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary folders

#### Acceptance Criteria

The valid fixture gives no finding. A sentence in two files is a finding, the same sentence twice in one file is not,
and the key of a sentence keeps only lowercase ASCII letters and digits; one document in two splits and one template in
two files are findings, while the same document twice in one split and the same template text for another reading are
not, and a renamed row stays in the check its source puts it in. With evaluation sets, an equal sentence is an exact
finding with its counts, a row of at least eight words that is
mostly made of evaluation five-grams is a near-duplicate finding, a row below the thresholds only counts a shared
five-gram, a short sentence that is fully contained is not a near duplicate, two sets give a row per set and split
sorted by name, and the written report has the documented header and columns. Tab-separated sets are read from the
sentence column or else the text column; a missing folder, an empty folder, a tab-separated set without such a column,
a file with no sentence (empty, blank lines only, header only; the file is named) even when other files are good, a
folder whose files are all empty, and sets with no sentence at all passed to the external check are errors; blank lines
are skipped while other sentences keep their text; and the inside-folder test compares full paths.

#### Test Scenarios

##### Split separation

**Tests**: `LeakageChecker_CheckInternal_ValidFixture_ReportsNothing`,
`LeakageChecker_CheckInternal_SentenceInTwoFiles_ReportsFinding`,
`LeakageChecker_CheckInternal_SentenceTwiceInOneFile_IsAllowed`,
`SentenceKeys_Key_MixedText_KeepsLowerCaseLettersAndDigits`

##### Document separation

**Tests**: `LeakageChecker_CheckInternal_DocumentInTwoSplits_ReportsFinding`,
`LeakageChecker_CheckInternal_DocumentTwiceInOneSplit_IsAllowed`,
`LeakageChecker_CheckInternal_RenamedHarvestRowReusingDocument_ReportsFinding`

##### Template separation

**Tests**: `LeakageChecker_CheckInternal_TemplateInTwoFiles_ReportsFinding`,
`LeakageChecker_CheckInternal_TemplateOfAnotherReading_IsAllowed`,
`LeakageChecker_CheckInternal_RenamedSyntheticRowReusingTemplate_ReportsFinding`

##### External sets

**Tests**: `LeakageChecker_CheckExternal_ExactMatch_ReportsFindingAndCounts`,
`LeakageChecker_CheckExternal_NearDuplicate_ReportsFinding`,
`LeakageChecker_CheckExternal_BelowContainmentThreshold_OnlyCountsSharedGram`,
`LeakageChecker_CheckExternal_ShortSentenceFullyContained_IsNotNearDuplicate`,
`LeakageChecker_CheckExternal_TwoSets_ReturnsRowPerSetAndSplitSortedByName`,
`LeakageChecker_WriteReport_Rows_WritesDocumentedFormat`, `SentenceKeys_Words_MixedText_SplitsOnNonAlphanumerics`,
`SentenceKeys_Ngrams_ShortAndLongSentence_ReturnsDistinctGrams`

##### Loading the evaluation folder

**Tests**: `LeakageChecker_LoadEvaluationSets_TsvSets_ReadSentenceOrTextColumn`,
`LeakageChecker_LoadEvaluationSets_TsvWithoutSentenceColumn_ThrowsInvalidDataException`,
`LeakageChecker_LoadEvaluationSets_MissingDirectory_ThrowsDirectoryNotFoundException`,
`LeakageChecker_LoadEvaluationSets_EmptyDirectory_ThrowsInvalidDataException`,
`LeakageChecker_LoadEvaluationSets_FileWithoutSentences_ThrowsNamingTheFile`,
`LeakageChecker_LoadEvaluationSets_AllFilesEmpty_ThrowsInvalidDataException`,
`LeakageChecker_LoadEvaluationSets_OneEmptyFileAmongGoodOnes_ThrowsNamingTheEmptyFile`,
`LeakageChecker_LoadEvaluationSets_BlankLinesAmongSentences_AreSkippedAndTextIsKept`,
`LeakageChecker_CheckExternal_SetsWithoutAnySentence_ThrowsInvalidDataException`,
`LeakageChecker_IsInside_ComparesFullPaths`
