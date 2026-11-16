### NumericExpressionRecognizer

**Purpose**: Recognize the numeric expressions of English text at a token and say how each token of the
expression is spoken: numbers, ordinals, decimals, fractions, years and decades, currency, percent, units,
times, dates and digit groups. The unit answers one question for one candidate token: does an expression start
here, which spans does it consist of, how many tokens does it consume, and was the reading a counted default
or a numeric fallback. It builds no `AlignedText`, decides no period or sentence break and keeps no state;
`TextNormalizer` owns those decisions and `NumberVerbalizer` builds the words.

**Data Model**: `NumericExpressionRecognizer` is an `internal static` class holding read-only tables, the
limits below and two internal value types:

- `NumericSpan(Start, Length, Spoken, Kind)`: one replacement the match asks for. `Kind` is `Number`,
  `Currency`, `DateTime` or `Unit` (see _NormalizationAlignment Design_)
- `NumericMatch`: a readonly struct (no list is allocated per token) with up to two spans (`SpanCount`,
  indexer), `TokensConsumed` (at least 1), `Ambiguous` (at most one count per match), `Fallback` (the token
  looks numeric but is left as written, or was read digit by digit because of a limit) and `PeriodUnit` (the
  last span is a unit token that may carry an abbreviation period, so `TextNormalizer` applies its period and
  sentence rules to it). A match with no span and `Fallback` set means "numeric-looking, left as written"

_Limits._ They bound the work per token and the size of any value, and are enforced before a reading is built.

| Limit | Value | Behavior beyond it |
| --- | --- | --- |
| Token length scanned (`MaxTokenLength`) | 128 characters | A token longer than this that starts like a number (digit, sign and digit, `.` and digit, `#` and digit, a currency symbol and a digit) is left as written and counted as a numeric fallback, **without scanning it** |
| Digit groups per number (`MaxGroups`) | 8 (digits separated by one punctuation character) | Left as written, fallback |
| Cardinal | 15 digits (999 trillion) | 16 to 64 digits: read digit by digit and counted as a fallback; more than 64 digits: left as written, fallback |
| Fraction digits of a decimal | 32 | More: left as written, fallback |
| Ordinal | 15 digits | More: left as written, fallback |
| Digit-by-digit total of a hyphen or dotted group number | 64 digits | More: left as written, fallback |
| Neighbor token examined as a word (unit, month, cue word) | 16 characters | A longer neighbor is never a cue |
| Look-behind and look-ahead | at most 3 tokens, each a constant-time test | - |
| Spoken value | 1,024 characters | Asserted for the worst case; the writer throws rather than truncates |

_Tokens and context._ The tokenizer is whitespace driven and not uniform: `$5.50` is two tokens (`$`, `5.50`),
`¥500` and `-$5` one, `50%` two (`50`, `%`), `5pm` one, `5-10` one, `5–10` (en dash) three, `1990's` one and
`'90s` two, and `Jan. 5, Feb. 3` breaks sentences after each month. The recognizer therefore classifies whole
tokens by shape and never assumes a split. Two tokens are **joined** when they are adjacent or separated by
exactly one space, no-break space (U+00A0) or narrow no-break space (U+202F). The analyzer has already
turned a tag placeholder into one space, so a number, a tag and a unit with no space (`5` tag `kg`) still
join. Every neighbor access first checks the neighbor's length and bounds (never `Start + Length` before
`Length > text.Length - Start`), so a hostile token list cannot overflow or cause a long scan.

_Vocabularies_ (private arrays in the unit, exposed only as cached read-only views, all case-sensitive unless
stated):

- **Months**: the twelve Title Case full names and the abbreviations `Jan Feb Mar Apr Jun Jul Aug Sep Sept Oct
  Nov Dec`, each with or without one trailing period (`IsMonthToken`). `May` is a month only in Title Case
  and only with a day or year neighbor (`May 5`, `in May 2024`); `may 5` and `May I go?` are untouched. Only
  the period of a month **abbreviation** (`Jan.`, `Sept.`) continues a date: the abbreviation lexicon consumes
  that period and suppresses the tokenizer's break after it. A full name and `May` have no lexicon row, so the
  period after them ends the sentence and the next number has no date context (`May. 5 people`)
- **Weekday abbreviations**: `Mon Tue Tues Wed Thu Thur Thurs Fri Sat Sun`. They add no numeric behavior;
  the list is shared with the lexicon test
- **Year cues** (previous token, ignoring case): in, since, until, till, by, during, circa, before, after, year
- **Quantity cues** (previous token, ignoring case): about, around, over, under, nearly, approximately,
  roughly, exactly, only, just, almost, than, all, some, another, these, those, many, few, total
- **Quantity nouns** (next token, ignoring case): people, persons, users, items, words, steps, times, days,
  hours, minutes, seconds, dollars, euros, pounds, miles, feet, meters, kilometers, units, pieces, copies,
  cases, points, votes, members, students, employees, customers, participants, visitors, calories, years
- **Eras** (next token, after a four-digit number): `AD`, `A.D.`, `BC`, `B.C.`, `BCE`, `CE`
- **Magnitude words** (lowercase): thousand, million, billion, trillion
- **Fraction denominators**: 2 to 10, 16, 32, 64, 100
- **Vulgar fractions**: `½ ⅓ ⅔ ¼ ¾ ⅛ ⅜ ⅝ ⅞`
- **Currencies**: `$` and `USD` (dollar(s), cent(s)); `€` and `EUR` (euro(s), cent(s)); `£` and `GBP`
  (pound(s), penny/pence); `¥` and `JPY` (yen, yen; no minor unit)
- **Units** (case-sensitive; single-letter and word-like units `m`, `g`, `l`, `s`, `in` are excluded as
  ambiguous):

| Written | Singular | Plural | Written | Singular | Plural |
| --- | --- | --- | --- | --- | --- |
| `mm` | millimeter | millimeters | `MB` | megabyte | megabytes |
| `cm` | centimeter | centimeters | `GB` | gigabyte | gigabytes |
| `km` | kilometer | kilometers | `TB` | terabyte | terabytes |
| `ft`, `Ft` | foot | feet | `Hz` | hertz | hertz |
| `yd` | yard | yards | `kHz` | kilohertz | kilohertz |
| `mi` | mile | miles | `MHz` | megahertz | megahertz |
| `mph` | mile per hour | miles per hour | `GHz` | gigahertz | gigahertz |
| `km/h` | kilometer per hour | kilometers per hour | `ms` | millisecond | milliseconds |
| `mg` | milligram | milligrams | `hr`, `hrs` | hour | hours |
| `kg` | kilogram | kilograms | `min` | minute | minutes |
| `lb`, `lbs` | pound | pounds | `°` | degree | degrees |
| `oz` | ounce | ounces | `°C` | degree Celsius | degrees Celsius |
| `ml` | milliliter | milliliters | `°F` | degree Fahrenheit | degrees Fahrenheit |
| `KB` | kilobyte | kilobytes | | | |

The units `ft`, `Ft`, `yd`, `mi`, `lb`, `lbs`, `oz`, `hr`, `hrs` and `min` may carry an abbreviation period
(`ft.`). A unit is singular only for exactly `1` (no fraction, no leading zero); `1.0` and `1.5` are plural.

**Span shapes (normative).** Every span the unit produces has one of these shapes, none contains whitespace,
and a `,` or `:` appears only between two ASCII digits:

| Shape | Description | Examples |
| --- | --- | --- |
| S1 | One token | `42`, `$5` (when the tokenizer glues them), `5kg`, `1,234.50` |
| S2 | One token and its adjacent period token | produced by `TextNormalizer` for a period-bearing unit (`lbs` and `.`), never by this unit alone |
| S3 | A currency symbol token and its adjacent number token | `$` `5.50` |
| S4 | An apostrophe token and its adjacent decade token | `'` `90s` |
| S5 | Each of the two halves of one token split into adjacent, non-overlapping spans: the hour or time words and the meridiem letters of a glued meridiem | `5pm` gives `5` (`five`) and `pm` (`P M`); `5:30pm` gives `5:30` (`five thirty`) and `pm` (`P M`) |

A construct of several tokens becomes several spans in source order, one per token or S3/S4 pair (a glued
meridiem is the one case of a single token split into two spans, S5), never
reordered and never merged across a gap; a unit or currency word is attached to the unit token or the
magnitude word. `$5 million` is the S3-or-S1 span `$5` (`five`) and the span `million` (`million dollars`);
`5 million USD` is the span `5` (`five`), the consumed word `million` (no span) and the span `USD`
(`dollars`); `January 5, 2024` is the spans `5` (`fifth`) and `2024` (`twenty twenty four`), leaving `January`
and the comma untouched; `3 1/2` is `3` (`three`) and `1/2` (`and one half`); `50 %` is `50` (`fifty`) and
`%` (`percent`).

#### Reading policy

The policy is normative: it is fixed, lexical (shapes, symbols, a few cue words; never the part of speech)
and en-US. Every row of the tables below is a golden row of the subsystem tests, as are the deferrals. Spoken
values follow the conventions of _SpeechTextEn System Design_, _Spoken-Text Conventions_.

**Numeric alphabet.** Only the ASCII digits `0-9` make a number. Full-width, Arabic-Indic, mathematical and
superscript digits are never numbers (identity); `char.IsDigit` is never used by this unit or by
`NumberVerbalizer`.

**Shape classification.** A token that starts like a number is scanned once, left to right, into a sign
(`-`, `+` or U+2212), an optional currency symbol or `#`, up to eight digit groups with the separator before
each (`,` `.` `:` `/` `-`; a separator continues the number only when a digit follows it) and the text after
the digits. A character outside this alphabet or a suffix that is not in the tables makes the token
**not numeric**: identity, no fallback count (`3D`, `101A`, `1e5`, `0x1F`, `1_000`, `Q3`). Otherwise the
separators decide the shape, and the shape decides the reader, in this order:

1. a single group (an integer, or a decimal when one period separates two groups, or en-US thousands when
   commas do): the plain-number readers below;
2. commas and periods only but not a valid en-US number (`1,23`, `1.234,5`, `12,34,567`, `1,2,3`): left as
   written, **fallback**;
3. a colon: a time; slashes: a fraction or a date; hyphens: a range, phone number, identifier or date; three
   or more periods: a dotted number; any other mixture of separators (`10.0.0.1/24`): identity.

**Integers, decimals, ordinals, digit sequences.**

| Input token(s) | Spoken | Notes |
| --- | --- | --- |
| `0`, `7`, `42`, `100`, `101`, `999` | `zero`, `seven`, `forty two`, `one hundred`, `one hundred one`, `nine hundred ninety nine` | `forty` is the standard spelling; no `and` |
| `1,234`, `1,000,000`, `999,999,999,999,999` | `one thousand two hundred thirty four`, `one million`, `nine hundred ninety nine trillion ...` | thousands separators must be exactly one to three digits (no leading zero) then groups of three |
| `-5`, `−7` (U+2212), `+3`, `-$5` | `minus five`, `minus seven`, `plus three` | a sign only when glued to the digits; with a time, date, range or dotted shape it is not read |
| `007`, `00`, `0123` | `zero zero seven`, `zero zero`, `zero one two three` | a leading zero on more than one digit is an identifier, digit by digit, not a fallback (up to 64 digits) |
| 10 to 15 digits without separators | cardinal | `1234567890` is `one billion ...`; no phone guess |
| 16 to 64 digits | digit by digit | **fallback +1** |
| more than 64 digits, a token over 128 characters, more than 8 groups | left as written | **fallback +1**, no span |
| `1,23`, `1.234,5`, `12,34,567` | left as written | not en-US: **fallback +1** |
| `3.5`, `0.5`, `.5`, `5.0`, `-3.5`, `1,234.5`, `1.234` | `three point five`, `zero point five`, `point five`, `five point zero`, `minus three point five`, `one thousand two hundred thirty four point five`, `one point two three four` | fraction digits one by one, zeros kept; a period is a decimal point, never thousands; more than 32 fraction digits: left as written, **fallback** |
| `1st`, `2nd`, `3rd`, `4th`, `11th`, `12th`, `13th`, `21st`, `22nd`, `101st`, `100th`, `1,000th`, `1ST` | `first`, `second`, `third`, `fourth`, `eleventh`, `twelfth`, `thirteenth`, `twenty first`, `twenty second`, `one hundred first`, `one hundredth`, `one thousandth`, `first` | the suffix (any case) must be the right one for the number (`1th`, `2st`, `11st` stay identity); a leading zero (`01st`) is not an ordinal; more than 15 digits: left as written, **fallback** |
| `#5`, `#1`, `#007` | `number five`, `number one`, `number zero zero seven` | a hash glued to the digits; `# 5` leaves the hash alone and reads `five` |

**Years and decades (policy Y1).** The policy applies to an unsigned, separator-free, decimal-free
four-digit integer from 1000 to 2099; everything else is a cardinal (`2100` is `two thousand one hundred`).

| Case (first matching row wins) | Reading |
| --- | --- |
| The previous token is a year cue, or a month (or the period after a month **abbreviation**), or a date context (below), or the next token is an era | **Year**, not counted: `in 1066` is `in ten sixty six`; `1066 AD` is `ten sixty six AD`; `before 1900` is `before nineteen hundred` |
| The previous token is a quantity cue, or the next token is a unit, a percent sign, a currency code or symbol, a magnitude word or a quantity noun | **Cardinal**: `about 1500 people`, `1500 people` and `1500 kg` read `one thousand five hundred ...`; `2024%` is `two thousand twenty four percent` |
| No cue and 1100 to 2099 | **Year**, counted as an **ambiguous reading**: `1999` is `nineteen ninety nine` |
| No cue and 1000 to 1099 | **Cardinal**, counted ambiguous: `1066` is `one thousand sixty six` |

Year words: `1999` `nineteen ninety nine`; `1905` `nineteen oh five`; `1900` `nineteen hundred`; `1100` `eleven hundred`;
`2010` `twenty ten`; `2024` `twenty twenty four`; `2099` `twenty ninety nine`. The years 1000 and 2000 and
2001 to 2009 read as their cardinals either way (`two thousand five`), so they are **never counted ambiguous**
and the ambiguity flag is set only where the year and cardinal readings differ. The word `BC` after a number
is not part of the number: `500 BC` is `five hundred B C`, because the existing initialism rule spells `BC`
(only `AD` stays).

A **date context** supplies the year cue when the previous token is a month (`May 2024`), a day that follows
a month (`Dec 25 2024`) or the comma after such a day (`January 5, 2024`). The year is then a `DateTime`
span. The look-behind covers at most three tokens and **never crosses a retained sentence boundary**: a
boundary before token _k_ is retained when token _k_ starts a sentence and token _k-1_ is not the period of a
month abbreviation (the only break the abbreviation lexicon suppresses). Every link of the chain month, day,
comma, year is checked, so a break between a month and a number, or between a day and a year, ends the date.
After a full month name or `May` the period is a sentence end whether or not the tokenizer flagged a break.
Accepted residual: a month abbreviation plus a number is a date even at a sentence end (`He left in Dec. 25
people stayed.` reads `December twenty fifth people stayed`, one sentence, as the lexicon already decides).

Decades: `1990s` `nineteen nineties`; `90s` `nineties`; `1900s` `nineteen hundreds`; `2000s` `two thousands`;
`2010s` `twenty tens`; `1800s` `eighteen hundreds`; `1990's` and `90's` the same; `'90s`, `’90s` and
`‘90s` (an apostrophe token directly before the decade token, S4, and no letter or digit before the apostrophe)
`nineties`. Only a two-digit number with a non-zero tens digit and a final zero, or a four-digit number from
1000 to 2099 ending in zero, followed by `s` or `'s`, is a decade; `5s`, `10000s`, `1995s`, `2100s`, `00s` and
`rock'90s` stay identity.

**Currency** (symbols `$ € £ ¥`, codes `USD EUR GBP JPY`).

| Input | Spoken | Notes |
| --- | --- | --- |
| `$5`, `$1`, `$0` | `five dollars`, `one dollar`, `zero dollars` | singular only for exactly 1 (`$1`, `$1.00`) |
| `$5.50`, `$1.01`, `$0.99`, `$.99`, `$5.00`, `$0.01`, `$0.00` | `five dollars and fifty cents`, `one dollar and one cent`, `ninety nine cents`, `ninety nine cents`, `five dollars`, `one cent`, `zero dollars` | minor units only for a symbol-prefixed amount with **exactly two** fraction digits, no magnitude, and a currency that has them |
| `$5.5`, `$5.123` | `five point five dollars`, `five point one two three dollars` | other fraction lengths are a decimal and the plural unit |
| `$1,234.50`, `$1,234,567.89` | `one thousand two hundred thirty four dollars and fifty cents`, ... | |
| `€5.50`, `€1`, `£3`, `£0.50`, `£1.01`, `¥500`, `¥5.5`, `¥5.25` | `five euros and fifty cents`, `one euro`, `three pounds`, `fifty pence`, `one pound and one penny`, `five hundred yen`, `five point five yen`, `five point two five yen` | `¥` never takes minor units; yen is its own plural |
| `-$5`, `$-5`, `−$5`, `-$0.99` | `minus five dollars`, `minus ninety nine cents` | |
| `$5 million`, `$2.5 billion`, `$3 thousand`, `$1 trillion` | `$5` `five` and `million` `million dollars`; `two point five` and `billion dollars`; ... | per token: the magnitude word (lowercase) after a symbol amount carries the unit; no minor units |
| `$5M`, `$5.5M`, `$10k`, `$3B`, `$1.5m` (letter `K k M m B b` glued, after a symbol only) | `five million dollars`, `five point five million dollars`, `ten thousand dollars`, `three billion dollars`, `one point five million dollars` | bare `10k` and `1.5M` (no symbol) stay identity |
| `5$`, `5€` (glued), `5 €` | `five dollars`, `five euros`, `five` and `euros` | |
| `5 USD`, `1 USD`, `5.50 EUR`, `5 million USD`, `500 JPY`, `3 GBP`, `5USD` | `five` and `dollars`; `one` and `dollar`; `five point five zero` and `euros`; `five` and (`million`) and `dollars`; `five hundred` and `yen`; `three` and `pounds`; `five dollars` | a **suffix** code or symbol is a per-token follow-up of a plain number, so the amount is a plain decimal and **never produces minor units**: `5.50 EUR` is `five point five zero euros` (a symbol-prefixed amount such as `$5.50` does) |
| `US$5`, `USD 5`, `$ 5` | identity (`$ 5` reads `$ five`) | prefix codes are deferred |
| `$1,23` | identity | **fallback** |

**Percent and units.**

- `%` after a number token, adjacent or after one space: the number is read as usual and `%` is `percent`
  (`50%`, `50 %`, `3.5%` is `three point five percent`, `-5%` is `minus five percent`, `100%`, `0%`). A `%`
  with no number before it, or after two spaces, stays identity (the symbol rule does not speak `%`)
- A unit after a plain number, glued (`5kg`, one span `five kilograms`, `72°F`, `10km/h`) or joined (`5 kg`:
  the number, then the unit as its own span): `5 kg` is `five` and `kilograms`; `1 kg` is `one` and
  `kilogram`; `1.5 kg` is `one point five` and `kilograms`; `60 mph` is `sixty` and `miles per hour`;
  `72 °F` is `seventy two` and `degrees Fahrenheit`; `-5°C` is `minus five degrees Celsius`; `440 Hz` is
  `four hundred forty hertz`; `6 ft.` is `six` and `feet`; `1 ft.` is `one` and `foot`
- The unit table is case-sensitive: `5 Kg` and `5 in`, `5 m`, `5 g` are identity
- The optional abbreviation period of `ft lb lbs oz yd mi hr hrs min` and `Ft` uses the Suffix period rules
  of `TextNormalizer`: `He is 6 ft. tall.` consumes the period, `He is 6 ft.` and `He is 6 ft. Then he left.`
  keep it (and force a break before the capitalized word), `5ft. tall` is `five feet tall`, and `5 Ft.
  Worth` is `five feet. Worth` (the suffix rules keep the period and force a break before any capitalized
  word, even a name; `Ft.` as Fort needs no number before it)

**Times.**

| Input | Spoken |
| --- | --- |
| `5:30`, `12:30`, `23:59` | `five thirty`, `twelve thirty`, `twenty three fifty nine` |
| `5:05`, `5:00`, `12:00` | `five oh five`, `five o'clock`, `twelve o'clock` |
| `17:45`, `17:00` | `seventeen forty five`, `seventeen hundred` |
| `07:05`, `07:00`, `0:30`, `00:00` | `oh seven oh five`, `oh seven hundred`, `zero thirty`, `zero hundred` |
| `5:30pm`, `5:30PM`, `5pm`, `5PM`, `12:00am`, `5:00pm` (glued; hour 1 to 12) | two adjacent spans inside the token (S5): `five thirty` and `P M`; `five thirty` and `P M`; `five` and `P M`; `five` and `P M`; `twelve` and `A M`; `five` and `P M` |
| `5 pm`, `5:30 pm`, `5:00 pm`, `5 am` (a lowercase `am` or `pm` token joined to an hour or `h:mm`) | `five` and `P M`; `five thirty` and `P M`; `five` and `P M`; `five` and `A M` |
| `5:30 p.m.`, `5 PM`, `5 a.m.` | the number per token and the **existing** initialism rule (`P M`, `A M`) |

Valid only for an hour 0 to 23 and minutes `00` to `59` written with two digits; a meridiem needs hour 1 to
12 (`13:30pm`, `0pm`, `13pm` stay identity). `25:99`, `3:2`, `5:60`, `5:30:15` and `1:1:1` stay identity
(`h:mm:ss` and ratios are deferred; not a fallback). A meridiem drops the `o'clock`/`hundred` of minute `00`.

**Dates.**

| Input | Spoken | Count |
| --- | --- | --- |
| `12/25/2024`, `25/12/2024`, `2024-12-25`, `2024/12/25`, `12-25-2024`, `31/12/2024`, `6/15/1999` | `December twenty fifth twenty twenty four`, ..., `June fifteenth nineteen ninety nine` | none (a month above 12 or a day above 12 decides the order) |
| `3/4/2024`, `1/2/24`, `03-04-2024`, `7/4/2076` | `March fourth twenty twenty four`, `January second twenty four`, `March fourth twenty twenty four`, `July fourth twenty seventy six` | **ambiguous +1** (both parts at most 12 and different; month first, the en-US order) |
| `12/12/2024`, `1/1/2000` | `December twelfth twenty twenty four`, `January first two thousand` | none (same parts) |
| `13/13/2024`, `0/5/2024`, `2/32/2024`, `1/1/00`, `5/5/5` | identity | not a date; no count |
| `2/30/2024`, `2024-02-30` | `February thirtieth twenty twenty four` | none (day at most 31, no calendar check; documented) |
| `1/1/05` | `January first oh five` | none |
| `2024-13-01` | digit by digit | not a date, so the hyphen-group rule applies |
| `12.25.2024` (dots) | `twelve point twenty five point two zero two four` | none (the dotted-group rule below, not a date) |
| `January 5, 2024`, `January 5th, 2024`, `5th January 2024`, `5 January 2024`, `December 25`, `May 2024`, `in May 2024`, `May 5`, `Dec 25 2024` | `5` is `fifth`, `2024` is `twenty twenty four`, a day next to a month is an ordinal; the month name, the comma and the ordinal suffix words stay untouched | none (a date context supplies the year cue) |
| `May 45`, `May 5 people`, `may 5` | `45` is a cardinal (above 31, not a day); `5` still a day; lowercase `may` is not a month | none |
| `May. 5 people`, `We finished in May. 5 people stayed.`, `January. 5 people` | `5` is `five`: the period after `May` or a full month name ends the sentence, so there is no date context | none |
| `January. 1999 people`, `May. 5, 2024` | `1999` is `one thousand nine hundred ninety nine` (the noun decides); in `May. 5, 2024` `5` is `five` and `2024` a year by the default | `May. 5, 2024` counts one ambiguous reading |
| `Month: May` and a blank line, then `5 people`; `Dec 25` and a blank line, then `2024 people` | `5` is `five`; `25` is `twenty fifth` and `2024` is `two thousand twenty four` | a retained break stops the date; none |
| `Jan. 5`, `Sept. 1999 people`, `Dec. 25, 2024` | `fifth`, `nineteen ninety nine`, `twenty fifth` and `twenty twenty four`: an abbreviation period keeps the date | none |

A numeric date has the shape digits, one separator, digits, the same separator, digits; the year is four digits
(or two digits for a slash date), the month and day one or two digits. Month names are matched in Title Case
only. A day token is one or two digits from 1 to 31, without a leading zero, optionally with an ordinal suffix,
directly after a month (or the period after a month abbreviation, with no retained sentence boundary between
them), or directly before a month. Weekday names add no behavior.

**Digit groups, ranges, phone numbers, fractions.**

| Input | Spoken | Rule |
| --- | --- | --- |
| `5-10`, `100-200`, `5-3` | `five to ten`, `one hundred to two hundred`, `five to three` | two hyphen groups, no leading zero, each at most 9 digits, not a 3-and-4-digit pair (a local phone number); a score reads the same |
| `1990-2000`, `2020-2024`, `2024-2020` | `nineteen ninety to two thousand`, `twenty twenty to twenty twenty four`, `two thousand twenty four to two thousand twenty` | both groups four-digit years 1000 to 2099 and the first not above the second read as years; otherwise as cardinals |
| `5–10` (en dash between two adjacent number tokens) | `five to ten` | per token: the dash is `to`; each number follows its own policy (two years follow the year policy, so `1990–2000` counts one ambiguous reading); both neighbors must themselves be read as numbers (`5–10`, `1,234–5,678`, `5:30–6:30`, `5–10kg` read; `1–2D`, `1,23–5`, `5–25:99`, `5–10x` keep the dash and every token is read on its own: `one–2D`, `1,23–five`); a spaced dash `5 – 10` and a dash between letters are identity |
| `555-1234`, `555-123-4567`, `1-800-555-1234`, `123-45-6789`, `01-02`, `1234567890-5`, `(555) 123-4567` | digits one by one (`five five five one two three four`); the area code in adjacent parentheses followed by `ddd-dddd` is also digit by digit | phone and identifier policy: any other hyphen shape of two or more groups; not a fallback (more than 64 digits in total is) |
| `3.2.1`, `2.0.1`, `1.2.3.4`, `192.168.0.1`, `1.02.3`, `12.25.2024` | `three point two point one`; `one hundred ninety two point one hundred sixty eight point zero point one`; `one point zero two point three`; `twelve point twenty five point two zero two four` | three or more period-separated groups: a group of one to three digits without a leading zero (or exactly `0`) is a cardinal, **any other group is read digit by digit**; at most 64 digits in total; more than 8 groups is a fallback |
| `1/2`, `2/3`, `3/4`, `7/16`, `9/10`, `63/64`, `1/100`, `37/100`, `3/32` | `one half`, `two thirds`, `three quarters`, `seven sixteenths`, `nine tenths`, `sixty three sixty fourths`, `one hundredth`, `thirty seven hundredths`, `three thirty seconds` | numerator at least 1 and below the denominator, no leading zeros, denominator 2 to 10, 16, 32, 64 or 100; **ambiguous +1** when the numerator is at most 12 and the denominator at most 31 (it could be a date: `7/16` and `9/10` count; `3/32`, `15/16` and `63/64` do not) |
| `24/7`, `9/11`, `5/5`, `1/1`, `0/5`, `01/2`, `10.0.0.1/24` | identity | not a fraction or a date |
| `3 1/2`, `5 1/2`, `3 ½` (a plain integer token, one space, a fraction token) | `three` and `and one half`; `five` and `and one half`; `three` and `and one half` | mixed number: per token; **never ambiguous** |
| `½ ⅓ ⅔ ¼ ¾ ⅛ ⅜ ⅝ ⅞`, `5½` | `one half`, `one third`, `two thirds`, `one quarter`, `three quarters`, `one eighth`, `three eighths`, `five eighths`, `seven eighths`; `five and one half` | a vulgar fraction token, alone or glued to an integer |

**Deferred (identity, asserted by golden rows so that a later increment flips them knowingly).** Roman
numerals (`Henry VIII`, `World War II`, `Chapter IV`); hyphenated digit compounds (`5-year-old`, `3-D`,
`F-16`, `7-Eleven`, `mid-1990s`, `20-something`); `US$5` and `USD 5`; `h:mm:ss` and ratios (`3:2`);
non-ASCII digits and digits with combining marks, emoji or surrogate pairs next to them; `1e5`, `0x1F`,
`1_000`; bare `10k` and `1.5M`; `5'10"`; the numero sign; `Q3 2024` (only the year is read); decimal-comma
locales; URLs, e-mail and markup (the P2-2 deferral stays); a token that contains an invisible character
(`5` U+200B `0`: rule 1 drops the invisible character and the number is not read).

**Counts.** A match is counted at most once as **ambiguous** (a bare year or 1000 to 1099 cardinal that the
cues did not decide; a numeric date with both parts at most 12 and different; a fraction that could be a
date) and, independently, as a **numeric fallback** when the token looked numeric but was left as written or
read digit by digit because of a limit or a non-en-US shape. A token that is not numeric at all produces
neither.

**Key Methods**:

- **TryRecognize(text, tokens, index, out match)**: validates the arguments (a null text throws
  `ArgumentNullException`, an index outside `tokens` throws `ArgumentOutOfRangeException`, a token that is
  empty or outside the text throws `ArgumentException`), then decides in constant work per token whether the
  token starts an expression. The algorithm:
  1. a single-character token is tested first: a currency symbol (pair with an adjacent number token, S3),
     an apostrophe before an adjacent decade token (S4), an en dash between two adjacent number tokens that
     are both read as numbers (each neighbor is recognized once, with at least one span; a counted fallback
     or an unrecognized neighbor leaves the dash as written), or a
     vulgar fraction;
  2. a token that does not start like a number (an O(1) first-character test) returns false;
  3. a token longer than 128 characters is a fallback, never scanned;
  4. otherwise the token is **scanned once** into the sign, symbol or hash, at most eight digit groups with
     their separators and the rest, using two small stack buffers (`stackalloc`, 128 characters and 16
     integers), so nothing is allocated except the spoken `string` of a match;
  5. the shape is classified and routed to its reader as described above; the readers look at most three
     tokens before and two after the token, each through the bounds-checked neighbor helpers, and use the
     tables (a linear search over at most 30 entries, a constant);
  6. the spoken value is built by `NumberVerbalizer` into a stack writer and turned into one `string`; the
     caps are enforced before any call, so the verbalizer never throws for recognizer input
- **IsMonthToken(token)**, **MonthNames**, **MonthAbbreviations**, **WeekdayAbbreviations**: the month and
  weekday vocabulary shared with `AbbreviationResolver` (its `next-date` guard) so the lexicon and the numeric
  rule agree; the three properties return cached read-only views of the private tables (no cast reaches the
  array, no call allocates); a test cross-checks the lists against the lexicon rows

**Algorithmic bound.** Every scanner reads a token's characters a constant number of times and never
backtracks; the number of groups, digits and neighbor tokens is bounded by the constants above; no regular
expression is used anywhere. The en dash does one extra constant-size recognition per neighbor (depth one: a
neighbor starts and ends with a digit, so it is never a dash). The work for a token is therefore O(token
length), at most 128 characters, and
the whole pass stays linear whatever the text contains (the subsystem tests assert this with one megabyte of
each repetitive numeric unit and with single huge tokens).

**Thread safety.** The class is `static` and stateless; all tables are read-only after type initialization
and there is no mutable static state, so concurrent calls give identical results.

**Culture invariance.** `char` arithmetic and ordinal comparisons only: no culture-sensitive parsing,
formatting or casing (case-insensitive word tests use ASCII folding), so the current culture never changes a
result.

**Error Handling**: invalid arguments throw as listed above and indicate a defect of the caller (the
normalizer validates its token list before the pass). Input text can never cause an exception: a hostile
token (very long, many separators, a digit run longer than the caps, supplementary-plane or lone surrogate
neighbors, combining marks) is either not numeric (identity) or a counted fallback, and every neighbor access
is bounds-checked. A numeric-looking token that cannot be read is never partially consumed (A7).

**Dependencies**: `NumberVerbalizer` (every spoken value), and the _NormalizationAlignment_ unit's
`NormalizationToken` and `NormalizationKind`. No OTS item and no lexicon file.

**Callers**: `TextNormalizer` (rule 2a), `AbbreviationResolver` (`IsMonthToken`), and the tests.
