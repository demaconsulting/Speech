### NumberVerbalizer

**Purpose**: Build the English words for numbers: cardinals, ordinals, years, decades, decimals, digit
sequences, fractions, clock times and calendar dates. The unit answers one question for already validated
ASCII digits: what are the words. It decides nothing about which text is a number, which reading applies or
what a span covers; that belongs to `NumericExpressionRecognizer`. It is a pure, stateless function library.

**Data Model**: `NumberVerbalizer` is an `internal static` class. It holds only read-only tables and
constants, and a small stack-only writer type declared in the same file.

*The spoken-text style* is the convention of *SpeechTextEn System Design*, *Spoken-Text Conventions*: lowercase
ASCII words separated by single spaces, **no hyphens, no commas, no digits** (`twenty four`, never
`twenty-four`), capitals only in the month names. The style follows from one reason: a phoneme backend then
sees only plain words, no compound-word hyphenation and no clause punctuation that would change how the
text is split into clauses.

*The limits* are named constants. The recognizer enforces them before it calls, so a call outside them is a
defect of the caller and throws.

| Constant | Value |
| --- | --- |
| `MaxCardinalDigits` | 15 |
| `MaxSpelledDigits` | 64 |
| `MaxFractionDigits` | 32 |
| `MaxSpokenLength` | 1,024 |

**Constant details** (Meaning):

- `MaxCardinalDigits`: The most significant digits a cardinal, ordinal or year-like reading accepts (999 trillion);
  leading zeros do not count
- `MaxSpelledDigits`: The most digits read one by one
- `MaxFractionDigits`: The most digits after a decimal point that are read
- `MaxSpokenLength`: The size in characters of the working buffer and so the upper bound of any spoken value

*The vocabulary* is a closed set of read-only arrays: the words of 0 to 19 (`zero` to `nineteen`), the tens
(`twenty` to `ninety`, including the standard spelling `forty`), the scale words (`thousand`, `million`, `billion`,
`trillion`), the plural decade words (`tens`, `twenties`, `thirties`, `forties`, `fifties`, `sixties`,
`seventies`, `eighties`, `nineties`) and the twelve month names. Any value the unit produces is made of these
words plus `hundred`, `oh`, `o'clock`, `point`, `and`, the ordinal forms (`first`, `second`, `third`,
`fifth`, `eighth`, `ninth`, `twelfth` and the regular `-th`/`-ieth` forms), the fraction words (`half`,
`halves`, `quarter`, `quarters`, `hundredth`, `hundredths` and the ordinals) and the plural decade forms
`hundreds` and `thousands`. A property test proves that no value contains a digit or a word outside this
closed set.

*The writer* `SpokenWriter` is a `ref struct` over a caller-supplied `Span<char>` (the methods use
`stackalloc char[1024]`), so building a value allocates one `string` (the result) and nothing else. It appends
a word preceded by exactly one space unless it is empty, can append characters directly after the last word
(the plural `s`), replace the last word (a cardinal word becomes its ordinal word) and read the last word.
Exceeding the capacity throws `InvalidOperationException` rather than truncating; the documented caps make
that impossible (the worst case, 64 digit words, is 64 words of at most five letters plus the separators).

**Reading rules.** All numbers are read in groups of three digits from the left (the first group holds the
remainder), each non-zero group as hundreds and a below-100 part, followed by its scale word.

- **Under 100**
  - Rule: 0 to 19 are one word; 20 and above are the tens word and, when not zero, the ones word, separated by a space
  - Examples: `forty two`, `nineteen`, `ninety nine`
- **Under 1,000**
  - Rule: The hundreds digit, `hundred`, then the rest; no `and`
  - Examples: `one hundred one`, `nine hundred ninety nine`
- **Cardinal**
  - Rule: Groups of three with scale words; zero groups are skipped; leading zeros are ignored; all-zero input is `zero`
  - Examples: `one thousand two hundred thirty four`, `one million one`, `zero`
- **Ordinal**
  - Rule: The cardinal with its last word replaced by the ordinal word: `one` first, `two` second, `three` third, `five`
    fifth, `eight` eighth, `nine` ninth, `twelve` twelfth, a word ending in `y` becomes `-ieth`, any other word takes
    `th`
  - Examples: `twenty first`, `one hundredth`, `one thousandth`, `eleventh`, `zeroth`
- **Integer as read in running text**
  - Rule: A number of more than one digit with a leading zero is an identifier and is read digit by digit (`007` gives
    `zero zero seven`); any other is a cardinal
  - Examples: `zero zero seven`, `forty two`
- **Digits**
  - Rule: Each digit as its word, one by one
  - Examples: `one two three`, `zero zero`
- **Decimal**
  - Rule: The integer part (as above; omitted when empty, so `.5` gives `point five`), `point`, then the fraction digits
    one by one with zeros kept
  - Examples: `three point five`, `five point zero`, `point five`, `one point two three four`
- **Year**
  - Rule: A four-digit value from 1000 to 2099 is read as two pairs: the first two digits as a number under 100 (or
    `ten` to `twenty`), then the last two: `hundred` for `00`, `oh` and the digit for `01` to `09`, the number
    otherwise. The values both readings speak alike (1000, 2000, 2001 to 2009) are read as the cardinal, and
    `IsYearReadingDifferent` reports false for them. Any other four-digit value is the cardinal
  - Examples: `nineteen ninety nine`, `nineteen oh five`, `nineteen hundred`, `eleven hundred`, `ten sixty six`,
    `twenty ten`, `twenty twenty four`, `twenty ninety nine`, `two thousand`, `two thousand five`
- **Decade**
  - Rule: Two digits ending in zero (`90`, tens digit 1 to 9): the plural decade word. Four digits ending in zero (1000
    to 2099): the year pair with `hundreds` for `00`, `thousands` for a multiple of 1000, otherwise the plural decade
    word
  - Examples: `nineties`, `nineteen nineties`, `nineteen hundreds`, `two thousands`, `twenty tens`, `eighteen hundreds`
- **Fraction**
  - Rule: The numerator as a cardinal, then the denominator word, plural when the numerator is not 1: `half`/`halves`
    for 2, `quarter`/`quarters` for 4, `hundredth`/`hundredths` for 100, otherwise the denominator's ordinal, with `s`
    appended for the plural. The numerator is at least 1, the denominator above the numerator and at most 100
  - Examples: `one half`, `three quarters`, `two thirds`, `seven sixteenths`, `nine tenths`, `thirty seven hundredths`,
    `three thirty seconds`
- **Clock time**
  - Rule: The hour (0 to 23; one or two digits) then the minute (exactly two digits, 00 to 59). A written leading zero
    in the hour reads as `oh` and the digit (`07` gives `oh seven`, `00` gives `zero`). Minute `00` gives `o'clock` for
    an hour 1 to 12 written without a leading zero and `hundred` otherwise, or nothing when a meridiem follows
    (`omitZeroMinute`); minutes 01 to 09 read `oh` and the digit; others as a number
  - Examples: `five thirty`, `five oh five`, `five o'clock`, `seventeen forty five`, `seventeen hundred`,
    `oh seven oh five`, `oh seven hundred`, `zero thirty`, `zero hundred`
- **Calendar date**
  - Rule: The month name, the day (1 to 31) as an ordinal and the year: a four-digit year as the year reading, a
    two-digit year as a cardinal with `oh` and the digit for `01` to `09`
  - Examples: `December twenty fifth twenty twenty four`, `January second twenty four`, `January first oh five`

**Key Methods** (all `internal static`; each `string`-returning method is a thin wrapper that creates a
writer over a stack buffer, calls the `Append...` method of the same name and returns the text):

- **Cardinal(digits)**, **Ordinal(digits)**: the cardinal or ordinal words of 1 to 15 significant ASCII
  digits. More than 15 significant digits throws `ArgumentException`
- **Year(digits)**: exactly four digits, as the year reading above
- **Decade(digits)**: two or four digits ending in zero, as the decade reading, without the trailing `s`
- **Digits(digits)**: one to 64 digits one by one
- **Decimal(integerDigits, fractionDigits)**: the integer part (may be empty) and one to 32 fraction digits
- **Fraction(numerator, denominator)**: as the fraction reading
- **Clock(hour, minute, omitZeroMinute)**: as the clock reading
- **DayOrdinal(day)**: the ordinal words of a day, 1 to 31
- **MonthName(month)**: the capitalized month name, 1 to 12
- **AppendCardinal, AppendOrdinal, AppendInteger, AppendDigits, AppendDecimal, AppendYear, AppendDecade,
  AppendFraction, AppendClock, AppendDate**: the writer-based forms the recognizer uses to compose longer
  values (a currency amount, a date) in one buffer without intermediate strings
- **IsYearReadingDifferent(value)**: whether the year reading of a value (0 to 9999) differs from its
  cardinal; false outside 1000 to 2099 and for 1000, 2000 and 2001 to 2009. The recognizer uses it so that
  a year that reads the same either way is never counted as an ambiguous reading

**Algorithm.** Every method validates its input first (`RequireDigits`: non-empty, characters `0` to `9` only,
never `char.IsDigit`, which accepts other scripts), then walks the digits with `char` arithmetic
(`digit - '0'`) over a fixed number of positions: a cardinal visits each digit once and appends a constant
number of words per group of three digits, a decimal reads at most 32 fraction digits, a digit sequence at most
64. There are no
loops over input of unbounded length, no recursion, no parsing into a wide integer and no culture-sensitive
call, so the cost is proportional to the number of digits, which the caps bound by a constant.

**Thread safety and culture.** The class is stateless; its tables are never modified after type
initialization, so concurrent calls are safe and give identical results. It uses no `ToString`, `Parse`,
`ToLower`, `ToUpper` or comparison that takes a culture, so the current culture can never change a result
(a test runs it under Turkish, Arabic, German, Thai and Persian cultures and compares with the invariant
result). A writer lives on the caller's stack and is never shared.

**Error Handling**: the unit validates, and never guesses. A span that is empty or holds a character other
than `0` to `9`, a value outside a documented cap (more than 15 significant digits for a cardinal, ordinal or
year-like reading, more than 64 for digit-by-digit, more than 32 fraction digits, a year that is not four
digits, a decade that is not a decade, an hour or minute out of range) throws `ArgumentException`; a month,
day, numerator or denominator out of range throws `ArgumentOutOfRangeException`; a writer that would exceed
its capacity throws `InvalidOperationException`. None of these can happen when the recognizer calls the
unit, because it checks every cap first, so an exception here is a defect of the caller and is never caused
by the text being normalized. Nothing is caught and nothing is logged.

**Dependencies**: the framework only (spans and `ArgumentException` types). No other unit and no OTS item.

**Callers**: `NumericExpressionRecognizer` (every spoken value), and the tests.
