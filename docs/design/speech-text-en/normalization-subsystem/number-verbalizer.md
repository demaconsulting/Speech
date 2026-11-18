### NumberVerbalizer

**Purpose**: Build the English words for numbers: cardinals, ordinals, years, decades, decimals, digit
sequences, fractions, clock times and calendar dates. The unit answers one question for already validated
ASCII digits: what are the words. It decides nothing about which text is a number, which reading applies or
what a span covers; that belongs to `NumericExpressionRecognizer`. It is a pure, stateless function library.

**Data Model**: `NumberVerbalizer` is an `internal static` class. It holds only read-only tables and
constants, and a small stack-only writer type declared in the same file.

*The spoken-text style* is the convention of *SpeechTextEn System Design*, *Spoken-Text Conventions*: lowercase
ASCII words separated by single spaces, **no hyphens, no commas, no digits** (`twenty four`, never
`twenty-four`), capitals only in the month names, so a phoneme backend sees only plain words.

*The limits* are named constants. The recognizer enforces them before it calls, so a call outside them is a
defect of the caller and throws.

- `MaxCardinalDigits` = 15: the most significant digits a cardinal, ordinal or year-like reading accepts (999
  trillion); leading zeros do not count
- `MaxSpelledDigits` = 64: the most digits read one by one
- `MaxFractionDigits` = 32: the most digits after a decimal point that are read
- `MaxSpokenLength` = 1,024: the size in characters of the working buffer and so the upper bound of any spoken
  value

*The vocabulary* is a closed set of read-only arrays: the words of 0 to 19, the tens (`twenty` to `ninety`,
including the standard spelling `forty`), the scale words (`thousand`, `million`, `billion`, `trillion`), the
plural decade words (`tens` to `nineties`) and the twelve month names. Any value the unit produces is made of
these words plus `hundred`, `oh`, `o'clock`, `point`, `and`, the ordinal forms, the fraction words (`half`,
`halves`, `quarter`, `quarters`, `hundredth`, `hundredths` and the ordinals) and the plural decade forms
`hundreds` and `thousands`; a property test proves that no value contains a digit or a word outside this set.

*The writer* `SpokenWriter` is a `ref struct` over a caller-supplied `Span<char>` (`stackalloc char[1024]`), so
building a value allocates one `string` (the result) and nothing else. It appends a word preceded by exactly
one space unless it is empty, can append characters directly after the last word (the plural `s`), replace the
last word (a cardinal word becomes its ordinal word) and read the last word. Exceeding the capacity throws
`InvalidOperationException` rather than truncating; the documented caps make that impossible.

**Reading rules.** All numbers are read in groups of three digits from the left (the first group holds the
remainder), each non-zero group as hundreds and a below-100 part, followed by its scale word.

- **Under 100**: 0 to 19 are one word; 20 and above are the tens word and, when not zero, the ones word
  (`forty two`, `nineteen`)
- **Cardinal**: groups of three with scale words; hundreds digit, `hundred`, then the rest; no `and`; zero groups
  are skipped; leading zeros are ignored; all-zero input is `zero` (`one hundred one`, `one thousand two hundred
  thirty four`, `one million one`)
- **Ordinal**: the cardinal with its last word replaced by the ordinal word: `one` first, `two` second, `three`
  third, `five` fifth, `eight` eighth, `nine` ninth, `twelve` twelfth, a word ending in `y` becomes `-ieth`, any
  other word takes `th` (`twenty first`, `one hundredth`, `eleventh`, `zeroth`)
- **Integer in running text**: a number of more than one digit with a leading zero is an identifier, read digit
  by digit (`zero zero seven`); any other is a cardinal
- **Digits**: each digit as its word, one by one (`one two three`)
- **Decimal**: the integer part (omitted when empty), `point`, then the fraction digits one by one with zeros
  kept (`three point five`, `five point zero`, `point five`)
- **Year**: a four-digit value from 1000 to 2099 is read as two pairs: the first two digits as a number under
  100 (or `ten` to `twenty`), then the last two: `hundred` for `00`, `oh` and the digit for `01` to `09`, the
  number otherwise (`nineteen ninety nine`, `nineteen oh five`, `nineteen hundred`, `ten sixty six`, `twenty
  twenty four`). The values both readings speak alike (1000, 2000, 2001 to 2009) are read as the cardinal
  (`two thousand five`), as is any other four-digit value
- **Decade**: two digits ending in zero (tens digit 1 to 9): the plural decade word (`nineties`). Four digits
  ending in zero (1000 to 2099): the year pair with `hundreds` for `00`, `thousands` for a multiple of 1000,
  otherwise the plural decade word (`nineteen nineties`, `nineteen hundreds`, `two thousands`, `twenty tens`)
- **Fraction**: the numerator as a cardinal, then the denominator word, plural when the numerator is not 1:
  `half`/`halves` for 2, `quarter`/`quarters` for 4, `hundredth`/`hundredths` for 100, otherwise the
  denominator's ordinal, with `s` for the plural (`one half`, `three quarters`, `two thirds`, `seven sixteenths`,
  `thirty seven hundredths`). Numerator at least 1, denominator above the numerator and at most 100
- **Clock time**: the hour (0 to 23; one or two digits) then the minute (exactly two digits, 00 to 59). A written
  leading zero in the hour reads as `oh` and the digit (`07` gives `oh seven`, `00` gives `zero`). Minute `00`
  gives `o'clock` for an hour 1 to 12 written without a leading zero and `hundred` otherwise, or nothing when a
  meridiem follows; minutes 01 to 09 read `oh` and the digit; others as a number (`five thirty`, `five oh five`,
  `five o'clock`, `seventeen hundred`, `oh seven oh five`, `zero thirty`)
- **Calendar date**: the month name, the day (1 to 31) as an ordinal and the year: a four-digit year as the year
  reading, a two-digit year as a cardinal with `oh` and the digit for `01` to `09` (`December twenty fifth twenty
  twenty four`, `January first oh five`)

**Key Methods** (all `internal static`):

- **Cardinal(digits)**, **Ordinal(digits)**: the words of 1 to 15 significant ASCII digits; more throws
  `ArgumentException`
- **Decade(digits)**: two or four digits ending in zero, as the decade reading, without the trailing `s`
- **Digits(digits)**: one to 64 digits one by one
- **Fraction(numerator, denominator)**: as the fraction reading
- **MonthName(month)**: the capitalized month name, 1 to 12
- **AppendCardinal, AppendOrdinal, AppendInteger, AppendDigits, AppendDecimal, AppendYear, AppendDecade,
  AppendFraction, AppendClock, AppendDate**: the writer-based forms the recognizer uses to compose longer
  values (a currency amount, a date) in one buffer without intermediate strings; the `string`-returning
  methods above are thin wrappers over them
- **IsYearReadingDifferent(value)**: whether the year reading of a value (0 to 9999) differs from its
  cardinal; false outside 1000 to 2099 and for 1000, 2000 and 2001 to 2009, so a year that reads the same
  either way is never counted as an ambiguous reading

**Algorithm.** Every method validates its input first (non-empty, characters `0` to `9` only, never
`char.IsDigit`, which accepts other scripts), then walks the digits with `char` arithmetic over a fixed number
of positions. There is no unbounded loop, recursion, wide-integer parsing or culture-sensitive call, so the
cost is proportional to the number of digits, which the caps bound by a constant.

**Thread safety and culture.** The class is stateless; its tables are never modified after type
initialization, so concurrent calls are safe and give identical results. It uses no culture-sensitive
formatting, parsing, casing or comparison, so the current culture can never change a result. A writer lives on
the caller's stack and is never shared.

**Error Handling**: the unit validates, and never guesses. A span that is empty or holds a character other
than `0` to `9`, a value outside a documented cap, a year that is not four digits, a decade that is not a
decade, or an hour or minute out of range throws `ArgumentException`; a month, day, numerator or denominator
out of range throws `ArgumentOutOfRangeException`; a writer that would exceed its capacity throws
`InvalidOperationException`. None of these can happen when the recognizer calls the unit, because it checks
every cap first, so an exception here is a defect of the caller and is never caused by the text being
normalized. Nothing is caught and nothing is logged.

**Dependencies**: the framework only. No other unit and no OTS item.

**Callers**: `NumericExpressionRecognizer` (every spoken value), and the tests.
