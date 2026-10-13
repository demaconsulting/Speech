### ByteLevelBpeTokenizer

**Purpose**: Convert text into the token ids the Chatterbox Turbo language model was trained on,
exactly as the reference Hugging Face tokenizer does. It is model-family neutral so a future
byte-level BPE model package can reuse the design.

**Data Model**: Immutable after construction apart from a pre-token cache: the vocabulary (token to id), the
ordered merge ranks, the added-token table (atomic special tokens such as paralinguistic tags), the
byte-to-unicode table, and the number of trailing end-of-text ids the post-processor appends (two for
Chatterbox Turbo, id 50256). The added tokens are matched by one regular expression whose alternatives are
ordered longest first, so a token that is a prefix of another never shadows it. A `ConcurrentDictionary`
caches the BPE ids per pre-token text; the cache is unbounded, so a long-lived shared instance fed many
distinct pre-tokens grows (acceptable for a per-model instance). The instance is thread-safe: everything else
is read-only, and a cache race only repeats deterministic work. The added-token flags in the document
(`special`, left-strip and right-strip flags, normalization) are ignored.

**Key Methods**:

- **FromGzipStream(stream, trailingEndOfTextCount)** / constructor `(stream, endOfTextToken, trailingEndOfTextCount)`:
  load and validate a gzip-compressed `tokenizer.json`. `endOfTextToken` (default `<|endoftext|>`) is
  looked up in the added tokens then the vocabulary; `trailingEndOfTextCount` (default 2, 0 disables) is how
  many of its id are appended to every encoding. A duplicate merge keeps its first (lowest) rank.
- **Encode(text)**: split out added tokens, pre-tokenize the remaining text with the GPT-2 style
  regex, map bytes to unicode, apply merges by rank, look up ids, and append the trailing ids

**Error Handling**: `InvalidDataException` for a non-gzip stream, an invalid or empty tokenizer
document (including null or malformed merge entries and null or empty added-token content) or an
end-of-text token that is not in the tokenizer when ids must be appended;
`ArgumentNullException` for null arguments; `ArgumentOutOfRangeException` for a negative
`trailingEndOfTextCount`; `InvalidOperationException` from `Encode` when a merged piece has no
vocabulary entry (corrupt tokenizer). The regular expressions carry a five-second match timeout as a
defensive guard against pathological input (`RegexMatchTimeoutException`); the timeout is not exercised by a
test because the fixed patterns cannot be driven to it.

**Dependencies**: `System.Text.Json`, `System.IO.Compression`, `System.Text.RegularExpressions`.

**Callers**: `OnnxChatterboxTurboEnglishSynthesisModel.CreateBackend` and the synthesis engine.
