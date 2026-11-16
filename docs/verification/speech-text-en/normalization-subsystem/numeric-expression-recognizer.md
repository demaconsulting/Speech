### NumericExpressionRecognizer Verification

#### Verification Approach

`NumericExpressionRecognizer` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/NumericExpressionRecognizerTests.cs`. The unit
reads a text and a token array, so the tests call `TryRecognize` directly. Token offsets come from the real
tokenizer of the part-of-speech package through the helper `Fakes/NormalizationHarness.cs` (a test-only
dependency; no tagger is involved), because the readers are written against the tokenizer's real token shapes
(`$` and `5.50` as two tokens, `5pm` as one, `5-10` as one, the en dash as three). Tests that need exact
control (hostile token ranges, the span-shape property, the caps) hand-build `NormalizationToken` arrays. The
spoken words are checked against the documented policy tables, and the words of a plain number against the
independent reference of `Fakes/ReferenceNumberWords.cs`; the span-shape invariants are checked over generated
token sequences instead of a list of examples.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the test project
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Dependencies**: the real tokenizer through `Fakes/NormalizationHarness.cs`; the reference words; no
  network, files or hardware
- **Isolation**: each test builds its own tokens; the unit keeps no state

#### Acceptance Criteria

Every token shape of the policy is read as documented: plain numbers by shape, exact thousands grouping,
leading zeros digit by digit; ordinals only with their matching suffix and decades only for tens and
hundreds, with the apostrophe form spanning both tokens; the year or cardinal decision for every cue word and
noun, the documented default and the ambiguity flag only where the readings differ; currency symbols, cents,
singular and plural, magnitude words and letters, suffix codes without cents and a prefix code left alone;
percent only after a number and units from the closed case-sensitive table with their period flag; valid and
invalid times with the meridiem forms; numeric and month-name dates with the ambiguity flag and Title Case
months; the sentence-boundary rule of the date context; ranges (the en dash only between two numbers that are
both read), phone numbers and identifiers, dotted numbers, fractions, mixed numbers and vulgar fractions;
read-only vocabulary views;
every produced span has one of the documented shapes with no whitespace and a comma or colon only between two
digits, and no match has more than two spans; oversized tokens are left as written and flagged as fallbacks and
hostile token ranges throw `ArgumentException` without overflow; non-numeric and deferred shapes are neither
recognized nor flagged; and concurrent calls give identical results.

#### Test Scenarios

##### Integers, decimals and separators

**Tests**: `NumericExpressionRecognizer_PlainNumbers_AreReadByShape` (a theory),
`NumericExpressionRecognizer_ThousandsSeparators_MustBeExactlyGrouped`,
`NumericExpressionRecognizer_LeadingZeros_AreReadDigitByDigit`

The theory covers `0`, `42`, `100`, `12345`, `1,234`, `1,234,567`, `-5`, `+3`, `−7` (U+2212), `3.5`, `.5`, `-3.5`,
`1,234.5` and `0.05`; the
grouping test accepts `1,234`, `12,345` and `1,000,000` and flags `1,23`, `12,34,567`, `1.234,5` and `1,2,3` as
fallbacks (left as written); the leading-zero test reads `007`, `00` and `0123` digit by digit without a fallback.

##### Ordinals and decades

**Tests**: `NumericExpressionRecognizer_Ordinals_RequireMatchingSuffix`,
`NumericExpressionRecognizer_Decades_AreRecognizedOnlyForTensAndHundreds`,
`NumericExpressionRecognizer_ApostropheDecade_SpansBothAdjacentTokens`

`1st` to `4th`, `11th` to `13th`, `22nd`, `101st`, `1,000th` and `1ST` are read; `1th`, `2st`, `11st` and `01st` are
not; `1990s`, `90s`, `1990's`, `1900s`, `2000s` and `2010s` are read and `5s`, `10000s`, `1995s`, `2100s` and `00s`
are not; `'90s`, `’90s` and `‘90s` give one span from the apostrophe to the `s` over two tokens, while
`rock'90s` is not a decade.

##### Year policy

**Tests**: `NumericExpressionRecognizer_Year_CueTable_DecidesYearOrCardinal`,
`NumericExpressionRecognizer_Year_NoCue_UsesDefaultAndIsCountedAmbiguous`

A table-driven test over every year cue (`in`, `since`, `until`, `till`, `by`, `during`, `circa`, `before`, `after`,
`year`), a month, a date context and an era reads the year without a count; every quantity cue, unit, percent
sign, currency, magnitude word and counted noun reads the cardinal without a count; `1999`, `1100`, `2010` and `2099`
read as years and `1066` and `1001` as cardinals, each counted ambiguous once, while `2000` to `2009` and `1000` are
never counted.

##### Currency

**Tests**: `NumericExpressionRecognizer_Currency_SymbolsCentsAndSingular`,
`NumericExpressionRecognizer_Currency_MagnitudeWordAndLetterSuffix_Decompose`,
`NumericExpressionRecognizer_Currency_SuffixCodeAndSymbol_ArePerToken`,
`NumericExpressionRecognizer_Currency_PrefixCode_IsLeftAlone`

The rows of the currency table: `$5`, `$1`, `$0`, `$5.50`, `$1.01`, `$.99`, `$5.00`, `$5.5`, `$5.123`,
`$1,234.50`, `€5.50`, `£0.50`, `£1.01`, `¥500`, `¥5.25`, `-$5` and `$-5`; `$5 million` is two spans (`$5` and `million
dollars`) over two tokens consumed, `$5M`, `$5.5M`, `$10k` and `$3B` one span, `5 USD`, `1 USD`, `5.50 EUR`, `5 million
USD` (three tokens consumed, two spans), `500 JPY` and `5€` are per token and give no cents, and `US$5`, `USD 5`
and `$1,23` are left alone (the last is a fallback).

##### Percent and units

**Tests**: `NumericExpressionRecognizer_Percent_FollowsANumberOnly`,
`NumericExpressionRecognizer_Units_TableSingularPluralAndPeriod`,
`NumericExpressionRecognizer_Units_ExcludedAmbiguousForms_AreLeftAlone`

`50%`, `50 %`, `3.5%` and `-5%` read as percent; a `%` alone or after two spaces does not. A table-driven test over every
unit of the table checks the singular for `1` and the plural for `5` and `1.0`, the glued and the joined forms,
that exactly the period-bearing units set the period flag, and that `5 m`, `5 g`, `5 in`, `5 Kg` and a unit with
no number before it are left alone.

##### Times

**Tests**: `NumericExpressionRecognizer_Times_ValidAndInvalidShapes`,
`NumericExpressionRecognizer_Meridiem_GluedSeparateAndDotted`

Every valid time of the table and the invalid `25:99`, `3:2`, `5:60`, `5:30:15`, `1:1:1`, `13:30pm`, `0pm` and `13pm`;
`5:30pm`, `5pm`, `12:00am` glued, `5 pm`, `5:30 pm` and `5 am` as a time and a meridiem span, and `5:30 p.m.` and `5
PM` as a time span only (the initialism rule reads the dotted and the upper-case letters).

##### Dates

**Tests**: `NumericExpressionRecognizer_NumericDates_OrderAndValidation`,
`NumericExpressionRecognizer_MonthNameDates_DecomposePerToken`,
`NumericExpressionRecognizer_MonthContext_RequiresTitleCase`,
`NumericExpressionRecognizer_MonthContext_StopsAtSentenceBoundaries`

The numeric dates of the table with their ambiguity flags (`3/4/2024`, `1/2/24` and `03-04-2024` counted,
`12/12/2024`, `25/12/2024` and `2024-12-25` not), the invalid `13/13/2024`, `0/5/2024`, `2/32/2024`, `1/1/00` and
`5/5/5`, and the calendar-free `2/30/2024`; `January 5, 2024`, `5th January 2024`, `May 2024` and `Dec 25 2024` as
per-token spans with the month and the comma untouched; `may 5` and `May I` not read as a month context, and `May
45` reading `45` as a cardinal. The sentence-boundary rule: after the period of `May.` or `January.` the number is
no day and no date year (`May. 5 people` is `five`, `January. 1999 people` a cardinal, `We finished in May. 5
people stayed.` `five`), the period of `Jan.`, `Sept.`, `Dec.` and `Mar.` keeps the date (`Jan. 5` is `fifth`,
`Mar. 5, 2024` a date year, `Dec. 25 people` still a day), a blank line between a month and a number or between
a day and a year ends the date, and hand-built tokens (with the break flag set on chosen tokens only) decide the
same way without the tokenizer.

##### Digit groups

**Tests**: `NumericExpressionRecognizer_HyphenGroups_RangePhoneIdAndDate`,
`NumericExpressionRecognizer_DottedGroups_VersionAndAddress`,
`NumericExpressionRecognizer_SlashForms_FractionsDatesAndIdentity`,
`NumericExpressionRecognizer_VulgarFractionsAndMixedNumbers`,
`NumericExpressionRecognizer_EnDashRange_RequiresBothEndpointsRead`

Ranges (`5-10`, `1990-2000`, `2024-2020`, the en dash between two adjacent numbers), phone numbers and identifiers
(`555-1234`, `1-800-555-1234`, `123-45-6789`, `01-02`, an area code in adjacent parentheses), dotted numbers
(`3.2.1`, `192.168.0.1`, `1.02.3` and `12.25.2024` with the digit-by-digit rule for a group with a leading
zero or more than three digits, and nine groups as a fallback), fractions (`7/16` and `9/10` ambiguous, `3/32`
and `15/16` not, `24/7`, `9/11` and `01/2` left alone) and the vulgar fractions and mixed numbers (`3 1/2`, `5½`,
`3 ½`, never ambiguous).

The en dash is read as `to` only when both neighbors are themselves read as numbers: `5–10`, `1990–2000`,
`1,234–5,678`, `5.5–6.5`, `0.5–1`, `5:30–6:30`, `5–10kg`, `5–10pm`, `5–10%`, `5–1/2`, `5–1,000`, `01–02`, a
50-digit endpoint and every dash of `1–2–3` are read; `1–2D`, `1,23–5`, `1,23–5,67`, `5–25:99`, `5–10x`, `5–10D`,
`5–.5`, `D5–10`, `a–b`, a leading or trailing dash and a dash that is not adjacent to both neighbors keep the dash
(`1–2D` leaves `1` read as `one`, `1,23–5` leaves `1,23` a counted fallback and reads `5`), a chain through an
unread endpoint reads no dash next to it, and hand-built tokens at the edges, a 129-character neighbor and a
malformed neighbor give `false` or `ArgumentException` as before.

##### Span shapes

**Test**: `NumericExpressionRecognizer_Spans_AreOnlyTheDocumentedShapes_OverGeneratedTokens`

Seeded generated token sequences from the numeric alphabet are recognized at every index and every span of every
match is checked: inside the text, non-empty, in ascending order and disjoint, no whitespace, no `;`, a `,` or `:`
only between two ASCII digits, a spoken text that matches `[A-Za-z' ]+` with no digit, at most two spans and at
least one consumed token, and an S3 or S4 span only for a currency symbol or apostrophe token with its adjacent
number or decade token.

##### Limits and hostile tokens

**Tests**: `NumericExpressionRecognizer_Caps_LeaveOversizedTokensAsWrittenAndFlagFallback`,
`NumericExpressionRecognizer_HostileTokenRanges_AreRejectedWithoutOverflow`

Tokens of 128 and 129 characters, digit runs of 15, 16, 64 and 65 digits, 32 and 33 fraction digits and 8 and 9
groups behave as documented; a 129-character numeric token returns a fallback match without a scan, in constant
time. Token arrays with an empty token, a negative start, a start or length near `int.MaxValue` and a token
beyond the text throw `ArgumentException` without an arithmetic overflow, a null text throws
`ArgumentNullException` and an index outside the array throws `ArgumentOutOfRangeException`.

##### Non-numeric and deferred shapes

**Test**: `NumericExpressionRecognizer_DeferredAndNonNumericShapes_AreIdentityWithoutFallback`

`3D`, `101A`, `1e5`, `0x1F`, `1_000`, `Q3`, `5-year-old`, `3-D`, `F-16`, `mid-1990s`, `10k`, `1.5M`, `Henry`, `VIII`,
a full-width digit, an Arabic-Indic digit, `5²`, a mathematical digit and a digit followed by a combining mark
produce no match, no span and no fallback.

##### Concurrency

**Test**: `NumericExpressionRecognizer_ConcurrentCalls_ReturnIdenticalResults`

Thirty-two tasks recognize the same token arrays twenty times each; every match equals the single-threaded one.

##### Vocabulary views

**Test**: `NumericExpressionRecognizer_VocabularyViews_AreReadOnly`

`MonthNames` (12 entries, `May` at index 4), `MonthAbbreviations` (12, no `May`) and `WeekdayAbbreviations` (10)
are not `string[]`; writing through the `IList<string>` view or adding to it throws `NotSupportedException`, the
contents are unchanged afterwards, and reading the property again returns the same cached instance.
