### ByteLevelBpeTokenizer

**Purpose**: Convert text into the token ids the Chatterbox Turbo language model was trained on,
exactly as the reference Hugging Face tokenizer does. It is model-family neutral so a future
byte-level BPE model package can reuse the design.

**Data Model**: Immutable after construction: the vocabulary (token to id), the ordered merge ranks,
the added-token table (atomic special tokens such as paralinguistic tags), the byte-to-unicode
table, and the number of trailing end-of-text ids the post-processor appends (two for Chatterbox
Turbo, id 50256).

**Key Methods**:

- **FromGzipStream(stream)** / constructor: load and validate a gzip-compressed `tokenizer.json`
- **Encode(text)**: split out added tokens, pre-tokenize the remaining text with the GPT-2 style
  regex, map bytes to unicode, apply merges by rank, look up ids, and append the trailing ids

**Error Handling**: `InvalidDataException` for a non-gzip stream or an invalid tokenizer document;
`ArgumentNullException` for null arguments. Regex matches use a timeout.

**Dependencies**: `System.Text.Json`, `System.IO.Compression`, `System.Text.RegularExpressions`.

**Callers**: `OnnxChatterboxTurboEnglishSynthesisModel.CreateBackend` and the synthesis engine.
