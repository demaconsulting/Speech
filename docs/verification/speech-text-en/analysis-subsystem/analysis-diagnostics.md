### AnalysisDiagnostics Verification

#### Verification Approach

`AnalysisDiagnostics` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/AnalysisDiagnosticsTests.cs` against a recording
sink that can be told to throw. That the analyzer supplies the counts, reports the heteronym event at most once per
request and the model warning once per analyzer, and only for an unusable model, is verified in the
`EnglishTextAnalyzer`
unit tests.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: each test uses its own sink

#### Acceptance Criteria

- **Request event**: one request produces exactly one Info event in the `TextEn.Analysis` category with the message
  `Analyzed one text: {S} sentences, {T} tokens, {A} annotations, {U} unknown words, {N} normalizations, {X} ambiguous
  readings, {F} numeric fallbacks, {P} overrides applied, {I} overrides ignored.`; the message matches a template of
  fixed
  words and nine numbers for any counts, so no word, spoken replacement, annotation value or dictionary content can
  appear; the two override counts come last and are zero for counts built without them
- **Heteronym and model events**: one Info event in the `TextEn.Heteronym` category with the message `Assigned {n}
  heteronym readings: {c} classified, {s} single-candidate, {o} by override, {l} below confidence {t}.`, built from
  fixed
  words and invariant numbers (the threshold with two decimals and a period) under any culture and reported only when at
  least one reading was assigned; one Warning event in the `TextEn.Model` category with the fixed message `Heteronym
  model unusable (invalid or incompatible file); readings disabled.`
- **Containment**: a missing sink is accepted and reports nothing, and a sink that throws is contained for all three
  reports, the event still having been delivered to it

#### Test Scenarios

##### Request event

**Tests**: `AnalysisDiagnostics_ReportRequest_Sink_ReceivesOneInfoEventWithCounts`,
`AnalysisDiagnostics_FormatMessage_AnyCounts_ContainsOnlyFixedWordsAndNumbers`

The first test reports the counts 3, 17, 12, 2, 4, 1, 5, 6 and 7 (distinct values, so a swapped count fails) and
requires
one Info event in the right category whose labeled counts are those values in the documented order, with counts built
without the last two reporting zero overrides applied and ignored. The second formats nine counts including a large one
and requires that the nine labeled numbers are the only digits of the message.

##### Heteronym and model events

**Tests**: `AnalysisDiagnostics_ReportReadings_Counts_ReceivesOneInfoEventInHeteronymCategory`,
`AnalysisDiagnostics_ReportReadings_NothingAssigned_ReportsNothing`,
`AnalysisDiagnostics_FormatReadingMessage_AnyCulture_ContainsOnlyFixedWordsAndInvariantNumbers`,
`AnalysisDiagnostics_ReportModelProblem_Sink_ReceivesOneFixedWarning`

For the counts 3, 2, 1 and 1 and the threshold 0.6 the sink receives exactly one event in the `TextEn.Heteronym`
category with the message `Assigned 6 heteronym readings: 3 classified, 2 single-candidate, 1 by override, 1 below
confidence 0.60.`, so the total is the sum of the first three counts. Counts of zero report nothing. Under `ar-SA` the
message for 1234, 5, 6 and 7 with the threshold 0.5 is exactly `Assigned 1245 heteronym readings: 1234 classified, 5
single-candidate, 6 by override, 7 below confidence 0.50.`. The model problem gives one Warning event in the
`TextEn.Model`
category with the fixed message, which names neither a file nor any text.

##### Sink containment

**Tests**: `AnalysisDiagnostics_ReportRequest_ThrowingSink_IsContained`,
`AnalysisDiagnostics_ReportReadingsAndModelProblem_NullOrThrowingSink_AreContained`

With a sink that throws, and with no sink, the request report completes normally and the throwing sink was still
reached.
With no sink the heteronym and model reports do nothing; with a sink that throws both are contained and the sink still
received both events.
