### HeteronymBenchAdapter Verification

#### Verification Approach

`HeteronymBenchAdapter` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/HeteronymBenchAdapterTests.cs`. The tests
write hand-written fake read sets in the format of the benchmark (a document with a language, and items with text, word,
span, answer and source) about the words of the training fixture, pin them with the external source fixture, write them
into a verified cache and read them through the real cache unit. The expected spans, labels and strata are stated in the
tests. No real benchmark file is used, and nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test has its own temporary folders, deleted on disposal

#### Acceptance Criteria

**Items.** Items are read with the stratum of their source and the label made of the word, a colon and the answer, and a
span that is the word; items whose source is the other dataset are skipped as duplicates and counted, before their
offsets are looked at; an item whose span counts bytes is repaired and counted as repaired; an item whose span is the
word neither as characters nor as bytes is skipped as bad offsets.

**Strict format.** A document that is not valid, not English, without an items object, or with an item that lacks a
usable field is refused with a message that names the file and, for an item, the item.

#### Test Scenarios

##### Items

**Tests**: `HeteronymBenchAdapter_Read_Items_ParsedWithStratumAndLabel`,
`HeteronymBenchAdapter_Read_WhdItems_SkippedAsDuplicates`,
`HeteronymBenchAdapter_Read_ByteOffsetItem_IsRepairedAndCounted`

The items of two own sources give the strata of those sources, the labels of word and answer, and spans that are the
word. Items of the other dataset are skipped as duplicates and counted, including one whose span would otherwise be
rejected. An item with an accented letter before the target whose span counts bytes is evaluated at the target and
counted as repaired, while two items whose span is not the word either way are skipped as bad offsets.

##### Strict format

**Tests**: `HeteronymBenchAdapter_Read_MalformedDocument_Throws` (a theory over eight defects),
`HeteronymBenchAdapter_Read_InvalidUtf8_Throws` and `HeteronymBenchAdapter_Read_RepeatedItemId_Throws`

Each document, such as one that is not valid JSON, is not a document of English items, has no items object or has an
item without a usable text, span, answer or source, throws `InvalidDataException` with a message that contains the
expected text.
