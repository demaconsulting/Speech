### AlphanumericTokenRecognizer

**Purpose**: Decide whether one token is an identifier or name that mixes letters and digits and say how each
part is spoken, so that `A4` is read `A four` (not the article `A` and a number) and `COVID-19` is read
`COVID nineteen` (not a pause and `one nine`). It serves two shapes that no other rule reads: an identifier of
capital letters and digits, and a name joined to a number by a hyphen.

**Data Model**: both methods return the spans (`NumericSpan`: start, length, spoken text, kind) of one token in
text order, or `null`; every span indexes the text (`tokenStart` plus the offset in the token).

- **Identifier** (`A4`, `B12`, `H2O`, `R2D2`, `MP3`, `RTX4090`): a token of two to twelve ASCII characters that
  starts with a capital letter and alternates runs of one to three capital letters and one to four digits, with
  at least one digit run. Each letter run is one `Initialism` span with the letters separated by single spaces
  (`M P`); each digit run is one `Number` span, read as a cardinal when it has at most three digits and no
  leading zero (`B12` gives `B twelve`, `A100` gives `A one hundred`) and digit by digit otherwise (`A007` gives
  `A zero zero seven`, `RTX4090` gives `R T X four zero nine zero`). A word of four or more capital letters
  before digits (`COVID19`) is not an identifier
- **Hyphenated name** (`COVID-19`, `Catch-22`, `Boeing-737`): ASCII letters, a hyphen and one to three ASCII
  digits, at most 16 characters in all. Only the digits get a span (`Number`, a cardinal); the letters and the
  hyphen are left as written so that the phonemizer sees one hyphenated word

**Key Methods**:

- **RecognizeIdentifier(token, tokenStart)**: a first pass checks that the token is alternating runs within
  the limits; a second pass builds one span per run
- **RecognizeHyphenatedNumber(token, tokenStart)**: splits at the first hyphen, checks the letters before it
  and the digits after it, and returns the single digit span

**Error Handling**: none. A token of any content is simply not recognized: lower case, a leading digit, a
single letter, digits only, more than four digits in a run, an over-long token and every other shape return
`null`. Each character is visited a constant number of times, so the work is linear in the (limited) token
length.

**Dependencies**: `NumberVerbalizer` (cardinal and digit-by-digit words), `NumericSpan` and `NormalizationKind`
of the same subsystem.

**Callers**: `TextNormalizer` (rule 4b, after the abbreviation and acronym lexicons so that an entry of either
wins), and the tests.
