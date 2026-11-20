### WordPronouncer

**Purpose**: Pronounce one word by the priority list of the package and report which source gave the answer. It is the
only place that knows the order of reading, dictionary, morphology, prefix and letter-to-sound model.

**Data Model**:

- `WordPronouncer`: immutable, holding the `CmuLexicon`, the `ReadingPronunciations`, the `LexiconCorrections` and the
  `LetterToSound` converter it was built with; thread-safe
- `PronouncedWord(Pronunciation, Source, CompoundBreak)`: the answer; `CompoundBreak` is the index of the first phone of
  the second part of a hyphenated compound that was pronounced part by part, otherwise -1
- Tables: the productive prefixes with their sounds (`counter`, `under`, `super`, `inter`, `multi`, `semi`, `anti`,
  `over`, `non`, `out`, `sub`, `dis`, `mis`, `pre`, `un`, `re`), the small function words that are unstressed in a later
  part of a compound, and the reduced forms of `to` and `of` inside a compound

**Key Methods**:

- **Pronounce(word, readingId)**: folds the word (lower case, straight apostrophes, diacritics removed, `ß` as `ss`; any
  other character makes the result `null`); a word with a hyphen is a compound; otherwise the sources in order: the
  reading, when `ReadingPronunciations.TryResolve` resolves the spelling under the id (source `Reading`); the reviewed
  correction of a wrong dictionary entry, when the word has one (`nobly`; source `Lexicon`); the
  dictionary's first variant (`Lexicon`), the velar comparative first for an adjective in `ng` (see *Morphology*,
  `OrderVariants`); `Morphology.TryDerive` (`Morphology`); a prefix and a remainder of at least
  four letters that the lexicon lists or the morphology derives (`Morphology`); the letter-to-sound converter
  (`LetterToSound`)
- **PronounceCompound**: the closed spelling of the dictionary when a part is not a word of its own (`wi-fi`); otherwise
  each part by the simple rules (a function word in a later part unstressed, the source the highest of the parts; a
  productive prefix (`re pre un dis mis non anti semi inter ex de`) with the sound and the weak stress it has as a
  prefix: `re-enter` is `R IY0 EH1 N T ER0`, not *ray-enter*), and then the stress of one compound word
  (`CompoundStress`). When the dictionary lists the closed spelling with exactly the same sounds its stress pattern is
  the word's, wherever its primary stress falls (`cooperate` is `K OW0 AA1 P ER0 EY2 T`, `nonfiction` keeps `F IH1`),
  except when the first element is a single letter (`e-mail` is spoken `ee-mail`). Otherwise the stress is that of the
  parts with one primary stress: the first is kept and a later one becomes secondary, so a true compound has its main
  stress on its first element (`battle-field` is `B AE1 T AH0 L F IY2 L D`, `well-known` is `W EH1 L N OW2 N`) and a
  word with a prefix keeps the primary stress on its root. A voice that reads two primary stresses in one written word
  speaks two words with a break between them. The prefixes are those whose root keeps the primary stress in the
  dictionary (99 percent of the words with `un-`, 96 of `dis-`, 95 of `mis-`, 92 of `re-`, 87 of `ex-`, 85 of `de-`, 83
  of `pre-`, 81 of `semi-`, 77 of `non-`, 76 of `anti-`, 73 of `inter-`); for `co-`, `sub-`, `post-`, `multi-`, `super-`
  and `pro-` it is a toss-up, and for `over-`, `under-`, `out-`, `self-` and the like it is on the first element, so
  those are compounds like any other. `PronouncedWord.ElementStarts` lists where each element after the first starts, so
  that a renderer can keep a sound rule inside one element
- **Letter(c)**, **Digit(c)**: a letter by its name (source `Spelled`) and a digit by the dictionary word for it (source
  `Digits`)

**Error Handling**: `Pronounce` returns `null` for a word with a character that cannot be folded to the Latin alphabet,
and the caller reports it as unsupported; it never throws for any string. A word with letters always gets a
pronunciation because the last source never fails.

**Dependencies**: `CmuLexicon`, `ReadingPronunciations`, `LexiconCorrections`, `Morphology`, `LetterToSound`,
`LetterNames` and `Phone`.

**Callers**: `EnglishPhonemizer` and the tests.
