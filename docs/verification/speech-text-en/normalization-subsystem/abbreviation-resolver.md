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
The full lexicon is exercised by the golden file `TestData/normalization-cases.tsv`, which the subsystem tests run
(see *NormalizationSubsystem Verification*) and which must name every lexicon form, so the unit tests keep one
representative form of each kind.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Dependencies**: the real tokenizer through `Fakes/NormalizationHarness.cs`; the embedded lexicon; no
  network, no files, no hardware
- **Isolation**: each test builds its own tokens; the shipped lexicon is immutable and shared

#### Acceptance Criteria

**Lexicon.** A representative listed form of each kind (title, address suffix, business suffix, Latin form, slash form)
resolves to its spoken form; ordinary words and words that merely start like a form are not abbreviations; the period
shape of a word-plus-period token is reported and a whole-token form is not. Every month and weekday abbreviation of an
independent expected table resolves to its full name as a prefix reading before a date and as a suffix reading after a
number, `Mar.` and `Sun.` only after a number, and `May` and the weekdays without a period stay as written. The loader
accepts comments, blank lines and CRLF and rejects malformed rows with an error naming the one-based line number, a null
reader throws, and racing first lookups all return the documented resolutions.

**Reading choice.** The ambiguous forms follow the neighbor cues in both directions (`Visit Elm St. today` Street,
`Mount St. Elias erupted.` Saint, `Dr. Smith` Doctor, `Maple Dr. He` Drive, `Sr. Maria` Sister, `John Smith Sr. He`
Senior); a possessive before an abbreviation is skipped; `Sr.` before a credential is Senior; only the listed name
suffixes and credentials continue a name; a single-letter initial resolves only before a name or another initial; `No.`
is `Number` only before a digit; the guards switch rows on and off, and the date guards count ASCII digits only; an
inconclusive context uses the default and is flagged for counting, as is a choice made with both cues present, and a
clear one is not; a lexicon without a prefix and suffix pair is decided by its default without counting.

#### Test Scenarios

##### Lexicon, listed forms and period shapes

**Tests**: `AbbreviationResolver_TryResolve_ListedForm_ReturnsSpokenForm` (an eight-row theory: `Rev.`, `Mrs`, `Ln`,
`Ltd`, `viz`, `Vol.` before a digit, `E.g.` and `n/a`), `AbbreviationResolver_TryResolve_PeriodShapes_AreReported`,
`AbbreviationResolver_TryResolve_OrdinaryWords_AreNotAbbreviations` (`mister`, `Doctors` and an unlisted phrase)

`Ave` followed by its own period token reports that shape and `Mr.` as a whole token does not. The remaining forms of
the lexicon are rows of the golden file, and its coverage test fails when a lexicon row has no golden input.

##### Month and weekday rows

**Tests**: `AbbreviationResolver_TryResolve_MonthAndWeekdayAbbreviations_ResolveToFullNamesInDateAndNonDateContexts`,
`AbbreviationResolver_TryResolve_DateGuards_RestrictMonthAndWeekdayReadings`

The full-name test is written against an expected table of its own (the twelve month abbreviations with `Sept`, and the
ten weekday abbreviations, each with its full name), independent of the lexicon file and of the numeric recognizer's
vocabulary: for each abbreviation `It was X. 5 then.` must resolve to the full name as a prefix reading and
`It was 5 X.` as a suffix reading, a month without a period resolves before a digit, and a bare `Mar.`, a bare `Sun.`
and `May 5` must not resolve. The guard test uses small custom tables: `next-date` passes before an ASCII digit and
before a month name or abbreviation and fails before a lower-case word, a capitalized non-month and a non-ASCII digit;
`prev-number` passes after an ASCII digit and fails after a letter, at the start of the text and after a non-ASCII
digit; a misspelled guard is still rejected by the loader.

##### Ambiguous forms, cues and defaults

**Tests**: `AbbreviationResolver_TryResolve_AmbiguousForm_FollowsTheNeighborCues` (a six-row theory),
`AbbreviationResolver_TryResolve_PossessiveBeforeAbbreviation_IsSkippedForContext`,
`AbbreviationResolver_IsNameContinuation_ListedFormsOnly` (a four-row theory: `Jr.`, `Ph.D`, `Ave`, `jr`),
`AbbreviationResolver_TryResolve_Initials_ResolveOnlyBeforeNames`,
`AbbreviationResolver_TryResolve_No_IsNumberBeforeDigitAndWordOtherwise`,
`AbbreviationResolver_TryResolve_Guards_RestrictReadingsToTheirContext`,
`AbbreviationResolver_TryResolve_InconclusiveContext_UsesDefaultAndIsCounted`,
`AbbreviationResolver_FromTsv_NonPairAlternatives_UseDefaultWithoutCounting`

The second abbreviation of `St. Patrick's St. is closed.` resolves to class `Suffix` with the spoken form Street, which
proves the possessive is skipped. `No. 5` is `Number` of class `Prefix` and `No. I don't know.` is class `Word`, neither
counted. `J. Smith` and `A. B. Jones` resolve to the letter while `vitamin C. The end.` is not an initial. `Ft.`
resolves before a capitalized word but not after a number or before a lower-case word, `Fig.` needs a digit next, and
`Mr` and `Ms` need a capitalized next word. `They St. said so.` reads Street and `They Dr. said so.` reads Doctor, both
by default and flagged; `Go see Jones St. Smith` reads Saint and is flagged; `Dr. Smith` is not. A custom lexicon whose
alternatives are a free and a word reading is decided by the default without a flag.

##### Loader and concurrent first lookups

**Tests**: `AbbreviationResolver_FromTsv_CommentsBlankLinesAndCrLf_AreAccepted`,
`AbbreviationResolver_FromTsv_MalformedRow_ThrowsInvalidDataExceptionNamingTheLine` (an eight-row theory),
`AbbreviationResolver_FromTsv_NullReader_ThrowsArgumentNullException`,
`AbbreviationResolver_TryResolve_ConcurrentFirstLookups_ReturnDocumentedResolutions`

The malformed tables have a wrong column count, an empty form, an empty spoken text, an unknown class, an unknown
default value, a duplicate reading, a form with two readings and no default, and an unknown guard; each message names
the expected one-based line (the first data row is line 5 after three comment lines and the header). Thirty-two tasks
resolve `Dr. Smith` (Doctor, prefix), `Maple Dr. He` (Drive, suffix) and `Jan. 5` (January) through the default lexicon
and every task receives exactly those documented resolutions; whether the default is one shared, lazily loaded instance
is a design choice and is not asserted.
