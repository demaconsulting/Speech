## AnalysisSubsystem Verification

### Verification Approach

The SpeechTextEn AnalysisSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem` that need no network and no hardware. The
subsystem-level tests in `AnalysisSubsystemTests.cs` exercise the units together through the public analyzer
(the real options, annotation vocabulary, adapter and diagnostics reporter, over the real part-of-speech
package), observing tagger creation through a counting adapter so the shared process-wide tagger is never
disturbed and "loaded once" and "never loaded" can be asserted. Each unit is verified in isolation by its own
test file (see the unit verification documents).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: every test builds its own analyzer; counting adapters are per test; no test depends on
  whether another test has already loaded the shared tagger
- **Fixtures**: a recording diagnostics sink (optionally throwing) and a counting tagger source, both
  defined in the test project

### Acceptance Criteria

The subsystem tests pass when: annotations read back through the typed reader at exactly the words they
annotate; selecting no annotations yields none but still yields sentence breaks; breaks are authoritative
and unaffected by abbreviations and tags; a placeholder is never covered and the analysis equals that of the
same text with spaces; an unused analyzer, an untagged analysis and an empty text never load the tagger,
while many concurrent requests load it exactly once and agree; one counts-only event is reported without any
word and a throwing sink changes nothing; and cancellation, whether it arrives during tagger initialization or
during the last sentence, throws `OperationCanceledException` and reports nothing.

### Test Scenarios

#### Annotations and options

**Tests**: `AnalysisSubsystem_Analyze_Sentence_AnnotationsReadBackThroughTypedReader`,
`AnalysisSubsystem_Analyze_PartOfSpeechNotSelected_ProducesNoAnnotationsButSentenceBreaks`

A sentence is analyzed and every annotation is read back through `TextEnAnnotations`; with the selection set
to `None` there are no annotations but the sentence break is still supplied.

#### Sentence boundaries and placeholders

**Tests**: `AnalysisSubsystem_Analyze_MultiSentenceText_ReturnsAuthoritativeBreaks`,
`AnalysisSubsystem_Analyze_PlaceholderBetweenWords_IsNeverCoveredAndContextIsCarried`

A text with an abbreviation, a placeholder and several sentences yields breaks only at the first word of each
later sentence; a text with a placeholder inside "I will ... record it" gives annotations identical to the
same text with a space, with "record" tagged as a verb because the words before the tag are still context.

#### Shared tagger

**Tests**: `AnalysisSubsystem_Analyze_UnusedOrUntaggedAnalyzer_NeverLoadsTagger`,
`AnalysisSubsystem_Analyze_ManyConcurrentRequests_LoadTaggerOnceAndAgree`

Analyzers that are constructed but not used, analyze without tagging, or analyze an empty text request no
tagger; thirty-two concurrent requests across four analyzers sharing one adapter request it exactly once and
all return the same annotations.

#### Diagnostics

**Tests**: `AnalysisSubsystem_Analyze_Request_ReportsOneCountsOnlyDiagnostic`,
`AnalysisSubsystem_Analyze_ThrowingDiagnosticsSink_StillReturnsSameResult`

A text of invented words produces one Info event in the `TextEn.Analysis` category whose message matches the
fixed template and contains none of the words; an analyzer whose sink throws returns the same result as one
without a sink.

#### Cancellation

**Tests**: `AnalysisSubsystem_Analyze_CancellationRequested_ThrowsOperationCanceledExceptionAndReportsNothing`,
`AnalysisSubsystem_Analyze_CancellationDuringLastSentence_ThrowsOperationCanceledExceptionAndReportsNothing`

In the first test the token is canceled the moment the tagger is first requested (tagger initialization) in
a multi-sentence text; in the second it is canceled right after the only (last) sentence was tagged. In both
the analysis throws `OperationCanceledException`, returns no result and the sink receives no event.

See also _SpeechTextEn System Verification_ and the unit documents `english-text-analyzer.md`,
`english-text-analyzer-options.md`, `text-en-annotations.md`, `part-of-speech-adapter.md` and
`analysis-diagnostics.md`.
