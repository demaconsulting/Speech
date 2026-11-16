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
_SpeechTextEn AnalysisSubsystem Verification_) and the NormalizationSubsystem tests (see _SpeechTextEn
NormalizationSubsystem Verification_), and the part-of-speech package the analysis builds on is
verified separately as an OTS item (see _PartOfSpeech Verification_). The system tests of the normalization
check its observable result: the `en.norm` annotations a caller reads, the text they produce when applied by
an independent applier that follows the documented consumer algorithm, the sentence breaks, and what a real
session delivers to a backend after chunking.

Automated coverage does not extend to the accuracy of the tagger beyond a few unambiguous golden sentences:
the analyzer relays the tagger's decisions, so the tests assert structure, offsets and wiring and use only
sentences whose tags the tagger assigns with full confidence. The accuracy of the abbreviation readings is
claimed only for the documented cases (the golden file records the known wrong-by-default readings
`Wall St. Journal` and `Elm St. Louis` explicitly); the numeric readings are claimed only for the documented
en-US policy (the golden file marks readings that are wrong by default, such as `1500 stars` without a cue),
and URLs, e-mail, markup, Roman numerals, hyphenated digit compounds, prefix currency codes and the hyphen
policy are deferred and are verified only as unchanged text.

System and unit tests reside in the `test/DemaConsulting.Speech.Text.En.Tests` project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: no external services, no network access, no native runtime and no audio hardware; the
  part-of-speech package's embedded model and the library's own two embedded lexicon tables are the only data
  used
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
tokens, annotations, unknown words, normalizations, ambiguous readings and numeric fallbacks, in the message
`... N normalizations, X ambiguous readings, F numeric fallbacks.`) and none of the words of the
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
to `P H D`.

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

Verifies that for 200 random, deliberately hostile texts every `en.norm` annotation is ordered, disjoint, in
bounds, free of whitespace edges, placeholders, clause separators and cut surrogate pairs, and that nothing
outside the annotations is changed; that a text of more than 800 characters mixing every rule reaches a real
session's backend as several chunks whose `en.norm` annotations equal, in order, the analyzer's own (same
source characters and values, nothing lost or moved); and that an audio tag between an abbreviation and the
name it belongs to leaves both `Dr.` and `&` annotated. Also verifies that a symbol-only text (`&`) reaches
the backend as one chunk annotated `&` to `and`, that `©; Brand` reaches it as the chunks `©;` (annotated
`©` to `copyright`) and `Brand` instead of losing the symbol, and that for a normalization-only analysis
the invisible-only clause of `Hello, <zero-width space>, there` is still merged into the first chunk with its
empty-valued annotation, also with the default analyzer (the part-of-speech tagger labels a zero-width space or
a lone `*` between commas as a word, but the analyzer gives a token without a letter or digit no part of
speech, so such a clause is not mistaken for content), and `Hello, *, there` is likewise two chunks.

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
`January 5, 2024, then left` and `$1,234.50, $5 million; 5:30pm: 50 %` keep every per-token annotation although
the clause cuts fall between the tokens of the date, that a chunk that consists only of `%` keeps its
annotation, that no annotation is split where the 200-character budget cut falls near a number (the
text is constructed so that the cut falls between `$5` and `million`), and that `5` followed by a tag and `kg`
keeps both annotations and covers no placeholder.

### Linear Time

**Tests**: `SpeechTextEn_Analyze_LargeRepetitiveTexts_CompleteWithinBudget`,
`SpeechTextEn_Analyze_LargeRepetitiveNumericTexts_CompleteWithinBudget`

Verifies, with normalization only (so no tagger is involved), that one megabyte of realistic text and of
adversarially repeated `St.`, `A.`, `&` and `Dr. He` (each followed by a space) analyzes within a generous time budget under a
cancellation safety limit, so a quadratic regression fails instead of hanging; and that one megabyte of each
repetitive numeric unit (`1,234.50`, `$5.50`, `5:30pm`, `January 5, 2024`, `1/2`, `12/25/2024`, `5 kg`,
`50 %`, `Jan. 5`, `5 ft.`) does the same.

### Package Files

**Tests**: `SpeechTextEn_PackageLicense_ProjectFile_NamesLicenseFileAtPackageRoot`,
`SpeechTextEn_PackageLicense_File_DeclaresMitOnlyAndPointsToDependencyNotice`,
`SpeechTextEn_ThirdPartyNotices_File_AttributesDependencyData`,
`SpeechTextEn_ProjectFile_Dependencies_PinTaggerExactlyAndReferenceNoInferenceRuntime`,
`SpeechTextEn_ProjectFile_Packaging_IsGatedUntilTaggerStopsRetainingText`,
`TextEn_PackageLicenseFiles_Content`

Verifies that the project file names `PACKAGE-LICENSE.md` as the license file (and no license expression)
and packs both it and `THIRD-PARTY-NOTICES.md` at the package root; that the license file starts with the
SPDX line `MIT` and nothing else, contains the MIT license text, describes the tagging package as a
separate dependency that is not bundled and carries its own license files, and states that this package
embeds two small lexicon tables of its own, written from scratch by DEMA Consulting under the same license,
and no third-party data; that the notices are scoped as a dependency notice, state that this package embeds
no third-party data in this release, reproduce the Manually Annotated Sub-Corpus
and Open English WordNet attribution and the Princeton WordNet notice, and record the dependency's
text-retention limitation with its upstream issue link; that the project pins the tagging package with
an exact version range, references only the Speech project, and references no ONNX package; and that the
project is not packable (`IsPackable` is `false`) and its comment names the release gate and the upstream
issue. The first five run in the test project against the repository files; the last is a FileAssert test
that confirms the content of the two license files in the repository (the license declaration is exactly
`MIT`, and the notices carry the attribution and the issue link).

**Pending while the package is gated**: the package is not packable, so no `.nupkg` exists and the built
package is not inspected. The former FileAssert assertion over the built package (both files at the package
root and the `.nuspec` license reference) is reinstated when packaging is re-enabled (see _SpeechTextEn
System Design_, _Release Gate_), together with the retention test required there.

## Acceptance Criteria

A SpeechTextEn system-level test run passes when all scenarios above pass without unexpected exceptions,
every subsystem and unit suite passes, and the automated verification boundary remains honest: structure,
offsets, wiring, sentence breaks, placeholder handling, diagnostics, the alignment contract of the `en.norm`
annotations (including their survival through real chunking, also for numeric constructs whose tokens are
separated by clause cuts and budget cuts), the documented abbreviation, symbol, clitic, initialism and
numeric behavior (the golden readings, the documented defaults and their counts), the linear-time guard and the packaging configuration are
claimed as automated coverage, while the linguistic accuracy of the tagger is left to the tagging package's
own evaluation, the abbreviation readings are claimed only for the documented cases and cues, the numeric
readings only for the documented en-US policy, and the
retention of analyzed words inside the tagging package's private buffer (a known
limitation of the dependency that violates the core `ITextAnalyzer` no-retention contract, and the reason
the package is gated) is documented rather than claimed as verified. The package license files are also
part of the acceptance criteria: both must be present, the project must pack them at the package root, the
license declaration must be MIT only, the license file must describe the two embedded lexicon tables and the
absence of third-party data, and the dependency notice must keep the attribution chain intact.
