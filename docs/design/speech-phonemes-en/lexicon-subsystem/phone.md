### Phone

**Purpose**: Provide the value types that the whole package is written in: a phone (one ARPAbet sound, with the stress
of a vowel), a pronunciation (an immutable sequence of phones) and the names of the letters as pronunciations. Keeping
them in one unit means every table, every rule and every golden speaks the same notation, the text form of the CMU
Pronouncing Dictionary.

**Data Model**:

- `Phone`: a readonly struct over one byte code. Codes 1 to 24 are the consonants; the vowels follow, each in the three
  stress classes 0 (unstressed), 1 (primary) and 2 (secondary), 69 codes in all. It exposes `IsVowel`, `Stress` (-1 for
  a consonant), `Symbol` (`EH1`), `BaseSymbol` (`EH`), `Code`, `WithStress`, `IsSibilant` and `IsVoiceless`, with
  `Create`, `TryParse` and `FromCode`
- `Pronunciation`: a sealed immutable class over a `Phone` array with value equality (so two pronunciations of the same
  phones are equal and usable as dictionary keys). It exposes `Count`, an indexer, `Phones` (a read-only span), `Empty`,
  `Parse`, `ToString`, `Append`, `DropLast` and `StartsWith`
- `LetterNames`: an explicit table from the letters a to z to the pronunciation of the letter's name (`EH1 F` for `f`,
  `D AH1 B AH0 L Y UW0` for `w`), written out rather than read from the lexicon, whose entry for a single letter is a
  word (`a` is the article first); so a spelled `A` or `I` is a letter and never the article or the pronoun

**Key Methods**:

- **Phone.TryParse(symbol)**: accepts a consonant symbol or a vowel symbol with a stress digit
- **Pronunciation.Parse(text)**: splits the text on spaces and parses each phone; a text that is not ARPAbet throws
  `FormatException` naming the phone
- **Pronunciation.ToString()**: the text form; `Parse(p.ToString())` equals `p`
- **LetterNames.TryGet(letter, out name)**: the name of an ASCII letter of either case

**Error Handling**: `Parse` throws `FormatException` for an unknown phone; `TryParse` and `TryGet` report failure with
`false`. Nothing else in the unit can fail.

**Dependencies**: the framework only.

**Callers**: every other unit of the package: `CmuLexicon` (storage), `Morphology`, `ReadingPronunciations`, `LtsModel`
and `LetterToSound` (the model's output and the letter-name fallback), `WordPronouncer`, the phonemizer (override
parsing) and both renderers.
