### ExternalEvaluationRecord Verification

#### Verification Approach

`ExternalEvaluationRecord` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/ExternalEvaluationRecordTests.cs`. The
tests render a hand-built evaluation (three sets with their conversion counts, strata and per-reading tallies, three
seeds, and pins of two fake sources) whose figures are written into the test, so the expected content is stated
independently of the unit. The fake sources carry words and labels that occur nowhere else, so the dataset-text test can
search the rendered files for them. Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test has its own temporary folder, deleted on disposal

#### Acceptance Criteria

**Record.** Two renderings of the same evaluation have the seven file names in ordinal order, the same content, line
feeds only, a final line feed and no byte order mark; the sources file holds the image hash, the settings and the pins
of each source, the conversion and summary files hold the counts and figures of each set, and the seeds file has one row
per set and seed followed by the mean (integer basis points rounded half up), the minimum and the maximum; a written
folder compares as current, and an edited file and a deleted file are named in file name order.

**No dataset text.** No file contains any sentence, target word or label of the fake sources.

#### Test Scenarios

##### Record

**Tests**: `ExternalEvaluationRecord_Render_IsDeterministicWithFixedFilesAndLineFeeds`,
`ExternalEvaluationRecord_Render_SourcesAndConversion_ListPinsAndCounts`,
`ExternalEvaluationRecord_Render_Seeds_HasPerSeedRowsAndSpread`,
`ExternalEvaluationRecord_WriteThenCompare_DetectsEditedAndMissingFiles`

The bytes of two renderings are equal and have the properties above; the sources, conversion, summary and seeds files
are compared with the expected rows of the hand-built evaluation; after one file is edited and another deleted, both
are named in file name order, and a missing folder names all seven files.

##### No dataset text

**Test**: `ExternalEvaluationRecord_Render_HoldsNoDatasetText`

None of the words, labels or sentences of the fake sources is found in any rendered file.
