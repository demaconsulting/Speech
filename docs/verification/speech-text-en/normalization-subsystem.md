## NormalizationSubsystem Verification

### Verification Approach

The SpeechTextEn NormalizationSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem` that need no network and no hardware. The
subsystem-level tests in `NormalizationSubsystemTests.cs` exercise the four units together (the alignment
model, the text normalizer, the abbreviation resolver and the acronym recognizer, with the real embedded
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

**Property tests.** The alignment invariants are checked on random hostile text with a fixed seed, so a
failure is reproducible. Time and cancellation are checked with generous budgets that a linear algorithm
meets and a quadratic one does not.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the test project
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Files**: the golden file `TestData/normalization-cases.tsv`, copied to the output folder, and the two
  embedded lexicons of the library
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

### Test Scenarios

#### Golden file

**Tests**: `NormalizationSubsystem_GoldenFile_EveryRow_MatchesExpectedSpokenTextSentencesAndAmbiguity`,
`NormalizationSubsystem_GoldenFile_Coverage_ContainsEveryLexiconForm`

The first test normalizes every row with the real tokenizer's tokens, applies the annotations with the
independent applier, reconciles the breaks, and collects every mismatch of spoken text, sentences or
ambiguity count so one run reports them all. The second reads the embedded abbreviation lexicon and asserts
that every form appears in some golden input, except a fixed list of forms (for example `Mrs`, `Rev.`, `Ste.`,
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

See also the unit documents `normalization-alignment.md`, `text-normalizer.md`, `abbreviation-resolver.md`
and `acronym-recognizer.md`.
