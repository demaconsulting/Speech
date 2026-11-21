### CorpusReader

**Purpose**: Turn the verified bodies into books, paragraphs, sentences and tokens, tokenizing exactly as the analyzer
does, so the audit compares the corpus with evaluation sentences on the analyzer's terms; and normalize a token with
the analyzer's rule, so a later feature learned from the corpus holds the forms the analyzer looks up.

**Data Model**:

- `CorpusParagraph` (text and, per sentence, the token spans as flat start and length pairs; `SentenceText`),
  `CorpusBook` (source id and paragraphs) and `TokenizedCorpus` (the books in input order; `Tokens`)
- `MaximumFormLength` 40, `NumberForm` `<num>`

**Key Methods**:

- **Tokenize(bodies, threads, token)**: splits each body into paragraphs at blank lines (lines joined by single spaces)
  and each paragraph into sentences and tokens with `PartOfSpeechAdapter.Shared.Analyze(text, tag: false)`, the
  analyzer's model-free tokenizer, one book per parallel iteration into its own slot, so the result does not depend on
  the thread count
- **Normalize(token)** and **NormalizeEligible(token)**: the analyzer's rule: a token with a digit and no letter (per
  Unicode scalar) becomes `<num>`; otherwise each UTF-16 unit maps the curly single quotes to the apostrophe and ASCII
  capitals to lower case. A form is eligible when it has at most 40 units, all in U+0021 to U+007E, U+00A1 to U+00FF or
  U+2010 to U+2027 (a set whose text is NFC-stable and platform-independent)
- **ForEachIndex(count, threads, token, body)**: the parallel loop of the subsystem's units; a failure inside it is
  rethrown as itself

**Error Handling**: null arguments throw `ArgumentNullException`; a thread count below 1 throws
`ArgumentOutOfRangeException`.

**Dependencies**: the Text.En library's `PartOfSpeechAdapter` and `FeatureHashing` (internals).

**Callers**: `CorpusLeakageAudit` (the tokenized corpus and `ForEachIndex`), `CorpusCommand` and the unit tests.
