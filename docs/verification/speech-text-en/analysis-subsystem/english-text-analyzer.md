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
dictionary says. The heteronym reading behavior is verified by
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerReadingTests.cs`: the real
HeteronymSubsystem and the real tagger are used with a hand-built fixture model (the test fixture builds it through the
real feature extractor and the real model writer from labeled example sentences, so no binary fixture file is committed
and it can never go stale against the feature set), supplied through the analyzer's internal constructor with a counting
model source that counts opens and can supply a corrupt or absent model, and with an observer that cancels at an exact
classification; the default analyzer, which uses the embedded model, is used where the test is about the default
behavior, and an analyzer with a provider that has no model source where it is about an absent model. The accuracy of
the shipped model is not verified here: it is covered by the trainer's committed-model tests and the evaluation record
(see *SpeechTextEn System Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access; the random-text test uses a fixed seed so a failure is
  reproducible; the heteronym tests build their own analyzer, provider and counting model source, so a model load
  count is per test; the culture tests set the current culture and restore it in a `finally` block

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
the normalizations and the ambiguous abbreviations; concurrent requests agree and create the tagger once; and the
analyzer does not keep the analyzed text alive. With
a pronunciation dictionary, a say, spell or pronunciation override annotates exactly its token (`en.norm`, `en.spell`,
`en.pron`), a skip override annotates nothing and bypasses the built-in rule, the word keeps its part of speech before
the replacement, overrides work around a tag placeholder, the detailed analysis lists a say or spell override with the
override kind, both entry points agree, sentence breaks are unchanged, every row of the golden file matches through the
public analyzer, the diagnostics event reports the two override counts and no content, cancellation inside the lookup
throws without a report. A non-empty dictionary without the
normalization family is rejected at construction, an empty dictionary is accepted, and the options are copied at
construction. Initialisms, dotted initialisms, single initials, credentials, the letters of a time such as `p.m.` and
ampersand initialisms are annotated `en.spell` with unchanged spoken text (the `and` of an ampersand initialism and
every word replacement stay `en.norm`), and nothing is spelled when normalization is not selected.

For heteronym readings: the reading and the confidence of a word are annotations on exactly the word's whole token,
after its part of speech and valid, in text order, only for a whole-token inventory form (not a hyphenated compound); a
default analyzer emits the readings of the embedded model (`record.verb` then `record.noun` for `I want to record a
record.`, with a confidence from 0 to 1, also when only normalization and readings are selected, and then without a part
of speech), while an analyzer with a provider that has no model source emits no reading (and nothing at all for the
selection of normalization and readings on that text); a selection without the reading family never opens a model; a
reading-only selection tags for context and emits no
`en.upos`; the model is opened lazily and once; a form with one candidate gets confidence `1.000` only when a model
source exists; a token that a replacement, a pronunciation or any override other than a reading override covers never
has a reading while the other words still do; a reading override fixes the reading with confidence `1.000`, without a
model of its own and over the classifier, and is ignored when the reading family is not selected; the detailed analysis
carries the
reading, the confidence and the low-confidence flag, and agrees with the language-neutral path; the confidence text is
invariant under any culture; the threshold counts classified readings below it and never removes one; the heteronym
event is one counts-only message at most once per request and the model warning is reported once per analyzer and only
for an unusable (invalid or incompatible) model, never for an absent one; a throwing sink changes nothing; cancellation
before any work, between candidate words and during the last candidate throws without a result and reports nothing; and
concurrent requests agree and load the model once.

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

**Test**: `EnglishTextAnalyzer_Analyze_TextWithoutWords_ReturnsNoAnnotationsAndNoBreaks`

Empty, whitespace-only and placeholder-only texts (a theory) return no annotations and an empty, non-null list of
breaks. Whether the tagger is created for such a text is a design choice and is not asserted.

##### Cancellation

**Tests**: `EnglishTextAnalyzer_Analyze_AlreadyCanceledToken_ThrowsOperationCanceledException`,
`EnglishTextAnalyzer_Analyze_CanceledDuringTaggerInitialization_ThrowsOperationCanceledException`,
`EnglishTextAnalyzer_Analyze_CanceledDuringOnlySentence_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_CanceledDuringFinalSentence_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_CanceledDuringNormalization_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_CanceledDuringOverrideLookup_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_NotCanceled_ReportsOneDiagnosticsEvent`

The initialization test cancels the token the moment the tagger is first requested; the only-sentence and
final-sentence tests cancel it from the adapter's internal after-sentence observer; the normalization test
analyzes a text of 1000 tokens with normalization only and cancels the token from the normalizer's
token-visited observer while token 300 is visited. The override-lookup test analyzes a text of 600 tokens that cannot
match, with normalization only and a one-entry dictionary, using a token that is already canceled, and requires the
exception and no event. Each asserts an `OperationCanceledException` and that a recording sink received no event; no
test asserts how many tokens were visited before the exception or how often the token is polled (those are design
choices). The last test is the control that
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
`EnglishTextAnalyzer_Analyze_PartOfSpeechNotSelected_ProducesNoAnnotationsAndUntaggedTokens`

The varied-texts theory compares the two entry points over empty, placeholder-only, emoji, CRLF, several-sentence and
abbreviation texts (`Dr.` and `p.m.` are normalized under the default options). With no annotations selected the
analysis has no annotations, still returns the break before `She` and seven tokens, none of which is tagged.

##### Normalization annotations and reconciled breaks

**Tests**: `EnglishTextAnalyzer_Analyze_AbbreviationToken_EmitsPartOfSpeechThenNormalization`,
`EnglishTextAnalyzer_Analyze_NormalizationOnly_EmitsOnlyNormalizationsAndUntaggedTokens`,
`EnglishTextAnalyzer_Analyze_NormalizationNotSelected_KeepsTokenizerBreaksAndEmitsNoNormalization`,
`EnglishTextAnalyzer_Analyze_PlaceholderAfterAbbreviation_IsNeverCoveredAndContextCrossesIt`

For `Dr. Smith lives here.` the first two annotations both start at the token `Dr.`, the part of speech first,
with the value `Doctor`, and the starts of all annotations never decrease. With normalization only, the
analysis of `Dr. Smith & Mr. Jones live on Main St. It is nice.` yields four annotations
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
`EnglishTextAnalyzer_Analyze_Overrides_ReportCountsAndNoContent`

For each row of the golden file the test builds an analyzer with the sample dictionary, normalization only and a
recording sink, and requires the spoken text, the pronunciation marks, the spelled spans and the tail of the
diagnostics message (`N overrides applied, N overrides ignored.`) to equal the row, every non-part-of-speech
annotation to be readable through a typed reader, and mismatches are collected so one run lists every failing row.
The counts test uses a dictionary of a sentinel word and payload and a text that repeats the sentinel word, followed by
`and 5 kg`,
expecting two applied overrides and one ignored (the unit consumed by the number) with neither the key nor the
payload in the message.

##### Heteronym reading annotations

**Tests**: `EnglishTextAnalyzer_Analyze_RecordSentence_EmitsReadingPairsOnExactSpansAfterPartOfSpeech`,
`EnglishTextAnalyzer_Analyze_WordShapes_ReadingOnWholeTokenOnly`,
`EnglishTextAnalyzer_Analyze_TagPlaceholders_KeepOffsetsAndReadings`,
`EnglishTextAnalyzer_Analyze_MultipleSentences_ReadingsMatchSingleSentenceAnalysis`

In `I want to record a record.` each `record` has, on exactly its own span, the part of speech, then `en.reading`, then
`en.reading-confidence` (`record.verb` for the first and `record.noun` for the second with the fixture model), the
confidence text matches `[01].ddd`, and the start offsets of all annotations are in text order.
`A record-breaking year.` has no reading (a hyphenated compound is one token that is not a form), while `RECORD`, a
sentence-initial `Record` and the `record` of `The record's value fell.` each get one reading on the word's own token
only. A tag placeholder between the words leaves both readings at their exact source offsets. For a text of two
sentences the readings equal those of the two sentences analyzed separately, so the context of a word is its own
sentence.

##### Heteronym selection and effectiveness

**Tests**: `EnglishTextAnalyzer_Analyze_DefaultAnalyzer_EmitsEmbeddedModelReadings`,
`EnglishTextAnalyzer_Analyze_NoModelSource_EmitsNoReadings`,
`EnglishTextAnalyzer_Analyze_ReadingNotSelected_EmitsNoReadings`,
`EnglishTextAnalyzer_Analyze_ReadingOnlySelection_EmitsReadingsWithoutPartOfSpeech`,
`EnglishTextAnalyzer_Analyze_RequestsWithoutHeteronymWords_EmitNoReadingsAndLaterRequestsStillGetThem`,
`EnglishTextAnalyzer_Analyze_SingleCandidateForm_EmitsConfidenceOneOnlyWithModel`

The default analyzer, which uses the embedded model, emits the readings `record.verb` and `record.noun` for `I want to
record a record.`, and so does an analyzer that selects normalization and readings (each reading has a confidence from 0
to 1 and no `en.upos` annotation is emitted for that selection). An analyzer built with a provider that has no model
source emits no reading kind with the default selection, and yields no annotation at all for normalization and readings
on that text. With a fixture model and the selection `Normalization` only, no
reading is emitted. With the selection `Reading` only, the text is tagged for context, readings are emitted, no
`en.upos` annotation is emitted and the detailed tokens carry no part of speech, confidence or known-word flag. An empty
text and a text without a heteronym word get no reading, and a later request with heteronym words gets the readings and
confidences that a fresh analyzer with the same model gives (`record.verb` then `record.noun` for
`I want to record a record.`, and `record.verb` for `They will record the meeting.`); when the model is opened, and that
it is opened once, are design choices and are not asserted. A form with exactly one candidate (the test takes the first
in the inventory) gets that reading with confidence `1.000` and one `single-candidate` in the heteronym event when a
model is available, and no reading from an analyzer whose provider has no model source.

##### Heteronym coexistence with replacements, pronunciations and overrides

**Tests**: `EnglishTextAnalyzer_Analyze_MixedCorpus_ReadingNeverOverlapsReplacementOrPronunciation`,
`EnglishTextAnalyzer_Analyze_OtherOverridesOnHeteronym_SuppressClassifier`

Over a corpus of four texts that mixes titles, currency, units, times, initialisms and overrides of the kinds say,
spell, pronounce and skip on heteronym forms (`wound`, `lead`, `tear`, `close`), no reading annotation overlaps an
`en.norm`, `en.spell` or `en.pron` annotation, every reading has its confidence on the same span, the counts of the two
kinds are equal, and some readings are still classified (so the rule is not vacuous). A skip, say, spell-out or
pronunciation override on `record` suppresses both readings of `I want to record a record.`, and an override of another
word leaves both.

##### Heteronym reading overrides

**Tests**: `EnglishTextAnalyzer_Analyze_ReadingOverride_BeatsClassifierAndNeedsNoModel`,
`EnglishTextAnalyzer_Analyze_ReadingOverrideWithoutReadingFamily_EmitsNothing`

A `Reading` override for `record` with `record.noun` gives both words the reading `record.noun` with confidence `1.000`,
with a model that would say verb for the first (the fixture model) and with the default analyzer (the embedded model);
the event reports `2 overrides applied` and a
heteronym event of `0 classified, 0 single-candidate, 2 by override`. When the selection lacks the reading family, the
same override emits no reading and no replacement, no pronunciation and no spelling.

##### Heteronym detail, confidence text and threshold

**Tests**: `EnglishTextAnalyzer_AnalyzeDetailed_Readings_AreOnTokensAndEqualAnalyze`,
`EnglishTextAnalyzer_Analyze_ConfidenceText_IsInvariantUnderTurkishAndFrenchCultures`,
`EnglishTextAnalyzer_Analyze_LowConfidenceThreshold_CountsClassifiedReadingsBelowIt`

With a threshold of 1 the detailed analysis lists the two `record` tokens with their reading, a confidence from 0.5 to 1
and the low-confidence flag, every other token with no reading, zero confidence and no flag, and the annotations of both
entry points agree. Under `tr-TR`, `fr-FR` and `ar-SA` the confidence text keeps a period and three ASCII digits. The
threshold theory (0, 0.6 and 1) emits both readings every time and reports `0`, `0` and `2` readings below the
confidence, with the threshold written with two decimals.

##### Heteronym diagnostics

**Tests**: `EnglishTextAnalyzer_Analyze_Diagnostics_HeteronymEventIsCountsOnlyAndAtMostOnce`,
`EnglishTextAnalyzer_Analyze_ModelWarning_OncePerAnalyzerOnlyForUnusableModel`,
`EnglishTextAnalyzer_Analyze_IncompatibleModel_NoReadingsAndOneWarning`,
`EnglishTextAnalyzer_Analyze_ThrowingSink_IsContained`

Two requests (one with a heteronym word, one without) under `ar-SA` give two `TextEn.Analysis` events and one Info-level
`TextEn.Heteronym` event that matches the fixed template with digits and a decimal period only and names neither a word
nor a reading id. A corrupt model (a changed last byte) gives no reading and exactly one Warning-level `TextEn.Model`
event with the fixed message after two requests, no heteronym event, and an absent model gives no warning and no event
at all. A model trained for a newer inventory revision gives no readings and one warning over two requests. A sink that
throws changes nothing: both readings are still returned.

##### Heteronym cancellation and concurrency

**Tests**: `EnglishTextAnalyzer_Analyze_Cancellation_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_ConcurrentCallers_GetIdenticalResults`

In a text with three candidate words, a token canceled before the call, or by the classifier observer after the first,
second or third classification, makes the request throw `OperationCanceledException` and report no event. Thirty-two
concurrent requests on one analyzer return annotations identical to those of a sequential request.

##### Concurrency and request independence

**Tests**: `EnglishTextAnalyzer_Analyze_ConcurrentRequestsOnOneAnalyzer_ReturnIdenticalResults`,
`EnglishTextAnalyzer_Analyze_AfterAnotherRequest_ResultEqualsFreshAnalyzerAndDisclosesNothingOfIt`

The concurrency test releases twenty-four tasks of twenty requests each on one analyzer whose very first requests race,
and every result equals that of a sequential request on a separate analyzer. The independence test analyzes
`Quokka marmoset rested. Zebra galloped.` with both entry points, then `I read it again.`, and requires that the
annotations, the sentence breaks and the detailed tokens of the second text equal those of a fresh analyzer, that no
annotation value contains a word of the first text, and that the two events reported for the second text (one per entry
point) contain none of those words either. What it proves is exactly this: a request's output depends only on that
request and never carries text of another. It does **not** prove that the analyzer keeps no reference to the text in
memory (a design obligation, no longer measured with weak references and forced collections because object lifetime
is not an output contract), and it does not prove that no copy of the words survives in the tagging package's private
per-thread buffers (upstream issue <https://github.com/demaconsulting/PartOfSpeech/issues/29>), which are deliberately
not inspected here; the pinned version (0.1.0-beta.5 or later) clears them, evidenced by the upstream package's own
tests (see *PartOfSpeech Verification* and *SpeechTextEn System Design*, *Retention Verification*).
