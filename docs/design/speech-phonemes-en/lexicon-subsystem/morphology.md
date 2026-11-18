### Morphology

**Purpose**: Give the pronunciation of a regular form that the dictionary does not list, from the pronunciation of its
stem and the sound of its ending: a plural or third-person `-s`, a past tense `-ed`, `-ing`, a comparative or
superlative, an `-ly` adverb, a possessive and the clitic contractions. The sound of an English ending follows from the
last sound of the stem, so the result is derived exactly where the letter-to-sound model would only guess the whole
word.

**Data Model**:

- `SuffixKind`: the endings: `PluralS`, `PastEd`, `Ing`, `PossessiveS`, `PluralPossessive`, `ComparativeEr`,
  `SuperlativeEst`, `AdverbLy`, and the contractions `ContractionD`, `ContractionLl`, `ContractionVe`, `ContractionRe`,
  `ContractionM` and `ContractionNt`
- `Derivation(Stem, Kind, StemPronunciations, Pronunciations)`: the result of `TryDerive`, with one derived
  pronunciation per variant of the stem (at most four)

**Key Methods**:

- **Apply(stem, kind)**: the stem's pronunciation with the sound of the ending, chosen by the last phone of the stem.
  After a sibilant a plural, third person or possessive adds `IH0 Z` and a past tense after `T` or `D` adds `IH0 D`;
  after another voiceless consonant the ending is `S` or `T`, otherwise `Z` or `D`; `-ing` adds `IH0 NG`; `-er` adds
  `ER0`, `-est` adds `AH0 S T` (`G AH0 S T` after a stem that ends in a stressed `NG`: `strongest`, `longest`,
  `youngest`; the dictionary has the stop in every such form); `-ly` adds `L IY0` (`IY0` after a stem that ends in `L`)
- **OrderVariants(word, variants, lexicon)**: the variants of a spelling, with the comparative that has the velar stop
  first for an adjective in `ng`: the dictionary lists `stronger` with and without the `G`, and the variant without it
  is a known defect, whereas `longer` and `younger` have it. The rule is read from the dictionary: a `-er` spelling
  whose stem ends in a stressed `NG` and whose `-est` form is listed with `NG G` takes the variant that ends in
  `NG G ER0` first; the agentives (`singer`, `hanger`) and the `-ing` forms are not touched, and a derived comparative
  gets no stop, because the agentives far outnumber the adjectives in the dictionary
- **AppendClitic(host, clitic)**: attaches `'s`, `'d`, `'ll`, `'ve`, `'re`, `'m` to the pronunciation of the host word:
  the possessive `'s` as a plural ending, the others as `AH0` plus the consonant after a consonant and as the consonant
  alone after a vowel (`they'll` is `DH EY1 L`, `it'll` is `IH1 T AH0 L`)
- **TryDerive(word, lexicon, out derivation, allowDerivedStems)**: finds the stem by spelling. For the word it tries, in
  this order of ending, the candidates `s`, `es` and `ies`; `ed`, `d`, `ied` and the doubled-consonant `ed`; `ing`,
  `+e`, the doubled consonant and `ying` for `ie`; `est`; `er`; `ly` and `ily`; and the possessive and contraction
  spellings (`cat's`, `cats'`, `they'll`, `isn't`). The first candidate whose stem the lexicon lists gives the
  derivation; a consonant-vowel-consonant ending prefers the silent-e stem (`hoped` from `hope`, `making` from `make`)
  over a rare name (`mak`); a plural is not tried on a word that ends in `ss`, `us` or `is`

**Measured agreement.** The audit compiled into the test assembly from the tools project (`measure-morphology` runs it
too) compares, for every dictionary entry that is a listed stem plus a regular ending, the derived pronunciation with
the dictionary's own. The class agreement (the s/z/iz and t/d/id decision, ignoring the IH0/AH0 spelling of the syllabic
ending, which the dictionary itself mixes) is above 98 percent for the plural, past, `-ing` and possessive endings, and
the test gate keeps it so; the numbers and the dominant disagreements are recorded in
`docs/evaluation/phonemes-en/summary.md`.

**Error Handling**: `TryDerive` returns `false` and no derivation when no stem and ending explain the word (the caller
then falls back to the letter-to-sound model); `Apply` and `AppendClitic` throw `ArgumentNullException` for a null
pronunciation and have no other failure.

**Dependencies**: `CmuLexicon` (stem lookup) and `Phone`/`Pronunciation`.

**Callers**: `WordPronouncer` (derived and prefixed words), `EnglishPhonemizer` (a clitic after a word or a spelled
replacement), `ReadingPronunciations` (the pronunciation of an inflected spelling of a reading) and the tools.
