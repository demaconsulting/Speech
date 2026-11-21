## CorpusSubsystem Verification

### Verification Approach

The SpeechTextEn CorpusSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests` that need no network and no real book. The subsystem tests in
`CorpusSubsystemTests.cs` run the real `corpus` command with its real units over a fixture
(`Fakes/CorpusFixture.cs`) that builds, in a temporary folder, a valid data folder at `data/text-en` (with
`Fakes/DatasetFixture.cs`), three small books generated in code from fixed word lists and wrapped in a downloaded
e-book's header and footer, and a source list without pins. Only the network and the pause are replaced: the command
takes the fetcher and the sleeper as delegates, and the fixture's fetcher serves the books from memory, counts the
requests and answers an unknown address with HTTP 404, while its sleeper records the pauses. The tests observe exit
codes, printed lines and the written source list and removal list. **No test uses the network or a real book.** Each
unit is verified in isolation by its own test file (see the unit verification documents).

The fetch and audit of the real corpus are manual steps that are recorded in the trainer's run log; they are not part
of the automated tests.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: every test builds its own temporary repository layout and deletes it afterwards; the fetcher and the
  sleeper are test doubles
- **Not used**: the network and any real book

### Acceptance Criteria

**Pinned corpus.** Fetching with pins writing and auditing the fixture corpus succeeds end to end, every used book is
pinned, and a re-audit from the cache reports the removals current without a request; a cached body edited after
pinning makes the audit exit 2, naming the book, with the removal list unchanged.

**Leakage audit.** A book that contains a development sentence of the fixture data loses exactly that sentence (rule
exact, file `dev.tsv`).

**Determinism.** Audits with one and with three threads write byte-identical removal lists.

**Command line and data check.** The written pins and removals pass the offline corpus check of `check-data`, and a
used book whose pin is blanked afterwards fails it, naming the book.

### Test Scenarios

#### Fetch and audit

**Tests**: `CorpusSubsystem_Corpus_FetchAudit_EndToEnd`, `CorpusSubsystem_Corpus_TamperedCache_IsRefused`

After `--fetch --write-pins` and `--audit --write`, `--audit --threads 1` exits 0 and prints the removals current and
the three tokenized books; the source list pins every used book and the mirror saw exactly the six requests of the
fetch. With one cached body changed, `--audit --write` exits 2 naming the book and leaves the removal list as it was.

#### Leakage audit

**Tests**: `CorpusSubsystem_Corpus_BookQuotingEvaluation_SentenceRemoved`

A fourth book holds the fixture's development sentence "The wind howled all night." between two corpus sentences; the
removal list has exactly that sentence (paragraph 0, sentence 1, rule exact, `dev.tsv`).

#### Determinism

**Tests**: `CorpusSubsystem_Corpus_DifferentThreadCounts_IdenticalRemovals`

With the fourth book of the previous scenario, `--audit --write` with one and with three threads writes byte-identical
removal lists of one row.

#### Offline data check

**Tests**: `CorpusSubsystem_Corpus_WrittenFiles_PassOfflineDataCheck`

After the fetch and the audit, the manifest of the fixture data is regenerated and `check-data` exits 0 and reports the
three used books; after the pin of a used book is blanked and the manifest regenerated again, it exits 1 with a
`corpus` finding on that row of `cluster-sources.tsv`.

See also *SpeechTextEn System Verification* and the unit documents `gutenberg-body.md`, `corpus-cache.md`,
`corpus-reader.md`, `corpus-leakage-audit.md` and `corpus-command.md`.
