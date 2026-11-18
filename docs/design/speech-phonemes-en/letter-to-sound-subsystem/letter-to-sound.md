### LetterToSound

**Purpose**: Answer, for the phonemizer, "how is this unknown word pronounced": run the trained model, keep the answers
already computed, and never return silence for something that has a letter.

**Data Model**:

- `_model`: the `LtsModel` it wraps (`Default` uses the embedded one)
- `_cache`: a `ConcurrentDictionary<string, Pronunciation>` of at most 4,096 entries; only words of at most 40 letters
  are cached and, when the map is full, new words are not cached
- `Default`: the shared converter over the embedded model, created once on first use under `Lazy<T>`

**Key Methods**:

- **TryConvert(word, out pronunciation)**: a cached answer returns at once. Otherwise the model converts the word; a
  result with no vowel is replaced by the letter names of the word's letters (`LetterNames`), so `www` is spoken as the
  letters and `sq` as `S Q`. The answer is cached and returned; the result is `true` when the pronunciation is not
  empty, which happens only for a text without any letter (an empty text, an apostrophe, digits). Characters other than
  letters and apostrophes are ignored by the model, so arbitrary text is safe
- **Constructor(model)**: wraps a model; a null model throws `ArgumentNullException`

**Error Handling**: `TryConvert` throws `ArgumentNullException` for a null word and nothing else; a word of any length
is converted (a word of 4,000 letters is pronounced, only not cached). Failure to load the embedded model surfaces from
`Default` as the model's `InvalidDataException`.

**Dependencies**: `LtsModel`, `LetterNames` and `Pronunciation`; the framework (concurrent collections).

**Callers**: `WordPronouncer` (the last source of the priority list), the rendering coverage tests and the tools.
