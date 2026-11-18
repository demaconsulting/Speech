### CmuLexicon

**Purpose**: Hold the CMU Pronouncing Dictionary in the package as a compact image and answer, for a word, all of its
pronunciation variants in dictionary order. The first variant is the default pronunciation of the word; the other
variants are the candidates that a reading of a heteronym can name.

**Data Model**:

*The image* is the decompressed content of the embedded resource `cmudict-en.bin.gz` (the layout is documented by
`LexiconImageFormat`): the magic `CMUL`, a version byte, the block size (32), the word count, then one entry per word in
ordinal order of the word. An entry holds the number of leading characters shared with the previous word (always 0 for
the first word of a block), the suffix as ASCII, the number of variants (at least 1) and, for each variant, a phone
count and that many one-byte phone codes. The lexicon holds 124,926 words with 133,971 pronunciations: the entries of
the dictionary whose word consists of the letters a to z and apostrophes, with the numbered variants of a word
(`word(2)`) stored under the headword and two identical repeated variants dropped.

*The instance* keeps the image bytes and the word count. `Default` is the shared instance over the embedded resource,
created once on first use under `Lazy<T>` with publication safety. `LexiconEntry(Word, Pronunciations)` is the result of
`Entries()`.

**Key Methods**:

- **FromGZipStream(stream)** and **FromImage(bytes)**: build a lexicon from a compressed or a decompressed image,
  validating the header
- **Lookup(word)**: folds the word to lower case and the typographic apostrophe to the straight one, rejects at once a
  text that cannot be a headword (empty, longer than the format allows, or with a character other than a to z and an
  apostrophe), binary-searches the block heads for the last head not above the word, decodes that block and returns the
  variants of the word if it is there, otherwise an empty list. The result is independent of the state of any other call
- **Contains(word)**: whether `Lookup` is not empty
- **Entries()**: enumerates every word with its variants in ordinal order, decoding block by block; used by the tests
  and the tools
- **WordCount**: the number of words

**Error Handling**: a header that is not valid or a truncated image throws `InvalidDataException`; `Default` therefore
throws for a defective build, and the failure is cached by the `Lazy<T>`. `Lookup` never throws for any string.

**Dependencies**: `Phone` and `Pronunciation` (the phone codes of the variants); the framework (gzip, spans).

**Callers**: `Morphology` (stem lookup), `ReadingPronunciations` (through its tests and tools), `WordTokenizer` (a
leading apostrophe is part of a word when the lexicon lists it), `WordPronouncer`, and the rendering tests.
