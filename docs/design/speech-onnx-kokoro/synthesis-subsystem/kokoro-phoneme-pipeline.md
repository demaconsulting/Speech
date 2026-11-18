### KokoroPhonemePipeline

**Purpose**: Turn the text of one speech segment, with the annotations of the English text analysis when there
are any, into the phoneme token ids Kokoro v1.0 expects. This is this package's whole text-to-token path: the
shared English phonemizer, the Kokoro symbol renderer and Kokoro's own vocabulary, with no pronunciation data of
its own.

**Data Model**: Immutable and thread-safe. Holds the `KokoroPhonemeVocabulary` it was constructed with, one
Phonemes.En `EnglishPhonemizer` and one `MisakiRenderer(MisakiProfile.Us, UseWeakForms)`. `UseWeakForms` is an
internal constant, `true`: the single switch for how the small function words are rendered (see below).

**Key Methods**:

- **KokoroPhonemePipeline(vocabulary)**: validates the vocabulary (`ArgumentNullException` when null) and
  creates the phonemizer and renderer. Called only from `OnnxKokoroEnglishSynthesisModel.CreateBackend` and the
  tests.
- **ToTokenIds(text, annotations)**: phonemizes `text` with `annotations` (offsets relative to `text`, or
  `null` for none: default readings, no normalization), asks the renderer for its parts with
  `RenderParts(phonemized, MaxPhonemeTokens)`, converts each part through the vocabulary and returns the token
  id sequences in speaking order, without the pad tokens (the engine adds them). A part that converts to no
  token is left out, so text with no sound at all returns an empty list.

**Function words.** With `UseWeakForms` on, the renderer writes the small function words (the, of, to, and, ...)
as in running speech: unstressed, some with a reduced vowel. Kokoro was trained on phonemes in which function
words are weak, and the measured phone error rate against the reference phonemizer's output is 1.99 percent
with weak forms and 4.88 percent with full forms on the tuning sentences (1.43 and 4.50 on the validation
sentences; stress agreement 94.2 against 71.4 percent), see `docs/evaluation/phonemes-en/kokoro-adoption.md`.
This departs from the first plan (full forms for Kokoro) on measurement only and is flagged for the owner's
listening check.

**Long text.** `RenderParts` of the renderer budgets 10 symbols of reserve below the limit, so a part has at most
500 symbols, cuts between words, prefers a clause mark and loses no sound; every symbol of a part is one token
or none, so a part never has more tokens than symbols. Nothing is truncated. The vocabulary's own cap of 510 is
therefore only a last guard.

**British voices.** The renderer profile is `Us` for every voice: the phonemes are General American, so the
British voices of the model (`bf_*`, `bm_*`) speak American pronunciations in their own voice quality. This is
a documented limit, not a selection error; British phonemes are deferred.

**Error Handling**: `ArgumentNullException` for a null vocabulary or a null text. A word that the dictionary
does not have is not an error: it is pronounced from its spelling. The pipeline never reports unknown words;
the Phonemes.En result lists them for a caller that wants to, and this pipeline does not use that list.

**Dependencies**: `EnglishPhonemizer`, `MisakiRenderer`, `MisakiProfile` and `SegmentAnnotation` (Phonemes.En
and Speech); `KokoroPhonemeVocabulary` from the ModelManagementSubsystem.

**Callers**: `OnnxKokoroSynthesisEngine` (the plain and the annotated generation, for every segment) and
`OnnxKokoroEnglishSynthesisModel.CreateBackend` (constructs the instance handed to the engine); the tests.
