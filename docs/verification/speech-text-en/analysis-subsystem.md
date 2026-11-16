## AnalysisSubsystem Verification

### Verification Approach

The SpeechTextEn AnalysisSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem` that need no network and no hardware. The
subsystem-level tests in `AnalysisSubsystemTests.cs` exercise the units together through the public analyzer
(the real options, annotation vocabulary, adapter, diagnostics reporter and pronunciation dictionary, over the real
part-of-speech package, and, because the default options select normalization, the real NormalizationSubsystem
too), observing tagger creation through a counting adapter so the shared process-wide tagger is never
disturbed and "loaded once" and "never loaded" can be asserted. The override tests load the sample dictionary
`TestData/overrides-dictionary.tsv` through its documented file format. Each unit is verified in isolation by its own
test file (see the unit verification documents); the normalization pipeline of the analyzer (the order and
merging of `en.upos` and `en.norm` annotations, the reconciled breaks, the detailed data, the selection
flags, the cancellation inside normalization and the counts) and the annotation of overrides and spelled
initialisms are verified by the `EnglishTextAnalyzer` unit tests, the dictionary by the `PronunciationOverrides`
unit tests, and the normalization rules themselves by the NormalizationSubsystem tests (see _SpeechTextEn
NormalizationSubsystem Verification_).

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
and unaffected by abbreviations and tags, and an abbreviation title does not split a sentence under the default
(normalizing) options; the normalization annotations read back at their spans, merged with the parts of speech,
with the sentence breaks reconciled; a placeholder is never covered and the analysis equals that of the
same text with spaces; an unused analyzer, an untagged analysis and an empty text never load the tagger,
while many concurrent requests load it exactly once and agree; one counts-only event (sentences, tokens,
annotations, unknown words, normalizations, ambiguous readings, numeric fallbacks, overrides applied and overrides
ignored) is reported without any word and a throwing sink changes nothing; every action of a pronunciation
dictionary read from the documented file format reaches a consumer through the typed readers, which, applied by the
documented algorithm, give the expected spoken text and phonemes; the replacement annotations of every kind form
one ascending, disjoint sequence and the pronunciations are disjoint from them; and cancellation, whether it arrives
during tagger initialization or during the last sentence, throws `OperationCanceledException` and reports nothing.

### Test Scenarios

#### Annotations and options

**Tests**: `AnalysisSubsystem_Analyze_Sentence_AnnotationsReadBackThroughTypedReader`,
`AnalysisSubsystem_Analyze_PartOfSpeechNotSelected_ProducesNoAnnotationsButSentenceBreaks`

A sentence is analyzed and every annotation is read back through `TextEnAnnotations`; with the selection set
to `None` there are no annotations but the sentence break is still supplied.

#### Sentence boundaries and placeholders

**Tests**: `AnalysisSubsystem_Analyze_MultiSentenceText_ReturnsAuthoritativeBreaks`,
`AnalysisSubsystem_Analyze_PlaceholderBetweenWords_IsNeverCoveredAndContextIsCarried`

A text with an abbreviation title (`Dr.`, which is also normalized under the default options), a placeholder
and several sentences yields breaks only at the first word of each
later sentence; a text with a placeholder inside "I will ... record it" gives annotations identical to the
same text with a space, with "record" tagged as a verb because the words before the tag are still context.

#### Normalization annotations

**Test**: `AnalysisSubsystem_Analyze_NormalizedText_AnnotationsReadBackAndBreaksAreReconciled`

A text with `St.` at a sentence end, a title and an ampersand is analyzed under the default options: the part-of-speech
and normalization annotations are both present in text order, the normalizations read back through
`TextEnAnnotations.TryReadNormalized` at their source spans, and the sentence breaks are reconciled (a break is added
after `Main St.` and none falls inside `Dr. Smith`).

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
fixed template of nine counts (ending with the overrides applied and ignored) and contains none of the words; an
analyzer whose sink throws returns the same result as one without a sink.

#### Pronunciation overrides through the typed readers

**Test**: `AnalysisSubsystem_Analyze_OverrideActions_ReadBackThroughTypedReaders`

The sample dictionary file is loaded and the text `Nginx runs a GIF in Polish, US and FBI and AT&T's SQL.` is
analyzed. A consumer written only against `TextEnAnnotations.TryReadReplacement` and `TryReadPronunciation` applies
the replacements in order and must obtain `engine ex runs a G I F in Polish, US and FBI and A T and T's sequel.`:
the say entries become words, the spell entry and the ampersand initialism become letters, the skip entries leave
`US` and `FBI` as written, and the one pronunciation (`Polish` with the IPA phonemes of the sample) is read with its
scheme. The spelled values are `G I F`, `A T` and `T`, and the `sequel` replacement is not spelled.

#### Disjoint replacements and pronunciations

**Test**: `AnalysisSubsystem_Analyze_OverrideAndSpelledText_ReplacementsAndPronunciationsAreDisjoint`

A text that mixes a title, an ampersand, an override, currency, a unit, a time, an initialism and a credential is
analyzed with the sample dictionary. Every annotation other than the part of speech, in start order, ends before
the next one starts, so the replacements of both kinds and the pronunciations form one non-overlapping sequence,
and at least one pronunciation, one spell-out and one normalization annotation are present.

#### Cancellation

**Tests**: `AnalysisSubsystem_Analyze_CancellationRequested_ThrowsOperationCanceledExceptionAndReportsNothing`,
`AnalysisSubsystem_Analyze_CancellationDuringLastSentence_ThrowsOperationCanceledExceptionAndReportsNothing`

In the first test the token is canceled the moment the tagger is first requested (tagger initialization) in
a multi-sentence text; in the second it is canceled right after the only (last) sentence was tagged. In both
the analysis throws `OperationCanceledException`, returns no result and the sink receives no event.

See also _SpeechTextEn System Verification_ and the unit documents `english-text-analyzer.md`,
`english-text-analyzer-options.md`, `text-en-annotations.md`, `pronunciation-overrides.md`,
`part-of-speech-adapter.md` and `analysis-diagnostics.md`.
