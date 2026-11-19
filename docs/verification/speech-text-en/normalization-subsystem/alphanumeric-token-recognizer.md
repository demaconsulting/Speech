### AlphanumericTokenRecognizer Verification

#### Verification Approach

`AlphanumericTokenRecognizer` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/AlphanumericTokenRecognizerTests.cs`. The unit is a
pure
function of one token's characters and its offset in the text, so the tests pass tokens as strings directly: no
tokenizer, no text and no other unit is involved. The tests compare the spoken texts of the returned spans, joined by a
bar, with the documented reading, and check offsets and kinds where they matter. The sentence-level rows
(`id-letter-digit`, `id-name-number`) are rows of the golden file run by the subsystem tests, and the rule order
against the lexicons is verified by the `TextNormalizer` tests (see *NormalizationSubsystem Verification* and
*TextNormalizer Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the test project
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files; the unit keeps no state

#### Acceptance Criteria

**Identifiers.** A token of capital letters and digits that starts with letters and alternates runs of one to three
letters and one to four digits is read with each letter run spelled and each digit run as a cardinal (up to three
digits, no leading zero) or digit by digit; the spans are adjacent, cover the token and index the text through the
token offset.
A word of four or more capitals before digits, more than four digits, lower case, a leading digit, no digits, a single
letter and an over-long token are not identifiers.

**Hyphenated names.** Letters, a hyphen and one to three digits give one `Number` span over the digits as a
cardinal; four digits, no digits, mixed letters, a leading hyphen, digits before the hyphen and ordinary compounds are
not recognized.

#### Test Scenarios

##### Identifiers

**Tests**: `AlphanumericTokenRecognizer_RecognizeIdentifier_SpellsLettersAndReadsDigits` (an eight-row theory),
`AlphanumericTokenRecognizer_RecognizeIdentifier_SpansCoverTheTokenAtItsOffset`,
`AlphanumericTokenRecognizer_RecognizeIdentifier_OtherShapes_ReturnNull` (a ten-row theory)

`A4`, `B12`, `H2O`, `R2D2` and `MP3` give `A|four`, `B|twelve`, `H|two|O`, `R|two|D|two` and `M P|three`; `A100` reads
`one hundred`, `A007` and `RTX4090` read their digits one by one. `MP3` at offset 7 gives an `Initialism` span at 7 of
length 2 and a `Number` span at 9 of length 1. `COVID19`, `A12345`, `a4`, `A4b`, `4A`, `AB`, `A`, `A4-B`, a
fourteen-character token and the empty string return nothing.

##### Hyphenated names

**Tests**: `AlphanumericTokenRecognizer_RecognizeHyphenatedNumber_ReadsOnlyTheDigits` (a four-row theory),
`AlphanumericTokenRecognizer_RecognizeHyphenatedNumber_OtherShapes_ReturnNull` (an eight-row theory)

`COVID-19`, `Catch-22`, `Boeing-737` and `F-16` at offset 5 give one span over the digits only, at the digits' offset,
read `nineteen`, `twenty two`, `seven hundred thirty seven` and `sixteen`. `COVID-1234`, `COVID-`, `COVID-1a`, `-19`,
`co2-19`, `well-known`, `a-b-12` and `mid-1990s` return nothing.
