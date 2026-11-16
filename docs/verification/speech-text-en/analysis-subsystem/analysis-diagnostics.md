### AnalysisDiagnostics Verification

#### Verification Approach

`AnalysisDiagnostics` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/AnalysisDiagnosticsTests.cs` against a recording
sink that can be told to throw.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: each test uses its own sink

#### Acceptance Criteria

One request produces exactly one Info event in the `TextEn.Analysis` category with the documented message
(`Analyzed one text: {S} sentences, {T} tokens, {A} annotations, {U} unknown words, {N} normalizations, {X}
ambiguous readings, {F} numeric fallbacks, {P} overrides applied, {I} overrides ignored.`); the message matches a
template of fixed words and nine numbers for any counts,
including large ones, so no word, spoken replacement, annotation value or dictionary content can appear; the two
override counts come last, are named, and are zero for counts built without them; a missing sink is
accepted and reports nothing; and a sink that throws is contained, with the event still delivered to it.

#### Test Scenarios

##### One counts event

**Test**: `AnalysisDiagnostics_ReportRequest_Sink_ReceivesOneInfoEventWithCounts`

The event's message states all nine counts (the test uses distinct values so that a swapped count fails),
including the ambiguous readings, the numeric fallbacks and the overrides applied and ignored.

##### Message contains only fixed words and numbers

**Test**: `AnalysisDiagnostics_FormatMessage_AnyCounts_ContainsOnlyFixedWordsAndNumbers`

The message must match the template `Analyzed one text: N sentences, N tokens, N annotations, N unknown words,
N normalizations, N ambiguous readings, N numeric fallbacks, N overrides applied, N overrides ignored.` for any nine
counts.

##### Override counts

**Test**: `AnalysisDiagnostics_FormatMessage_OverrideCounts_AreNamedAndOrdered`

For the counts 1 to 9 the message ends with `7 numeric fallbacks, 8 overrides applied, 9 overrides ignored.`, so the
applied count precedes the ignored count and both follow the numeric fallbacks. Counts built with only the first
seven values report zero overrides applied and zero ignored. That the analyzer supplies the two counts, and that
no dictionary content reaches the message, is verified in the `EnglishTextAnalyzer` unit tests.

##### No sink

**Test**: `AnalysisDiagnostics_ReportRequest_NullSink_DoesNothing`

##### Failing sink

**Test**: `AnalysisDiagnostics_ReportRequest_ThrowingSink_IsContained`
