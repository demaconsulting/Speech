### WordPronouncer

**Purpose**: Pronounce one word by the priority list of the package and report which source gave the answer. It is the
only place that knows the order of reading, dictionary, morphology, prefix and letter-to-sound model.

**Data Model**:

- `WordPronouncer`: immutable, holding the `CmuLexicon`, the `ReadingPronunciations`, the two `LexiconCorrections`
  tables (the corrections and the additions) and the `LetterToSound` converter it was built with; thread-safe
- `PronouncedWord(Pronunciation, Source, ElementStarts)`: the answer; `ElementStarts` lists, for a hyphenated compound
  that was pronounced part by part, the index of the first phone of every element after the first (`state-of-the-art` is
  `[4, 6, 8]`), otherwise it is `null`. The derived `CompoundBreak` is the first of them (the start of the second
  element), or -1 for a word that is not such a compound. A renderer reads the element starts to keep a sound rule
  inside one element (the vowel-before-`r` rules, the stress of a compound) and does not apply it across a hyphen
- Tables: the productive prefixes with their sounds (`counter`, `under`, `super`, `inter`, `multi`, `semi`, `anti`,
  `over`, `non`, `out`, `sub`, `dis`, `mis`, `pre`, `un`, `re`), the small function words that are unstressed in a later
  part of a compound, the reduced forms of `to` and `of` inside a compound, and the British-spelling rules (pattern and
  replacement on the lower-case word: `oeu` to `eu`; `ae` to `e` between consonants in a word of at least seven letters;
  a word-initial `oe` and the `oe` of `rhoea` to `e`; `-our` to `-or` after a consonant before the end, `-s`, `-ed`,
  `-ing`, `-ite`, `-ful`, `-able`, `-ably`, `-less`, `-ism` and `-ist`; `-tre`, `-bre`, `-vre` to `-ter`, `-ber`, `-ver`
  with their `-s`, `-d` and `-ring` forms; `-ise` and `-yse` to `-ize` and `-yze` with their endings; the doubled `l` of
  `-elled`, `-alled` and the like; `-fence` and `-cence` to `-fense` and `-cense`; `-ogue` to `-og`; `-amme` to `-am`),
  each rule restricted to a few letters of context so it applies where the letters are the convention, not the root

**Key Methods**:

- **Pronounce(word, readingId)**: folds the word (lower case, straight apostrophes, diacritics removed, `ß` as `ss`; any
  other character makes the result `null`); a word with a hyphen is a compound; otherwise the sources in order: the
  reading, when `ReadingPronunciations.TryResolve` resolves the spelling under the id (source `Reading`); the reviewed
  correction of a wrong dictionary entry, when the word has one (`nobly`; source `Lexicon`); the
  dictionary's first variant (`Lexicon`), the velar comparative first for an adjective in `ng` (see *Morphology*,
  `OrderVariants`); the reviewed addition of a word the dictionary lacks (`agave`, `aquiver`; `Lexicon`);
  `Morphology.TryDerive` (`Morphology`); a prefix and a remainder of at least four letters that the lexicon lists or the
  morphology derives (`Morphology`); for a word not written with a capital, a British spelling respelled by the rules
  above, alone and up to three together (fewest first), into a word the dictionary lists (`manoeuvring` is
  `maneuvering`, `analyse` is `analyze`; `Morphology`); for a word written with a capital, an English place name in
  `-mouth` after at least two letters, whose first element is pronounced as a word of its own and whose `-mouth` is
  `M AH0 TH` (`Rhinemouth`; `Morphology`); the letter-to-sound converter (`LetterToSound`). A word written with a
  capital may be a name, whose letters are not a spelling convention, so it is not respelled (the rules' wrong
  respellings of dictionary words are names: `Kraemer` is not `Kremer`); and the dictionary lists every English place
  name in `-mouth` it has with the reduced `M AH0 TH` (`Dartmouth`, `Falmouth`, `Monmouth`, `Plymouth`, `Portsmouth`,
  `Weymouth`) and its common nouns with the full vowel (`cottonmouth`, `frogmouth`), while the letter-to-sound model
  gives an unknown place name the full `AW` that the voice reads as two words. The measurements behind both rules are
  in `docs/evaluation/phonemes-en/renderers.md`
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

**Dependencies**: `CmuLexicon`, `ReadingPronunciations`, `LexiconCorrections` (corrections and additions), `Morphology`,
`LetterToSound`, `LetterNames` and `Phone`.

**Callers**: `EnglishPhonemizer` and the tests.
