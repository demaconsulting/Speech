### TextEnDataSet Verification

#### Verification Approach

`TextEnDataSet` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/TextEnDataSetTests.cs`. The tests load the small
valid folder written by the dataset fixture with the real `TsvTable` and a real `CheckReport`, and damage one file at a
time (delete it, give a data file the wrong header, make an offset not an integer, break the format of another table) to
see the finding.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder

#### Acceptance Criteria

**Loading** (`Loading`). A valid folder loads every table with no finding. A missing file, a wrong header, a non-integer
offset and a format defect in another table are findings and do not throw, and the other tables still load (a wrong
header loads no row of that file). The typed rows carry the file, split, line and offsets they came from, and the forms
of a reading are listed (empty for an unknown reading).

**Overlay loading** (`OverlayLoading`). The valid fixture loads two regression rows and a header-only generalization set
with no finding; the overlay rows carry their file, line, offsets and overlay columns; a missing overlay file and a
non-integer overlay offset are findings, and the row with the bad offset is kept with a null offset.

**Source vocabulary** (`SourceVocabulary`). Each known source family gives its allowed licenses, its generated flag and
its kind; an unknown source is unknown and has no kind.

#### Test Scenarios

##### Loading

**Tests**: `TextEnDataSet_Load_ValidFixture_LoadsAllTablesWithoutFindings`,
`TextEnDataSet_Load_Rows_CarryFileSplitLineAndOffsets`,
`TextEnDataSet_FormsOf_KnownAndUnknownReading_ReturnListedForms`,
`TextEnDataSet_Load_MissingFile_ReportsFindingAndContinues`,
`TextEnDataSet_Load_NonIntegerOffsets_ReportsFindingAndKeepsRow`,
`TextEnDataSet_Load_WrongDataHeader_ReportsFindingAndLoadsNoRowsOfThatFile`,
`TextEnDataSet_Load_OtherTsvFileWithBadFormat_ReportsFinding`

##### Source vocabulary

**Tests**: `SourceInfo_TryGet_KnownSource_ReturnsKindAndLicenses`, `SourceInfo_TryGet_UnknownSource_ReturnsFalse`,
`SourceInfo_KindOf_Source_ReturnsKindOrNull`

The vocabulary is an internal part of the unit (the `SourceInfo` record). One generated source (only `CC0-1.0`), the
American National Corpus source (only `CC-BY-3.0-US`) and an `-llm` source of a corpus that allows `CC0-1.0` or
`Public-Domain` are checked for their licenses, and one source of each kind for its kind.

##### Overlay loading

**Tests**: `TextEnDataSet_Load_ValidFixture_LoadsAllTablesWithoutFindings`,
`TextEnDataSet_Load_OverlayRows_CarryFileLineOffsetsAndColumns`,
`TextEnDataSet_Load_MissingOverlayOrBadOffset_IsFinding`
