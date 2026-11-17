# SpeechTextEn System Verification Design

This document describes the system-level verification strategy for the SpeechTextEn library.

## Verification Approach

SpeechTextEn is verified at system level through its public API, exactly as a model package and a host use
it: the tests construct `EnglishTextAnalyzer` (with default and with explicit options), call the Speech
library's `ITextAnalyzer.Analyze` on it, and read the results with the library's own annotation reader. The
end-to-end tests go one step further and drive the real Speech `SynthesisSession`: a small test model
supplies an `EnglishTextAnalyzer` through `ISynthesisModel.TextAnalyzer` and a recording
`IAnnotatedSynthesisBackend`, the real `SpeechSynthesizerFactory` loads the engine, and the tests assert
what the backend actually receives - which proves the analyzer works across the real seam, that its
annotations survive chunking of a long text, that its sentence breaks (not the punctuation heuristic) decide
where sentences are split, and that audio tags inside a request leave the words around them annotated.

Below the system level, the unit and subsystem behavior is verified by the AnalysisSubsystem tests (see
*SpeechTextEn AnalysisSubsystem Verification*), the HeteronymSubsystem tests (see *SpeechTextEn HeteronymSubsystem
Verification*), the NormalizationSubsystem tests (see *SpeechTextEn
NormalizationSubsystem Verification*) and the TrainingDataSubsystem tests (see *SpeechTextEn
TrainingDataSubsystem Verification*), and the part-of-speech package the analysis builds on is
verified separately as an OTS item (see *PartOfSpeech Verification*). The system tests of the normalization
check its observable result: the `en.norm` annotations a caller reads, the text they produce when applied by
an independent applier that follows the documented consumer algorithm, the sentence breaks, and what a real
session delivers to a backend after chunking.

The system tests of the pronunciation overrides use the sample dictionary file
`TestData/overrides-dictionary.tsv` (the sample of the user guide plus the entries of the golden rows), loaded
through a `StreamReader` as a host would. They check the observable result of an analysis with a dictionary: the
`en.norm`, `en.spell` and `en.pron` annotations a caller reads through the typed readers
`TryReadReplacement`, `TryReadSpellOut` and `TryReadPronunciation`, the text produced by a consumer that follows the
documented algorithm using only those public readers, the precedence of a user override over the built-in rules,
the diagnostics counts, concurrent use of one shared analyzer, and what a real session delivers to a backend after
chunking. The same statements are checked on hostile random text by property tests (the fuzz test of the
alignment contract of the public analyzer here, and the unit and subsystem property tests below this level).
Verifying that a phoneme string is correct is out of scope: only the syntax of a pronunciation is verified, and
the backends own their phoneme inventories and the decision whether and how to speak a pronunciation.

The system tests of the heteronym readings check the observable behavior of the reading family through the public
API: that the default analyzer, which embeds no trained model, emits no reading annotation, that a `Reading` override
fixes the reading with no model at all and beats a classifier, that a real session delivers reading and confidence
annotations to an annotation-aware backend, re-based into whichever chunk holds the word, and that the heteronym
diagnostics are counts only. No trained model ships in this release, so the classified readings are verified with a
hand-built fixture model that the test project produces by running the real tagger, the real feature extractor and the
real model writer on a few labeled example sentences (no binary model is committed); it exercises the real format, the
real loader and the real classifier, but it does not measure the accuracy of any real model, which is not claimed. The
embedded inventory tables are verified by the project-file test and the subsystem tests.

Automated coverage does not extend to the accuracy of the tagger beyond a few unambiguous golden sentences:
the analyzer relays the tagger's decisions, so the tests assert structure, offsets and wiring and use only
sentences whose tags the tagger assigns with full confidence. The accuracy of the abbreviation readings is
claimed only for the documented cases (the golden file records the known wrong-by-default readings
`Wall St. Journal` and `Elm St. Louis` explicitly); the numeric readings are claimed only for the documented
en-US policy (the golden file marks readings that are wrong by default, such as `1500 stars` without a cue),
and URLs, e-mail, markup, Roman numerals, hyphenated digit compounds, prefix currency codes and the hyphen
policy are deferred and are verified only as unchanged text. The documented limitations of the overrides (a
multi-word entry or a key with a trailing period never matches, an override on the tail token of a split word or
on a word consumed by a number rule is not applied) are not defects: they are pinned by rows of the golden file and
by unit tests, so a change of the tokenizer or of the number rules cannot silently change them.

System and unit tests reside in the `test/DemaConsulting.Speech.Text.En.Tests` project. The training-data tooling
(the non-shipped trainer) has its own test project, `test/DemaConsulting.Speech.Text.En.Trainer.Tests`, which holds the
system-level scenarios of the training-data requirements and the tests of the TrainingDataSubsystem (see *SpeechTextEn
TrainingDataSubsystem Verification*).

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: no external services, no network access, no native runtime and no audio hardware; the
  part-of-speech package's embedded model, the library's own embedded tables (the two lexicons and the reading
  inventory) and the sample
  pronunciation dictionary file `TestData/overrides-dictionary.tsv` (copied next to the test assembly) are the only
  data used; the heteronym tests build their model in memory; the two packaging tests start the `dotnet` command line
  tool to evaluate the package project (a local evaluation, no build and no network; they are skipped when the tool
  cannot be started)
- **Isolation**: each end-to-end test uses a fresh scratch model directory and its own recording backend

## External Interface Simulation

The Speech library is used for real. The model and the backend are small test doubles that implement the
library's public `ISynthesisModel` and `IAnnotatedSynthesisBackend` interfaces and record what they
receive; a recording `ISpeechDiagnostics` sink (optionally throwing) simulates a host sink.

## System-Level Test Scenarios

### Annotation and Reading

**Tests**: `SpeechTextEn_Analyze_Sentence_AnnotatesWordsWithReadablePartOfSpeech`,
`SpeechTextEn_Analyze_PartOfSpeechDisabled_ProducesNoAnnotations`

Verifies that a sentence gets one readable part-of-speech annotation per word and none on its final period,
and that selecting no annotations produces none.

### Sentence Breaks

**Tests**: `SpeechTextEn_Analyze_MultipleSentencesAndTag_ReturnsAuthoritativeBreaks`,
`SpeechTextEn_SynthesisSession_AnalyzerSentenceBreaks_DriveChunkBoundaries`

Verifies that the breaks are non-null and sit at the first word of each later sentence, not inside
"Dr. Smith", and that through the real session the text is split exactly there, whereas the same text
without the analyzer is split after "Dr.".

### Placeholders

**Tests**: `SpeechTextEn_Analyze_TagPlaceholder_IsNeverCoveredByAnAnnotation`,
`SpeechTextEn_SynthesisSession_TextWithAudioTag_AnnotatesWordsAroundTheTag`

Verifies that no annotation covers a placeholder, and that a request such as "I've [whispers] read it."
delivers annotated words on both sides of the tag to the backend with no placeholder in any received text.

### Contract: Concurrency, Cancellation and End-to-End Delivery

**Tests**: `SpeechTextEn_Analyze_ConcurrentRequestsOnSharedAnalyzer_ReturnIdenticalResults`,
`SpeechTextEn_Analyze_CanceledToken_ThrowsOperationCanceledException`,
`SpeechTextEn_SynthesisSession_AnnotatedBackend_ReceivesPartOfSpeechAnnotationsThatSurviveChunking`

Verifies that one shared analyzer gives identical results to many concurrent requests, that a canceled token
throws `OperationCanceledException`, and that a long, multi-sentence request (several times the chunk
length) reaches an annotation-aware backend as several chunks, each carrying part-of-speech annotations on
words of that chunk, with exactly the annotations the analyzer produced.

### Diagnostics

**Tests**: `SpeechTextEn_Diagnostics_AnalyzedRequest_ReportsCountsOnlyAndSinkFailureIsContained`,
`SpeechTextEn_Diagnostics_NumericCounts_AreReportedAsCountsOnly`

Verifies that one event in the `TextEn.Analysis` category reaches the sink with counts only (sentences,
tokens, annotations, unknown words, normalizations, ambiguous readings, numeric fallbacks, overrides applied and
overrides ignored, in the message `... N normalizations, X ambiguous readings, F numeric fallbacks, P overrides
applied, I overrides ignored.`) and none of the words of the
text, and that an analyzer whose sink throws still returns its full result. The numeric test analyzes a text
with a bare year, a month-first date and an oversized digit run and asserts the exact ambiguity and fallback
counts in the message and that no digit or spoken word of the text appears in it.

### Normalization Annotations

**Tests**: `SpeechTextEn_Analyze_Abbreviation_AnnotatesSpokenFormWithoutChangingText`,
`SpeechTextEn_Analyze_NormalizationNotSelected_ProducesNoNormAnnotations`

Verifies that `Dr. Smith lives here.` yields exactly one `en.norm` annotation over the exact characters
`Dr.`, which the typed reader returns as `Doctor`, that applying it gives `Doctor Smith lives here.` while the
analyzed text is unchanged and no unchanged word is annotated, that selecting part of speech only yields no
`en.norm` annotation, and that selecting normalization only yields only `en.norm` annotations and never
creates the tagger.

### Abbreviations

**Test**: `SpeechTextEn_Analyze_ContextDependentAbbreviations_ResolveFromNeighbors` (a theory)

Verifies, for seven texts, that `St.`, `Dr.`, `Sr.` and `No.` are resolved from their neighbors: `Visit St.
Louis` reads Saint and `Main St. It` reads Street, `Dr. Smith` reads Doctor and `Maple Dr. He` reads Drive,
`Sr. Maria` reads Sister and `Smith Sr. He` reads Senior, and `No. 5` reads Number; each annotation covers
the expected source characters (the period stays outside the span where it ends a sentence).

### Sentence Boundaries

**Tests**: `SpeechTextEn_Analyze_AbbreviationAtSentenceEnd_BreaksAfterItAndKeepsThePeriod`,
`SpeechTextEn_SynthesisSession_AbbreviationBreaks_DriveChunkBoundaries`,
`SpeechTextEn_Analyze_CredentialsAfterNameSuffix_StayInOneSentence`,
`SpeechTextEn_SynthesisSession_NameWithCredentials_IsOneChunk`

Verifies that in `I live on Main St. It is nice. Dr. Smith agrees. No. I don't know.` the breaks sit exactly
before `It`, `Dr.`, `No.` and the following `I` (a break added after a sentence-final abbreviation, none
inside `Dr. Smith`), and that through the real session the text reaches the backend as the chunks `I live on
Main St.`, `It is nice.` and `Dr. Smith agrees.`, with the `en.norm` annotations on `St` and `Dr.` inside the
chunk that contains them. Also verifies that `John Smith Jr. Ph.D. spoke.` and `Ann Lee Esq. Ph.D. M.D.
left.` stay one sentence each (breaks only before the next sentence and before the sentence starters `I`
and `He` of `I met John Smith Jr. He left.`), with `Junior`, `Esquire`, `P H D` and `M D` as the spoken forms,
and that the first reaches the real session's backend as one chunk annotated `Jr.` to `Junior` and `Ph.D.`
to `P H D` (the latter an `en.spell` annotation).

### Symbols, Apostrophes, Invisible Characters and Clitics

**Tests**: `SpeechTextEn_Analyze_Symbols_AreSpokenAsWords`,
`SpeechTextEn_Analyze_AdjacentMarks_AreSpokenAsSeparateWords`,
`SpeechTextEn_Analyze_TypographicApostrophes_AreCanonicalized`,
`SpeechTextEn_Analyze_InvisibleCharacters_AreDroppedExplicitly`,
`SpeechTextEn_AnalyzeDetailed_Clitics_RecordTheirRole`

Verifies that applying the annotations speaks `&`, `=`, `@`, a slash between words, the copyright sign, `w/o`
and `AT&T` as words; that adjacent marks (`A™®B`, `©™®`) are spoken as separate words with one space between
them and one annotation per mark; that typographic apostrophes in contractions and elisions become plain
apostrophes while
quote marks stay; that a soft hyphen, a zero-width space and a byte-order mark are annotated with an empty
value (read as "speak nothing") rather than lost silently; and that the detailed analysis records `Has`,
`Possessive` and `Not` for the clitics of a sample sentence.

### Acronyms and Initialisms

**Test**: `SpeechTextEn_Analyze_AcronymsAndInitialisms_AreCaseSensitive`

Verifies that `US`, `IT`, `FBI` and `U.S.` are spoken letter by letter while the pronoun `us` and the word
`NASA` are untouched.

### Alignment Contract and Chunk Survival

**Tests**: `SpeechTextEn_Analyze_NormalizedText_AnnotationsSatisfyAlignmentContract`,
`SpeechTextEn_SynthesisSession_NormalizedText_AnnotationsSurviveChunking`,
`SpeechTextEn_SynthesisSession_NormalizedTextWithAudioTag_KeepsBothAnnotations`,
`SpeechTextEn_SynthesisSession_SymbolOnlyText_KeepsItsNormalization`,
`SpeechTextEn_SynthesisSession_SymbolBeforeClausePunctuation_KeepsItsNormalization`,
`SpeechTextEn_SynthesisSession_InvisibleOnlyClause_KeepsDroppedAnnotationWithItsNeighbor`,
`SpeechTextEn_SynthesisSession_InvisibleOnlyClauseWithPartOfSpeech_IsMergedIntoItsNeighbor`,
`SpeechTextEn_SynthesisSession_BareSymbolClauseWithPartOfSpeech_IsMergedIntoItsNeighbor`

Verifies that for 200 random, deliberately hostile texts every replacement annotation (`en.norm` or `en.spell`) is
ordered, disjoint, in bounds, free of whitespace edges, placeholders, clause separators and cut surrogate pairs, and
that nothing outside the annotations is changed; that a text of more than 800 characters mixing every rule reaches a
real session's backend as several chunks whose `en.norm` annotations equal, in order, the analyzer's own (same source
characters and values, nothing lost or moved); and that an audio tag between an abbreviation and the name it belongs to
leaves both `Dr.` and `&` annotated. Also verifies that a symbol-only text (`&`) reaches the backend as one chunk
annotated `&` to `and`, that `©; Brand` reaches it as the chunks `©;` (annotated `©` to `copyright`) and `Brand` instead
of losing the symbol, and that for a normalization-only analysis the invisible-only clause of
`Hello, <zero-width space>, there` is still merged into the first chunk with its empty-valued annotation, also with the
default analyzer (the part-of-speech tagger labels a zero-width space or a lone `*` between commas as a word, but the
analyzer gives a token without a letter or digit no part of speech, so such a clause is not mistaken for content), and
`Hello, *, there` is likewise two chunks.

### Numbers

**Tests**: `SpeechTextEn_Analyze_Numbers_AreSpokenAsWordsWithoutChangingText`,
`SpeechTextEn_Analyze_NumericSentenceBreaks_FollowTheSentenceEnd`

Verifies, through the public analyzer, that cardinals, thousands-separated numbers, negatives, decimals,
ordinals, percentages, fractions, years, decades, ranges and phone numbers (for example `1,234`, `-5`,
`3.5`, `22nd`, `50%`, `3 1/2`, `in 1999`, `1990s`, `5-10`, `555-1234`) get `en.norm` annotations whose values,
read with the typed reader, contain only letters, spaces and the apostrophe and no digit, that applying the
annotations with the independent applier gives the expected spoken sentence, and that the analyzed text is
unchanged and no annotation covers an unchanged word. The second test verifies that the sentence breaks
follow the sentence end around numbers (`Item 5. Then item 6.` breaks before `Then`, `It is 3.5 miles.` has
no break inside `3.5`, `$1.25. Then $2.` breaks once).

### Currency and Units

**Test**: `SpeechTextEn_Analyze_CurrencyAndUnits_AreSpokenAsWords`

Verifies `$5.50`, `$1`, `€5`, `£0.50`, `¥500`, `$5 million`, `$5M`, `5 USD`, `50 %`, `5 kg`, `1 kg`, `72°F` and
`6 ft.` (a period consumed mid-sentence) spoken as `five dollars and fifty cents`, `one dollar`, `five euros`,
`fifty pence`, `five hundred yen`, `five` with `million dollars`, `five million dollars`, `five` with
`dollars`, `fifty percent`, `five` with `kilograms`, `one` with `kilogram`, `seventy two degrees Fahrenheit`
and `six` with `feet`, and that a text without a unit table entry (`5 m`) is unchanged.

### Dates and Times

**Tests**: `SpeechTextEn_Analyze_DatesAndTimes_AreSpokenAsWords`,
`SpeechTextEn_Analyze_MonthAndWeekdayAbbreviations_ResolveFromDateContext`,
`SpeechTextEn_Analyze_FullMonthPeriod_EndsTheSentenceAndGivesNoDateContext`

Verifies `5:30pm`, `17:00`, `12/25/2024`, `2024-12-25` and `January 5, 2024` spoken as `five thirty P M`,
`seventeen hundred`, `December twenty fifth twenty twenty four` (twice) and `January fifth, twenty twenty
four` (the month name and the comma untouched), and that `Jan. 5, 2024`, `Sat. 5 Jan. 2024` and `Sept. 11,
2001 was a Tuesday.` read their month and weekday abbreviations without a sentence break inside the date,
while `He left Mon. He came Tue.` keeps both sentence ends and a bare `Mar.` or `Sun.` is left as written.
In `We finished in May. 5 people stayed. Born Jan. 5, 2024.` the period after `May` keeps its sentence end and
`5` is read as `five`, while `Jan. 5, 2024` stays one date (`January fifth, twenty twenty four`).

### Numeric Ambiguity and Limits

**Tests**: `SpeechTextEn_Analyze_AmbiguousNumbers_UseDocumentedDefaultsAndAreCounted`,
`SpeechTextEn_Analyze_OversizedNumbers_AreBoundedAndCounted`

Verifies that a bare `1999` is read as a year and a bare `1066` as a cardinal, that `3/4/2024` is read month
first and `7/16` as a fraction, each with the documented default and one counted ambiguity (detailed counts
through the diagnostics sink), and that `about 1500 people`, `in 1999` and `12/25/2024` are not counted. For
limits, a 16-digit run is read digit by digit, a 65-digit run, a 129-character token and `1,23` are left as
written, each counted as one numeric fallback, and no spoken value exceeds 1,024 characters.

### Numeric Alignment Contract and Chunk Survival

**Tests**: `SpeechTextEn_Analyze_NumericText_AnnotationsSatisfyAlignmentContract`,
`SpeechTextEn_SynthesisSession_NumericText_AnnotationsSurviveChunking`,
`SpeechTextEn_SynthesisSession_DateAcrossClauseCut_KeepsEveryTokenAnnotation`,
`SpeechTextEn_SynthesisSession_PercentSymbolChunk_KeepsItsNormalization`,
`SpeechTextEn_SynthesisSession_NumbersAtBudgetCut_AreNeverSplit`,
`SpeechTextEn_Analyze_NumberNextToAudioTag_KeepsBothAnnotations`

Verifies that for random, deliberately hostile numeric texts every `en.norm` annotation satisfies the amended
alignment contract (no whitespace, no semicolon, a comma or colon only between two digits, no break strictly
inside a span, no digit in a numeric value), that a long text mixing every numeric form reaches a real session's
backend as several chunks whose `en.norm` annotations equal, in order, the analyzer's own, that
`January 5, 2024, then left` and `$1,234.50, $5 million; 5:30pm: 50 %` keep every per-token annotation (the `5:30`
words span as `en.norm` and its `pm` letters as a delivered `en.spell` span) although
the clause cuts fall between the tokens of the date, that a chunk that consists only of `%` keeps its
annotation, that no annotation is split where the 200-character budget cut falls near a number (the
text is constructed so that the cut falls between `$5` and `million`), and that `5` followed by a tag and `kg`
keeps both annotations and covers no placeholder.

### Spelled Initialisms

**Tests**: `SpeechTextEn_Analyze_SpelledInitialisms_AreAnnotatedAsSpellOut`,
`SpeechTextEn_Analyze_AmpersandInitialisms_AreDecomposedIntoLetterSpans`,
`SpeechTextEn_Analyze_Meridiems_AreTypedSpellOutSpans`,
`SpeechTextEn_Analyze_SayOverrideWithSingleLetterPayload_IsNotSpelled`

Verifies that in `A. Smith met the FBI at 5 p.m. in the U.S. with J. Doe, Ph.D.` every spelled replacement (the
single-letter initials `A.` and `J.`, `FBI`, the meridiem `p.m.`, `U.S.` and the credential) is an `en.spell` annotation
whose value reads back through `TryReadSpellOut` as capital letters, that no `en.norm` annotation has the shape of a
letter sequence (so a backend never mistakes a lone `A` or `I` for a word), and that applying the annotations speaks
`A Smith met the F B I at five P M in the U S with J Doe, P H D.`. It also verifies that `AT&T's R&D and Q&A.` is
decomposed into letter-group spans of kind `en.spell` (`AT`, `T`, `R`, `D`, `Q`, `A`) and `&` spans of kind `en.norm`
with the value `" and "`, speaking `A T and T's R and D and Q and A.`, with the possessive tail outside every span. For
the meridiem, `at 5pm today`, `at 5PM today`, `at 5:30pm today`, `at 12:00am today` and `at 5 p.m. today` are each
annotated as exactly two non-overlapping spans in order, the number or time words as `en.norm` (`five`, `five thirty`,
`twelve`; the glued ones abut the meridiem span exactly) and the meridiem letters as `en.spell` (`P M` or `A M` with the
single separating space when glued, `P M` for the separate token), both readable through `TryReadReplacement` with
`isSpelled` false for the first and true for the second, and the applied text has exactly one space between the words.
Finally a `Say("foo", "I")` override (the control) is annotated `en.norm` with the value `I`, which `TryReadReplacement`
reports as not spelled and `TryReadSpellOut` refuses, proving `en.norm` carries no spell-out promise and a backend must
not infer spelling from its value.

### Pronunciation Overrides

**Tests**: `SpeechTextEn_Analyze_Overrides_AnnotateWholeWordsPerAction`,
`SpeechTextEn_Analyze_OverrideActions_ReadBackThroughTypedReaders`

Verifies, with the sample dictionary file and normalization only, that in a sentence that holds `Nginx`, a longer
word that merely starts with `nginx`, `GIF`, `Polish`, `SQL`, `NASA's` and `US` exactly four words are annotated,
each on its own characters and in text order: `Nginx` as an
`en.norm` annotation `engine ex`, `GIF` as an `en.spell` annotation `G I F`, `Polish` as an `en.pron` annotation
with the IPA phonemes of the sample, and `SQL` as an `en.norm` annotation `sequel`; the longer word that merely
starts with `nginx` (a different word), and the skipped `NASA` and `US`, get none, and the analyzed text is
unchanged. Through the `ITextAnalyzer`
interface and the default options, a consumer written only against `TryReadReplacement` and `TryReadPronunciation`
applies the replacements to `Nginx runs Kubernetes and kubernetes, a GIF and Polish food.` and must obtain
`engine ex runs koo ber net eez and kubernetes, a G I F and Polish food.` (the case-sensitive entry does not apply to
the lower-case word), with exactly one replacement flagged as spelled and exactly one pronunciation, on `Polish`.

### Override Precedence

**Tests**: `SpeechTextEn_Analyze_OverridePrecedence_UserOverridesBuiltInRules`,
`SpeechTextEn_Analyze_OverrideOnConsumedToken_IsIgnoredAndCounted`,
`SpeechTextEn_Analyze_OverrideOnAbbreviationWord_KeepsSentenceBreaksAndChunks`

Verifies, for seven texts analyzed with and without a one-entry dictionary, that a user override wins over the
built-in rule for its word and that the built-in reading returns without it: a skip entry keeps `US`, `AT&T` and
`y’all` as written (otherwise `U S`, `A T and T` and `y'all`), say entries give `sequel` and `see eye ay` instead of
`S Q L` and `C I A` and `mister` instead of `Mister`, and a spell entry for `FBI` agrees with the built-in rule. A
case-sensitive entry for `Polish` is not applied to `polish`. The second test verifies that an override on a word
that a number or currency match consumed (`kg` in `5 kg`, `million` in `$3 million`) is ignored, the built-in reading
`five kilograms` and `three million dollars` stands, the diagnostics event ends `0 overrides applied, 2 overrides
ignored.`, while the same words on their own are overridden (`The kay gee and the mill yon.`, ending `2 overrides
applied, 0 overrides ignored.`). The third test verifies, through the public analyzer and the real chunker, that
`Say("Fig", "figure")` on `See Fig. 5 for details.` leaves no sentence break, annotates `Fig` as `figure` and yields
exactly one chunk, and that `Skip("Dr")` on `Hello—Dr. Smith left.` keeps the breaks of the analyzer without a
dictionary (none) and one chunk.

### Override Dictionary

**Tests**: `SpeechTextEn_Overrides_SampleDictionaryFile_LoadsAndDrivesAnalysis`,
`SpeechTextEn_Overrides_SharedAnalyzerConcurrentRequests_ReturnIdenticalResults`

Verifies that the sample dictionary file loads through a `StreamReader` into 22 entries and drives an analysis end to
end: `Wi-Fi and Nginx-based Kubernetes apps; Cafe,` followed by the Turkish place name with a dotted capital I, speaks
as `why fie and engine ex based koo ber net eez apps; Cafe,` followed by that name in lower case (the sample entry for
it says `istanbul`), with one `arpabet:K AE0 F EY1` pronunciation on `Cafe`. It also verifies that one analyzer with one
dictionary, used by 32 concurrent workers that each analyze 20 of five different texts, returns for every request
exactly the result (annotations and breaks) that a sequential analysis of the same text returned.

### Override Alignment Contract and Chunk Survival

**Tests**: `SpeechTextEn_Analyze_Overrides_AnnotationsSatisfyAlignmentContract`,
`SpeechTextEn_SynthesisSession_OverrideAnnotations_SurviveChunking`,
`SpeechTextEn_SynthesisSession_OverridesAroundAudioTag_KeepEveryAnnotation`

Verifies, for 300 seeded random hostile texts analyzed with the sample dictionary (one analyzer with normalization
only, and every fifth text with the default options), that both entry points and a repeated call agree, that every
replacement annotation (`en.norm` or `en.spell`) is readable through `TryReadReplacement` with a spelled flag that
matches its kind, ascending, disjoint and in bounds, with whitespace-free edges, no clause separator inside, and no
placeholder in its characters or value, that every annotation other than the part of speech is disjoint from the
others, and that every pronunciation reads back and holds no whitespace or clause separator. Through a real
synthesis session, a text of more than 800 characters repeating a paragraph that mixes every override action,
built-in rules, clause cuts and a budget cut is delivered in several backend calls whose replacement, spelling and
pronunciation annotations equal, in order and on the same source characters, those of the analyzer (more than 60,
including both kinds), so none is lost, duplicated or moved. A request `Nginx [whispers] SQL, Polish [laughs] GIF
done.` delivers the four override annotations (`engine ex`, `sequel`, the IPA pronunciation and `G I F`), in order,
from both sides of the two audio tags.

### Override Diagnostics

**Test**: `SpeechTextEn_Diagnostics_Overrides_ReportCountsOnlyAndNeverDictionaryContent`

Verifies that a dictionary of sentinel keys, a sentinel payload, a phoneme string and the skipped unit `kg`, applied
to a text that uses three of them and a unit after a number, produces one `TextEn.Analysis` event that matches the
fixed template with `3 overrides applied, 1 overrides ignored`, and that the message contains none of the
sentinel words, payloads, scheme names, phonemes or words of the analyzed text.

### Heteronym Readings

**Tests**: `SpeechTextEn_Analyze_DefaultAnalyzerWithoutModel_EmitsNoReadingAnnotations`,
`SpeechTextEn_Analyze_ReadingsOnSharedAnalyzerConcurrentRequests_ReturnIdenticalResults`,
`SpeechTextEn_Analyze_ReadingOverride_BeatsClassifierAndNeedsNoModel`,
`SpeechTextEn_SynthesisSession_AnnotatedBackend_ReceivesReadingsThatSurviveChunking`

Verifies that the default analyzer (the public constructor, which embeds no model) analyzes `I want to record a record.`
with only part-of-speech annotations, so a host that does nothing sees no change. It verifies that one analyzer with a
fixture model, shared by thirty-two concurrent requests that race for the model, gives every request the readings and
confidences of a sequential request (the readings are present in that result); when the model is opened and that it is
opened once are design choices, not asserted. It verifies that a reading entry
`record` / `record.noun`, loaded from the tab-separated format, gives both `record` words the reading `record.noun` with
the confidence `1.000`, readable through `TryReadReading`, both with an analyzer that has no model and with one that has
a fixture model that would otherwise choose the verb for the first word, so the override beats the classifier and needs
no model. It verifies, through a real synthesis session, that a text of ten repeated sentences (more than three chunk
lengths) delivers to an annotation-aware backend in several calls exactly the readings the analyzer produced (twenty in
all), each reading read through `TryReadReading` on exactly the characters `record` of its chunk and with its
confidence annotation, read through `TryReadReadingConfidence`, on the same start and length, so no reading is lost,
duplicated or moved by the chunking.

### Heteronym Diagnostics

**Test**: `SpeechTextEn_Diagnostics_Readings_AreCountsOnly`

Verifies that an analysis of `I want to record a record.` with a fixture model reports two events to the sink: the
`TextEn.Analysis` event with its unchanged wording (the exact template ending `overrides ignored.`) and one
`TextEn.Heteronym` event of the form `Assigned 2 heteronym readings: 2 classified, 0 single-candidate, 0 by override,
{n} below confidence 0.60.`; and that no message contains the word `record` or the reading name `noun`.

### Large Input and Cancellation

**Tests**: `SpeechTextEn_Analyze_LargeRepetitiveTexts_AnnotatesEveryRepetitionAlike`,
`SpeechTextEn_Analyze_LargeRepetitiveNumericTexts_AnnotatesEveryRepetitionAlike`,
`SpeechTextEn_Analyze_CanceledToken_ThrowsOperationCanceledException`

Verifies, with normalization only (so no tagger is involved), through the public API, that one megabyte of realistic
text and of adversarially repeated `St.`, `A.`, `&` and `Dr. He` (each followed by a space) is annotated and broken
correctly, and that one megabyte of each repetitive numeric unit (`1,234.50`, `$5.50`, `5:30pm`, `January 5, 2024`,
`1/2`, `12/25/2024`, `5 kg`, `50 %`, `Jan. 5`, `5 ft.`, `5 million USD`, `555-123-4567`, `1990s`, `3 1/2`, `1999`) does
the same. Correctness is asserted as exact output: for each unit a short reference text of nine repetitions is analyzed,
and every repetition of the large text away from its two ends must carry exactly the annotations (position, length, kind
and value) and the sentence breaks of the same repetition in the reference, with the same count. No elapsed time is
asserted: how the scan is kept linear is recorded in the normalization subsystem design, and a hang guard (a ten-minute
cancellation) only ends a test that would never finish. A token canceled beforehand makes the analyzer throw
`OperationCanceledException`; cancellation requested during processing is verified at the unit and subsystem level.

### Package Files

**Tests**: `SpeechTextEn_PackageLicense_ProjectFile_NamesLicenseFileAtPackageRoot`,
`SpeechTextEn_PackageLicense_File_DeclaresMitOnlyAndPointsToDependencyNotice`,
`SpeechTextEn_ThirdPartyNotices_File_AttributesDependencyData`,
`SpeechTextEn_Dependencies_EvaluatedReferences_PinTaggerExactlyAndReferenceNoInferenceRuntime`,
`SpeechTextEn_PackageVersion_EvaluatedProject_PacksOnlyPrereleaseWhileTaggerIsPrerelease`,
`SpeechTextEn_Analyze_ReadingInventoryInPackage_ValidatesReadingOverridesOfflineAndShipsNoModel`,
`TextEn_PackageLicenseFiles_Content`

Verifies that the project file names `PACKAGE-LICENSE.md` as the license file (and no license expression) and packs both
it and `THIRD-PARTY-NOTICES.md` at the package root; that the license file starts with the SPDX line `MIT` and nothing
else, contains the MIT license text, describes the tagging package as a separate dependency that is not bundled and
carries its own license files, and states that this package embeds two small lexicon tables of its own, written from
scratch by DEMA Consulting under the same license, and no third-party data; that the notices are scoped as a dependency
notice, state that this package embeds no third-party data in this release, and reproduce the Manually Annotated
Sub-Corpus and Open English WordNet attribution and the Princeton WordNet notice.

The dependency and packaging tests do not read the text of the project file: they ask the dotnet command line tool to
evaluate it (`dotnet msbuild <project> -getItem:PackageReference -getItem:ProjectReference` and
`-getProperty:IsPackable`, no build is run). The tool is found through `DOTNET_HOST_PATH` or the `dotnet` command, and a
test is skipped when the tool cannot be started. The dependency test verifies that the evaluated package references pin
the tagging package with an exact version range (`[x.y.z]`), that the only evaluated project reference is the Speech
project, and that no package reference names ONNX. The packaging test verifies, for as long as the pinned tagging
package is a prerelease, that the project is not packable for the stable package versions `1.0.0` and `1.0.0+build.5`
and for `Version=1.0.0`, and is packable for `1.0.0-beta.1`, `1.0.0-beta.1+build.5`, `0.0.1-test` (the version the build
script's pack verification uses) and `Version=1.0.0-rc.1`, that `PackageVersion` wins over `Version` in both directions,
and that every version is packable once the pin is a stable version (the temporary guard must then be removed). The
build script's pack verification complements it by checking that a package is actually produced; the test checks the
outcome and not how the project file spells the condition. The reading-inventory test verifies the inventory shipped
with the package through the public API: a reading entry `record` / `record.noun` is accepted (validated against the
inventory) and fixes both readings of `I want to record a record.` with no model, an entry for the undefined reading
`record.nonsense` is refused with `InvalidDataException`, and the default analyzer emits no reading, so the release
ships no model. That the tables are linked from the data folder under fixed resource names is a design matter. The last
test, `TextEn_PackageLicenseFiles_Content`, is a FileAssert test over the **built package**, not a repository file: when
a `.nupkg` was built (only for a prerelease version) it contains `PACKAGE-LICENSE.md` (starting with the line
`SPDX-License-Identifier: MIT` followed by the license heading, and naming `THIRD-PARTY-NOTICES.md`) and
`THIRD-PARTY-NOTICES.md` (the dependency notice with its attribution) at the package root, and its `.nuspec` references
`PACKAGE-LICENSE.md` as the license file; for a stable version no package is built, and the test accepts the absence of
a package (it rejects more than one).

### Request Noninterference

**Tests**: `SpeechTextEn_Analyze_AfterAnotherRequest_ResultAndDiagnosticsDiscloseNothingOfIt`,
`SpeechTextEn_Analyze_ConcurrentRequestsWithDifferentTexts_EachGetsItsOwnResult`

Verifies the observable side of the rule that an analyzer must not retain request text: after an analyzer has analyzed
`Quokka marmoset rested. Zebra galloped.`, a later request for `I read it again.` gets exactly the result a fresh
analyzer gives, no annotation value carries a word of the earlier text, and the one diagnostics event of the later
request contains none of those words; and thirty-two concurrent requests with four different texts on one shared
analyzer each get the result their text gets alone. What these tests do not prove is that no reference to analyzed text
remains in memory. That is a design obligation, not a tested behavior: this library's own state keeps no text (see the
*EnglishTextAnalyzer* design), and the tagging package's per-thread working buffers are private, so
`DemaConsulting.PartOfSpeech` 0.1.0-beta.5 offers no public API or documented test hook that inspects them. This
repository therefore has no test that tags a sentinel and inspects the buffer, and no forced-collection test: that would
need reflection, a memory scan or garbage-collector timing, which are not acceptable evidence. The repository pins a
tagging package version that clears the buffers (see the dependency policy in the system design), the upstream fix's own
regression tests are cited by version in *PartOfSpeech Verification*, and the remaining gap (no repository-owned test of
the dependency's private state) is stated there for the repository owner's approval.

### Training Data

**Tests**: `TrainingDataSubsystem_CheckData_CommittedData_PassesAllChecks`,
`TrainingDataSubsystem_CheckData_SingleViolation_ExitsOneNamingTheCheck`,
`TrainingDataSubsystem_CheckData_ManifestDrift_NamesFileAndPassesAfterRegeneration`,
`TrainingDataSubsystem_CheckData_ExternalOverlap_IsReportedWhenSetsSupplied`,
`TrainingDataSubsystem_CheckData_NoEvaluationSets_PassesAndSaysSkipped`

These tests reside in `test/DemaConsulting.Speech.Text.En.Trainer.Tests` and are described in *SpeechTextEn
TrainingDataSubsystem Verification*. They verify the four training-data system requirements (open licenses, integrity,
evaluation isolation and provenance) through the one observable interface of the data tooling, the `check-data` command:
the real committed `data/text-en` folder passes every check, and each class of defect (a restricted license, a malformed
file, a changed file, a wrong label or span, a sentence, document or template shared between splits, a missing
attribution file, an evaluation-only file name, an overlap with an external evaluation set) written into a temporary
copy makes the command fail with a finding that names the check. The external comparison is verified only with synthetic
evaluation sets written by the tests: the real evaluation corpora are never committed and never used by an automated
test, so the comparison of the committed data with them is an operator task whose last result the data README records,
and the automated tests claim only that the check works and that, without evaluation sets, the run passes and says the
external check was skipped. The provenance records are checked by the unit tests of the schema and license checks, and
at system level by the committed-data run. The correctness of the labels themselves is not claimed by any automated
test.

## Acceptance Criteria

A SpeechTextEn system-level test run passes when all scenarios above pass without unexpected exceptions, every subsystem
and unit suite passes, and the automated verification boundary remains honest: structure, offsets, wiring, sentence
breaks, placeholder handling, diagnostics, the alignment contract of the `en.norm` and `en.spell` replacement
annotations and the `en.pron` pronunciation annotations (including their survival through real chunking, also for
numeric constructs whose tokens are separated by clause cuts and budget cuts), the documented abbreviation, symbol,
clitic, initialism and numeric behavior (the golden readings, the documented defaults and their counts), the behavior of
user pronunciation overrides (whole-word matching, one annotation per action on the exact source characters, precedence
over the built-in rules, counts only in the diagnostics, identical results under concurrent use, and the documented
limitations pinned by the golden rows), the spelled-initialism annotations, the heteronym reading behavior (no reading
annotation from a default analyzer, a `Reading` override that fixes the reading without a model and beats a classifier,
the survival of the reading and confidence annotations through real chunking, and the counts-only heteronym
diagnostics), the large-input output checks and the packaging configuration (evaluated through the dotnet command line
tool) are claimed as automated coverage, while the linguistic accuracy of the tagger is left to the tagging package's
own evaluation, the accuracy of a trained heteronym model is not claimed (none ships, and the fixture model only proves
the mechanics), the abbreviation readings are claimed only for the documented cases and cues, the numeric readings only
for the documented en-US policy, the correctness of the phonemes of a pronunciation override (only its syntax is
verified; the backends own their phoneme inventories) is not claimed, no elapsed-time, allocation, thread,
polling-cadence or object-lifetime behavior is claimed (those are design choices, not asserted by test), and the
clearing of the tagging package's private working buffers is evidenced by the upstream package's regression tests cited
by version, with the remaining gap (no repository-owned test of that private state) stated for the repository owner's
approval, not claimed as verified here. The package license files are also part of the acceptance criteria: both must be
present, the project must pack them at the package root, the license declaration must be MIT only, the license file must
describe the embedded tables and the absence of third-party data, and the dependency notice must keep the attribution
chain intact. The training-data scenarios pass when the real committed data passes `check-data` and each class of seeded
defect is reported by the check that owns it; the labels of the data are not claimed as verified.
