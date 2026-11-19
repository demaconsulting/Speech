## LexiconSubsystem Design

![LexiconSubsystem Structure](PhonemesEnLexiconSubsystemView.svg)

### Overview

The SpeechPhonemesEn LexiconSubsystem holds everything the package knows about the pronunciation of words as data, and
the laws that extend that data: what a word sounds like when it is in the dictionary, what a regular inflection of a
known word sounds like, and what every reading of a heteronym sounds like. It knows nothing of text, tokens,
annotations, renderers or voices. It contains the following units:

- **CmuLexicon**: the embedded CMU Pronouncing Dictionary as a compact, searchable image, with its lookup, enumeration
  and loader (and the documented binary format, `LexiconImageFormat`)
- **Phone**: the one-byte `Phone` and the immutable `Pronunciation` that all the other units are written in terms of,
  with the CMUdict text form, and the `LetterNames` table that gives each letter its name as a pronunciation
- **Morphology**: the sound rules for the endings of regular inflected forms, possessives and contractions, and the
  derivation of a form from a stem that the lexicon knows (`Derivation`, `SuffixKind`)
- **ReadingPronunciations**: the reviewed table that maps every reading id of the English text analysis inventory to the
  pronunciation of its headword, the listed spellings the rules cannot give, and the resolution of a spelling under a
  reading

### Interfaces

Every type of the subsystem is internal; the public face of the package exposes only ARPAbet symbol strings, so the
phone and pronunciation types can change freely. The tools and tests assemblies see them through `InternalsVisibleTo`.

- `CmuLexicon.Default` (the shared lazy instance), `FromGZipStream`, `FromImage`, `Lookup(word)` (all variants in
  dictionary order, empty when the word is absent), `Contains`, `Entries()` and `WordCount`
- `Pronunciation.Parse` and `ToString` (the text form), `Append`, `DropLast`, `StartsWith`, value equality;
  `Phone.Create`, `TryParse`, `Stress`, `BaseSymbol`, `IsVowel`, `WithStress`
- `Morphology.Apply(stem, kind)`, `AppendClitic(host, kind)` and `TryDerive(word, lexicon, out derivation)`
- `ReadingPronunciations.Default`, `Parse(readings, overrides)`, `TryGetEntry`, `TryResolve(form, readingId, out
  pronunciation)`, `LemmaOf` and `TryInflect`

The subsystem consumes only the framework and, for the reading ids it must cover, the reading inventory of SpeechTextEn
in its tests; the tables themselves contain ids as plain text. LetterToSoundSubsystem, PhonemizerSubsystem and
RenderingSubsystem use it; it uses none of them.

### Design

**One pronunciation type.** A `Phone` is one byte: consonants 1 to 24 and each vowel in each of its three stress classes
(0, 1, 2) after them, 69 codes in all, so a pronunciation is a short byte array and the lexicon stores one byte per
phone. `Pronunciation` is an immutable sequence of phones with value equality, the CMUdict text form (`R EH1 K ER0 D`)
as its parse and print format, and the few operations the rules need (append, drop the last phone, prefix test).

**The lexicon image.** `CmuLexicon` keeps the decompressed image as a byte array (about 1.7 MB in memory) and never
builds a dictionary of strings. The image is a header, then the words in ordinal order, front-coded in blocks of 32
words (each entry stores how many leading characters it shares with the previous word, its suffix and its variants); a
lookup binary-searches the block heads and decodes at most one block. Words are lower-case `a` to `z` and apostrophes;
the variants of a word are in dictionary order, and the first is the default. Lookup folds case and the typographic
apostrophe and returns an empty list for anything that cannot be a headword, never an exception.

**Morphology.** A regular ending is chosen by the last phone of the stem: after a sibilant `IH0 Z` (plural, third
person, possessive) or `IH0 D` after `T` or `D` (past tense); after another voiceless consonant `S` or `T`, otherwise
`Z` or `D`; `-ing` adds `IH0 NG`; `-er` adds `ER0`, `-est` `AH0 S T`, `-ly` `L IY0` (`IY0` after a stem that ends in
`L`); the contractions `'ll 've 'd 'm 're` reduce to `AH0` plus the consonant after a consonant and are the consonant
alone after a vowel. The conventions follow the majority of the dictionary's own entries. `TryDerive` finds the stem by
spelling: the first candidate the lexicon knows among the `s`, `es`, `ies`, `ed`, `d`, `ied`, undoubled, `ing`, `+e`,
`ying`, `est`, `er`, `ly` and `ily` candidates of the word, preferring the silent-e stem for a consonant-vowel-consonant
ending (`hoped` from `hope`, not from a rare name `hop`) and not trying a plural on a word that ends in `ss`, `us` or
`is`; it returns one derivation per variant of the stem (at most four). The rules are measured against the dictionary by
an audit that is compiled into the test assembly from the tools project (the class agreement of the s/z/iz and t/d/id
decision is at least 98 percent for every regular ending) so the measurement is also a regression gate.

**Reading pronunciations.** The dictionary has no senses and lacks a variant for about a quarter of the heteronyms, so a
reading id such as `record.noun` is mapped by a frozen, reviewed table (`reading-pronunciations.tsv`, one row per id of
the inventory, 395 rows) to the pronunciation of its headword; each row says where it came from: `cmu#N` (variant N of
the lemma in the lexicon), `rule:<name>` (a named derivation from a lexicon entry) or `authored`. The pronunciation of
an inflected spelling that the text analysis can label with a reading (`records`, `recorded`, `abusing`) is the
headword's pronunciation plus the sound of the ending, by the morphology rules; the few spellings the rules would get
wrong (`does`, `lives`, `dove`, `wound`, a dozen in `form-overrides.tsv`) are listed and win over the rules. A pair that
nothing resolves is a gap, never a silent default; the gap list is committed (`reading-gaps.tsv`) and empty.

**Error handling.** A malformed embedded resource is a defective build: the loaders throw `InvalidDataException` or
`FormatException` naming the problem, and the `Lazy<T>` caches the failure. Lookup and resolution never throw for any
text.
