### AcronymRecognizer Verification

#### Verification Approach

`AcronymRecognizer` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/AcronymRecognizerTests.cs`. The unit is a pure
function of one token's characters and the shouting-guard flag, so the tests pass tokens as strings directly:
no tokenizer, no text and no other unit is involved. The shipped lexicon (the real embedded `acronyms.tsv`)
is used for the behavior of the product, and small custom tables parsed from strings for the loader. The
case-sensitive behavior, which is the reason the unit exists, is checked with case pairs.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files; the shipped lexicon is immutable and shared

#### Acceptance Criteria

The listed initialisms are recognized in their exact case only (`US`, `IT`, `AM`, `FBI` and `PM` yes; `us`,
`Us`, `It`, `it`, `am`, `Am`, `fbi`, `Fbi` and `Id` no); a recognized token is spoken as its capital letters
separated by spaces, including the rule-spelled `NYC`, `HTTP` and `PHP`; in an all-capital sentence an
ambiguous form (`US`, `IT`, `AM`) and a rule-spelled form (`NYC`) are left alone while an unambiguous listed
form (`FBI`) and a dotted form (`U.S.`) are still spoken; capitals with a vowel (`NASA`, `NATO`, `SCUBA`),
Roman-numeral-shaped ones (for example `MMMM`), a single letter, over-long tokens and malformed dotted
or ampersand shapes are not initialisms; dotted letter forms are spoken upper-cased and spaced and report
whether the token carries the final period; ampersand forms are spoken group by group with `and` and keep a
possessive tail; the loader builds a custom lexicon that the recognizer then uses exactly; malformed rows
are rejected with an error naming the one-based line number; a null reader throws; and racing first uses
of the default lexicon all give the documented recognitions.

#### Test Scenarios

##### Case sensitivity

**Test**: `AcronymRecognizer_TryRecognize_CaseVariants_AreCaseSensitive` (a theory over 15 tokens)

##### Spoken form

**Test**: `AcronymRecognizer_TryRecognize_Initialism_IsSpokenLetterByLetter` (a theory over `FBI`, `US`, `ASAP`,
`NYC`, `HTTP` and `PHP`)

##### Shouting guard

**Test**: `AcronymRecognizer_TryRecognize_ShoutingSentence_LeavesAmbiguousAndRuleSpelledForms`

With the guard on, `US`, `IT`, `AM` and `NYC` are not recognized while `FBI` and `U.S.` are; with it off, `US`
is recognized.

##### Not an initialism

**Test**: `AcronymRecognizer_TryRecognize_TokensThatAreNotInitialisms_AreNotRecognized` (a theory over 29
tokens)

Includes words read as words, Roman-numeral shapes, a vowel-less token that is too long (six and seven
letters), mixed case, digits and malformed dotted shapes (`U.`, `U..S`, `US.`, `a.bc`, `.U.S`, `1.2.`) and
ampersand shapes (`&`, `A&`, `&A`, `A&&B`, `a&b`, `ABCDE&F`, `Johnson&Johnson`, `A&B2`).

##### Dotted and ampersand forms

**Tests**: `AcronymRecognizer_TryRecognize_DottedLetters_AreSpelled` (a theory over `U.S.`, `U.S.A.`, `p.m.`,
`J.R.R.`, `U.S` and `a.m`), `AcronymRecognizer_TryRecognize_AmpersandForms_AreSpelledWithAnd` (a theory over
`AT&T`, `R&D`, `Q&A`, `AT&T's`, the same with a typographic apostrophe and `A&B&C`)

##### Loader and concurrent first use

**Tests**: `AcronymRecognizer_FromTsv_CustomLexicon_IsUsed`,
`AcronymRecognizer_FromTsv_MalformedRow_ThrowsInvalidDataExceptionNamingTheLine` (a theory over seven
malformed tables), `AcronymRecognizer_FromTsvAndDefault_NullReaderAndConcurrentFirstUse_BehaveAsDocumented`

A custom table with comments, a blank line and CRLF endings lists `NASA` (recognized as listed) and an
ambiguous `XY` (left alone when the sentence is shouted). The malformed tables have a wrong column count, a
lower-case form, an empty form, an empty spoken text, a spoken text with surrounding whitespace, an unknown
ambiguity value and a duplicate form (reported on line 3, the others on line 2). A null reader throws, and
thirty-two tasks using the default lexicon for the first time at once all recognize `US` as `U S` and `us` as no
initialism; whether the default is one shared, lazily created instance is a design choice and is not asserted.
