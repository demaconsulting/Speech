## NormalizationSubsystem Verification

### Verification Approach

The SpeechTextEn NormalizationSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem` that need no network and no hardware. The
subsystem-level tests in `NormalizationSubsystemTests.cs` exercise the six units together (the alignment
model, the text normalizer, the numeric expression recognizer, the number verbalizer, the abbreviation
resolver and the acronym recognizer, with the real embedded
lexicons) exactly as the analyzer drives them, but without the analyzer, the tagger or the diagnostics reporter.

**Test-only dependency on the real tokenizer.** The production normalization code takes plain token offsets and
has no dependency on the `DemaConsulting.PartOfSpeech` package. To feed it the tokens it would receive in
production, the subsystem and unit tests use the helper `Fakes/NormalizationHarness.cs`, which calls the real
`Tokenizer.Tokenize` of the OTS item (no tagger is involved) and converts its sentences to the normalizer's
token array, marking the first token of each tokenizer sentence. This is a deliberate choice: the rules are
written against the tokenizer's real conventions (which of `Dr.`, `Ave.` and `Sen.` stay whole, where the
tokenizer breaks after a title), and a hand-built token list would test only the author's belief about them.
That the conventions hold is itself verified as an OTS item (see _PartOfSpeech Verification_). The harness
also supplies two things that must not share code with the production alignment model: an **independent
annotation applier** that sorts the `en.norm` annotations by start, copies the gaps and inserts each value,
following the documented consumer algorithm without using `AlignedText`, and the reader of
the golden file with its escape decoding.

**Golden file.** `TestData/normalization-cases.tsv` holds more than 80 rows with the columns `id`, `input`,
`spoken`, `sentences`, `ambiguous` and `note` (the escapes `\t \n \r \uXXXX \\` are supported, and the
sentences of the reconciled breaks are joined by the broken bar). A row states the input, the exact text
after the annotations are applied, the reconciled sentences and the number of ambiguous resolutions. It
covers every kind of rule: titles, ranks, titles without a period and their guard, name and address and business
suffixes, `St.` and `Dr.` and `Sr.` and `No.` both ways (including the documented wrong-by-default `Wall St.
Journal` and `Elm St. Louis` and the counted `Go see Jones St. Smith` and `They St. said so.`), Latin
abbreviations, slash forms, operators, marks, ampersand and dotted initialisms, case pairs such as `US` and
`us`, the shouting guard, dropped characters, clitic and elision apostrophes, an audio-tag placeholder, a
surrogate pair, CRLF and the empty and blank texts. The file is copied to the test output folder by the test
project.

**Numeric golden file.** `TestData/number-cases.tsv` holds about 510 rows with the columns `id`, `input`,
`spoken`, `sentences`, `ambiguous`, `fallbacks` and `note` (the same escapes; the harness accepts the older
six-column files, taking zero fallbacks). It is authored from the documented reading policy (see
_NumericExpressionRecognizer Design_), not generated from the implementation, and covers every row of the
policy tables (integers, signs, thousands separators and their invalid forms, decimals, ordinals and the
wrong-suffix cases, hash numbers, years with and without cues, decades, currency, percent, units and their
singular and plural and period forms, times and invalid times, numeric and month-name dates, month and weekday
abbreviations, ranges, phone numbers, dotted numbers, fractions and mixed numbers, the limit rows at 15, 16,
64 and 65 digits, 32 and 33 fraction digits and a 129-character token), every deferred form (Roman numerals,
hyphenated compounds, `1e5`, `0x1F`, non-ASCII digits, a mathematical digit, emoji next to a number), the
sentence-break rows (`3.5`, `$1.25. Then $2.`, `Item 5.`, `He left at 5 p.m. He left.`, `5 ft. tall`, `He is 6
ft. Then he left.`) and the tag-placeholder, no-break-space and CRLF rows. The month-date rows pin the
sentence-boundary rule (a period after `May` or a full month name ends the sentence and gives the next number
no date reading; a month abbreviation's period and a blank line between a day and a year are covered too) and
the en-dash rows (`range-en-dash-*`) pin the both-endpoints-read rule (`5–10`, `1,234–5,678`, `5:30–6:30`,
`5–10kg` read; `1–2D`, `1,23–5`, `5–25:99`, `5–10x` keep the dash). A row's `fallbacks` column is the
number of numeric fallbacks that were counted.

**Independent reference.** `Fakes/ReferenceNumberWords.cs` implements the words of cardinals, ordinals, year
pairs, clock times and day ordinals with a recursive decomposition over `ulong` values and vocabulary arrays of
its own, deliberately a different algorithm from `NumberVerbalizer`; it also supplies the closed spoken
vocabulary used by the property tests. It is shared by the verbalizer unit tests and the subsystem tests.

**Property tests.** The alignment invariants are checked on random hostile text with a fixed seed, so a
failure is reproducible, and again on random numeric text (the digits, separators, symbols, units, month names,
long digit runs, tag placeholders, surrogates, combining marks and whitespace variants of the numeric rules).
Time and cancellation are checked with generous budgets that a linear algorithm
meets and a quadratic one does not.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the test project
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Files**: the golden files `TestData/normalization-cases.tsv` and `TestData/number-cases.tsv`, copied to the
  output folder, and the two embedded lexicons of the library
- **Dependencies**: the real tokenizer of `DemaConsulting.PartOfSpeech` through the test helper
  `Fakes/NormalizationHarness.cs`; no network, no tagger, no audio hardware
- **Isolation**: every test builds its own inputs and results; the shared default lexicons are immutable;
  the time-budget tests use a 10-second budget and a 30-second cancellation safety limit

### Acceptance Criteria

The subsystem tests pass when: every row of the golden file produces the expected spoken text, reconciled
sentences and ambiguity count (a failure message names the row id), and the golden inputs contain every form
of the abbreviation lexicon except those that the unit tests cover; the listed sentences break only where a
sentence really ends (a sentence-final abbreviation ends its sentence and keeps its period, a title or `St.`
before a name does not); for 500 random hostile texts every alignment invariant holds (ordered, disjoint,
in-bounds, non-empty spans with whitespace-free edges, no placeholder, no cut surrogate pair, chunk-safe
content, empty values only for dropped invisible characters, separating spaces exactly where a letter or
digit touches, segments that tile the text, text outside the spans unchanged, an independent applier that
agrees with `AlignedText`, and valid break edits); concurrent and repeated normalization of one text gives
identical results; one megabyte of realistic and of adversarially repetitive text, and one-megabyte single
tokens of `&`, of a dropped character and of a typographic apostrophe, normalize within the time budget; no
span ever cuts a surrogate pair or a combining sequence; and a text with nothing to normalize yields no span
and no break edit.

The numeric behavior passes when: every row of the numeric golden file produces the expected spoken text,
sentences, ambiguity count and fallback count (a failure names the row id); the spoken value of every numeric
span contains only letters, spaces and the apostrophe of `o'clock`, never a digit, from the closed vocabulary
and within 1,024 characters; every year from 1000 to 2099, every `h:mm` time and every cardinal and ordinal
from 0 to 9999 (plus 10,000 seeded random values up to 15 digits) agree with the independent reference; for
random numeric hostile texts every alignment invariant holds including the amended chunk-safety rule (no
whitespace, no semicolon, a comma or colon only between two digits, only the four span shapes) and that no
reconciled break lies inside a span, and the real chunker keeps every annotation in exactly one chunk, for several
chunk budgets; the results are identical under foreign cultures and under concurrent requests; one megabyte of
every repetitive numeric unit and single huge numeric tokens normalize within the time budget; surrogate pairs,
supplementary-plane digits and combining marks next to numbers are never cut or read; and the existing
abbreviation golden rows whose digits are now spoken (`Number five`, `Figure three`, `five P M`) carry the
new expectations.

### Test Scenarios

#### Golden file

**Tests**: `NormalizationSubsystem_GoldenFile_EveryRow_MatchesExpectedSpokenTextSentencesAndAmbiguity`,
`NormalizationSubsystem_GoldenFile_Coverage_ContainsEveryLexiconForm`

The first test normalizes every row with the real tokenizer's tokens, applies the annotations with the
independent applier, reconciles the breaks, and collects every mismatch of spoken text, sentences or
ambiguity count so one run reports them all. The second reads the embedded abbreviation lexicon and asserts
that every form appears in some golden input (of either golden file, since the month and weekday forms are
exercised by the numeric file), except a fixed list of forms (for example `Mrs`, `Rev.`, `Ste.`,
`viz.`, `w/`) that the abbreviation-resolver unit tests cover; adding a lexicon row without a test fails it.
The file includes `cred-*` rows (a name suffix followed by a credential, `Sr.` read as Senior before a
credential, the tokenizer break between credentials suppressed, and the sentence-starter, business-suffix and
tag-placeholder cases that keep or ignore their break) and `sym-marks-*` rows for adjacent marks.

#### Sentence breaks

**Test**: `NormalizationSubsystem_SentenceBreaks_Abbreviations_BreakOnlyWhereTheSentenceEnds` (a theory)

Fifteen texts plus four credential texts, each with its expected sentences: `Dr. Smith lives here. He sleeps.`, a break added after
`Main St.` and after `Maple Dr.`, no break inside `Visit St. Louis`, one sentence for `St. Patrick's St. is
closed.`, a break after the word `No.`, after `Sr.` and after `etc.` and `p.m.` and `U.S.` at a sentence end,
none for `etc. and more`, `U.S. Army` or `Sen. Smith`, and `(see Elm St.) Then go.`; one sentence for `John Smith
Jr. Ph.D. spoke.` and `Ann Lee Ph.D. M.D. spoke.`, a break after the credential in `John Smith Jr. Ph.D. He
spoke.`, and a break before the sentence starter in `I met John Smith Jr. He left.`.

#### Alignment contract on hostile text

**Test**: `NormalizationSubsystem_AlignmentContract_RandomHostileText_SatisfiesAllInvariants`

Five hundred random texts are built from a hostile alphabet (words, titles, symbols, clitics, initialisms,
invisible characters, every kind of whitespace, placeholders, emoji, lone surrogates, combining marks, NUL).
For each, every span, the segment partition, the independent applier, the break edits and the reconciled
breaks are checked against invariants A1 to A8 and A10 of the _Alignment Contract_ (see _SpeechTextEn System
Design_). Invariant A6 is checked including the adjacency rule: a leading space is expected exactly where a
letter or digit of the source touches the span or the directly preceding span ends with a letter, and no
seam between two adjacent spans ever carries both a trailing and a leading space.

#### Concurrency

**Test**: `NormalizationSubsystem_Normalize_ConcurrentRequests_ReturnIdenticalResults`

Thirty-two tasks race on the lazily created default lexicons and each normalizes one text twenty times; every
result key (spans, forced and suppressed breaks, clitic marks, ambiguity count) equals the single-threaded one.

#### Linear time

**Tests**: `NormalizationSubsystem_Normalize_LargeRepetitiveTexts_CompleteWithinBudget`,
`NormalizationSubsystem_Normalize_OneHugeToken_CompletesWithinBudget` (a theory over `a&`, `a` with a
zero-width space, `a’b` and `&`)

The first normalizes one megabyte of realistic text and of repeated `St.`, `A.`, `&` and `Dr. He` (each followed by
a space) and
asserts each pass takes under ten seconds and finds spans; the second builds one token of a million
characters by hand (the tokenizer is not under test for giant tokens) and asserts the same bound.

#### Surrogate pairs, combining marks and plain text

**Tests**: `NormalizationSubsystem_Normalize_SurrogatePairsNextToRules_AreNeverCut`,
`NormalizationSubsystem_Normalize_CombiningMarksAroundAbbreviations_KeepSpansOnTokenEdges`,
`NormalizationSubsystem_Normalize_PlainText_ChangesNothing`

An emoji (and a lone surrogate) next to every rule never produces a span that starts or ends inside a pair;
a text in which words around `Dr.` and `St.` carry combining acute accents (U+0301) still resolves both
abbreviations with whitespace-bounded spans; and a text with no abbreviation, symbol or initialism has no
span, no break edit and is returned unchanged by `ApplySpokenForms`.

#### Numeric golden file and reference comparisons

**Tests**: `NormalizationSubsystem_GoldenFile_NumberCases_EveryRow_MatchesExpectedSpokenTextSentencesAndCounts`,
`NormalizationSubsystem_Numbers_NoDigitsInSpokenText_AndOnlyClosedVocabulary`,
`NormalizationSubsystem_Years_EveryYearFrom1000To2099_ReadAsReference`,
`NormalizationSubsystem_Times_EveryHourAndMinute_ReadAsReference`,
`NormalizationSubsystem_Currency_SingularPluralAndCents_AreCorrect`,
`NormalizationSubsystem_Units_SingularPluralAndPeriods_AreCorrect`

The first test normalizes every row of `number-cases.tsv` with the real tokenizer's tokens, applies the
annotations with the independent applier, reconciles the breaks and collects every mismatch of spoken text,
sentences, ambiguity count or fallback count, so one run reports them all. The second asserts for every golden
row and for random numeric texts that each numeric-kind span's value matches `[A-Za-z' ]+`, has no digit,
contains only words of the closed reference vocabulary and is at most 1,024 characters. The year test
analyzes each of the 1,100 years in a year context and compares the words with the reference; the time test
does the same for each of the 24 x 60 times, with and without a meridiem. The currency and units tests
assert the singular for exactly one (`$1`, `1 kg`, `1 ft.`) and the plural for `0`, `1.0`, `1.5` and `2`, cents
only for two fraction digits (`$5.5` and `$5.123` give decimals, `¥5.25` and `5.50 EUR` give none), and the
period of `6 ft.` consumed mid-sentence and kept at a sentence end.

#### Numeric sentence breaks

**Test**: `NormalizationSubsystem_SentenceBreaks_NumbersAndDates_BreakOnlyWhereTheSentenceEnds` (a theory)

Texts with their expected sentences: no break inside `3.5` or `$1.25`, a break after a sentence-final number
(`Item 5. Then item 6.`), none after `No. 5`, none inside `Jan. 5, 2024` or `Sat. 5 Jan. 2024 and Sun. Mar. 5`
(the tokenizer's false breaks after the month and weekday abbreviations are suppressed), a break after `He is 6
ft. Then he left.` and `He left Mon. He came Tue.`, and none in `Then Fri. ended.`. A break remains after `We
finished in May.` and `It is January.` (a full month name or `May` has no lexicon row, so the period ends the
sentence and the next number is no date), after `Jan. 5.` before a new sentence, and after `Month: May` and a
blank line.

#### Alignment contract on numeric text

**Tests**: `NormalizationSubsystem_AlignmentContract_RandomNumericText_SatisfiesAllInvariants`,
`NormalizationSubsystem_NumericSpans_RealChunker_KeepsEveryAnnotationInExactlyOneChunk`,
`NormalizationSubsystem_Normalize_NumericSurrogatesAndCombiningMarks_AreNeverCutOrRecognized`

The first builds seeded random texts from a numeric alphabet (digits, the separators `, . : / - – + − % $ € £ ¥
° #`, `1st`, `USD`, `kg`, `pm`, month and weekday names with and without periods, `million`, `½`, `1,234`,
`$5.50`, `12/25/2024`, digit runs of 16, 64, 65, 128 and 129 characters, tag placeholders, surrogates,
combining marks, invisible characters and whitespace variants including the no-break space) and checks
invariants A1 to A8b and A10 of the _Alignment Contract_, outside-span text unchanged, determinism, and the
analyzer-level order of the annotations (`en.upos` before `en.norm` at an equal start). The second feeds the
analyzer's annotations to the real Speech `SentenceChunker` for several budgets (200, 120, 60 and 25
characters) and asserts that each `en.norm` annotation lands in exactly one chunk on the same covered
characters. The third puts supplementary-plane digits and letters, lone surrogates and combining marks next to
numbers and asserts that no span cuts a pair and the number is not read.

#### Culture invariance and numeric concurrency

**Tests**: `NormalizationSubsystem_Normalize_UnderForeignCultures_GivesIdenticalResults`,
`NormalizationSubsystem_Normalize_ConcurrentNumericRequests_ReturnIdenticalResults`

The golden numeric subset is normalized under `tr-TR`, `ar-SA`, `de-DE`, `th-TH` and `fa-IR` as the current
and current UI culture (restored in a `finally` block) and must equal the invariant-culture result;
thirty-two tasks normalize a numeric text twenty times each and every result equals the single-threaded one.

#### Numeric linear time

**Tests**: `NormalizationSubsystem_Normalize_LargeRepetitiveNumericTexts_CompleteWithinBudget`,
`NormalizationSubsystem_Normalize_OneHugeNumericToken_CompletesWithinBudget`

One megabyte of each repetitive numeric unit (`1,234.50`, `$5.50`, `5:30pm`, `January 5, 2024`, `1/2`,
`12/25/2024`, `5 kg`, `50 %`, `Jan. 5`, `5 ft.`) and single huge tokens (a million digits, `1,` repeated,
`1.` repeated, `1-` repeated, `1/` repeated, `1:` repeated, `$` and a million digits, `5` and a million `k`)
normalize under the time budget, and the time for four times as many tokens grows by less than a generous
factor, so a quadratic or backtracking regression fails.

See also the unit documents `normalization-alignment.md`, `text-normalizer.md`, `numeric-expression-recognizer.md`,
`number-verbalizer.md`, `abbreviation-resolver.md`
and `acronym-recognizer.md`.
