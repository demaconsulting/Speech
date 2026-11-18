### AcronymRecognizer Verification

#### Verification Approach

`AcronymRecognizer` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/AcronymRecognizerTests.cs`. The unit is a pure
function of one token's characters and the shouting-guard flag, so the tests pass tokens as strings directly:
no tokenizer, no text and no other unit is involved. The shipped lexicon (the real embedded `acronyms.tsv`)
is used for the behavior of the product, and small custom tables parsed from strings for the loader. The
case-sensitive behavior, which is the reason the unit exists, is checked with case pairs. The sentence-level
shouting rows (`acr-shouting-per-sentence` and the other initialism rows) are rows of the golden file run by the
subsystem tests (see *NormalizationSubsystem Verification*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files; the shipped lexicon is immutable and shared

#### Acceptance Criteria

**Recognition.** The listed initialisms are recognized in their exact case only (`US`, `FBI` yes; `us`, `Us`, `It` no);
a recognized token is spoken as its capital letters separated by spaces, including the rule-spelled `NYC`; in an
all-capital sentence an ambiguous form (`US`, `IT`, `AM`) and a rule-spelled form (`NYC`) are left alone while an
unambiguous listed form (`FBI`) and a dotted form (`U.S.`) are still spoken; capitals with a vowel (`NASA`),
Roman-numeral-shaped ones (`MMMM`), a single letter, over-long tokens and malformed dotted or ampersand shapes
are not initialisms; dotted letter forms are spoken upper-cased and spaced and report whether the token carries the
final period; ampersand forms are spoken group by group with `and` and keep a possessive tail.

**Lexicon.** The loader builds a custom lexicon that the recognizer then uses exactly; malformed rows are rejected with
an error naming the one-based line number; a null reader throws; and racing first uses of the default lexicon all give
the documented recognitions.

#### Test Scenarios

##### Recognition

**Tests**: `AcronymRecognizer_TryRecognize_CaseVariants_AreCaseSensitive` (a five-row theory),
`AcronymRecognizer_TryRecognize_Initialism_IsSpokenLetterByLetter` (`FBI`, `ASAP`, `NYC`),
`AcronymRecognizer_TryRecognize_ShoutingSentence_LeavesAmbiguousAndRuleSpelledForms`,
`AcronymRecognizer_TryRecognize_TokensThatAreNotInitialisms_AreNotRecognized` (an eleven-row theory: `NASA`,
two Roman-numeral shapes (`MMMM` among them), `A`, a six- and a seven-letter vowel-less token, `Nyc`, `U..S`, `U,S.`,
`a&b` and `ABCDE&F`),
`AcronymRecognizer_TryRecognize_DottedLetters_AreSpelled` (`U.S.`, `p.m.` and `U.S`, with the period flag),
`AcronymRecognizer_TryRecognize_AmpersandForms_AreSpelledWithAnd` (`AT&T`, the typographic possessive `AT&T’s` and
`A&B&C`)

With the shouting guard on, `US`, `IT`, `AM` and `NYC` are not recognized while `FBI` and `U.S.` are; with it off, `US`
is recognized.

##### Lexicon loader and concurrent first use

**Tests**: `AcronymRecognizer_FromTsv_CustomLexicon_IsUsed`,
`AcronymRecognizer_FromTsv_MalformedRow_ThrowsInvalidDataExceptionNamingTheLine` (a seven-row theory),
`AcronymRecognizer_FromTsvAndDefault_NullReaderAndConcurrentFirstUse_BehaveAsDocumented`

A custom table with comments, a blank line and CRLF endings lists `NASA` (recognized as listed) and an
ambiguous `XY` (left alone when the sentence is shouted). The malformed tables have a wrong column count, a
lower-case form, an empty form, an empty spoken text, a spoken text with surrounding whitespace, an unknown
ambiguity value and a duplicate form (reported on line 3, the others on line 2). A null reader throws, and
thirty-two tasks using the default lexicon for the first time at once all recognize `US` as `U S` and `us` as no
initialism; whether the default is one shared, lazily created instance is a design choice and is not asserted.
