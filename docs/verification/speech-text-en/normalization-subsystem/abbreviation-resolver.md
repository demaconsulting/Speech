### AbbreviationResolver Verification

#### Verification Approach

`AbbreviationResolver` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/AbbreviationResolverTests.cs`. The unit is a
lookup over a lexicon and a few neighbor tokens, so the tests use the shipped lexicon (the real embedded
`abbreviations.tsv`) for the behavior of the product and small custom tables parsed from strings for the loader
and for the alternatives that the shipped lexicon does not have. Token offsets come from the real tokenizer of
the part-of-speech package through the test helper `Fakes/NormalizationHarness.cs` (a test-only dependency; no
tagger is involved): a private helper tokenizes a sentence, finds the first token that starts with the
candidate word and resolves it, so the cues are exercised on the tokenizer's real token shapes. The ambiguous
forms are verified with mutation-style pairs, where one neighboring word is changed and the reading must flip.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Dependencies**: the real tokenizer through `Fakes/NormalizationHarness.cs`; the embedded lexicon; no
  network, no files, no hardware
- **Isolation**: each test builds its own tokens; the shipped lexicon is immutable and shared

#### Acceptance Criteria

The shipped lexicon loads, has at least 70 rows and is one shared instance. Each unambiguous form resolves to
its spoken form, not counted as ambiguous. The ambiguous forms follow the neighbor cues in both directions:
`Visit St. Louis` Saint but `Visit Elm St. today` Street, `Dr. Smith` Doctor but `Maple Dr. He` Drive, `5th St.`
and `123 Elm Dr.` Street and Drive by strong suffix evidence, `The Dr. said` Doctor, `Sr. Maria` Sister but
`John Smith Sr. He` Senior, and the documented wrong-by-default `Wall St. Louis` Saint; a possessive before an
abbreviation is skipped; `No.` is `Number` only before a digit; the guards switch rows on and off; an
inconclusive context uses the default and is flagged for counting, as is a choice made with both cues
present, and a clear one is not; the period shape is reported; a single-letter initial resolves only before a
name or another initial; ordinary words and words that merely start like a form are not abbreviations; a
lexicon without a prefix and suffix pair is decided by its default without counting; the loader accepts
comments, blank lines and CRLF and rejects malformed rows with an error naming the one-based line number; a
null reader throws; the month and weekday rows resolve as a prefix before a date and as a suffix elsewhere,
`Mar.` and `Sun.` resolve only after a number, `May` and the weekdays written without a period have no row, and the
recognizer's month and weekday vocabulary equals the rows of the table; and racing first use yields one instance.

#### Test Scenarios

##### Lexicon and listed forms

**Tests**: `AbbreviationResolver_Default_Lexicon_LoadsWithRows`,
`AbbreviationResolver_TryResolve_ListedForm_ReturnsSpokenForm` (a theory over 38 forms),
`AbbreviationResolver_TryResolve_OrdinaryWords_AreNotAbbreviations` (a theory)

The theory covers the titles and ranks, the titles without a period, the address, business and name suffixes, the
Latin and general forms (`viz.`, `misc.`, `cf.`, `E.g.`, `I.e.`), `Vol.` before a digit and the slash forms `n/a`
and `c/o`, each in a neutral sentence. Words such as `cat`, `mister`, `Doctors`, `Drive` and `Stop` resolve to
nothing.

##### Ambiguous forms and context cues

**Tests**: `AbbreviationResolver_TryResolve_AmbiguousForm_FollowsTheNeighborCues` (a theory over 14
sentences), `AbbreviationResolver_TryResolve_PossessiveBeforeAbbreviation_IsSkippedForContext`,
`AbbreviationResolver_TryResolve_No_IsNumberBeforeDigitAndWordOtherwise`,
`AbbreviationResolver_IsNameContinuation_ListedFormsOnly` (a theory)

The sentences are the mutation pairs and the strong-evidence cases of the acceptance criteria, including
`Mount St.` before a mountain name (Saint) and `St. Patrick's St. is closed.`, where the first reading is Saint. The second
abbreviation of the latter resolves to class `Suffix` with the spoken form Street, which proves the
possessive is skipped. `No. 5` is `Number` of class `Prefix` and `No. I don't know.` is class `Word`, neither
counted. `Sr.` reads Senior before a credential (`John Smith Sr. Ph.D. spoke.`, `Joe Smith Sr. Esq. arrived.`)
because the credential is not taken for the name of a Sister. The name-continuation theory accepts `Jr`, `Jr.`,
`Ph.D`, `Ph.D.`, `III` and `M.D.` and rejects `Ave`, `Smith`, `V`, `He` and the lower-case `jr`.

##### Guards and defaults

**Tests**: `AbbreviationResolver_TryResolve_Guards_RestrictReadingsToTheirContext`,
`AbbreviationResolver_TryResolve_InconclusiveContext_UsesDefaultAndIsCounted`,
`AbbreviationResolver_FromTsv_NonPairAlternatives_UseDefaultWithoutCounting`

`Ft.` resolves before a capitalized word but not after a number or before a lower-case word (a number before it
is the unit `ft.` of the numeric rule); `Fig.` needs a
digit next; `Mr` and `Ms` need a capitalized next word. `They St. said so.` reads Street and `They Dr. said so.`
reads Doctor, both by default and flagged; `Go see Jones St. Smith` reads Saint and is flagged; `Dr. Smith` is
not. A custom lexicon whose alternatives are a free and a word reading is decided by the default without a
flag.

##### Date guards and month and weekday rows

**Tests**: `AbbreviationResolver_TryResolve_DateGuards_RestrictMonthAndWeekdayReadings`,
`AbbreviationResolver_Table_MonthAndWeekdayRows_AgreeWithRecognizerVocabulary`,
`AbbreviationResolver_TryResolve_MonthAndWeekday_PrefixSuffixAndBareForms`

The guard test uses small custom tables: `next-date` passes before an ASCII digit and before a month name or
abbreviation (with or without its period) and fails before a lower-case word, a capitalized non-month and a
non-ASCII digit; `prev-number` passes after an ASCII digit and fails after a letter, at the start of the text and
after a non-ASCII digit; the two names are accepted by the loader and a misspelled guard is still rejected. The
table test reads the shipped lexicon and asserts that the rows are exactly the month abbreviations with and
without a period and the weekday abbreviations of `NumericExpressionRecognizer`, that every period form has
a prefix row guarded by `next-date` and a default suffix row, that `Mar.` and `Sun.` have `prev-number` on the
suffix reading, that the months written without a period are prefix rows with `next-digit`, and that there is no row for `May`.
The resolution test asserts, on the real tokenizer's tokens, that `Jan. 5` and `Sat. 5 Jan. 2024` resolve to the
prefix reading (period consumed, not counted ambiguous), that `in Dec. She left.` and `He left Mon. He came
Tue.` resolve to the suffix reading, that `Then Fri. ended.` resolves to Friday, that `Jan 5` and `Sept 11` resolve
without a period, and that a bare `Mar.`, a bare `Sun.` and the word `May` do not resolve.

##### Period shape and initials

**Tests**: `AbbreviationResolver_TryResolve_PeriodShapes_AreReported`,
`AbbreviationResolver_TryResolve_Initials_ResolveOnlyBeforeNames`

`Ave` followed by its own period token reports that shape and `Mr.` as a whole token does not; `J. Smith` and
`A. B. Jones` resolve to the letter with class `Prefix`, while `vitamin C. The end.` is not an initial.

##### Loader

**Tests**: `AbbreviationResolver_FromTsv_CommentsBlankLinesAndCrLf_AreAccepted`,
`AbbreviationResolver_FromTsv_MalformedRow_ThrowsInvalidDataExceptionNamingTheLine` (a theory over ten
malformed tables), `AbbreviationResolver_FromTsv_NullReader_ThrowsArgumentNullException`

The malformed tables have a wrong column count, an empty form, an empty spoken text, an unknown class, an
unknown default value, an unknown guard, a duplicate reading, a form with two defaults and two tables in
which a form has no default; each message names the expected one-based line (the first data row is line 5
after three comment lines and the header).

##### Thread-safe first load

**Test**: `AbbreviationResolver_Default_ConcurrentFirstUse_ReturnsOneInstance`

Thirty-two tasks read the lazy default and all receive the same, fully loaded instance.
