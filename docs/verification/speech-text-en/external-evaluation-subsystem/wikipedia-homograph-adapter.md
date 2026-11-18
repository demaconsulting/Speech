### WikipediaHomographAdapter Verification

#### Verification Approach

`WikipediaHomographAdapter` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/WikipediaHomographAdapterTests.cs`. The
tests write hand-written fake files in the format of the dataset (a header of five quoted fields, tab-separated rows,
quoted sentences with doubled quotes, byte offsets) about the words of the training fixture, pin them with the external
source fixture, write them into a verified cache and read a split through the real cache unit. The expected character
spans are stated in the tests. No real dataset file is used, and nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test has its own temporary folders, deleted on disposal

#### Acceptance Criteria

**Rows.** A sentence with doubled quotes is read back exactly and its span is the target; sentences with an accented or
astral character before the target are converted so the span is the target, and the sentences with non-ASCII characters
and the rows whose offsets changed are counted without skipping a row; a row whose span is another word than its
homograph column, or whose byte offsets cut a character, is skipped as bad offsets; a file with CRLF line ends is read
like one with LF; and each split reads only the pinned tables of its own folder in ordinal path order, with the stratum
of the split, and ignores a pinned file that is not a table.

**Strict format.** A file with a header that is not the five fields, a row without five fields, a non-integer offset, a
quoted field that is not closed or a closing quote followed by another character is refused with a message that names
the file and the problem.

#### Test Scenarios

##### Rows

**Tests**: `WikipediaHomographAdapter_ReadSplit_QuotedSentence_KeepsDoubledQuotesAndOffsets`,
`WikipediaHomographAdapter_ReadSplit_NonAsciiSentence_ConvertsByteOffsets`,
`WikipediaHomographAdapter_ReadSplit_HomographMismatch_SkipsAsBadOffsets`,
`WikipediaHomographAdapter_ReadSplit_CrLfFile_IsRead`,
`WikipediaHomographAdapter_ReadSplit_TrainAndEval_ReadOnlyTheirOwnPinnedFilesInOrder`

A sentence with quotation marks round trips and its span is `lead`. Two sentences, one with an accented letter and an
astral character before the target and one with an accented letter after it, give spans that are the target; two rows
count as non-ASCII and one as converted. A span that is another word and a span that cuts an accented letter are skipped
as bad offsets and the rows read equal the usable records plus the two skips. The CRLF file gives the same records as an
LF file. The evaluation split lists its files in ordinal order with the evaluation stratum, the training split lists
only its own files with the training stratum, a pinned file that is not a table is never read, and the two splits share
no sentence.

##### Strict format

**Tests**: `WikipediaHomographAdapter_ReadSplit_MalformedFile_Throws` (a theory over ten defects, including an empty
file, a file of blank lines and headers with four and six fields) and
`WikipediaHomographAdapter_ReadSplit_HeaderOnlyFile_IsReadWithoutRows`

Each defect throws `InvalidDataException` whose message contains the pinned path and the expected text.
