### WordPronouncer

**Purpose**: Pronounce one word by the priority list of the package and report which source gave the answer. It is the
only place that knows the order of reading, dictionary, morphology, prefix and letter-to-sound model.

**Data Model**:

- `WordPronouncer`: immutable, holding the `CmuLexicon`, the `ReadingPronunciations` and the `LetterToSound` converter
  it was built with; thread-safe
- `PronouncedWord(Pronunciation, Source, CompoundBreak)`: the answer; `CompoundBreak` is the index of the first phone of
  the second part of a hyphenated compound that was pronounced part by part, otherwise -1
- Tables: the productive prefixes with their sounds (`counter`, `under`, `super`, `inter`, `multi`, `semi`, `anti`,
  `over`, `non`, `out`, `sub`, `dis`, `mis`, `pre`, `un`, `re`), the small function words that are unstressed in a later
  part of a compound, and the reduced forms of `to` and `of` inside a compound

**Key Methods**:

- **Pronounce(word, readingId)**: folds the word (lower case, straight apostrophes, diacritics removed, `ß` as `ss`; any
  other character makes the result `null`); a word with a hyphen is a compound; otherwise the sources in order: the
  reading, when `ReadingPronunciations.TryResolve` resolves the spelling under the id (source `Reading`); the
  dictionary's first variant (`Lexicon`); `Morphology.TryDerive` (`Morphology`); a prefix and a remainder of at least
  four letters that the lexicon lists or the morphology derives (`Morphology`); the letter-to-sound converter
  (`LetterToSound`)
- **PronounceCompound**: the closed spelling of the dictionary when a part is not a word of its own (`wi-fi`); otherwise
  each part by the simple rules with its own stress, a function word in a later part unstressed, and the source the
  highest of the parts
- **Letter(c)**, **Digit(c)**: a letter by its name (source `Spelled`) and a digit by the dictionary word for it (source
  `Digits`)

**Error Handling**: `Pronounce` returns `null` for a word with a character that cannot be folded to the Latin alphabet,
and the caller reports it as unsupported; it never throws for any string. A word with letters always gets a
pronunciation because the last source never fails.

**Dependencies**: `CmuLexicon`, `ReadingPronunciations`, `Morphology`, `LetterToSound`, `LetterNames` and `Phone`.

**Callers**: `EnglishPhonemizer` and the tests.
