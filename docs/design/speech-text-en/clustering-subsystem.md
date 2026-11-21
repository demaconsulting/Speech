## ClusteringSubsystem Design

### Overview

The SpeechTextEn ClusteringSubsystem is the `cluster` command of the Text.En trainer
(`tools/DemaConsulting.Speech.Text.En.Trainer`), the same console tool that hosts `check-data`, `train`, `evaluate` and
`evaluate-external`. The tool is built and tested with the repository but is neither packaged nor shipped, and the
command itself is run by hand (CI runs only its tests). The subsystem induces **hierarchical word classes** (bit-string
paths of a binary merge tree, the class model of Brown et al. 1992 in its windowed form) from a pinned
corpus of US public-domain books and writes them as the word-cluster table `data/text-en/word-clusters.tsv` with its
record `data/text-en/clustering.json`. It changes no feature, setting or model: a later change consumes the table. It
contains the following units:

- **GutenbergBody**: extracts and normalizes the pinned body of a downloaded e-book
- **ClusterCorpusCache**: the source list `cluster-sources.tsv`, the mirror-only, rate-limited fetch, the body pins and
  the verified reading of bodies from the cache or from a snapshot
- **CorpusSnapshot**: the byte-reproducible archive of the normalized corpus
- **CorpusLeakageAudit**: the content-level comparison of the corpus with every evaluation sentence and the removal list
  `cluster-removals.tsv`
- **ClusterCorpusReader**: tokenization with the analyzer's tokenizer, the runtime normalization with its self-check,
  and the integer word and bigram counts
- **ClusterInducer**: the deterministic clustering over the counts (with its input, the bigram graph)
- **WordClusterTable**: the table rows with the shared-hash rule, and the table's fixed text form
- **ClusteringRecord**: the record `clustering.json`
- **ClusterCommand**: the command line entry of `cluster` and its dispatch from the trainer's `Program`

The corpus, its pins and the removals are tool inputs, not software items (see *Training Data, Training and External
Evaluation* in *SpeechTextEn System Design*).

### Interfaces

The subsystem exposes one command line interface and no public API: every type is internal. `cluster <mode>
[options]` with exactly one of the modes `--fetch [--write-pins]`, `--audit [--write]`, `--preflight [--words a,b,...]`,
`--write` and `--check`; the options and exit codes are described with *ClusterCommand Design*. The exit codes are 0
(done or current), 1 (an output is stale or a book failed) and 2 (a usage, I/O or data error). Results and progress are
lines that start with `cluster:` on the standard output; usage and input errors go to the standard error.

The subsystem consumes:

- the .NET base class library (the network is reached only by the default mirror fetcher, and only in `--fetch`);
- the TrainingDataSubsystem: `TsvTable` and `CheckReport` (strict reading of the source list), `TextEnDataSet` (the
  evaluation sentences), `SentenceKeys` (sentence keys, words and 5-grams), `CheckDataCommand.FindDataRoot` and
  `LeakageChecker`'s real-path helpers (the cache location rule);
- the TrainingSubsystem: `DeterministicMath.Log` (every logarithm) and `TrainingCorpus.TaggerVersion` (the tokenizer
  version);
- the internals of the Text.En library through `InternalsVisibleTo`: `PartOfSpeechAdapter` (the model-free tokenizer),
  `HeteronymContext` and `FeatureHashing` (the runtime lookup key).

The classes live in the `DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem` namespace. The committed inputs are
`data/text-en/cluster-sources.tsv` and `data/text-en/cluster-removals.tsv`; the committed outputs are
`data/text-en/word-clusters.tsv` and `data/text-en/clustering.json`. The cache is
`data/text-en/external/cluster-corpus/` (`raw/`, `bodies/` and the snapshot `cluster-corpus.tar`), which version control
ignores.

### Design

**Pipeline.** `ClusterCommand` resolves the data, output and cache folders and runs one mode. `--fetch` has
`ClusterCorpusCache` make every used book's body and metadata available (through `GutenbergBody`) and optionally write
new pins. Every other mode loads the verified bodies (from the cache or `--from-snapshot`) and has `ClusterCorpusReader`
tokenize them once. `--audit` runs `CorpusLeakageAudit` and writes or compares `cluster-removals.tsv`. `--preflight` has
the reader count the corpus with the committed removals and prints the coverage report. `--write` and `--check` audit
(and refuse to continue when the committed removals differ), count, induce with `ClusterInducer`, build the table with
`WordClusterTable`, and the record with `ClusteringRecord`; `--write` builds the snapshot with `CorpusSnapshot` and
writes the table and the record, while `--check` only measures the snapshot (computes its hash without writing it) and
compares the table and the record byte for byte against the files in the output folder and the existing snapshot (the
`--from-snapshot` archive, otherwise the cache's) by hash, so a missing, stale or tampered snapshot is reported and
never silently rebuilt.

**Mirror only, politely (decision).** Books are fetched only over HTTPS from an allowed official mirror
(`gutenberg.pglaf.org`, or `aleph.gutenberg.org` with `--mirror`), never from the main web site: the source list refuses
any other address and the fetcher refuses any other host and never follows a redirect (a redirect fails that book).
Requests are sequential with at least `--delay-ms` (default 2,000, minimum 1,000) between them, a failed request is
retried at most three times after 5, 15 and 45 seconds, and HTTP 403 or 429 stops the run. Only missing or failing
files are requested.

**Pins and snapshot (decision).** Each used book's normalized body is pinned by size and SHA-256 in
`cluster-sources.tsv`; a pin is filled only when the row has none and `--write-pins` is given, and is never overwritten.
Every read verifies the pin. The snapshot archive holds the bodies and the list with every header field fixed, so its
SHA-256 identifies the corpus and is recorded; it is kept in the cache and not published (where to host it is an open
question for the owner; a release asset named after the hash prefix is the proposal).

**Content-level audit before counting (decision).** Book-level exclusion (the books of the development and natural
holdout documents are not `used`) is complemented by a content audit against every evaluation file, so alternate
editions, anthologies and quotations are found. The removals are committed and reviewed on their own (`--audit --write`)
and `--write` refuses a stale list. The reader skips each removed sentence after checking its text hash, so a list made
for another tokenizer or corpus fails instead of removing the wrong sentences.

**Runtime keys (decision).** The reader tokenizes with the analyzer's own model-free tokenizer and normalizes each token
with the analyzer's rule, and asserts once per distinct token text that the hash of the normalized form equals the
runtime key `ContextToken.LowerHash`. Because the normalization is idempotent, each table word's runtime key is the hash
of the word itself. When two vocabulary words share a hash, `WordClusterTable` leaves out **all** of them and counts
them, so a lookup can never return another word's class; a word outside the table still matches a table hash with a
probability of about the table size divided by 2^32 (about 1 in 100,000 for 50,000 words).

**Determinism and no platform-dependent arithmetic (decision).** The table must be byte-identical on Windows, Linux and
macOS and for any `--threads`. Therefore:

- every count is a 64-bit integer, and the vocabulary, the bigrams and the removals are ordered by integer and ordinal
  string comparisons only;
- every logarithm is `DeterministicMath.Log`, which uses only addition, subtraction, multiplication and division in a
  fixed order (range reduction, a fixed-length series, exact power-of-two scaling); the clusterer's table of logarithms
  of small counts is filled with that same function, so a value read from the table and one computed directly are the
  same bits;
- the clusterer uses only `+`, `-`, `*` and `/` on `double`, each expression written in a fixed order and each sum taken
  in ascending slot order; it calls no platform math function (`Math.Log`, `Math.Exp`), no fused multiply-add, no vector
  or SIMD reduction and no culture-sensitive operation. IEEE 754 defines these operations exactly (correctly rounded)
  and the .NET JIT neither contracts them into fused multiply-adds nor reorders them, on x64 and on ARM64 alike, which
  is the same property that `train --check` already relies on in the CI matrix;
- parallel work is split into phases separated by the completion of a parallel loop (a full barrier). Within a phase
  each cell is written by exactly one iteration with a formula that does not depend on how the iterations are divided,
  and every iteration reads only state that is frozen for the phase; the merge is the minimum under the total order
  (loss, lower slot, higher slot), found per row and reduced in ascending row order. Books are tokenized, counted and
  audited in parallel into separate slots and merged in ordinal source-id order.

So the algorithm has no platform-dependent arithmetic, and its result does not depend on the thread count. The golden
tests (a fixed-input clustering with its exact paths and merge losses, a fixture corpus with its exact table, and a
snapshot with its exact SHA-256) run in every CI job, so any platform difference would fail the build.

**Clustering (summary).** The first `C` words by rank fill slots `0` to `C - 1`; each further word enters the free slot,
the two slots whose merge loses the least mutual information are merged into the lower slot and the higher slot becomes
free; after the last word the clusters are merged down to one, and each merge makes a tree node whose child from the
lower slot gets bit 0. The losses are maintained incrementally in O(C²) per word, so the run time grows as V·C² for V
words (see *ClusterInducer Design*). The default is `C = 512`.

**Unit collaboration.** `GutenbergBody` knows the e-book format and nothing else. `ClusterCorpusCache` is the only unit
that opens the network or a cache file. `CorpusSnapshot` knows the archive format. `ClusterCorpusReader` is the only
unit that tokenizes; `CorpusLeakageAudit` consumes its tokenized corpus. `ClusterInducer` sees only integer counts and
knows no words. `WordClusterTable` and `ClusteringRecord` know their file formats. `ClusterCommand` depends on every
other unit; there are no cycles.

**Concurrency and cancellation.** Tokenization, counting, the audit and the clustering use up to `--threads` worker
threads (default: the processor count); the thread count is never recorded. The fetch is single threaded. The command
runs to completion; nothing is kept between runs except the cache, the snapshot and the written files.
