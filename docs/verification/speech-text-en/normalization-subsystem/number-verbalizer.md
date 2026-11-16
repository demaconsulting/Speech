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
the domain is small and with seeded random values elsewhere, so a failure is reproducible.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the test project
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Dependencies**: the test-only reference `Fakes/ReferenceNumberWords.cs`; no network, files or hardware
- **Isolation**: each test builds its own inputs; the culture tests restore the current cultures in a `finally`
  block

#### Acceptance Criteria

The cardinal words of every value from 0 to 9999, of 10,000 seeded random values of up to 15 digits and of
every scale boundary (999, 1,000, 999,999, 1,000,000, and so on up to 999 trillion, with the skipped empty
groups) equal the reference, with no hyphen, comma or `and`; the ordinal words of every value from 0 to 9999
equal the reference and the ordinal suffix rule (`st`, `nd`, `rd`, `th`, including 11, 12, 13, 111 and
112) holds for 1 to 1000; the year words of every four-digit value from 1000 to 2099 equal the reference (with
`oh` for the single digits, `hundred` for `00`, and 1000, 2000 and 2001 to 2009 equal to the cardinal), and the
decade words of the two- and four-digit decades match the documented table; the decimal words keep every
fraction digit including zeros and omit an empty integer part; digit sequences have one word per digit; the
fraction words are right for every denominator class; the clock words of every hour 0 to 23 and every minute 00 to
59 equal the reference, including `o'clock`, `hundred`, `oh` and the leading-zero forms; the month names and day
ordinals are right; for the worst-case inputs (15-digit cardinals and ordinals, 64 digits one by one, a decimal
with 64 integer digits spelled and 32 fraction digits) the value is at most 1,024 characters, and every value
the unit produces for generated inputs has no ASCII digit, consists of letters, spaces and the apostrophe, and
uses only words of the reference vocabulary; the words are identical under foreign cultures; and every
invalid input throws.

#### Test Scenarios

##### Cardinals

**Tests**: `NumberVerbalizer_Cardinal_EveryValueFrom0To9999_MatchesReference`,
`NumberVerbalizer_Cardinal_RandomValuesUpTo15Digits_MatchesReference`,
`NumberVerbalizer_Cardinal_ScaleBoundaries_UseCorrectScaleWords`

The first compares all 10,000 values, with and without leading zeros. The second compares 10,000 seeded random
values whose digit count is itself random from 1 to 15. The third checks the boundaries, such as 1,000,000 (`one
million`), 1,000,001 (`one million one`), 10^12 (`one trillion`) and 999,999,999,999,999 (the longest value),
that groups of zero are skipped and that `zero` is spoken only for a value of zero.

##### Ordinals

**Tests**: `NumberVerbalizer_Ordinal_EveryValueFrom0To9999_MatchesReference`,
`NumberVerbalizer_OrdinalSuffix_MatchesNumber_For1To1000`

The first compares all ordinal words (`first`, `eleventh`, `twenty first`, `one hundredth`, `one thousandth`,
`zeroth`). The second asserts that the suffix a number requires (`1st`, `2nd`, `3rd`, `4th`, `11th`, `12th`,
`13th`, `21st`, `101st`, `111th`) agrees with the ordinal word's ending for every number from 1 to 1000.

##### Years and decades

**Tests**: `NumberVerbalizer_Year_EveryYearFrom1000To2099_MatchesReference`,
`NumberVerbalizer_Decade_Forms_AreSpoken`

Every year from 1000 to 2099 equals the reference, and `IsYearReadingDifferent` is false exactly for 1000, 2000
and 2001 to 2009. The decade test asserts `nineties`, `nineteen nineties`, `nineteen hundreds`, `two
thousands`, `twenty tens`, `eighteen hundreds`, `tens`, `twenties` and that `5`, `00`, `1995` and `2100` throw.

##### Decimals and digit sequences

**Tests**: `NumberVerbalizer_Decimal_FractionDigitsAreSpelledIncludingZeros`,
`NumberVerbalizer_Digits_SpellsEveryDigit`

`3.5`, `0.05`, `5.0`, `.5` and `1.234` give `three point five`, `zero point zero five`, `five point zero`,
`point five` and `one point two three four`; an integer part with a leading zero is spelled; `007` gives `zero zero
seven`; 32 fraction digits are accepted and 33 are rejected; 64 digits are spelled and 65 are rejected.

##### Fractions

**Test**: `NumberVerbalizer_Fraction_SingularPluralAndSpecialDenominators`

`1/2` `one half`, `3/2` is rejected (not below the denominator), `2/3` `two thirds`, `3/4` `three quarters`,
`1/4` `one quarter`, `7/16` `seven sixteenths`, `3/32` `three thirty seconds`, `1/100` `one hundredth`,
`37/100` `thirty seven hundredths`, and the numerator and denominator limits throw.

##### Clock times

**Tests**: `NumberVerbalizer_Clock_AllHoursAndMinutes_MatchReference`,
`NumberVerbalizer_Clock_OClockHundredAndLeadingZeroForms`

All 24 x 60 times, with and without the omitted zero minute, equal the reference. The second asserts `5:00` `five
o'clock`, `12:00` `twelve o'clock`, `17:00` `seventeen hundred`, `0:00` and `00:00` `zero hundred`, `07:00` `oh
seven hundred`, `07:05` `oh seven oh five`, `5:05` `five oh five`, and that the omitted zero minute leaves the hour
alone.

##### Calendar

**Test**: `NumberVerbalizer_Calendar_MonthNamesAndDayOrdinals_AreSpoken`

The twelve month names, the ordinals of days 1 to 31 and the dates `December twenty fifth twenty twenty four`,
`January second twenty four` and `January first oh five`; day 0, day 32, month 0 and month 13 throw.

##### Output bound and closed vocabulary

**Tests**: `NumberVerbalizer_WorstCaseInputs_StayWithinOutputBound`,
`NumberVerbalizer_AnyOutput_HasNoDigitAndOnlyReferenceVocabulary`

The worst cases are at most 1,024 characters (the longest, 64 digits one by one, is a few hundred). For generated
inputs of every method, each word of the value is in the reference vocabulary and no character is an ASCII digit.

##### Culture invariance

**Test**: `NumberVerbalizer_UnderForeignCultures_GivesIdenticalWords`

A fixed set of inputs is spoken under `tr-TR`, `ar-SA`, `de-DE`, `th-TH`, `fa-IR` and the invariant culture as the
current and current UI culture, and all results equal the invariant ones.

##### Argument validation

**Test**: `NumberVerbalizer_InvalidDigits_ThrowArgumentException`

An empty span, a letter, a full-width digit, an Arabic-Indic digit, a sign or a separator in the digits, more than
15 significant digits for a cardinal, an ordinal or a year-like reading, a year that is not four digits, an hour
above 23, minutes above 59 or not two digits, and a decade that is not a decade each throw `ArgumentException`.
