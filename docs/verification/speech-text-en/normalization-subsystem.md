## NormalizationSubsystem Verification

### Verification Approach

The SpeechTextEn NormalizationSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem` that need no network and no hardware. The
subsystem-level tests in `NormalizationSubsystemTests.cs` exercise the eight units together (the alignment
model, the text normalizer, the numeric expression recognizer, the number verbalizer, the abbreviation
resolver, the acronym recognizer, the web address recognizer and the alphanumeric token recognizer, with the
real embedded lexicons) exactly as the analyzer drives them, but
without the analyzer, the tagger or the diagnostics reporter.

**Test-only dependency on the real tokenizer.** The production normalization code takes plain token offsets and
has no dependency on the `DemaConsulting.PartOfSpeech` package. To feed it the tokens it would receive in
production, the subsystem and unit tests use the helper `Fakes/NormalizationHarness.cs`, which calls the real
`Tokenizer.Tokenize` of the OTS item (no tagger is involved) and converts its sentences to the normalizer's
token array, marking the first token of each tokenizer sentence. This is a deliberate choice: the rules are
written against the tokenizer's real conventions (which of `Dr.`, `Ave.` and `Sen.` stay whole, where the
tokenizer breaks after a title), and a hand-built token list would test only the author's belief about them.
That the conventions hold is itself verified as an OTS item (see *PartOfSpeech Verification*). The harness
also supplies what must not share code with the production alignment model: an **independent annotation
applier** that sorts the replacement annotations (`en.norm` and `en.spell`) by start, copies the gaps and
inserts each value, following the documented consumer algorithm without using `AlignedText`; the segment and
spoken-text helpers over a result (extension methods, not production members); the readers of the golden files
with their escape decoding; a directive-taking `Normalize`; and `NormalizeWithOverrides`, which resolves the
sample dictionary over the real tokenizer's tokens exactly as the analyzer does.

**Golden files.** `TestData/normalization-cases.tsv` has the columns `id`, `input`, `spoken`, `sentences`,
`ambiguous` and `note`; `TestData/number-cases.tsv` adds `fallbacks` (the escapes `\t \n \r \uXXXX \\` are
supported, and the sentences of the reconciled breaks are joined by the broken bar). A row states the input, the
exact text after the annotations are applied, the reconciled sentences, the number of ambiguous resolutions and,
for the numeric file, the number of numeric fallbacks. Together they are the main evidence of the subsystem and
they carry most of what the unit tests no longer enumerate:

- the first file covers every kind of non-numeric rule: titles, ranks and their guards, name, address and business
  suffixes, `St.` and `Dr.` and `Sr.` and `No.` both ways (including the documented wrong-by-default `Wall St.
  Journal` and the counted `Go see Jones St. Smith`), the credential rows (`cred-*`), Latin abbreviations, the
  abbreviation lexicon rows, the sentence-break rows (a sentence-final abbreviation ends its sentence and keeps its
  period, a title or `St.` before a name does not), operators, marks and adjacent marks, slash forms, ampersand and
  dotted initialisms, case pairs such as `US` and `us`, the shouting rows (`acr-shouting-per-sentence`), the web
  address rows (`web-*`: a URL, an e-mail address, a `www` address, a bare domain, and file names that are not
  domains), the identifier rows (`id-*`: `A4`, `B12`, `H2O`, `MP3`, `COVID-19`, `Catch-22`), the rows of a hyphenated
  compound and a number with a glued letter (`num-hyphen-compound-and-k`), the square-feet row (`abbr-square-feet`),
  dropped and
  invisible characters, clitic and elision apostrophes, plain text that must change nothing, an audio-tag
  placeholder, a surrogate pair, CRLF and the empty and blank texts;
- the second file is authored from the documented reading policy (see *NumericExpressionRecognizer Design*), not
  generated from the implementation, and covers every row of the policy tables (integers, signs, thousands separators
  and their invalid forms, decimals, ordinals, hash numbers, years with and without cues, decades, currency, units and
  their singular, plural and period forms, times, numeric and month-name dates, month and weekday abbreviations, ranges,
  phone numbers, dotted numbers, fractions), the limit rows at 15, 16, 64 and 65 digits, 32 and 33 fraction digits and
  a 129-character token, every deferred form (Roman numerals, `mid-1990s`, `1e5`, `0x1F`, non-ASCII digits), the
  hyphenated-compound and glued-letter rows (`id-*` and `range-en-dash-bad-*`) and the numeric sentence-break,
  adjacent-span, unit-period and invisible-character rows. The month-date rows pin the
  sentence-boundary rule (a period after `May` or a full month name ends the sentence and gives the next number no date
  reading) and the en-dash rows pin the both-endpoints-read rule.

**Override sample dictionary.** `TestData/overrides-dictionary.tsv` is the sample pronunciation dictionary and
`TestData/overrides-cases.tsv` holds the golden rows that run against it (they are owned by the
`PronunciationOverrides` unit tests, see *PronunciationOverrides Verification*). The subsystem tests use the
dictionary for random text and reuse the rows for culture invariance. Only the behavior of the normalizer with
directives is verified here; whether a backend can speak a phoneme string is not.

**Independent reference and property tests.** `Fakes/ReferenceNumberWords.cs` implements cardinals, ordinals, year
pairs, clock times and day ordinals by a different algorithm from `NumberVerbalizer` and supplies the closed spoken
vocabulary used by the property tests. The alignment invariants are checked on seeded random text, so a failure is
reproducible. Time is deliberately not asserted: large and adversarial inputs are checked by their exact output, and
cancellation by the exception it raises, never by an elapsed time, a polling cadence or a visit count (those are design
choices recorded in the normalization subsystem design).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the test project
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Files**: the golden files `TestData/normalization-cases.tsv` and `TestData/number-cases.tsv`, the override
  sample dictionary `TestData/overrides-dictionary.tsv` and override golden file `TestData/overrides-cases.tsv`,
  copied to the output folder, and the two embedded lexicons of the library
- **Dependencies**: the real tokenizer of `DemaConsulting.PartOfSpeech` through `Fakes/NormalizationHarness.cs`; no
  network, no tagger, no audio hardware
- **Isolation**: every test builds its own inputs and results; the shared default lexicons are immutable; the
  culture tests restore the current cultures in a `finally` block; the large-input tests carry a ten-minute
  cancellation that only ends a test which would never finish

### Acceptance Criteria

**Span contract** (`SpanContract`). Every row of both golden files produces the expected spoken text, reconciled
sentences, ambiguity count and fallback count (a failure message names the row id). For 500 random hostile texts and for
random numeric text every alignment invariant holds (ordered, disjoint, in-bounds, non-empty spans with whitespace-free
edges, no placeholder, no cut surrogate pair, chunk-safe content, empty values only for dropped characters, separating
spaces exactly where a letter or digit touches, segments that tile the text, text outside the spans unchanged, an
independent applier that agrees with `AlignedText`, valid break edits, no break inside a span); the real chunker keeps
every annotation of random numeric and override text in exactly one chunk, for several chunk budgets; surrogate pairs,
supplementary-plane digits and combining marks next to rules and numbers are never cut or read.

**Abbreviations, symbols and acronyms** (`AbbreviationResolution`, `SymbolsAndMarks`, `AcronymHandling`). Every golden
row passes and the golden inputs contain every form of the abbreviation lexicon (except the listed forms the unit
tests cover), so adding a lexicon row without a test fails; the listed sentences break only where a sentence really
ends; spelled spans are letter sequences and ampersand initialisms are decomposed with unchanged spoken text.

**Web addresses and identifiers** (`AddressAndIdentifierReading`). Every golden row passes: a URL, an e-mail address, a
`www` address and a bare domain are spoken as their pieces (the colon after the scheme stays, the slashes after it are
dropped, a final sentence period stays the sentence's own), file names and dotted names with an unknown ending are
unchanged, identifiers of letters and digits have their letters spelled and digits read, hyphenated names and
compounds have their number read, a number with a glued letter is read and the letter spelled, and the random numeric
texts, which contain these shapes, still satisfy every alignment invariant.

**Numbers, currency, units, times and dates** (`NumberReading`, `CurrencyReading`, `TimeReading`). The spoken value of
every numeric span contains only letters, spaces and the apostrophe of `o'clock`, from the closed vocabulary; the
numeric sentence breaks fall only where the sentence ends (not inside `3.5`, `$1.25` or `Jan. 5, 2024`, but after
`We finished in May.`); single huge numeric tokens are left as written and counted; and the normalization is identical
under foreign cultures. The word-level readings of years, times, cardinals and ordinals are proven against the
independent reference in the verbalizer unit tests.

**Token directives** (`TokenDirectives`). For 600 random hostile texts resolved against the sample dictionary every
directive is counted exactly once as applied or ignored, replacements and pronunciation marks form one ascending,
disjoint sequence with whitespace-free edges, spelled spans are letter sequences, and an independent consumer agrees
with the production application; the real chunker keeps every replacement and pronunciation annotation in exactly one
chunk and an override on an abbreviation never changes where a text is chunked; a directive on any word never changes
the reconciled sentence breaks; and a dictionary that matches nothing gives a result equal in every respect to that of
no dictionary.

**Long input and cancellation** (`LongInputAndCancellation`). One megabyte of realistic, repetitive and numeric text
normalizes, away from its two ends, exactly as the same repetitions do in a short text; one-megabyte single tokens of
`&`, of a dropped character and of a typographic apostrophe give one span per separator on its exact character;
concurrent and repeated normalization of one text gives identical results; and a cancellation during normalization
ends the analysis with `OperationCanceledException` and no diagnostics.

### Test Scenarios

#### Golden files

**Tests**: `NormalizationSubsystem_GoldenFile_EveryRow_MatchesExpectedSpokenTextSentencesAndAmbiguity`,
`NormalizationSubsystem_GoldenFile_NumberCases_EveryRow_MatchesExpectedSpokenTextSentencesAndCounts`,
`NormalizationSubsystem_GoldenFile_Coverage_ContainsEveryLexiconForm`

The first two normalize every row with the real tokenizer's tokens, apply the annotations with the independent applier,
reconcile the breaks and collect every mismatch of spoken text, sentences, ambiguity count and (numeric file) fallback
count, so one run reports them all. The third reads the embedded abbreviation lexicon and asserts that every form
appears in some golden input (of either file, since the month and weekday forms are exercised by the numeric file),
except a fixed list of forms (for example `Mrs`, `Rev.`, `Ste.`, `viz.`, `w/`) that the abbreviation-resolver unit tests
cover.

#### Web addresses and identifiers

**Tests**: `NormalizationSubsystem_GoldenFile_EveryRow_MatchesExpectedSpokenTextSentencesAndAmbiguity`,
`NormalizationSubsystem_GoldenFile_NumberCases_EveryRow_MatchesExpectedSpokenTextSentencesAndCounts`,
`NormalizationSubsystem_AlignmentContract_RandomNumericText_SatisfiesAllInvariants`

The golden rows pin the readings: `Visit https://www.example.com/docs?id=7 today.` gives
`Visit H T T P S:W W W dot example dot com slash docs question mark id equals 7 today.`,
`Mail john.smith@example.com, now.` keeps its comma, `Visit example.com. Then relax.` keeps the sentence break after the
domain, `Open report.pdf or Node.js now.` is unchanged, `Use paper A4 and vitamin B12 with H2O and MP3.` gives `A four`,
`B twelve`, `H two O` and `M P three`, `COVID-19 and Catch-22 spread.` gives
`COVID nineteen and Catch twenty two spread.`, `Item SCP-184 and A-1 and SCP-049.` gives
`Item S C P one hundred eighty four and A one and S C P zero four nine.`, `Item #: 5 and Case # 12 and # Title.` gives
`Item number: five and Case number twelve and # Title.`, and `Her 12-year-old ran 10K in 24-hour heat.` gives
`twelve-year-old` and `ten K`. The random numeric texts include addresses, identifiers, compounds and glued letters
among their words, so a span of these rules that broke the alignment contract (whitespace, a colon, an overlap, a cut
surrogate pair) fails there.

#### Alignment contract on hostile and numeric text

**Tests**: `NormalizationSubsystem_AlignmentContract_RandomHostileText_SatisfiesAllInvariants`,
`NormalizationSubsystem_AlignmentContract_RandomNumericText_SatisfiesAllInvariants`,
`NormalizationSubsystem_Numbers_NoDigitsInSpokenText_AndOnlyClosedVocabulary`

The first checks 500 seeded random texts built from a hostile alphabet (words, titles, symbols, clitics, initialisms,
invisible characters, every kind of whitespace, placeholders, emoji, lone surrogates, combining marks, NUL) against
invariants A1 to A8 and A10 of the *Alignment Contract* (see *SpeechTextEn System Design*), including the adjacency
rule: a leading space exactly where a letter or digit touches or the preceding span ends with a letter, a trailing space
exactly where a letter or digit touches and no following span carries the separating space, and no seam with both. The
second builds seeded random texts from a numeric alphabet (digits, separators, symbols, units, month and weekday names,
digit runs of 16 to 129 characters, placeholders, surrogates, combining marks, invisible characters, the no-break space)
and additionally checks the chunk-safety rule (no whitespace, no semicolon, a comma or colon only between two digits,
only the four span shapes), determinism, and the analyzer-level order of the annotations (`en.upos` before `en.norm` at
an equal start). The third asserts for every golden row and for random numeric texts that each numeric span's value
matches `[A-Za-z' ]+`, has no digit, uses only reference vocabulary and is at most 1,024 characters.

#### Surrogate pairs, combining marks and numeric sentence breaks

**Tests**: `NormalizationSubsystem_Normalize_SurrogatePairsNextToRules_AreNeverCut`,
`NormalizationSubsystem_Normalize_CombiningMarksAroundAbbreviations_KeepSpansOnTokenEdges`,
`NormalizationSubsystem_Normalize_NumericSurrogatesAndCombiningMarks_AreNeverCutOrRecognized`,
`NormalizationSubsystem_SentenceBreaks_NumbersAndDates_BreakOnlyWhereTheSentenceEnds`

An emoji (and a lone surrogate) next to every rule never produces a span that starts or ends inside a pair; words around
`Dr.` and `St.` that carry combining accents still resolve with whitespace-bounded spans; supplementary-plane digits and
letters next to numbers are neither cut nor read as ASCII numbers. The sentence-break test lists twenty-five texts with
their expected sentences: no break inside `3.5`, `$1.25` or `Jan. 5, 2024` (the tokenizer's false breaks after month and
weekday abbreviations are suppressed), a break after a sentence-final number (`Item 5. Then item 6.`), none after
`No. 5`, a break after `He is 5 ft. Then left.`, and a break that remains after `We finished in May.`, `It is January.`
and `Jan. 5.` before a new sentence.

#### Real chunker and overrides on abbreviations

**Test**: `NormalizationSubsystem_NumericSpans_RealChunker_KeepsEveryAnnotationInExactlyOneChunk`

Random texts of numeric constructs, overridable words, initialisms and tag placeholders separated by spaces, clause
marks and placeholders are normalized with and without the sample dictionary, turned into the annotations the analyzer
would emit (spans as `en.norm` or `en.spell`, marks as `en.pron`) and given to the real `SentenceChunker` for four
budgets (200, 120, 60 and 25 characters), 150 texts each. The sequence of (source characters, kind, value) delivered by
the chunks must equal the sequence emitted, so no annotation is lost, duplicated, split or invented. The same test then
runs seventeen texts (`Fig`, `Dr`, `St`, `No`, `Jan` and `Ph.D`, mid-sentence and sentence-final, `Dr` after an em dash,
tag placeholders touching `Fig`) with a one-entry real dictionary for each action (skip, say, spell, pronounce), and the
reconciled breaks and the chunks equal those of the text without a dictionary at three budgets, with every annotation of
the override delivered once.

#### Alignment contract with overrides

**Tests**: `NormalizationSubsystem_AlignmentContract_RandomTextWithOverrides_SatisfiesAllInvariants`,
`NormalizationSubsystem_Normalize_DictionaryWithoutHits_EqualsNoDictionary`,
`NormalizationSubsystem_Normalize_DirectiveAnywhere_NeverChangesSentenceBreaks`

The first checks 600 seeded random texts built from overridable words, punctuation, tag placeholders, a long word,
surrogates, combining marks and a NUL character, each tokenized, resolved against the sample dictionary and normalized
with the directives, against the invariants that concern overrides (the unified ascending order of spans and marks, the
spelled-letter grammar, chunk safety of every span and mark, a mark on exactly one token, equality with an independent
consumer that merges `en.norm` and `en.spell`) and that every directive is counted exactly once. The second shows that
for 400 random numeric texts a dictionary whose words never occur gives a result equal in every field to that of no
dictionary. The third is a seeded differential test (400 abbreviation-rich texts with periods, dashes and placeholders)
that puts each of the four directive kinds on every word token a period touches and on other word tokens, and once on
every sampled word at once; the reconciled breaks always equal those without directives, every directive is counted
once, spans and marks stay ascending and disjoint and no break lies inside a span.

#### Large and adversarial input

**Tests**: `NormalizationSubsystem_Normalize_LargeRepetitiveTexts_NormalizeEveryRepetitionAlike`,
`NormalizationSubsystem_Normalize_OneHugeToken_NormalizesEverySeparatorAsInAShortToken` (a four-row theory over `a&`,
`a` with a zero-width space, `a’b` and `&`),
`NormalizationSubsystem_Normalize_OneHugeNumericToken_IsLeftAsWrittenAndCounted`

The first normalizes one megabyte of realistic text and of repeated `St.`, `A.`, `&`, `Dr. He` and of every numeric unit
(`1,234.50`, `$5.50`, `5:30pm`, `January 5, 2024`, `1/2`, `5 kg`, `Jan. 5`, `5 ft.`, `5 million USD`, `555-123-4567`,
`192.168.0.1`, `1990s`, `3 1/2`, `5–10`, `1999` and others). For each unit a short reference text is normalized, and the
spans, forced breaks and suppressed breaks of one repetition are the pattern that every repetition of the large text,
away from the two ends, must repeat exactly, shifted by its position; no span may cross the boundary between two
repetitions. The second builds one token of a million characters by hand (the tokenizer is not under test for giant
tokens) and asserts one span per separator on its exact character, no numeric fallback and no break edit. The third
feeds single huge numeric tokens (a million digits, `1,` repeated, `1.` repeated, `1-` repeated, `1/` repeated, `1:`
repeated, `$` and a million digits, `5` and a million `k`) and asserts no span and exactly one numeric fallback. No
elapsed time is asserted.

#### Culture invariance and concurrency

**Tests**: `NormalizationSubsystem_Normalize_UnderForeignCultures_GivesIdenticalResults`,
`NormalizationSubsystem_Normalize_ConcurrentRequests_ReturnIdenticalResults`

The whole numeric golden file and every row of the override golden file (against the sample dictionary) are normalized
under `tr-TR`, `az-Latn-AZ`, `ar-SA`, `de-DE`, `th-TH` and `fa-IR` as the current and current UI culture (restored in a
`finally` block) and must give the documented spoken text and counts. Thirty-two tasks race on the lazily created
default lexicons and each normalizes two texts (abbreviations, clitics and every numeric construct) twenty times; every
result key (spans, forced and suppressed breaks, clitic marks, ambiguity count) equals the single-threaded one.

See also the unit documents `normalization-alignment.md`, `text-normalizer.md`, `numeric-expression-recognizer.md`,
`number-verbalizer.md`, `abbreviation-resolver.md` and `acronym-recognizer.md`.
