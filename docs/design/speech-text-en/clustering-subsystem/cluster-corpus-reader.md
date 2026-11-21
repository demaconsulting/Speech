### ClusterCorpusReader

**Purpose**: Turn the verified bodies into the integer word and bigram counts the clusterer needs, tokenizing and
normalizing exactly as the analyzer does, so every table word is the analyzer's lookup key of itself.

**Data Model**:

- `CorpusParagraph` (text and, per sentence, the token spans as flat start and length pairs; `SentenceText`),
  `CorpusBook` (source id and paragraphs) and `TokenizedCorpus` (the books in ordinal id order; `Tokens`)
- `SkippedSentence`: a removal as the reader applies it: source id, paragraph, sentence and text SHA-256
- `ReaderSettings`: the minimum count (at least 1) and the vocabulary limit (0 for none)
- `CorpusCounts`: the vocabulary words by rank with their counts, the `BigramGraph`, the count of every eligible form,
  and the numbers of paragraphs, sentences, tokens, removed sentences and removed tokens; `Types` and
  `CoveragePerTenThousand(words)`
- `ReaderVersion` 1, `MaximumFormLength` 40, `NumberForm` `<num>`

**Key Methods**:

- **Tokenize(bodies, threads, token)**: splits each body into paragraphs at blank lines (lines joined by single spaces)
  and each paragraph into sentences and tokens with `PartOfSpeechAdapter.Shared.Analyze(text, tag: false)`, the
  analyzer's model-free tokenizer, one book per parallel iteration into its own slot
- **Normalize(token)** and **NormalizeEligible(token)**: the analyzer's rule: a token with a digit and no letter (per
  Unicode scalar) becomes `<num>`; otherwise each UTF-16 unit maps the curly single quotes to the apostrophe and ASCII
  capitals to lower case. A form is eligible when it has at most 40 units, all in U+0021 to U+007E, U+00A1 to U+00FF or
  U+2010 to U+2027 (a set whose text is NFC-stable and platform-independent)
- **Count(corpus, skipped, settings, threads, token)**:
  1. per book, in parallel: verifies each skipped sentence (it must exist and its text must have the recorded SHA-256),
     skips it, and turns the remaining tokens into local form ids in first-appearance order with -1 for an ineligible
     token and after each sentence; once per distinct token text it asserts that `FeatureHashing.Fnv1a(form)` equals
     `HeteronymContext.Create(...).LowerHash` (the **self-check**);
  2. sequentially in book order: global ids by first appearance and integer word counts;
  3. the vocabulary: the forms with at least the minimum count, by count descending and then ordinal form, cut to the
     limit;
  4. the bigrams: every adjacent pair of vocabulary tokens within a sentence as the integer key `(left rank << 32) |
     right rank`, sorted and run-length counted, then the graph (`BigramGraph.FromSortedKeys`).
  No hash-table iteration order is observable, so the result does not depend on the thread count
- **ForEachIndex(count, threads, token, body)**: the parallel loop of the subsystem's units; a failure inside it is
  rethrown as itself

**Error Handling**: null arguments throw `ArgumentNullException`; a thread count, minimum count or limit out of range
throws `ArgumentOutOfRangeException`. A removal that names an unknown book, paragraph or sentence, or whose text hash
differs, throws `InvalidDataException` (the removals are stale). A form that does not hash to the runtime key throws
`InvalidOperationException` (the reader no longer matches the analyzer).

**Dependencies**: the Text.En library's `PartOfSpeechAdapter`, `HeteronymContext` and `FeatureHashing` (internals),
`ClusterInducer`'s `BigramGraph`, and `GutenbergBody` (`Sha256Hex`).

**Callers**: `CorpusLeakageAudit` (the tokenized corpus and `ForEachIndex`), `ClusterCommand` and the unit tests.
