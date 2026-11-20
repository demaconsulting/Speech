### EnglishPhonemizer

**Purpose**: Be the single public entry point of the package: turn the text of one segment, optionally with the
annotations of the text analysis, into a `PhonemizedText`, by driving the spoken-text builder, the tokenizer and the
pronouncer and applying the host overrides and the readings.

**Data Model**:

- `EnglishPhonemizer`: a sealed class holding the shared `CmuLexicon` and a `WordPronouncer` over the embedded lexicon,
  reading tables and letter-to-sound converter; immutable and thread-safe, with one public parameterless constructor
- `PhonemizedText`: an immutable list of `PhonemizedItem` (`Items`) with the distinct out-of-vocabulary words
  (`OovWords`) and the distinct unsupported tokens (`UnsupportedTokens`), each in order of first occurrence
- `PhonemizedItem`: a sealed class with `Kind`, `Text` (as spoken), `Phones` (the ARPAbet symbols, empty without sound),
  `Arpabet` (the symbols joined by spaces), `Source`, `FromReading` (a reading was applied), `ReadingId` (the id that
  was requested, whether or not it was applied), `Oov` and `IpaOverride`; internally it also carries the pronunciation
  as phones and the element starts of a hyphenated compound (where each element after the first begins; the first of
  them is the compound break)
- `PhonemizedItemKind`: `Word`, `Punctuation`, `Unsupported`; `PronunciationSource`: the sources in priority order

**Key Methods**:

- **Phonemize(text)**: the same as `Phonemize(text, null)`
- **Phonemize(text, annotations)**: builds the spoken text and the annotation index, tokenizes, and adds the items of
  each token (see *PhonemizerSubsystem Design*). A word token that starts with unchanged original text takes a host
  override first (ARPAbet parsed, or IPA carried with the normal pronunciation as a fallback), then a reading
  annotation, each applied to the whole token or to its headword with a clitic ending attached, then the pronouncer's
  answer. An item that could not be pronounced at all becomes an unsupported item. An empty or null annotation list is
  the same as no annotations

**Error Handling**: a null text throws `ArgumentNullException`; every other input, including text of any content and
malformed or out-of-range annotations, produces a result.

**Dependencies**: `WordTokenizer` (spoken text, annotation index, tokens), `WordPronouncer`, `CmuLexicon`, `Morphology`
(clitics), `ReadingPronunciations` and `LetterToSound` (through the pronouncer), and the Speech and SpeechTextEn
annotation types.

**Callers**: the Kokoro and Piper model packages, the renderers (which read the result types) and the tools and tests.
