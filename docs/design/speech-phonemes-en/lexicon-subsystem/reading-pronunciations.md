### ReadingPronunciations

**Purpose**: Map a reading id of the English text analysis inventory (`record.noun`, `wind.coil`) and a spelling to a
pronunciation. The dictionary has no senses and lacks a variant for about a quarter of the heteronyms, so the readings
are resolved by a frozen, reviewed table plus the morphology rules for inflected spellings, with no silent default.

**Data Model**:

*The reading table* is the embedded resource `reading-pronunciations.tsv` (the file
`data/phonemes-en/reading-pronunciations.tsv` is linked into the assembly): a header, then one row per reading id of the
inventory (395 rows) with the columns `reading_id`, `arpabet` (the headword's pronunciation) and `source`. The source is
`cmu#N` (variant N of the lemma in the dictionary), `rule:<name>` (derived from a dictionary entry by a named rule) or
`authored` (written from the dictionary and phonetic knowledge; this includes the three lemmas that the dictionary
lacks). `ReadingEntry(ReadingId, Pronunciation, Source)` is one row.

*The form overrides* are the embedded resource `form-overrides.tsv` (12 rows): `form`, `reading_id`, `arpabet` and a
note, for the spellings that the rules would get wrong (`does`, `lives`, `dove`, `wound`, `houses`, `mouths`, `axes`,
`analyses`, `diagnoses`, `overthrew`, `overthrown`, `sown`).

The lemma of a reading id is the text before its first period, and spellings are lower-case ASCII.

*The lexicon corrections* are the embedded resource `lexicon-corrections.tsv` (the file
`data/phonemes-en/lexicon-corrections.tsv`): `word`, `arpabet` and a note giving the reason, for the dictionary entries
that are transcription errors (`nobly`, which the dictionary lists with the vowel of *knobbly*, and `novelly`).
`LexiconCorrections` (a helper of this unit, with `Parse(table)`, `Default`, `TryGet(word, out pronunciation)` and
`Entries`) parses them; a word that is not lower-case letters and apostrophes, a phone that is not ARPAbet, a wrong
column count and a repeated word are a `FormatException`. The word pronouncer consults the table before the dictionary,
so a correction replaces every variant the dictionary lists, and the dictionary image and its pins stay untouched. The
table was found by comparing every dictionary word that is a stem plus a neutral suffix (`-ly`, `-ness`, `-ful`,
`-less`, `-ment`, `-ish`) with the entry of its stem and reading the 137 disagreements; two were errors.

**Key Methods**:

- **Parse(readings, overrides)**: builds the tables from text; throws `FormatException` naming the file and line for a
  wrong column count, a source that is none of the three kinds, a phone that is not ARPAbet, a repeated reading id, an
  override for an id with no row or a repeated override key
- **Default**: the tables embedded in the assembly, loaded once on first use under `Lazy<T>`
- **TryResolve(form, readingId, out pronunciation)**: an override for exactly this form and id first; the headword's
  pronunciation when the form is the lemma; the headword's pronunciation plus the ending when `TryInflect` says the form
  is a regular inflection of the lemma; otherwise `false` and a null pronunciation (a gap)
- **TryInflect(lemma, form, lemmaPronunciation)**: recognizes `-s`, `-es`, `-ies`, `-ed`, `-d`, `-ied` (with a doubled
  final consonant), `-ing` (silent `e` dropped, consonant doubled, `-ie` to `-ying`), `-er`, `-r`, `-est`, `-st` and
  `'s`, and appends the sound of the ending with `Morphology`
- **TryGetEntry**, **LemmaOf**, **Entries**, **OverrideCount**: access for the phonemizer, the tests and the tools

**Review of the table.** The first authoring pass changed 93 of the 395 mechanically proposed rows; an independent
second pass read all 853 resolved spelling and reading pairs against the reading descriptions and changed 2 rows. The
tools command `propose-readings --check` verifies the table against the inventory, every `cmu#N` row against the lexicon
and the gap list against the committed one, which is empty.

**Error Handling**: a malformed embedded table is a defective build (`FormatException` through the loader, cached by the
`Lazy<T>`); `TryResolve` returns `false` for any pair it cannot explain and throws only `ArgumentNullException` for a
null argument.

**Dependencies**: `Morphology` (endings), `Phone`/`Pronunciation`, and the framework.

**Callers**: `WordPronouncer` (a reading annotation), the tests and the tools.
