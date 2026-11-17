### EnglishTextAnalyzer Verification

#### Verification Approach

`EnglishTextAnalyzer` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerTests.cs`. Its documented
collaborators (the adapter, the diagnostics reporter, the options, the annotation vocabulary and the shared
default `TextNormalizer` with its lexicons) are real, over the real part-of-speech package; tagger creation
is observed through a counting adapter, and the one test that must cancel inside normalization supplies a
`TextNormalizer` whose token-visited observer cancels at an exact token. The pronunciation override tests use real
`PronunciationOverrides` dictionaries, built in the test or loaded from the sample dictionary
`TestData/overrides-dictionary.tsv`, and the golden file `TestData/overrides-cases.tsv` is run through the public
analyzer. Golden assertions use only sentences whose tags the tagger assigns with full confidence, and one
property-style test checks the structural guarantees against a large number of random, deliberately hostile texts.
Which phonemes a backend can speak is not verified here; the tests check only that annotations carry what the
dictionary says.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access; the random-text test uses a fixed seed so a failure is
  reproducible

#### Acceptance Criteria

Each word of a sentence has one annotation exactly on its source span, in order, with the expected tag and no annotation
on punctuation or symbols; the same spelling is tagged by context; sentence breaks are non-null, at later sentence
starts, not at abbreviations, and empty for one sentence; placeholders are never covered, separate touching words, give
the same result as spaces and never host a break; for 300 random texts every annotation is in bounds, on non-whitespace
edges, free of placeholders and surrogate-pair cuts, every part-of-speech annotation reads back, and every break is on a
word start; texts without words yield empty results and no tagger load; cancellation throws before any work, between
sentences and inside normalization; a null text or null options throws; the detailed analysis agrees with the
language-neutral one; a token that is an abbreviation carries its part of speech and then its `en.norm` annotation, in
text order; normalization alone needs no tagger and adds no part of speech, and without normalization the tokenizer's
breaks are returned unchanged and no `en.norm` annotation or clitic role is produced; the detailed analysis lists every
normalization with its source text, spoken text and kind and every clitic with its role; the diagnostics event counts
the normalizations and the ambiguous abbreviations; the language-neutral path allocates clearly less than the detailed
path; concurrent requests agree and create the tagger once; and the analyzer does not keep the analyzed text alive. With
a pronunciation dictionary, a say, spell or pronunciation override annotates exactly its token (`en.norm`, `en.spell`,
`en.pron`), a skip override annotates nothing and bypasses the built-in rule, the word keeps its part of speech before
the replacement, overrides work around a tag placeholder, the detailed analysis lists a say or spell override with the
override kind, both entry points agree, sentence breaks are unchanged, every row of the golden file matches through the
public analyzer, the diagnostics event reports the two override counts and no content, cancellation inside the lookup
throws without a report, and a dictionary without hits adds no per-token allocation. A non-empty dictionary without the
normalization family is rejected at construction, an empty dictionary is accepted, and the options are copied at
construction. Initialisms, dotted initialisms, single initials, credentials, the letters of a time such as `p.m.` and
ampersand initialisms are annotated `en.spell` with unchanged spoken text (the `and` of an ampersand initialism and
every word replacement stay `en.norm`), and nothing is spelled when normalization is not selected.

#### Test Scenarios

##### Word annotations

**Tests**: `EnglishTextAnalyzer_Analyze_SimpleSentence_AnnotatesEachWordOnItsSourceSpan`,
`EnglishTextAnalyzer_Analyze_UnambiguousSentence_ProducesExpectedUniversalTags`,
`EnglishTextAnalyzer_Analyze_SameSpellingInDifferentContext_UsesContextForTag`,
`EnglishTextAnalyzer_Analyze_PunctuationAndSymbols_CarryNoAnnotation`,
`EnglishTextAnalyzer_Analyze_TokenWithoutLetterOrDigit_CarriesNoPartOfSpeech`,
`EnglishTextAnalyzer_Analyze_NumeralToken_KeepsItsPartOfSpeech`

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
whitespace, placeholders, surrogate-pair emoji, combining marks, lone surrogates and a NUL character. Every
annotation of either kind must satisfy the geometric guarantees, an `en.upos` annotation must read back as a
part of speech and an `en.norm` or `en.spell` annotation as a replacement through the combined reader.

##### Texts without words

**Test**: `EnglishTextAnalyzer_Analyze_TextWithoutWords_ReturnsNoAnnotationsAndLoadsNoTagger`

Empty, whitespace-only and placeholder-only texts (a theory).

##### Cancellation

**Tests**: `EnglishTextAnalyzer_Analyze_AlreadyCanceledToken_ThrowsWithoutLoadingTagger`,
`EnglishTextAnalyzer_Analyze_CanceledDuringTaggerInitialization_ThrowsOperationCanceledException`,
`EnglishTextAnalyzer_Analyze_CanceledDuringOnlySentence_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_CanceledDuringFinalSentence_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_CanceledDuringNormalization_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_CanceledDuringOverrideLookup_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_NotCanceled_ReportsOneDiagnosticsEvent`

The initialization test cancels the token the moment the tagger is first requested; the only-sentence and
final-sentence tests cancel it from the adapter's internal after-sentence observer; the normalization test
analyzes a text of 1000 tokens with normalization only and cancels the token from the normalizer's
token-visited observer while token 300 is visited, which the normalizer's periodic check (every 256 tokens)
observes. The override-lookup test analyzes a text of 600 tokens that cannot match, with normalization only and a
one-entry dictionary, using a token that is already canceled, and requires the exception and no event. Each asserts an
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
`EnglishTextAnalyzer_Analyze_PartOfSpeechNotSelected_ProducesNoAnnotationsAndLoadsNoTagger`,
`EnglishTextAnalyzer_Analyze_ComparedWithAnalyzeDetailed_AllocatesLess`

The varied-texts theory compares the two entry points over empty, placeholder-only, emoji, CRLF, several-sentence and
abbreviation texts (`Dr.` and `p.m.` are normalized under the default options). The allocation test runs the two entry
points over the same large normalization-only text, after a warm-up, and requires the language-neutral path to allocate
less than 90 percent of what the detailed path does, because it builds no token or normalization object and no text
copy.

##### Normalization annotations and reconciled breaks

**Tests**: `EnglishTextAnalyzer_Analyze_AbbreviationToken_EmitsPartOfSpeechThenNormalization`,
`EnglishTextAnalyzer_Analyze_NormalizationOnly_LoadsNoTaggerAndEmitsOnlyNormalizations`,
`EnglishTextAnalyzer_Analyze_NormalizationNotSelected_KeepsTokenizerBreaksAndEmitsNoNormalization`,
`EnglishTextAnalyzer_Analyze_PlaceholderAfterAbbreviation_IsNeverCoveredAndContextCrossesIt`

For `Dr. Smith lives here.` the first two annotations both start at the token `Dr.`, the part of speech first,
with the value `Doctor`, and the starts of all annotations never decrease. With normalization only, the
analysis of `Dr. Smith & Mr. Jones live on Main St. It is nice.` creates no tagger, yields four annotations
that are all `en.norm`, and leaves every token without a part of speech. With part of speech only, and with
no annotations, `I live on Main St. It is nice.` keeps the tokenizer's single sentence and has no `en.norm`
annotation, while the default analyzer breaks before `It`. For `Dr. {placeholder} Smith waved.` the single
normalization covers `Dr.`, reads `Doctor` (the name after the tag is context) and no annotation covers the
placeholder.

##### Detailed normalization data

**Tests**: `EnglishTextAnalyzer_AnalyzeDetailed_NormalizedText_ListsNormalizationsAndCliticRoles`,
`EnglishTextAnalyzer_AnalyzeDetailed_NormalizationNotSelected_HasNoNormalizationsAndNoCliticRoles`

The detailed analysis of `Dr. Smith & Jones can’t go. John's book is here.` lists the abbreviation `Dr.` as
`Doctor`, the symbol `&` as `and` and the typographic apostrophe as `'`, each with its source slice, and the
clitic roles `Not` and `Possessive`; without normalization the list is empty and every clitic role is `None`.

##### Normalization counts in the diagnostics

**Test**: `EnglishTextAnalyzer_Analyze_NormalizedText_ReportsNormalizationAndAmbiguityCounts`

The event for `Dr. Smith & Jones said They St. said so.` reports one sentence, three normalizations and one
ambiguous reading (the inconclusive `They St.`), no numeric fallback and, with no dictionary, zero overrides applied
and zero ignored, in the fixed message template of nine counts.

##### Pronunciation override annotations

**Tests**: `EnglishTextAnalyzer_Analyze_SayOverride_EmitsNormAndKeepsPartOfSpeech`,
`EnglishTextAnalyzer_Analyze_SpellOutOverride_EmitsSpell`,
`EnglishTextAnalyzer_Analyze_PronunciationOverride_EmitsPron`,
`EnglishTextAnalyzer_Analyze_SkipOverride_EmitsNothingForToken`,
`EnglishTextAnalyzer_Analyze_OverrideAdjacentToAudioTag_AnnotatesAroundPlaceholder`

A say entry for `Nginx` yields one `en.norm` annotation `engine ex` on exactly that token, with the `en.upos`
annotation of the word listed before it at the same start, and applying the replacement gives `Use engine ex
today.`. A spell entry for `gif` yields one `en.spell` annotation on `GIF` whose value `G I F` reads back through
`TryReadSpellOut`. A sensitive IPA entry and an insensitive ARPAbet entry for `Polish` and `polish` yield two
`en.pron` annotations on the exact tokens, the first reading back through `TryReadPronunciation`, and applying the
replacements leaves the text unchanged. A skip entry for `US` removes the `en.spell` annotation that the
built-in rule gives `US` in the same text without a dictionary, while the part-of-speech annotations stay equal.
For `Nginx`, `SQL` and `Nginx` separated by tag placeholders, the three `en.norm` annotations sit on the words and
no annotation covers a placeholder.

##### Override details and consistency

**Tests**: `EnglishTextAnalyzer_AnalyzeDetailed_ListsOverrideNormalizationKind`,
`EnglishTextAnalyzer_Analyze_AnalyzeAndDetailed_ProduceSameAnnotationsWithOverrides`,
`EnglishTextAnalyzer_Analyze_OverridesDoNotChangeSentenceBreaks`,
`EnglishTextAnalyzer_Analyze_OverridesOnAbbreviations_DoNotChangeSentenceBreaks`

The detailed analysis lists the say and the spell override as normalizations of the `Override` kind, the spelled one
flagged as spelled, lists the built-in `FBI` as an initialism, and leaves the pronunciation out of the
normalizations while keeping its annotation. The theory compares the two entry points with the sample dictionary
over an empty text, a placeholder text with an emoji, several sentences and texts mixing numbers, units, titles and
initialisms: the annotations and breaks are equal, and the normalizations equal the `en.norm` and `en.spell`
annotations. The break test compares the breaks of a text with overrides to those of the same text with the
overridden words replaced by neutral words of the same length and to those of the text without a dictionary; all
three are equal. A second theory runs ten texts with an abbreviation word whose period the tokenizer separates or that
is sentence-final (`Fig`, `Dr`, `St`, `No`, `Jan`, `Ph.D`, `Dr` after an em dash, a tag placeholder next to `Fig`)
under a one-entry dictionary for each action and requires the breaks of both entry points to equal those of the
analyzer without a dictionary.

##### Constructor validation and options copy

**Tests**: `EnglishTextAnalyzer_Ctor_OverridesWithoutNormalization_Throws`,
`EnglishTextAnalyzer_Ctor_EmptyOverridesWithoutNormalization_IsAccepted`,
`EnglishTextAnalyzer_Ctor_OptionsCopiedAtConstruction_IgnoresLaterWith`

A non-empty dictionary with the selection `None` or part of speech only throws `ArgumentException` with the
parameter name `options`, and the same dictionary with normalization is accepted. An empty or absent dictionary is
accepted for any selection and the analysis equals that of an analyzer without it. A with-copy of the options made
after construction, carrying another dictionary, does not reach the analyzer.

##### Spelled initialisms

**Tests**: `EnglishTextAnalyzer_Analyze_Initialisms_EmitSpellInsteadOfNorm`,
`EnglishTextAnalyzer_Analyze_WordReplacement_StaysNorm`,
`EnglishTextAnalyzer_Analyze_AmpersandInitialism_EmitsLetterSpansAndAndNorm`,
`EnglishTextAnalyzer_Analyze_NormalizationNotSelected_EmitsNoSpell`

The initialism theory covers `FBI`, `U.S.`, `J.`, `Ph.D.`, `M.D.`, `p.m.`, `US` and `CD`: each yields one `en.spell`
annotation on its source with the expected letters, readable through `TryReadSpellOut`, and no `en.norm`
annotation at the same start. A text of `Dr.`, `&` and a currency amount yields only `en.norm` annotations. The
ampersand theory (`AT&T's`, the typographic possessive, `R&D`, `Q&A` and a mid-sentence `AT&T`) expects letter-group
spans of kind `en.spell` and `&` spans of kind `en.norm` (and, for the typographic apostrophe, an `en.norm`
span for it), with the spoken text `A T and T's network` and the possessive tail outside every span. With
only part of speech selected, a text full of initialisms yields only `en.upos` annotations.

##### Override golden cases and counts

**Tests**: `EnglishTextAnalyzer_Analyze_GoldenCases_MatchThroughThePublicApi`,
`EnglishTextAnalyzer_Analyze_Overrides_ReportCountsAndNoContent`,
`EnglishTextAnalyzer_Analyze_OverridesWithoutHits_AddNoPerTokenAllocation`

For each row of the golden file the test builds an analyzer with the sample dictionary, normalization only and a
recording sink, and requires the spoken text, the pronunciation marks, the spelled spans and the tail of the
diagnostics message (`N overrides applied, N overrides ignored.`) to equal the row, every non-part-of-speech
annotation to be readable through a typed reader, and mismatches are collected so one run lists every failing row.
The counts test uses a dictionary of a sentinel word and payload and a text that repeats the sentinel word, followed by
`and 5 kg`,
expecting two applied overrides and one ignored (the unit consumed by the number) with neither the key nor the
payload in the message. The allocation test analyzes 4,000 repetitions of a pangram with and without the sample
dictionary after a warm-up and requires the dictionary to add fewer than 4,096 bytes.

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
issue <https://github.com/demaconsulting/PartOfSpeech/issues/29>; see *SpeechTextEn System Design*), which
is deliberately not inspected or cleared here.
