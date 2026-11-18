### ExternalRowConversion Verification

#### Verification Approach

`ExternalRowConversion` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/ExternalRowConversionTests.cs`. The
offset conversion is verified with theories of hand-written sentences and byte spans whose expected character spans are
stated in the test. The mapping loader and the converter run against the reading inventory of the training fixture,
mapping files written by the test and records written by the test, with a set of training sentence keys that the test
chooses. Nothing is mocked and no real dataset is used.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds its own temporary data folder and mapping files and deletes them on disposal

#### Acceptance Criteria

**Conversion.** ASCII spans are unchanged; the span after an accented letter and after an astral character is converted
to the matching character span; a span that is empty, reversed, beyond the end, negative or that begins or ends inside a
multi-byte character is refused. Each skip reason is counted, the adapter's own skips are added, and the rows read equal
the rows used plus the rows skipped; a sentence equal to a training sentence after normalization is kept and counted as
an overlap.

**Mapping.** A mapping with the right header maps its labels and leaves other labels unmapped; a missing file, a wrong
header, a repeated label, an unknown reading identifier, a malformed row and a file that is not UTF-8 are each refused
with a message that names the problem. Usable records become rows with the mapped reading identifier, the stratum as
source and the right span, clustered as the set was asked to cluster them.

#### Test Scenarios

##### Conversion

**Tests**: `ByteOffsets_TryToCharOffsets_ValidSpans_ConvertsToUtf16` (a theory over three cases),
`ByteOffsets_TryToCharOffsets_InvalidSpans_ReturnsFalse` (a theory over five cases),
`ExternalRowConverter_Convert_EachSkipReason_IsCountedAndReadEqualsUsedPlusSkipped`,
`ExternalRowConverter_Convert_SentenceEqualToTrainingSentence_IsKeptAndCounted`

The valid cases are the ASCII identity, an accented letter before the target and an astral character before the span;
the invalid cases are a reversed span, a negative start, a span past the end and spans that cut a multi-byte character.
Records that are not placed on their sentence, that have an unmapped label, that are not a form of the inventory and
whose mapped reading is not a candidate are each counted under their reason with the adapter's own skips added. A record
whose sentence is a training sentence with other case and punctuation is kept, and the overlap count is one.

##### Mapping

**Tests**: `ReadingIdMap_Load_ValidFile_MapsLabels`, `ReadingIdMap_Load_InvalidFile_Throws` (a theory over seven
cases), `ReadingIdMap_Load_InvalidUtf8_Throws`, `ExternalRowConverter_Convert_MappedRows_CarryReadingIdAndStratum`

The mapped rows carry the reading identifier and the stratum, keep the offsets and are clustered by sentence or one row
per cluster as the set was asked.
