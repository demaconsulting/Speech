## ClusteringSubsystem Verification

### Verification Approach

The SpeechTextEn ClusteringSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests` that need no network and no real book. The subsystem tests in
`ClusteringSubsystemTests.cs` run the real `cluster` command with its real units over a fixture
(`Fakes/ClusteringFixture.cs`) that builds, in a temporary folder, a valid data folder at `data/text-en` (with
`Fakes/DatasetFixture.cs`), three small books generated in code from fixed word lists and wrapped in a downloaded
e-book's header and footer, and a source list without pins. Only the network and the pause are replaced: the command
takes the fetcher and the sleeper as delegates, and the fixture's fetcher serves the books from memory, counts the
requests and answers an unknown address with HTTP 404, while its sleeper records the pauses. The tests observe exit
codes, printed lines and the written table, record, removal list and snapshot. **No test uses the network or a real
book.** Each unit is verified in isolation by its own test file (see the unit verification documents).

Three golden results make the CI matrix compare platforms: the fixed-input clustering of *ClusterInducer Verification*
(exact paths and merge losses), the fixture corpus's exact table (tokenizer, counts and clustering together) and the
snapshot's exact SHA-256 of *CorpusSnapshot Verification*. They run in the Windows, Linux and macOS jobs, so a platform
difference in any arithmetic or ordering fails the build.

The full induction of the real corpus (`cluster --check`) is a manual step that is recorded in the trainer's run log; it
is not part of the automated tests.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: every test builds its own temporary repository layout and deletes it afterwards; the fetcher and the
  sleeper are test doubles
- **Not used**: the network and any real book

### Acceptance Criteria

**Pinned corpus.** Fetching with pins writing, auditing, writing and checking the fixture corpus succeeds end to end,
the record's source-list and snapshot hashes match the files, and a cached body edited after pinning makes the write
exit 2, naming the book, with no table written.

**Leakage audit.** A book that contains a development sentence of the fixture data loses exactly that sentence (rule
exact, file `dev.tsv`), the record counts one removed sentence of six tokens, and its words do not reach the table.

**Runtime keys.** Every word of the written table is unique by lookup hash and hashes to the analyzer's key of itself;
so is every word of the committed table, which also parses with the committed record's limits and matches the record's
hash and word count.

**Determinism.** One and three threads write byte-identical tables and records, and the fixture corpus gives the golden
table.

**Record and check.** A check after a write reports every output current; a hand-edited table is reported stale (exit
1); the snapshot alone, with the cache deleted, reproduces the table without any request and without re-creating the
cache; the written table and record
pass the offline word-cluster check of `check-data` with this build's tokenizer, and an edit of the source list
afterwards fails it, naming the stale hash.

### Test Scenarios

#### Fetch, audit, write and check

**Tests**: `ClusteringSubsystem_Cluster_FetchAuditWriteCheck_EndToEnd`,
`ClusteringSubsystem_Cluster_TamperedCache_IsRefused`

After `--fetch --write-pins` and `--audit --write`, `--write --clusters 4 --min-count 1 --threads 2` and `--check
--threads 1` both exit 0 and the check prints all three outputs current; the record's table hash, word count,
source-list hash, snapshot hash, settings and used-book count agree with the files, and the write printed progress. With
one cached body changed, the write exits 2 naming the book and writes no table.

#### Leakage audit

**Tests**: `ClusteringSubsystem_Cluster_BookQuotingEvaluation_SentenceRemoved`

A fourth book holds the fixture's development sentence "The wind howled all night." between two corpus sentences; the
removal list has exactly that sentence (paragraph 0, sentence 1, rule exact, `dev.tsv`), the record counts it, and the
word "howled" is not in the table.

#### Runtime keys

**Tests**: `ClusteringSubsystem_Cluster_TableWords_AreRuntimeKeys`,
`ClusteringSubsystem_CommittedTable_WordsAreUniqueRuntimeKeys`

Every table word has a distinct `FeatureHashing.Fnv1a` hash, equal to `HeteronymContext.Create(word, ...).LowerHash`.
The committed-table test finds `data/text-en/word-clusters.tsv` above the test binaries (it is skipped, and says so,
when the checkout has no table), parses it with the clusters and minimum count of the committed `clustering.json`, and
checks the table hash and word count against the record.

#### Determinism

**Tests**: `ClusteringSubsystem_Cluster_DifferentThreadCounts_IdenticalFiles`,
`ClusteringSubsystem_Cluster_FixtureCorpus_GoldenTable`

Writes with one and with three threads to two output folders give byte-identical tables and records; a write of the
fixture corpus with four clusters gives exactly the golden table text.

#### Record and check

**Tests**: `ClusteringSubsystem_Cluster_EditedTable_CheckIsStale`,
`ClusteringSubsystem_Cluster_FromSnapshot_ReproducesTable`,
`ClusteringSubsystem_Cluster_WrittenFiles_PassOfflineDataCheck`

After a path in the table is edited, `--check` exits 1 and reports the table stale. With the snapshot copied out and
the cache deleted, `--check --from-snapshot` exits 0, makes no request and leaves the cache folder absent. After a
write, the manifest of the fixture data is regenerated and `check-data` (with `TrainingCorpus.TaggerVersion`) exits 0
and reports the word-cluster table; after a title in the source list is changed and the manifest regenerated again, it
exits 1 with a `clusters` finding on `sourcesSha256`.

See also *SpeechTextEn System Verification* and the unit documents `gutenberg-body.md`, `cluster-corpus-cache.md`,
`corpus-snapshot.md`, `corpus-leakage-audit.md`, `cluster-corpus-reader.md`, `cluster-inducer.md`,
`word-cluster-table.md`, `clustering-record.md` and `cluster-command.md`.
