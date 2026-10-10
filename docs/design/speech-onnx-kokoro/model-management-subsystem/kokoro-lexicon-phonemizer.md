### KokoroLexiconPhonemizer

**Purpose**: Convert English text into Kokoro v1.0's own IPA phoneme string by looking each word
up in a verified, precomputed word-to-phoneme lexicon, rather than reimplementing
grapheme-to-phoneme (G2P) linguistics by hand. This is the first stage of this package's
self-owned text-to-speech pipeline, feeding `KokoroPhonemeVocabulary.ToTokenIds`.

**Data Model**: An in-memory `IReadOnlyDictionary<string, string>` lexicon (`_lexicon`), lower-case
word to phoneme string, loaded once at construction from the embedded, gzip-compressed resource
`DemaConsulting.Speech.Onnx.Kokoro.Resources.kokoro-en-lexicon.tsv.gz`. A compiled `TokenPattern`
regex matches one word (letters and internal apostrophes), one run of whitespace, or one other
single character. A fixed `PassthroughPunctuation` set declares the exact non-alphabetic
vocabulary characters Kokoro v1.0's tokenizer recognizes as punctuation.

**Key Methods**:

- **KokoroLexiconPhonemizer()**: loads and parses the embedded lexicon immediately via
  `LoadLexicon()`, decompressing the gzip stream and splitting each TSV line on its first tab.
  Throws `InvalidOperationException` if the embedded resource is missing - an assembly-build
  defect, never a runtime/environment condition a caller could recover from.
- **Phonemize(text)**: tokenizes `text` with `TokenPattern`; for each whitespace token, appends a
  single vocabulary space character; for each word token, looks up its lower-cased form in the
  lexicon, appending its phoneme string when found and otherwise recording it (lower-cased) as an
  unknown word; for any other single character, appends it only when `PassthroughPunctuation`
  contains it. Returns a tuple of the assembled phoneme string and the distinct unknown words
  encountered, in encounter order.

**Error Handling**: `Phonemize` throws `ArgumentNullException` for a null `text` argument. The
constructor throws `InvalidOperationException` only for a missing embedded resource (a build
defect). No other exception is expected from normal operation.

**Text Normalization**: before tokenizing, `Phonemize` runs the internal `KokoroTextNormalizer`,
which strips diacritics and markdown, reads URLs and e-mail addresses aloud, expands common
abbreviations, and spells out numbers, currency, times, percentages, ordinals and years. Words
resolve in this order: lexicon hit; possessive (`'s`); camelCase/PascalCase parts; all-caps
acronym spelled by letter names; then a spelling correction (a missing-apostrophe contraction, or
the most frequent lexicon word within a Damerau-Levenshtein distance of 1, or 2 for words longer
than 8 letters, using the optional third lexicon column - zipf frequency x 100 - to break ties).
Only a word with no match at all is dropped and reported. Any dropped character still acts as a
word separator so neighbors do not run together.

**Known Stage-1 Limitations** (both deliberate, honestly documented, not hidden defects):

- **Out-of-vocabulary words**: a word absent from the embedded lexicon (an uncommon proper noun, a
  neologism, or a word CMUdict itself does not list) is silently dropped from the phoneme stream
  rather than guessed at. The caller can detect this via `Phonemize`'s `UnknownWords` return
  value. A later pass may add a real fallback G2P (for example invoking `espeak-ng`) without
  changing this class's public contract.
- **Homographs**: the lexicon maps one spelling to exactly one pronunciation, computed with no
  surrounding sentence context, so a handful of English words (for example "read" or "lead") are
  always rendered with the one pronunciation misaki's isolated, no-context G2P call chose for
  that spelling - the same simplification every static-dictionary G2P makes.

**Lexicon Provenance**: precomputed once, offline, by running the real `misaki.en.G2P` tool (the
same phonemizer `hexgrad/kokoro` itself depends on) over every single-pronunciation, purely
alphabetic word, plus every apostrophe contraction (for example "don't" or "can't"), in the
public-domain CMUdict word list, so every phoneme string this class ever
emits was produced by the same tool Kokoro's own authors use, not by this library's own guesswork.
Kokoro's phoneme encoding packs several common diphthongs and affricates into single, non-obvious
Unicode characters (for example `I` represents the diphthong `/aɪ/` as in "life", and `O`
represents `/oʊ/` as in "know") - confirmed empirically rather than assumed.

**Dependencies**: none beyond the BCL (`System.IO.Compression`, `System.Text.RegularExpressions`).

**Callers**: `OnnxKokoroSynthesisEngine.Generate` (phonemizes each segment's text before
tokenization via `KokoroPhonemeVocabulary.ToTokenIds`); `OnnxKokoroEnglishSynthesisModel.CreateBackend`
(constructs the instance passed to the engine).
