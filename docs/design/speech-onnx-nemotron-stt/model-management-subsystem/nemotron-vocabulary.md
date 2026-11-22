### NemotronVocabulary

**Purpose**: Load the model's `vocab.txt` and answer the four vocabulary questions the
recognizer needs: which `lang_id` selects a locale, which token id is the RNN-T blank, whether a
token starts a new word, and what text a sequence of token ids spells.

**Data Model**: An immutable array of token strings, indexed by token id; the blank id
(`BlankId`) and a locale-to-language-id dictionary derived once at construction. The language id
of a locale is its ordinal among the `<unk>` and `<xx-XX>` locale marker tokens in vocabulary
order, with `<unk>` equal to 0 - for the shipped vocabulary `en-GB` is 24 and `en-US` is 25. The
blank id is the index of the explicit blank token when the vocabulary has one and otherwise the
last index; it is 13087 in the shipped `vocab.txt`.

**Key Methods**:

- **NemotronVocabulary(tokens)**: builds the vocabulary from an in-memory token list; throws
  `ArgumentException` for an empty list.
- **Load(path)** *(static)*: reads a `vocab.txt` file, one token per line with the line index as
  the token id, and constructs the vocabulary.
- **Count**: the number of tokens.
- **TryGetLanguageId(locale, out languageId)**: resolves a locale such as `en-US` to its
  ordinal; returns `false` for a locale the vocabulary does not declare.
- **StartsWord(tokenId)**: returns `true` when the token exists and its text starts with the
  SentencePiece word-boundary marker (U+2581); an id outside the vocabulary is not a word start.
  It lets the recognition engine split an over-long utterance only between words: the tokens
  before a word-starting token can be finalized while the word it starts, which may still be
  growing, is carried into the next utterance.
- **Detokenize(tokenIds)**: concatenates the tokens, maps the SentencePiece word marker
  (U+2581) to a space, strips `<xx-XX>` locale markers, collapses whitespace runs, and trims;
  returns an empty string for an empty sequence.

**Error Handling**: the constructor throws for an empty vocabulary and `Load` propagates file
exceptions; `TryGetLanguageId` reports an unknown locale and `StartsWord` an out-of-range id
through their return values rather than throwing.

**Dependencies**: `System.IO` and `System.Text.RegularExpressions` only. No dependency on any
other unit.

**Callers**: `OnnxNemotronMultilingualRecognitionModel.CreateBackend` (loads the vocabulary,
derives `lang_id` and the blank id); `OnnxNemotronRecognitionEngine` (detokenizes emitted tokens,
detects closing punctuation by detokenizing one token, and finds word starts with `StartsWord`).
