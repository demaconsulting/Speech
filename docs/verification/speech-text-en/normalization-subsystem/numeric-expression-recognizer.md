### NumericExpressionRecognizer Verification

#### Verification Approach

`NumericExpressionRecognizer` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/NumericExpressionRecognizerTests.cs`. The unit
reads a text and a token array, so the tests call `TryRecognize` directly. Token offsets come from the real
tokenizer of the part-of-speech package through the helper `Fakes/NormalizationHarness.cs` (a test-only
dependency; no tagger is involved), because the readers are written against the tokenizer's real token shapes
(`$` and `5.50` as two tokens, `5pm` as one, `5-10` as one, the en dash as three). Tests that need exact
control (hostile token ranges, the span-shape property, the caps, the sentence-boundary flags) hand-build
`NormalizationToken` arrays. The spoken words are checked against the documented policy tables; the broad value
ranges (currency, units, years, times, ranges and the many plain-number rows) are rows of the golden file
`TestData/number-cases.tsv`, which the subsystem tests run (see *NormalizationSubsystem Verification*), so the unit
tests keep one fact per reading rule. The span-shape invariants are checked over generated token sequences instead of a
list of examples.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the test project
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Dependencies**: the real tokenizer through `Fakes/NormalizationHarness.cs`; no network, files or hardware
- **Isolation**: each test builds its own tokens; the unit keeps no state

#### Acceptance Criteria

**Numbers.** Plain numbers are read by shape (integers, signs, decimals, a leading point); thousands grouping must be
exactly one to three digits followed by groups of three, otherwise the token is left as written and counted; a leading
zero reads digit by digit without a fallback. An ordinal is read only with the suffix its number requires, in either
case; decades only for tens and hundreds, with the apostrophe form spanning both tokens. Every year cue, quantity cue
and counted noun decides year or cardinal as documented; with no cue a number from 1100 is a year and below that a
cardinal, and the guess is counted only where the two readings differ.

**Currency and units.** Currency symbols read dollars and cents (singular only for exactly one, cents only for exactly
two fraction digits), a magnitude word or letter is a follow-up span, a suffix code or symbol is per token without cents
and a prefix code is left alone. Percent is read only after a number; a unit of the closed, case-sensitive table is read
with its singular and plural and period flag; excluded or unknown units are left alone.

**Times and dates.** Valid `h:mm` times are read with the glued, separate and dotted meridiem forms and invalid shapes
are identity without a fallback. Numeric dates follow the three orders with the ambiguity flag where month and day could
be exchanged; a date with a month name is read per token with the month and the comma untouched; an ordinal day next to
a month is a date part; only a Title Case month name is a month; and the date context stops at a sentence boundary the
normalizer keeps (the period after `May` or a full month name), not at the period of a month abbreviation.

**Digit groups.** Ranges, hyphen-separated identifiers and phone numbers, dotted numbers, slash fractions (ambiguous
when they could be a date), mixed numbers and vulgar fractions are read as documented, and the en dash is read as `to`
only between two numbers that are both read.

**Limits.** Every produced span has one of the documented shapes (no whitespace, a comma or colon only between two
digits, at most two spans); oversized tokens, digit runs, fraction digits and group counts beyond the caps are left as
written and flagged as fallbacks; hostile token ranges throw without overflow; non-numeric and deferred shapes are
neither recognized nor flagged.

#### Test Scenarios

##### Numbers: shapes, ordinals, decades and years

**Tests**: `NumericExpressionRecognizer_PlainNumbers_AreReadByShape` (a four-row theory),
`NumericExpressionRecognizer_ThousandsSeparators_MustBeExactlyGrouped`,
`NumericExpressionRecognizer_LeadingZeros_AreReadDigitByDigit`,
`NumericExpressionRecognizer_Ordinals_RequireMatchingSuffix`,
`NumericExpressionRecognizer_Decades_AreRecognizedOnlyForTensAndHundreds`,
`NumericExpressionRecognizer_ApostropheDecade_SpansBothAdjacentTokens`,
`NumericExpressionRecognizer_Year_CueTable_DecidesYearOrCardinal`,
`NumericExpressionRecognizer_Year_NoCue_UsesDefaultAndIsCountedAmbiguous`

The theory covers an integer, a signed number, a decimal and a thousands number. The grouping test accepts `1,234` and
`1,000,000` and flags `1,23`, `12,34,567` and `1.234,5` as fallbacks; the leading-zero test reads `007` digit by digit.
`1st` to `4th`, `11th` to `13th` and `22nd` are read, `1th` and `2st` are not; `1990s` and `90s` are decades, `5s` and
`1995s` are not; `'90s` gives one span from the apostrophe to the `s` over two tokens, while `rock'90s` is not a decade.
The cue table runs every year cue, a month, a date context, an era, every quantity cue, unit, percent sign, currency,
magnitude word and counted noun; `1999` and `1100` read as years and `1066` as a cardinal, each counted ambiguous once,
while `2000` to `2009` and `1000` are never counted.

##### Currency and units

**Tests**: `NumericExpressionRecognizer_Currency_SymbolsCentsAndSingular`,
`NumericExpressionRecognizer_Units_TableSingularPluralAndPeriod`

The currency test checks `$5`, `$1`, `$5.50`, `$5.5`, `$5.123`, `€5.50`, `¥5.25`, a magnitude word and letter after a
symbol amount, the per-token suffix code (`5 USD`, `5.50 EUR`, `5 million USD`) without cents, and `USD 5` left alone.
The unit test goes through every unit of the table for the singular for `1` and the plural for `5` and `1.0`, the glued
and the joined forms, that exactly the period-bearing units set the period flag, and that `5 m`, `5 g`, `5 in`, `5 Kg`
and a unit with no number before it are left alone; the percent forms (`50%`, `50 %`, a `%` alone) are part of it.

##### Times and dates

**Tests**: `NumericExpressionRecognizer_Times_ValidAndInvalidShapes`,
`NumericExpressionRecognizer_NumericDates_OrderAndValidation`,
`NumericExpressionRecognizer_MonthNameDates_DecomposePerToken`,
`NumericExpressionRecognizer_OrdinalDay_NextToMonth_IsDateTime`,
`NumericExpressionRecognizer_MonthContext_RequiresTitleCase`

The time test reads every valid shape of the table and leaves `25:99`, `5:60`, `5:30:15` and `13:30pm` as identity;
`5:30pm` and `5pm` give two adjacent spans inside the token (the time words, then the meridiem letters, with exact
offsets), `5 pm` and `5:30 pm` a time and a meridiem span, and `5:30 p.m.` a time span only (the initialism rule reads
the dotted letters). The numeric dates carry their ambiguity flags (`3/4/2024` counted, `12/12/2024`, `25/12/2024` and
`2024-12-25` not) and `13/13/2024`, `2/32/2024` and `5/5/5` are not dates. `January 5, 2024`, `5th January 2024` and
`Dec 25 2024` are per-token spans with the month and the comma untouched. An ordinal day next to a month (`January 5th`,
`5th January`) is a `DateTime` span, while `the 5th floor`, `January 45th` and an ordinal after a kept sentence boundary
stay plain numbers. `may 5`, `MAY 5` and `jan 5` give no month context. Hand-built tokens with the break flag on chosen
tokens decide the boundary rule without the tokenizer: after `May.` or `January.` the number is no day (`May. 5 people`
is `five`), while `Jan. 5` and `Dec. 25 people` keep the date, and a blank line between a month and a number ends it.

##### Digit groups

**Tests**: `NumericExpressionRecognizer_HyphenGroups_RangePhoneIdAndDate`,
`NumericExpressionRecognizer_DottedGroups_VersionAndAddress`,
`NumericExpressionRecognizer_SlashForms_FractionsDatesAndIdentity`,
`NumericExpressionRecognizer_VulgarFractionsAndMixedNumbers`,
`NumericExpressionRecognizer_EnDashRange_RequiresBothEndpointsRead`

Hyphen groups read ranges (`5-10`, `1990-2000`), phone numbers and identifiers (`555-1234`, `1-800-555-1234`) and the
leading-zero and size rules; dotted groups read versions and addresses (`3.2.1`, `192.168.0.1`) with the cardinal and
digit-by-digit group rules and the eight-group cap; slash forms read fractions (`7/16` ambiguous, `3/32` not), dates,
and leave `24/7` and `9/11` alone; vulgar fractions are read alone, glued (`5½`) and after a whole number (`3 ½`,
`3 1/2`). The en dash is read as `to` only when both neighbors are read as numbers (`5–10`, `1,234–5,678`, `5:30–6:30`,
`5–10kg`, every dash of `1–2–3`); `1–2D`, `1,23–5`, `5–25:99` and `5–10x` keep the dash and every token follows its own
rule.

##### Span shapes

**Test**: `NumericExpressionRecognizer_Spans_AreOnlyTheDocumentedShapes_OverGeneratedTokens`

Seeded generated token sequences from the numeric alphabet are recognized at every index and every span of every match
is checked: inside the text, non-empty, ascending and disjoint, no whitespace, no `;`, a `,` or `:` only between two
ASCII digits, a spoken text that matches `[A-Za-z' ]+` with no digit, at most two spans and at least one consumed token,
and a two-token span only for a currency symbol or apostrophe token with its adjacent number or decade token (or the two
halves of a glued meridiem token).

##### Limits, hostile tokens and non-numeric shapes

**Tests**: `NumericExpressionRecognizer_Caps_LeaveOversizedTokensAsWrittenAndFlagFallback`,
`NumericExpressionRecognizer_HostileTokenRanges_AreRejectedWithoutOverflow`,
`NumericExpressionRecognizer_DeferredAndNonNumericShapes_AreIdentityWithoutFallback`

Tokens of 128 and 129 characters, digit runs of 15, 16, 64 and 65 digits, 32 and 33 fraction digits and 8 and 9 groups
behave as documented; a 129-character numeric token returns a fallback match without a scan. Token arrays with an empty
token, a negative start, a start or length near `int.MaxValue` and a token beyond the text throw `ArgumentException`
without an arithmetic overflow. `3D`, `101A`, `1e5`, `0x1F`, `1_000`, `Q3`, `3-D`, `VIII`, a full-width digit and a
digit followed by a combining mark produce no match, no span and no fallback.

Concurrent recognition of the same tokens gives identical results; that is verified at subsystem level by
`NormalizationSubsystem_Normalize_ConcurrentRequests_ReturnIdenticalResults`.
