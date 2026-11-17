### TsvTable Verification

#### Verification Approach

`TsvTable` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/TsvTableTests.cs`. Each test writes a small file
of exactly the bytes it needs into a temporary folder (so the byte order mark, carriage return and invalid UTF-8 cases
are real bytes) and loads it with a real `CheckReport` that records the findings.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test uses its own temporary file

#### Acceptance Criteria

A valid file loads its header and rows with no finding. A missing file, an empty file, invalid UTF-8, a byte order mark,
a carriage return, a missing final newline, an empty line, a line with a wrong number of columns and a line that is not
in normalization form C each give a finding and no exception; a byte order mark is reported without damaging the header,
and a line with the wrong number of columns is not returned as a row. A sentence that starts with a quotation mark keeps
it and its column. A wrong header is a finding and returns no rows; comment lines are skipped only when allowed and are
otherwise ordinary lines. Findings carry the file name and the one-based line, and rows carry their line.

#### Test Scenarios

##### Strict format

**Tests**: `TsvTable_Load_ValidFile_ReturnsHeaderAndRowsWithoutFindings`,
`TsvTable_Load_MissingFile_ReportsFindingAndReturnsEmptyTable`, `TsvTable_Load_EmptyFile_ReportsFinding`,
`TsvTable_Load_InvalidUtf8_ReportsFinding`, `TsvTable_Load_ByteOrderMark_ReportsFindingOnly`,
`TsvTable_Load_CarriageReturn_ReportsFindingWithLine`, `TsvTable_Load_MissingFinalNewline_ReportsFinding`,
`TsvTable_Load_EmptyLine_ReportsFindingWithLine`, `TsvTable_Load_WrongColumnCount_ReportsFindingAndSkipsRow`,
`TsvTable_Load_NonNfcLine_ReportsFinding`

##### No quoting

**Test**: `TsvTable_Load_SentenceStartingWithQuote_KeepsQuoteAndColumns`

A row whose sentence begins with a quotation mark is loaded; the cell is returned unchanged and the row still has the
header's number of columns.

##### Header and comments

**Tests**: `TsvTable_Load_WrongHeader_ReportsFindingAndReturnsNoRows`, `TsvTable_Load_CommentLinesAllowed_AreSkipped`,
`TsvTable_Load_CommentLinesNotAllowed_AreTreatedAsRows`

##### Finding locations

**Tests**: `TsvTable_Load_FormatDefect_FindingCarriesFileAndLine`,
`TsvTable_Load_ValidFile_ReturnsHeaderAndRowsWithoutFindings`
