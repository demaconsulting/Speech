### NumberVerbalizer Verification

#### Verification Approach

`NumberVerbalizer` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/NumberVerbalizerTests.cs`. The unit is a pure
function library over ASCII digit spans, so the tests call it directly and need no tokenizer, no lexicon and no
collaborator. The central evidence is an **independent reference implementation**,
`Fakes/ReferenceNumberWords.cs`: it builds the words of cardinals and ordinals from 0 to 10^15 - 1, of year
pairs, of clock times and of day ordinals by a recursive decomposition over `ulong` values with vocabulary
arrays of its own. It is deliberately a different algorithm from the unit under test (which reads digit groups
from the left over a stack buffer) and shares no code with it, so a defect would have to occur identically in
two unrelated implementations to pass. The unit's results are compared with the reference exhaustively where
the domain is small and with seeded random values elsewhere, so a failure is reproducible. The unit's production
surface is the writer-based `Append*` family; the harness (`Fakes/NormalizationHarness.cs`) wraps it in string
helpers (year, decimal, clock and date words) so that the tests read naturally, and the string forms the unit itself
exposes (`Cardinal`, `Ordinal`, `Decade`, `Digits`, `Fraction`, `MonthName`) are called directly.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the test project
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Dependencies**: the test-only reference `Fakes/ReferenceNumberWords.cs`; no network, files or hardware
- **Isolation**: each test builds its own inputs; the culture test restores the current cultures in a `finally`
  block

#### Acceptance Criteria

**Words.** The cardinal words of every value from 0 to 9999, of 10,000 seeded random values of up to 15 digits and of
every scale boundary (with the skipped empty groups) equal the reference, with no hyphen, comma or `and`; the ordinal
words of every value from 0 to 9999 equal the reference and end the way the written suffix (`st`, `nd`, `rd`, `th`,
including 11, 12 and 13) says for 1 to 1000; the year words of every four-digit value from 1000 to 2099 equal the
reference (1000, 2000 and 2001 to 2009 equal to the cardinal) and the decade words match the documented table; the
decimal words keep every fraction digit including zeros, omit an empty integer part and spell every digit of a digit
sequence; the fraction words are right for every denominator class; the clock words of every hour 0 to 23 and every
minute 00 to 59 equal the reference, including `o'clock`, `hundred` and `oh`; and the month names, day ordinals and
dates are right.

**Safety.** For the worst-case inputs the value is at most 1,024 characters, and every value the unit produces for
generated inputs has no ASCII digit, consists of letters, spaces and the apostrophe, and uses only words of the
reference vocabulary; the words are identical under foreign cultures; and every invalid input throws
`ArgumentException`.

#### Test Scenarios

##### Cardinals

**Tests**: `NumberVerbalizer_Cardinal_EveryValueFrom0To9999_MatchesReference`,
`NumberVerbalizer_Cardinal_RandomValuesUpTo15Digits_MatchesReference`,
`NumberVerbalizer_Cardinal_ScaleBoundaries_UseCorrectScaleWords`

The first compares all 10,000 values, with and without leading zeros. The second compares 10,000 seeded random values
whose digit count is itself random from 1 to 15. The third checks the boundaries, such as 1,000,000 (`one million`),
1,000,001 (`one million one`), 10^12 (`one trillion`) and 999,999,999,999,999 (the longest value), that groups of zero
are skipped and that `zero` is spoken only for a value of zero.

##### Ordinals, years and decades

**Tests**: `NumberVerbalizer_Ordinal_EveryValueFrom0To9999_MatchesReference`,
`NumberVerbalizer_Year_EveryYearFrom1000To2099_MatchesReference`, `NumberVerbalizer_Decade_Forms_AreSpoken`

The ordinal test compares all words (`first`, `eleventh`, `twenty first`, `one hundredth`, `zeroth`) and asserts that
the suffix a number requires agrees with the ordinal word's ending for every number from 1 to 1000. Every year from 1000
to 2099 equals the reference, and `IsYearReadingDifferent` is false exactly for 1000, 2000 and 2001 to 2009. The decade
test asserts `nineties`, `nineteen nineties`, `nineteen hundreds`, `two thousands`, `tens` and `twenties`.

##### Decimals, digit sequences and fractions

**Tests**: `NumberVerbalizer_Decimal_FractionDigitsAreSpelledIncludingZeros`,
`NumberVerbalizer_Fraction_SingularPluralAndSpecialDenominators`

`3.14`, `0.05`, `5.0` and `.5` give `three point one four`, `zero point zero five`, `five point zero` and `point five`;
an integer part with a leading zero is spelled digit by digit (`007.5`); 32 fraction digits are accepted and 33 are
rejected; a digit sequence gives one word per digit (`0123456789`), 64 digits are spelled and 65 are rejected. For
fractions `1/2` is `one half`, `2/3` `two thirds`, `3/4` `three quarters`, `1/4` `one quarter`, `7/16`
`seven sixteenths`, `3/32` `three thirty seconds` and `37/100` `thirty seven hundredths`, and a numerator that is not
below the denominator or a denominator beyond the limit throws.

##### Clock times and calendar

**Tests**: `NumberVerbalizer_Clock_AllHoursAndMinutes_MatchReference`,
`NumberVerbalizer_Calendar_MonthNamesAndDayOrdinals_AreSpoken`

All 24 x 60 times, with and without the omitted zero minute and with a leading zero of the hour, equal the reference,
and the named forms are asserted: `5:00` `five o'clock`, `17:00` `seventeen hundred`, `07:05` `oh seven oh five` and
`5:05` `five oh five`. The twelve month names, the ordinals of days 1 to 31 and the dates `December twenty fifth twenty
twenty four` and `January first oh five` are checked; day 0, day 32, month 0 and month 13 throw.

##### Output bound, culture invariance and argument validation

**Tests**: `NumberVerbalizer_WorstCaseInputs_StayWithinOutputBound`,
`NumberVerbalizer_UnderForeignCultures_GivesIdenticalWords`, `NumberVerbalizer_InvalidDigits_ThrowArgumentException`

The worst cases (15-digit cardinals and ordinals, 64 digits one by one, a decimal with 64 integer digits and 32 fraction
digits) are at most 1,024 characters; for generated inputs of every method each word of the value is in the reference
vocabulary and no character is an ASCII digit. A fixed set of inputs is spoken under `tr-TR`, `ar-SA`, `de-DE`, `th-TH`,
`fa-IR` and the invariant culture as the current and current UI culture, and all results equal the invariant ones. An
empty span, a letter, a full-width digit, an Arabic-Indic digit, a sign or a separator in the digits, more than 15
significant digits for a cardinal or an ordinal, a year that is not four digits, an hour above 23, minutes above 59 or
not two digits, and a decade that is not a decade (`95`, `00`, `2100`) each throw `ArgumentException`.
