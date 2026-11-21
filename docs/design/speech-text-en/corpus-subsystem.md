## CorpusSubsystem Design

### Overview

The SpeechTextEn CorpusSubsystem is the `corpus` command of the Text.En trainer
(`tools/DemaConsulting.Speech.Text.En.Trainer`), the same console tool that hosts `check-data`, `train`, `evaluate` and
`evaluate-external`. The tool is built and tested with the repository but is neither packaged nor shipped, and the
command itself is run by hand (CI runs only its tests). The subsystem fetches a pinned corpus of US public-domain books
from an official mirror and audits it against every evaluation set, so that a later context feature (word vectors or
word classes) can be learned from a corpus whose origin and overlap with the evaluation data are on record. No model,
feature or setting uses the corpus yet. It contains the following units:

- **GutenbergBody**: extracts and normalizes the pinned body of a downloaded e-book
- **CorpusCache**: the source list `cluster-sources.tsv`, the mirror-only, rate-limited fetch, the body pins and
  the verified reading of bodies from the cache
- **CorpusReader**: tokenization with the analyzer's tokenizer and the runtime normalization of a token
- **CorpusLeakageAudit**: the content-level comparison of the corpus with every evaluation sentence and the removal list
  `cluster-removals.tsv`
- **CorpusCommand**: the command line entry of `corpus` and its dispatch from the trainer's `Program`

The corpus, its pins and the removals are tool inputs, not software items (see *Training Data, Training and External
Evaluation* in *SpeechTextEn System Design*).

### Interfaces

The subsystem exposes one command line interface and no public API: every type is internal. `corpus <mode>
[options]` with exactly one of the modes `--fetch [--write-pins]` and `--audit [--write]`; the options and exit codes
are described with *CorpusCommand Design*. The exit codes are 0 (done or current), 1 (the removals are stale or a book
failed) and 2 (a usage, I/O or data error). Results and progress are lines that start with `corpus:` on the standard
output; usage and input errors go to the standard error.

The subsystem consumes:

- the .NET base class library (the network is reached only by the default mirror fetcher, and only in `--fetch`);
- the TrainingDataSubsystem: `TsvTable` and `CheckReport` (strict reading of the source list), `TextEnDataSet` (the
  evaluation sentences), `SentenceKeys` (sentence keys, words and 5-grams), `CheckDataCommand.FindDataRoot` and
  `LeakageChecker`'s real-path helpers (the cache location rule);
- the internals of the Text.En library through `InternalsVisibleTo`: `PartOfSpeechAdapter` (the model-free tokenizer)
  and `FeatureHashing` (the runtime's ASCII case folding).

The classes live in the `DemaConsulting.Speech.Text.En.Trainer.CorpusSubsystem` namespace. The committed inputs are
`data/text-en/cluster-sources.tsv` and `data/text-en/cluster-removals.tsv`. The cache is
`data/text-en/external/cluster-corpus/` (`raw/` and `bodies/`), which version control ignores.

### Design

**Pipeline.** `CorpusCommand` resolves the data and cache folders and runs one mode. `--fetch` has
`CorpusCache` make every used book's body and metadata available (through `GutenbergBody`) and optionally write
new pins. `--audit` loads the pin-verified bodies from the cache, has `CorpusReader` tokenize
them once, runs `CorpusLeakageAudit` and writes or compares `cluster-removals.tsv`.

**Mirror only, politely (decision).** Books are fetched only over HTTPS from an allowed official mirror
(`gutenberg.pglaf.org`, or `aleph.gutenberg.org` with `--mirror`), never from the main web site: the source list refuses
any other address and the fetcher refuses any other host and never follows a redirect (a redirect fails that book).
Requests are sequential with at least `--delay-ms` (default 2,000, minimum 1,000) between them, a failed request is
retried at most three times after 5, 15 and 45 seconds, and HTTP 403 or 429 stops the run. Only missing or failing
files are requested.

**Pins (decision).** Each used book's normalized body is pinned by size and SHA-256 in `cluster-sources.tsv`; the pins
are the identity of the corpus. A pin is filled only when the row has none and `--write-pins` is given, and is never
overwritten, so a book whose mirror text changed fails the fetch instead of being re-pinned. Every read verifies the
pin. There is no separate archive of the corpus (owner decision): `--fetch` into an empty cache reproduces it bit for
bit while every book still matches its pin, and a book that does not is re-pinned only after review (see the data
README).

**Content-level audit (decision).** Book-level exclusion (the books of the development and natural holdout documents
are not `used`) is complemented by a content audit against every evaluation file, so alternate editions, anthologies
and quotations are found. The removals are committed and reviewed on their own (`--audit --write`); without `--write`
the audit compares the removals it computes with the committed list byte for byte. The removals hold the SHA-256 of
each removed sentence, so a later user of the corpus can prove the list still fits it. The audit needs the corpus cache,
so CI cannot detect stale removals; the audit is re-run before the corpus is next used.

**Determinism (decision).** The removal list must be byte-identical on Windows, Linux and macOS and for any
`--threads`. Books are tokenized and audited in parallel into separate slots and merged in ordinal source-id order,
every attribution picks the first evaluation sentence in file order and then ordinal id, and the rows are sorted with
integer and ordinal string comparisons only.

**Unit collaboration.** `GutenbergBody` knows the e-book format and nothing else. `CorpusCache` is the only unit
that opens the network or a cache file. `CorpusReader` is the only
unit that tokenizes; `CorpusLeakageAudit` consumes its tokenized corpus. `CorpusCommand` depends on every other unit;
there are no cycles.

**Concurrency and cancellation.** Tokenization and the audit use up to `--threads` worker threads (default: the
processor count). The fetch is single threaded. The command runs to completion; nothing is kept between runs except the
cache and the written files.
