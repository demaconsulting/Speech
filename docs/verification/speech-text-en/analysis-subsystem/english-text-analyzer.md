### EnglishTextAnalyzer Verification

#### Verification Approach

`EnglishTextAnalyzer` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerTests.cs` and, for the heteronym
readings, `EnglishTextAnalyzerReadingTests.cs`. Its documented collaborators (the adapter, the diagnostics reporter,
the options, the annotation vocabulary and the shared default `TextNormalizer` with its lexicons) are real, over the
real part-of-speech package; tagger creation is observed through a counting adapter, and the tests that must cancel at
an
exact point supply an adapter or a `TextNormalizer` whose observer cancels there. The override tests use real
`PronunciationOverrides` dictionaries, built in the test or loaded from the sample dictionary
`TestData/overrides-dictionary.tsv`, and the golden file `TestData/overrides-cases.tsv` is run through the public
analyzer. Golden assertions use only sentences whose tags the tagger assigns with full confidence, and one
property-style test checks the structural guarantees against random, deliberately hostile texts. Which phonemes a
backend can speak is not verified here.

The reading tests use the real HeteronymSubsystem and the real tagger with a hand-built fixture model (the fixture
builds
it through the real feature extractor and the real model writer from labeled example sentences, so no binary fixture is
committed and it can never go stale against the feature set), supplied through the analyzer's internal constructor
with a
counting model source that can supply a corrupt or absent model and an observer that cancels at an exact classification.
The default analyzer, which uses the embedded model, is used where the test is about the default behavior, and a
provider that has no model source where it is about an absent model. The accuracy of the shipped model is not verified
here: it is covered by the trainer's committed-model tests and the evaluation record (see *SpeechTextEn System
Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access; the random-text test uses a fixed seed; every test builds its own
  analyzer, provider and counting source; the culture tests restore the current culture in a `finally` block

#### Acceptance Criteria

- **Part-of-speech annotations**: each word has one annotation exactly on its source span, in order, with the expected
  tag, tagged by context, and no annotation on punctuation, symbols or a token without a letter or digit; for random
  hostile texts every annotation is in bounds, on non-whitespace edges, free of placeholders and surrogate-pair cuts;
  with part of speech not selected there are none, and a text without words yields empty results
- **Sentence breaks**: non-null, at later sentence starts, not at abbreviations, empty for one sentence, reconciled when
  normalization is selected and the tokenizer's own otherwise
- **Placeholders**: never covered, never hosting a break, and the result equals that of the same text with spaces
- **Contract**: cancellation throws before any work, during a sentence and inside normalization and reports nothing; a
  null text or null options throws; concurrent requests agree with a sequential request; a request's output carries no
  text of another request
- **Detailed analysis**: agrees with the language-neutral one, lists every token with tag, confidence and known-word
  flag, every normalization with source, spoken text and kind, every clitic with its role (none without normalization)
  and the readings with the low-confidence flag
- **Normalization annotations**: an abbreviation carries its part of speech and then its `en.norm` annotation;
  initialisms, dotted initialisms, initials, credentials, meridiem letters and ampersand initialisms are `en.spell` with
  unchanged spoken text while the `and` and every word replacement stay `en.norm`; normalization alone needs no tagger
  and adds no part of speech; nothing is replaced or spelled without normalization
- **Overrides**: a say, spell, pronounce or skip override annotates exactly its token (a skip bypasses the built-in
  rule),
  the word keeps its part of speech before the replacement, overrides work around a placeholder, both entry points
  agree, sentence breaks are unchanged, every golden row matches; a non-empty dictionary without normalization is
  rejected at construction, an empty one is accepted and the options are copied
- **Reading annotations**: the reading and the confidence are annotations on exactly the whole token of an inventory
  form (not a hyphenated compound), after its part of speech, in text order, in the confidence text invariant under any
  culture; the default analyzer emits the embedded model's readings and an analyzer without a model source emits none;
  a reading-only selection tags for context but emits no part of speech and a selection without the family emits no
  reading; a single-candidate form gets `1.000` only with a model; no reading overlaps a replacement or pronunciation
  and an override other than a reading override suppresses the classifier; a reading override fixes the reading with
  `1.000` without a model and is ignored without the family; the model warning is reported once per analyzer and only
  for an unusable model; cancellation before any work and between candidate words reports nothing; concurrent requests
  agree

#### Test Scenarios

##### Part-of-speech annotations

**Tests**: `EnglishTextAnalyzer_Analyze_SimpleSentence_AnnotatesEachWordOnItsSourceSpan`,
`EnglishTextAnalyzer_Analyze_SameSpellingInDifferentContext_UsesContextForTag`,
`EnglishTextAnalyzer_Analyze_PunctuationAndSymbols_CarryNoAnnotation`,
`EnglishTextAnalyzer_Analyze_TokenWithoutLetterOrDigit_CarriesNoPartOfSpeech`,
`EnglishTextAnalyzer_Analyze_NumeralToken_KeepsItsPartOfSpeech`,
`EnglishTextAnalyzer_Analyze_RandomText_AnnotationsAndBreaksAreWellFormed`,
`EnglishTextAnalyzer_Analyze_NormalizationOnly_EmitsOnlyNormalizationsAndUntaggedTokens`,
`EnglishTextAnalyzer_Analyze_TextWithoutWords_ReturnsNoAnnotationsAndNoBreaks`

The simple-sentence test checks each word's span, order and tag; the context test tags the same spelling differently in
two sentences; punctuation, symbols, a bare symbol and an invisible character carry no part of speech while a numeral
keeps its own. The random-text test generates three hundred texts from pieces that include words, numbers,
punctuation, every kind of whitespace, placeholders, surrogate-pair emoji, combining marks, lone surrogates and a NUL
character, and requires every annotation to satisfy the geometric guarantees and read back through the typed readers.
With normalization only, `Dr. Smith & Mr. Jones live on Main St. It is nice.` yields four annotations that are all
`en.norm` and leaves every token without a part of speech. Empty, whitespace-only and placeholder-only texts return no
annotations and an empty, non-null list of breaks.

##### Sentence breaks

**Tests**: `EnglishTextAnalyzer_Analyze_MultipleSentences_ReturnsBreaksAtLaterSentenceStarts`,
`EnglishTextAnalyzer_Analyze_AbbreviationPeriod_DoesNotBreakSentence`,
`EnglishTextAnalyzer_Analyze_SingleSentence_ReturnsEmptyNonNullBreaks`,
`EnglishTextAnalyzer_Analyze_NormalizationNotSelected_KeepsTokenizerBreaksAndEmitsNoNormalization`

Breaks fall at the first token of each later sentence and not after an abbreviation period; one sentence gives an empty
list. With part of speech only, and with no annotations, `I live on Main St. It is nice.` keeps the tokenizer's single
sentence and has no `en.norm` annotation, while the default analyzer breaks before `It`.

##### Placeholders

**Tests**: `EnglishTextAnalyzer_Analyze_PlaceholderBetweenWords_IsNeverCovered`,
`EnglishTextAnalyzer_Analyze_PlaceholderBetweenWords_EqualsAnalysisWithSpaceInItsPlace`,
`EnglishTextAnalyzer_Analyze_PlaceholderAfterAbbreviation_IsNeverCoveredAndContextCrossesIt`

In `I've {placeholder} read it.` the words around the placeholder keep their part of speech, nothing covers the
placeholder and words touching one stay separate tokens. The analysis of a text with placeholders equals that of the
same text with spaces, and the break after `Hello there.{placeholder}` falls at `Welcome`, never at the placeholder. For
`Dr. {placeholder} Smith waved.` the single normalization covers `Dr.` and reads `Doctor`.

##### Contract: cancellation, arguments, concurrency, independence

**Tests**: `EnglishTextAnalyzer_Analyze_AlreadyCanceledToken_ThrowsOperationCanceledException`,
`EnglishTextAnalyzer_Analyze_CanceledDuringOnlySentence_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_CanceledDuringNormalization_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_NullText_ThrowsArgumentNullException`,
`EnglishTextAnalyzer_Analyze_ConcurrentRequestsOnOneAnalyzer_ReturnIdenticalResults`,
`EnglishTextAnalyzer_Analyze_AfterAnotherRequest_ResultEqualsFreshAnalyzerAndDisclosesNothingOfIt`

An already-canceled token throws for a short text and for a text of 600 tokens with a one-entry dictionary; the
adapter's after-sentence observer cancels right after the only sentence was tagged; a text of 1,000 tokens with
normalization only is canceled from the normalizer's token-visited observer while token 300 is visited. Each asserts an
`OperationCanceledException` and that a recording sink received no event; no test asserts how often the token is polled
(a design choice), and the analyzer's own periodic and final checks are cheap backstops verified by review. A null text
(both entry points) and null options throw `ArgumentNullException`. Twenty-four tasks of twenty requests each on one
analyzer whose very first requests race all equal a sequential request on another analyzer. The independence test
analyzes `Quokka marmoset rested. Zebra galloped.` with both entry points and then `I read it again.`, and requires that
the second text's annotations, breaks, detailed tokens and two reported events equal those of a fresh analyzer and
contain no word of the first text. It proves that a request's output never carries text of another request; it does not
prove that the analyzer keeps no reference to the text in memory (a design obligation) nor inspect the tagging
package's private per-thread buffers (upstream issue <https://github.com/demaconsulting/PartOfSpeech/issues/29>, cleared
by the pinned version; see *PartOfSpeech Verification* and *SpeechTextEn System Design*, *Retention Verification*).

##### Detailed analysis

**Tests**: `EnglishTextAnalyzer_AnalyzeDetailed_Sentence_ListsTokensWithTagConfidenceAndKnownWordFlag`,
`EnglishTextAnalyzer_Analyze_VariedTexts_MatchesAnalyzeDetailedAnnotationsAndBreaks`,
`EnglishTextAnalyzer_AnalyzeDetailed_NormalizedText_ListsNormalizationsAndCliticRoles`,
`EnglishTextAnalyzer_AnalyzeDetailed_NormalizationNotSelected_HasNoNormalizationsAndNoCliticRoles`,
`EnglishTextAnalyzer_AnalyzeDetailed_Readings_AreOnTokensAndEqualAnalyze`

A sentence with an invented spelling lists four tokens each with a tag and a confidence in range, the invented spelling
not known. The
varied-texts theory compares the two entry points over placeholders, emoji, several sentences and abbreviations, and
requires the detailed normalizations to equal the `en.norm` and `en.spell` annotations. `Dr. Smith & Jones can’t go.
John's book is here.` lists `Dr.` as `Doctor`, `&` as `and` and the typographic apostrophe as `'` with their source
slices and the clitic roles `Not` and `Possessive`; without normalization the list is empty and every role `None`. With
a threshold of 1 the two `record` tokens carry their reading, a confidence from 0.5 to 1 and the low-confidence flag,
every other token none, and the annotations of both entry points agree.

##### Normalization annotations

**Tests**: `EnglishTextAnalyzer_Analyze_AbbreviationToken_EmitsPartOfSpeechThenNormalization`,
`EnglishTextAnalyzer_Analyze_NormalizationOnly_EmitsOnlyNormalizationsAndUntaggedTokens`,
`EnglishTextAnalyzer_Analyze_Initialisms_EmitSpellInsteadOfNorm`,
`EnglishTextAnalyzer_Analyze_WordReplacement_StaysNorm`,
`EnglishTextAnalyzer_Analyze_AmpersandInitialism_EmitsLetterSpansAndAndNorm`,
`EnglishTextAnalyzer_Analyze_NormalizationNotSelected_EmitsNoSpell`

For `Dr. Smith lives here.` the first two annotations start at `Dr.`, the part of speech first, with the value `Doctor`,
and the starts of all annotations never decrease. The initialism theory covers `FBI`, `U.S.`, `J.`, `Ph.D.`, `M.D.`,
`p.m.`, `US` and `CD`: each yields one `en.spell` annotation on its source with the expected letters, readable through
`TryReadSpellOut`, and no `en.norm` at the same start. A text of `Dr.`, `&` and a currency amount yields only `en.norm`
annotations. The ampersand theory (`AT&T's`, the typographic possessive, `R&D`, `Q&A`, a mid-sentence `AT&T`) expects
letter-group spans of kind `en.spell` and `&` spans of kind `en.norm`, with the possessive tail outside every span. With
only part of speech selected, a text full of initialisms yields only `en.upos` annotations.

##### Overrides

**Tests**: `EnglishTextAnalyzer_Analyze_SayOverride_EmitsNormAndKeepsPartOfSpeech`,
`EnglishTextAnalyzer_Analyze_OverrideAdjacentToAudioTag_AnnotatesAroundPlaceholder`,
`EnglishTextAnalyzer_AnalyzeDetailed_ListsOverrideNormalizationKind`,
`EnglishTextAnalyzer_Analyze_AnalyzeAndDetailed_ProduceSameAnnotationsWithOverrides`,
`EnglishTextAnalyzer_Analyze_OverridesDoNotChangeSentenceBreaks`,
`EnglishTextAnalyzer_Analyze_OverridesOnAbbreviations_DoNotChangeSentenceBreaks`,
`EnglishTextAnalyzer_Analyze_GoldenCases_MatchThroughThePublicApi`,
`EnglishTextAnalyzer_Analyze_Overrides_ReportCountsAndNoContent`,
`EnglishTextAnalyzer_Ctor_OverridesWithoutNormalization_Throws`,
`EnglishTextAnalyzer_Ctor_EmptyOverridesWithoutNormalization_IsAccepted`,
`EnglishTextAnalyzer_Ctor_OptionsCopiedAtConstruction_IgnoresLaterWith`

One test covers the four actions. A say entry for `Nginx` yields one `en.norm` annotation `engine ex` on exactly that
token, with the `en.upos` of the word listed before it at the same start, and applying the replacement gives `Use engine
ex today.`; a spell entry for `gif` yields one `en.spell` annotation `G I F`; a sensitive IPA entry and an insensitive
ARPAbet entry for `Polish` and `polish` yield two `en.pron` annotations on the exact tokens and leave the text
unchanged; a skip entry for `US` removes the `en.spell` annotation the built-in rule gives it, the part-of-speech
annotations staying equal. Three `Nginx`/`SQL` words separated by placeholders keep their `en.norm` annotations on the
words. The detailed analysis lists the say and spell overrides as normalizations of the `Override` kind (the spelled one
flagged), lists the built-in `FBI` as an initialism and leaves the pronunciation out. The theory comparing both entry
points with the sample dictionary requires equal annotations and breaks. The break tests compare the breaks of a text
with overrides to those of the same text with neutral words and without a dictionary (all equal), including ten texts
with an abbreviation word whose period the tokenizer separates (`Fig`, `Dr`, `St`, `No`, `Jan`, `Ph.D`) under a
one-entry dictionary for each action. Each row of the golden file (sample dictionary, normalization only, recording
sink) must match in spoken text, pronunciation marks, spelled spans and the tail of the diagnostics message, every
non-part-of-speech annotation readable through a typed reader, mismatches collected so one run lists every failing row.
A dictionary of a sentinel word repeated before `and 5 kg` reports two overrides applied and one ignored with neither
key nor payload in the message. A non-empty dictionary with the selection `None` or part of speech only throws
`ArgumentException` with the parameter name `options`; an empty or absent dictionary is accepted for any selection; a
with-copy of the options made after construction does not reach the analyzer.

##### Reading annotations

**Tests**: `EnglishTextAnalyzer_Analyze_RecordSentence_EmitsReadingPairsOnExactSpansAfterPartOfSpeech`,
`EnglishTextAnalyzer_Analyze_WordShapes_ReadingOnWholeTokenOnly`,
`EnglishTextAnalyzer_Analyze_TagPlaceholders_KeepOffsetsAndReadings`,
`EnglishTextAnalyzer_Analyze_MultipleSentences_ReadingsMatchSingleSentenceAnalysis`,
`EnglishTextAnalyzer_Analyze_ConfidenceText_IsInvariantUnderTurkishAndFrenchCultures`,
`EnglishTextAnalyzer_Analyze_SingleCandidateForm_EmitsConfidenceOneOnlyWithModel`,
`EnglishTextAnalyzer_Analyze_ReadingOverride_BeatsClassifierAndNeedsNoModel`,
`EnglishTextAnalyzer_Analyze_ReadingOverrideWithoutReadingFamily_EmitsNothing`,
`EnglishTextAnalyzer_Analyze_MixedCorpus_ReadingNeverOverlapsReplacementOrPronunciation`,
`EnglishTextAnalyzer_Analyze_ReadingOnlySelection_EmitsReadingsWithoutPartOfSpeech`,
`EnglishTextAnalyzer_Analyze_ReadingNotSelected_EmitsNoReadings`,
`EnglishTextAnalyzer_Analyze_DefaultAnalyzer_EmitsEmbeddedModelReadings`,
`EnglishTextAnalyzer_Analyze_NoModelSource_EmitsNoReadings`,
`EnglishTextAnalyzer_Analyze_ModelWarning_OncePerAnalyzerOnlyForUnusableModel`,
`EnglishTextAnalyzer_Analyze_Cancellation_ThrowsAndReportsNothing`,
`EnglishTextAnalyzer_Analyze_ConcurrentCallers_GetIdenticalResults`

In `I want to record a record.` each `record` has, on exactly its own span, the part of speech, then `en.reading`, then
`en.reading-confidence` (`record.verb` then `record.noun` with the fixture model), the confidence text matches
`[01].ddd` and the starts are in text order. `A record-breaking year.` has no reading (a hyphenated compound is not a
form), while `RECORD`, a sentence-initial `Record` and the `record` of `The record's value fell.` each get one reading
on
the word's own token only; a placeholder between the words leaves both readings at their exact offsets; for two
sentences the readings equal those of the two sentences analyzed separately. Under `tr-TR`, `fr-FR` and `ar-SA` the
confidence text keeps a period and three ASCII digits. A form with exactly one candidate gets `1.000` and one
`single-candidate` in the heteronym event when a model is available, and no reading from an analyzer whose provider has
no model source. A `Reading` override of `record` to `record.noun` gives both words that reading with `1.000`, over the
fixture model and the embedded model, the event reporting `2 overrides applied` and `0 classified, 0 single-candidate, 2
by override`; without the reading family the same override emits nothing. Over a corpus mixing titles, currency, units,
times, initialisms and say, spell, pronounce and skip overrides on `wound`, `lead`, `tear` and `close`, no reading
overlaps an `en.norm`, `en.spell` or `en.pron` annotation, each reading has its confidence on the same span, and some
readings are still classified. With `Reading` alone the text is tagged for context, readings are emitted and no
`en.upos` is; with `Normalization` only none is emitted. The default analyzer emits `record.verb` and `record.noun` for
`I want to record a record.` from the embedded model, also with normalization and readings selected (then without a part
of speech), and, over a later request, the readings a fresh analyzer gives, none for an empty text or a text without a
heteronym word; an analyzer with no model source emits no reading kind. A corrupt model gives no reading and exactly one
Warning-level `TextEn.Model` event over two requests, a model for a newer inventory the same, and an absent model no
warning. In a text with three candidate words, a token canceled before the call or by the classifier observer after the
first, second or third classification makes the request throw and report nothing. Thirty-two concurrent requests
return the annotations of a sequential request.

##### Diagnostics supplied by the analyzer

**Tests**: `EnglishTextAnalyzer_Analyze_NormalizedText_ReportsNormalizationAndAmbiguityCounts`,
`EnglishTextAnalyzer_Analyze_Diagnostics_HeteronymEventIsCountsOnlyAndAtMostOnce`,
`EnglishTextAnalyzer_Analyze_LowConfidenceThreshold_CountsClassifiedReadingsBelowIt`

The event for `Dr. Smith & Jones said They St. said so.` reports one sentence, three normalizations, one ambiguous
reading (the inconclusive `They St.`), no numeric fallback and zero overrides applied and ignored in the fixed message
template. Two requests (one with a heteronym word) under `ar-SA` give two `TextEn.Analysis` events and one Info-level
`TextEn.Heteronym` event that matches the fixed template with digits and a decimal period only and names no word or
reading id. The threshold theory (0, 0.6 and 1) emits both readings every time and reports `0`, `0` and `2` readings
below the confidence, with the threshold written with two decimals. The reporter's own failure containment, a throwing
or
missing sink and the message templates are verified by the `AnalysisDiagnostics` unit tests.
