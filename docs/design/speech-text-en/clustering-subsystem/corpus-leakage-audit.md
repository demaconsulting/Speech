### CorpusLeakageAudit

**Purpose**: Find every corpus sentence that repeats, nearly repeats or quotes a sentence of an evaluation set, so it is
listed for review and kept out of any use of the corpus, and record the removals reproducibly. Book-level exclusion
cannot catch another edition, an anthology or a quotation in another book; this audit compares content.

**Data Model**:

- `EvaluationSentence`: evaluation file, row id and sentence
- `CorpusRemoval`: one row of `cluster-removals.tsv`: source id, 0-based paragraph and sentence indexes (as the reader
  enumerates them), rule (`exact`, `near` or `contains`), evaluation file and id of the attributed sentence, and the
  SHA-256 of the removed sentence's UTF-8 text
- `AuditCount`: per evaluation file and rule, the removed corpus sentences, the distinct evaluation sentences they name
  and the distinct books
- `EvaluationFiles`: `dev.tsv`, `holdout-natural.tsv`, `holdout-independent.tsv`, `regression.tsv`,
  `generalization.tsv`, in attribution order

**Normalization and thresholds.** `SentenceKeys` of the TrainingDataSubsystem gives the key (lower-case ASCII letters
and digits), the words and the distinct word 5-grams of a sentence, and the thresholds equal the data check's leakage
rules (8 words, 80 percent, computed in integers as `hits * 5 >= total * 4`).

**Key Methods**:

- **LoadEvaluationSentences(dataRoot)**: loads the data folder with `TextEnDataSet` and returns every row of the
  development, natural holdout and independent holdout files plus the regression and generalization rows, ordered by
  file and then ordinal id; refuses a data folder that does not load cleanly
- **EvaluationListingSha256(dataRoot)**: the SHA-256 of the lines `file TAB sha256 LF` of the five files (`-` for a
  missing file), recorded so the record shows which evaluation data the audit used; it delegates to
  `ClusterDataChecker.EvaluationListingSha256`, which owns the computation (same format) so `check-data` can report
  drift offline
- **Run(corpus, evaluation, threads, token)**: builds an index (first sentence per key, first sentence per 5-gram, every
  sentence of at least five words per 5-gram) and audits each book in parallel into its own slot. Per paragraph, each
  sentence is tested for **exact** (equal non-empty key) and then **near** (at least 8 words and at least 80 percent of
  its 5-grams in the index; attributed to the first sentence owning one of the shared 5-grams). Then **contains**: an
  evaluation sentence of at least 8 words with at least 80 percent of its 5-grams among the paragraph's 5-grams, or of 5
  to 7 words with all its 5-grams in one sentence of the paragraph, removes every not-yet-removed sentence of the
  paragraph that shares a 5-gram with it. The removals are merged and sorted by source id (ordinal), paragraph and
  sentence; the result does not depend on the thread count
- **Counts(removals)**: one `AuditCount` per evaluation file and rule, in file then rule order
- **Write(removals)** and **Parse(bytes)**: the header `source_id`, `paragraph`, `sentence`, `rule`, `eval_file`,
  `eval_id`, `text_sha256`, LF endings; `Parse` validates every cell and the order

**Error Handling**: null arguments throw `ArgumentNullException`, a thread count below 1 throws
`ArgumentOutOfRangeException`; a data folder that does not load cleanly and a malformed removals file throw
`InvalidDataException`.

**Dependencies**: `ClusterCorpusReader` (the tokenized corpus and the parallel loop helper), `GutenbergBody`
(`Sha256Hex`), and the TrainingDataSubsystem's `SentenceKeys`, `TextEnDataSet` and `CheckReport`.

**Callers**: `ClusterCommand` and the unit tests.
