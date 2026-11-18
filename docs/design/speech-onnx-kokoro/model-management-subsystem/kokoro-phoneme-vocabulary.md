### KokoroPhonemeVocabulary

**Purpose**: Load Kokoro v1.0's own character-level IPA phoneme vocabulary from this package's
embedded copy of its `tokenizer.json`, and convert a phoneme string (rendered by the Phonemes.En
`MisakiRenderer` inside `KokoroPhonemePipeline`) into the integer token id sequence the ONNX model expects. This
is the last stage of this package's self-owned text-to-token path, feeding the engine's `input_ids` tensor.

**Data Model**: An in-memory `IReadOnlyDictionary<char, int>` vocabulary (`_vocab`), loaded once at
construction from the embedded resource
`DemaConsulting.Speech.Onnx.Kokoro.Resources.kokoro-v1.0-tokenizer.json`'s `model.vocab` JSON
object. `MaxPhonemeTokens = 510` (the maximum token ids this class will emit, leaving room for the
model's leading/trailing pad token within its 512-token context window: `512 - 2 = 510`).
`PadTokenId = 0` (the pad token id the model expects at both the start and end of `input_ids`; the
caller, not this class, adds those two tokens).

**Key Methods**:

- **KokoroPhonemeVocabulary()**: loads and parses the embedded `tokenizer.json` immediately via
  `LoadVocab()`. Throws `InvalidOperationException` if the embedded resource is missing or does
  not contain the expected `model.vocab` object - both indicate an assembly-build defect, never a
  runtime/environment condition a caller could recover from. Vocabulary entries whose JSON key is
  not exactly one UTF-16 code unit are skipped rather than causing a parse failure, keeping this
  loader forward-compatible with a future `tokenizer.json` revision.
- **ToTokenIds(phonemes)**: converts a phoneme string into the model's token id sequence in order,
  dropping any character not present in the vocabulary - matching the proven Python reference
  pipeline's own `[vocab[c] for c in phonemes if c in vocab]` behavior exactly - and truncating the
  result to `MaxPhonemeTokens` entries if it would otherwise exceed that length (the pipeline splits long text
  into parts of at most 500 symbols first, so this cap is the last guard and not the way long text is
  handled). Never pads with
  `PadTokenId`; the caller adds the leading/trailing pad token itself.

**Error Handling**: `ToTokenIds` throws `ArgumentNullException` for a null `phonemes` argument. The
constructor throws `InvalidOperationException` only for a missing or malformed embedded resource
(a build defect). No other exception is expected from normal operation.

**Vocabulary Format Confirmed**: directly from the real
`onnx-community/Kokoro-82M-v1.0-ONNX` `tokenizer.json` (downloaded, inspected, and re-embedded
verbatim) - the model consumes a simple per-character vocabulary under `model.vocab`, with no
byte-pair-encoding or sub-word merging, unlike a typical text LLM tokenizer.

**Dependencies**: `System.Reflection` (embedded resource access), `System.Text.Json` (vocabulary
parsing). No dependency on any other unit of this package - this
class is a pure, stateless-after-construction converter.

**Callers**: `KokoroPhonemePipeline` (converts each part's phoneme string into token ids); the engine's
`RunProbeInference` (tokenizes the probe character) and its padding (`PadTokenId`);
`OnnxKokoroEnglishSynthesisModel.CreateBackend` (constructs the instance handed to the pipeline).
