## AnalysisSubsystem Verification

### Verification Approach

The SpeechTextEn AnalysisSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem` that need no network and no hardware. The
subsystem-level tests in `AnalysisSubsystemTests.cs` exercise the units together through the public analyzer
(the real options, annotation vocabulary, adapter, diagnostics reporter and pronunciation dictionary, over the real
part-of-speech package, and, because the default options select normalization, the real NormalizationSubsystem
too), injecting a test tagger factory (that still returns the real shared tagger, so the shared process-wide tagger
is never disturbed) to cancel at the tagger or to make its first creation fail, and asserting results, exceptions and
recovery rather than how often the tagger is created. The override tests load the sample dictionary
`TestData/overrides-dictionary.tsv` through its documented file format.

Each unit is verified in isolation by its own test file (see the unit verification documents). The normalization
pipeline of the analyzer (annotation order, reconciled breaks, detailed data, selection flags, cancellation and
counts), the annotation of overrides and spelled initialisms and the reading rules in detail (coexistence, single
candidate, diagnostics, cancellation) belong to the `EnglishTextAnalyzer` unit tests, the dictionary to the
`PronunciationOverrides` unit tests and the normalization rules to the NormalizationSubsystem tests (see *SpeechTextEn
NormalizationSubsystem Verification*). The heteronym readings are verified at this level through the real
HeteronymSubsystem with a hand-built fixture model.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: every test builds its own analyzer; test tagger factories are per test; no test depends on
  whether another test has already created the shared tagger
- **Fixtures**: a recording diagnostics sink (optionally throwing), a test tagger source and, for the heteronym
  tests, a model source and a fixture model builder, all defined in the test project

### Acceptance Criteria

The subsystem tests pass when: annotations read back through the typed reader at exactly the words they annotate;
selecting no annotations yields none but still yields authoritative sentence breaks, and an empty request yields no
annotations and an empty, non-null break list; an abbreviation title does not split a sentence under the default
(normalizing) options; a placeholder is never covered and the analysis equals that of the same text with spaces; the
normalization annotations read back at their spans, merged with the parts of speech, with the sentence breaks
reconciled; many concurrent requests agree with a sequential request and a request after a failed tagger creation
succeeds; cancellation throws `OperationCanceledException` and reports nothing; one counts-only event is reported
without
any word and a throwing sink changes nothing; every action of a pronunciation dictionary read from the documented file
format reaches a consumer through the typed readers, the replacement annotations of every kind form one ascending,
disjoint sequence and the pronunciations are disjoint from them; the reading annotations of an analysis with a model
read
back through the typed readers and coexist with the part of speech on the same span; and a reading override loaded from
the tab-separated format is validated when it is read, applied and counted.

### Test Scenarios

#### Annotations and options

**Tests**: `AnalysisSubsystem_Analyze_Sentence_AnnotationsReadBackThroughTypedReader`,
`AnalysisSubsystem_Analyze_PartOfSpeechNotSelected_ProducesNoAnnotationsButSentenceBreaks`

A sentence is analyzed and every annotation is read back through `TextEnAnnotations`. With the selection set to `None`
there are no annotations but the sentence break is still supplied, and an empty text returns no annotations and an
empty, non-null break list.

#### Sentence boundaries and placeholders

**Tests**: `AnalysisSubsystem_Analyze_MultiSentenceText_ReturnsAuthoritativeBreaks`,
`AnalysisSubsystem_Analyze_PlaceholderBetweenWords_IsNeverCoveredAndContextIsCarried`

A text with an abbreviation title (`Dr.`, which is also normalized under the default options), a placeholder and several
sentences yields breaks only at the first word of each later sentence. A text with a placeholder inside "I will ...
record it" gives annotations identical to the same text with a space, with "record" tagged as a verb because the words
before the tag are still context.

#### Normalization annotations

**Test**: `AnalysisSubsystem_Analyze_NormalizedText_AnnotationsReadBackAndBreaksAreReconciled`

A text with `St.` at a sentence end, a title and an ampersand is analyzed under the default options: the part-of-speech
and normalization annotations are both present in text order, the normalizations read back through
`TextEnAnnotations.TryReadNormalized` at their source spans, and the sentence breaks are reconciled (a break is added
after `Main St.` and none falls inside `Dr. Smith`).

#### Concurrency, recovery and cancellation

**Tests**: `AnalysisSubsystem_Analyze_ManyConcurrentRequests_AgreeWithSequentialResult`,
`AnalysisSubsystem_Analyze_TaggerFailsOnce_LaterRequestSucceeds`,
`AnalysisSubsystem_Analyze_CancellationRequested_ThrowsOperationCanceledExceptionAndReportsNothing`

Thirty-two concurrent requests across four analyzers sharing one adapter, racing on their first use, all return the
annotations a sequential request gives. When the first creation of the tagger fails the first request throws
`InvalidOperationException` and the next request returns exactly the annotations and breaks of a healthy analyzer. How
the tagger is obtained (lazily, once, shared, never for an analyzer that has nothing to tag) is a design strategy
recorded in the analysis subsystem design; no test counts tagger creations. A token canceled the moment the tagger is
first requested, in a multi-sentence text, makes the analysis throw `OperationCanceledException`, return no result and
send the sink no event.

#### Diagnostics

**Tests**: `AnalysisSubsystem_Analyze_Request_ReportsOneCountsOnlyDiagnostic`,
`AnalysisSubsystem_Analyze_ThrowingDiagnosticsSink_StillReturnsSameResult`

A text of invented words produces one Info event in the `TextEn.Analysis` category whose message matches the fixed
template of nine counts (ending with the overrides applied and ignored) and contains none of the words; an analyzer
whose sink throws returns the same result as one without a sink.

#### Pronunciation overrides through the typed readers

**Tests**: `AnalysisSubsystem_Analyze_OverrideActions_ReadBackThroughTypedReaders`,
`AnalysisSubsystem_Analyze_OverrideAndSpelledText_ReplacementsAndPronunciationsAreDisjoint`

The sample dictionary file is loaded and the text `Nginx runs a GIF in Polish, US and FBI and AT&T's SQL.` is
analyzed. A consumer written only against `TextEnAnnotations.TryReadReplacement` and `TryReadPronunciation` applies the
replacements in order and must obtain `engine ex runs a G I F in Polish, US and FBI and A T and T's sequel.`: the say
entries become words, the spell entry and the ampersand initialism become letters, the skip entries leave `US` and `FBI`
as written, and the one pronunciation (`Polish` with the IPA phonemes of the sample) is read with its scheme. A text
that
mixes a title, an ampersand, an override, currency, a unit, a time, an initialism and a credential is analyzed with the
same dictionary: every annotation other than the part of speech, in start order, ends before the next one starts, and
at least one pronunciation, one spell-out and one normalization annotation are present.

#### Heteronym readings

**Tests**: `AnalysisSubsystem_Analyze_ReadingFamily_ReadsBackThroughTypedReadersAndCoexistsWithPartOfSpeech`,
`AnalysisSubsystem_Analyze_ReadingOverrideFromTsv_AppliesAndCounts`

The text `I want to record a record.` is analyzed by an analyzer with the default options and a provider over a
hand-built
fixture model (the HeteronymSubsystem is real; only the weights are made by the test, see *SpeechTextEn
HeteronymSubsystem Verification*). Reading the annotations with `TextEnAnnotations.TryReadReading` gives `record.verb`
and then `record.noun`, each on exactly the characters `record`, and on every such span the part of speech and the
confidence (read with `TryReadReadingConfidence`) are present on the same start and length. A dictionary read from two
`reading` lines is applied by the default analyzer to `They read the record.`: the readings are `read.past` and
`record.noun` (the override decides both words, whatever the embedded model would choose), the analysis event reports
`2 overrides applied, 0 overrides ignored`, and a `reading` line whose id is not a candidate of its word is rejected
with
`InvalidDataException` when the file is read.

See also *SpeechTextEn System Verification* and the unit documents `english-text-analyzer.md`,
`english-text-analyzer-options.md`, `text-en-annotations.md`, `pronunciation-overrides.md`,
`part-of-speech-adapter.md` and `analysis-diagnostics.md`.
