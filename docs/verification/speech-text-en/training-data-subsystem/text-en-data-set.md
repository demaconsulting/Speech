### TextEnDataSet Verification

#### Verification Approach

`TextEnDataSet` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/TextEnDataSetTests.cs`. The tests load the small
valid folder written by the dataset fixture with the real `TsvTable` and a real `CheckReport`, and damage one file at a
time (delete it, give a data file the wrong header, make an offset not an integer, break the format of another table) to
see the finding.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder

#### Acceptance Criteria

A valid folder loads every table with no finding. A missing file, a wrong header, a non-integer offset and a format
defect in another table are findings and do not throw, and the other tables still load (a wrong header loads no row of
that file). The typed rows carry the file, split, line and offsets they came from, a sentence that starts with a
quotation mark keeps its offsets, and the forms of a reading are listed (empty for an unknown reading). The vocabulary
of sources gives each known source its allowed licenses and its generated flag, and gives an unknown source as unknown.

#### Test Scenarios

##### Loading

**Tests**: `TextEnDataSet_Load_ValidFixture_LoadsAllTablesWithoutFindings`,
`TextEnDataSet_Load_MissingFile_ReportsFindingAndContinues`,
`TextEnDataSet_Load_NonIntegerOffsets_ReportsFindingAndKeepsRow`,
`TextEnDataSet_Load_WrongDataHeader_ReportsFindingAndLoadsNoRowsOfThatFile`,
`TextEnDataSet_Load_OtherTsvFileWithBadFormat_ReportsFinding`

##### Typed rows

**Tests**: `TextEnDataSet_Load_Rows_CarryFileSplitLineAndOffsets`,
`TextEnDataSet_Load_SentenceStartingWithQuote_KeepsSentenceAndOffsets`,
`TextEnDataSet_FormsOf_KnownAndUnknownReading_ReturnListedForms`

##### Source vocabulary

**Tests**: `SourceInfo_TryGet_KnownSource_ReturnsKindAndLicenses`, `SourceInfo_TryGet_UnknownSource_ReturnsFalse`,
`SourceInfo_KindOf_Source_ReturnsKindOrNull`

The vocabulary is an internal part of the unit (the `SourceInfo` record). The known sources are checked as a group:
each generated source allows only `CC0-1.0`, the American National Corpus source only `CC-BY-3.0-US`, the open-train and
silver sources `CC0-1.0` or `Public-Domain`, another harvested source only `Public-Domain`, and each with the `-llm`
suffix gives the same result.
