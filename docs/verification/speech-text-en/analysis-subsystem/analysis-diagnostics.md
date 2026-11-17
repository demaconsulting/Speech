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
(`Analyzed one text: {S} sentences, {T} tokens, {A} annotations, {U} unknown words, {N} normalizations, {X} ambiguous readings, {F} numeric fallbacks, {P} overrides applied, {I} overrides ignored.`);
the message matches a template of fixed words and nine numbers for any counts, including large ones, so no word, spoken
replacement, annotation value or dictionary content can appear; the two override counts come last, are named, and are
zero for counts built without them; a missing sink is accepted and reports nothing; and a sink that throws is contained,
with the event still delivered to it. The heteronym event is one Info event in the `TextEn.Heteronym` category with the
message
`Assigned {n} heteronym readings: {c} classified, {s} single-candidate, {o} by override, {l} below confidence {t}.`,
built from fixed words and invariant numbers (the threshold with two decimals and a period) under any culture, and
reported only when at least one reading was assigned; the model warning is one Warning event in the `TextEn.Model`
category with the fixed message `Heteronym model unusable (invalid or incompatible file); readings disabled.`; and both
new reports tolerate a missing sink and a sink that throws.

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

##### Heteronym event

**Test**: `AnalysisDiagnostics_ReportReadings_Counts_ReceivesOneInfoEventInHeteronymCategory`

For the counts 3, 2, 1 and 1 and the threshold 0.6 the sink receives exactly one Info event in the `TextEn.Heteronym`
category whose message is `Assigned 6 heteronym readings: 3 classified, 2 single-candidate, 1 by override, 1 below
confidence 0.60.`, so the total is the sum of the first three counts and the threshold has two decimals.

##### Heteronym message under another culture

**Test**: `AnalysisDiagnostics_FormatReadingMessage_AnyCulture_ContainsOnlyFixedWordsAndInvariantNumbers`

Under `ar-SA` the message for 1234, 5, 6 and 7 with the threshold 0.5 is exactly `Assigned 1245 heteronym readings:
1234 classified, 5 single-candidate, 6 by override, 7 below confidence 0.50.` and matches a template of fixed words,
ASCII digits and a decimal period.

##### Nothing assigned

**Test**: `AnalysisDiagnostics_ReportReadings_NothingAssigned_ReportsNothing`

With a sink and counts of zero nothing is reported, and the total of such counts is zero.

##### Model warning

**Test**: `AnalysisDiagnostics_ReportModelProblem_Sink_ReceivesOneFixedWarning`

The sink receives exactly one Warning event in the `TextEn.Model` category with the fixed message, which names neither a
file nor any text.

##### No sink or failing sink for the new reports

**Test**: `AnalysisDiagnostics_ReportReadingsAndModelProblem_NullOrThrowingSink_AreContained`

With no sink both reports do nothing and do not throw; with a sink that throws both reports are contained and the sink
still received both events. That the analyzer reports the heteronym event at most once per request and the warning once
per analyzer, and only for an unusable model, is verified in the `EnglishTextAnalyzer` unit tests.
