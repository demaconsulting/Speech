### EnglishTextAnalyzer Verification

#### Verification Approach

`EnglishTextAnalyzer` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerTests.cs`. Its documented
collaborators (the adapter, the diagnostics reporter, the options and the annotation vocabulary) are real,
over the real part-of-speech package; tagger creation is observed through a counting adapter. Golden
assertions use only sentences whose tags the tagger assigns with full confidence, and one property-style test
checks the structural guarantees against a large number of random, deliberately hostile texts.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access; the random-text test uses a fixed seed so a failure is
  reproducible

#### Acceptance Criteria

Each word of a sentence has one annotation exactly on its source span, in order, with the expected tag and no
annotation on punctuation or symbols; the same spelling is tagged by context; sentence breaks are non-null,
at later sentence starts, not at abbreviations, and empty for one sentence; placeholders are never covered,
separate touching words, give the same result as spaces and never host a break; for 300 random texts every
annotation is in bounds, on non-whitespace edges, free of placeholders and surrogate-pair cuts, and every
break is on a word start; texts without words yield empty results and no tagger load; cancellation throws
before any work and between sentences; a null text or null options throws; the detailed analysis agrees with
the language-neutral one; selecting no annotations yields none and no tagger load; concurrent requests agree
and create the tagger once; and the analyzer does not keep the analyzed text alive.

#### Test Scenarios

##### Word annotations

**Tests**: `EnglishTextAnalyzer_Analyze_SimpleSentence_AnnotatesEachWordOnItsSourceSpan`,
`EnglishTextAnalyzer_Analyze_UnambiguousSentence_ProducesExpectedUniversalTags`,
`EnglishTextAnalyzer_Analyze_SameSpellingInDifferentContext_UsesContextForTag`,
`EnglishTextAnalyzer_Analyze_PunctuationAndSymbols_CarryNoAnnotation`

##### Sentence breaks

**Tests**: `EnglishTextAnalyzer_Analyze_MultipleSentences_ReturnsBreaksAtLaterSentenceStarts`,
`EnglishTextAnalyzer_Analyze_AbbreviationPeriod_DoesNotBreakSentence`,
`EnglishTextAnalyzer_Analyze_SingleSentence_ReturnsEmptyNonNullBreaks`

##### Placeholders

**Tests**: `EnglishTextAnalyzer_Analyze_PlaceholderBetweenWords_IsNeverCovered`,
`EnglishTextAnalyzer_Analyze_PlaceholderBetweenWords_EqualsAnalysisWithSpaceInItsPlace`,
`EnglishTextAnalyzer_Analyze_PlaceholderTouchingWords_SeparatesTheWords`,
`EnglishTextAnalyzer_Analyze_PlaceholderAtSentenceBoundary_BreaksBeforeNextWord`

##### Well-formed annotations for any text

**Test**: `EnglishTextAnalyzer_Analyze_RandomText_AnnotationsAndBreaksAreWellFormed`

Three hundred texts are generated from pieces that include words, numbers, punctuation, every kind of
whitespace, placeholders, surrogate-pair emoji, combining marks, lone surrogates and a NUL character.

##### Texts without words

**Test**: `EnglishTextAnalyzer_Analyze_TextWithoutWords_ReturnsNoAnnotationsAndLoadsNoTagger`

Empty, whitespace-only and placeholder-only texts (a theory).

##### Cancellation

**Tests**: `EnglishTextAnalyzer_Analyze_AlreadyCanceledToken_ThrowsWithoutLoadingTagger`,
`EnglishTextAnalyzer_Analyze_CanceledDuringTaggerInitialization_ThrowsOperationCanceledException`,
`EnglishTextAnalyzer_Analyze_CanceledDuringOnlySentence_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_CanceledDuringFinalSentence_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_NotCanceled_ReportsOneDiagnosticsEvent`

The initialization test cancels the token the moment the tagger is first requested; the only-sentence and
final-sentence tests cancel it from the adapter's internal after-sentence observer. Each asserts an
`OperationCanceledException` and that a recording sink received no event; the last test is the control that
an uncanceled request still reports exactly one event. The analyzer's own periodic and final checks
(while the result is built and before reporting) are cheap backstops for cancellation arriving from another
thread; they are verified by review, since a deterministic test would need a timing dependency.

##### Argument validation

**Tests**: `EnglishTextAnalyzer_Analyze_NullText_ThrowsArgumentNullException`,
`EnglishTextAnalyzer_Constructor_NullOptions_ThrowsArgumentNullException`

##### Detailed analysis and annotation selection

**Tests**: `EnglishTextAnalyzer_AnalyzeDetailed_Sentence_ListsTokensWithTagConfidenceAndKnownWordFlag`,
`EnglishTextAnalyzer_Analyze_ComparedWithAnalyzeDetailed_ReturnsSameAnnotationsAndBreaks`,
`EnglishTextAnalyzer_Analyze_VariedTexts_MatchesAnalyzeDetailedAnnotationsAndBreaks`,
`EnglishTextAnalyzer_Analyze_PartOfSpeechNotSelected_ProducesNoAnnotationsAndLoadsNoTagger`

##### Concurrency and no retained references

**Tests**: `EnglishTextAnalyzer_Analyze_ConcurrentRequestsOnOneAnalyzer_ReturnIdenticalResultsAndLoadTaggerOnce`,
`EnglishTextAnalyzer_Analyze_AfterReturn_KeepsNoReferenceToTextOrResults`

The reference test builds a unique, non-interned string, analyzes it with both entry points inside a helper
that is marked not to inline, and returns only weak references to the string and to both results. It then
forces blocking full collections (with finalizer waits) in a bounded loop of ten attempts, so neither JIT
liveness in Debug versus Release nor collector timing differences between operating systems can keep an
object alive or make the test flaky. The forced collection is the point of the test, so the analyzer rule
against it is waived locally with that justification. What it proves is exactly this: the analyzer, as
this library's own state, keeps no reference to the input string or to its results after the call returns.
It does **not** prove that no copy of the words survives in memory: the tagging package keeps token
characters in a private, never-cleared per-thread buffer (known limitation of the dependency, upstream
issue <https://github.com/demaconsulting/PartOfSpeech/issues/29>; see _SpeechTextEn System Design_), which
is deliberately not inspected or cleared here.
