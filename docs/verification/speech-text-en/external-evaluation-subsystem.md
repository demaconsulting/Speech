## ExternalEvaluationSubsystem Verification

### Verification Approach

The SpeechTextEn ExternalEvaluationSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests` that need no network, no hardware and no external dataset. The
subsystem tests in `ExternalEvaluationSubsystemTests.cs` run the real `evaluate-external` command with its real units
over a small trainable data folder (`Fakes/TrainingFixture.cs`) and fake external sources that
`Fakes/ExternalSourceFixture.cs` writes in code: hand-written files in the format of WikipediaHomographData and of the
heteronym-bench read set, about the words of the training fixture, packed into in-memory archives with the pins and
mapping files that describe them. The rig (`Fakes/ExternalEvaluationRig.cs`) trains the fixture model and runs the
command as a function with string writers for the output and error streams. Only the network is replaced: the command
takes the archive fetcher as a delegate, and the tests hand it one that serves the fake archives, counts its calls and
can fail. The tests observe exit codes, printed lines, the files of the cache and of the record, and the number of
fetches. **No test reads a real dataset, uses the network or contains a sentence of a real dataset.** Each unit is
verified in isolation by its own test file (see the unit verification documents).

Two tests check **committed files** and nothing of the real datasets: the committed benchmark mapping resolves, label by
label, to readings of the committed inventory, and the committed external record has exactly the expected files, each
with its header and rows that follow a strict grammar of numbers, set names, our reading identifiers and pins. The
figures of the committed record come from a local run against the real datasets and are claimed only as that record
states them; a maintainer who has the datasets verifies the record with `evaluate-external --check`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: every test builds its own temporary folders (data, model, cache and record) and deletes them
  afterwards; the fetcher is a test double
- **Not used**: the network and any real dataset text. The committed-file tests read the repository files above the test
  binaries and are skipped only when the repository root cannot be found

### Acceptance Criteria

**Fetch and verify.** A run with the fetch option over an empty cache downloads both fake sources, leaves exactly the
pinned files in the cache, evaluates the three sets and writes a record whose sources file lists the pins. A cached file
that was edited makes the command exit 2 with a message that names the file, with no figure printed and no record
written. The default and outside-repository cache folders are the only ones accepted (verified at unit level).

**Label mapping.** A set with an unmapped label and a word that is not in the inventory exits 0 with the skips counted
by reason and the remaining rows evaluated; a sentence with non-ASCII characters before the target is evaluated at the
target; the committed benchmark mapping resolves to readings of the committed inventory.

**Seed variance and output.** The seed study prints each seed with its interval and the record holds the per-seed rows
with a consistent mean, minimum and maximum; neither the printed output nor any record file contains a sentence or a
label of the fixtures.

**Command contract.** Failures of every kind exit 2 with no figure, a good cache exits 0 and a record that was never
written is stale (exit 1); the committed record files are well formed.

### Test Scenarios

#### Fetch, convert, evaluate and record

**Tests**: `ExternalEvaluationSubsystem_Evaluate_FetchConvertEvaluateAndRecord_EndToEnd`,
`ExternalEvaluationSubsystem_Evaluate_TamperedCache_IsRefusedWithNoFigures`

With an empty cache, `--fetch --write` downloads the two fake sources (two fetches), exits 0 with an empty error stream
and writes the seven record files; the cache holds exactly the pinned files, the sources file lists the commit and
listing hash of each pin, and the conversion file has the counts of each set (for the benchmark, two rows skipped as
duplicates of the other dataset). With one byte of a cached file changed and no `--fetch`, the command exits 2, names
the source and the file, prints no accuracy, baseline or interval line and writes no record.

#### Label mapping and skipped rows

**Tests**: `ExternalEvaluationSubsystem_Evaluate_SkippedRows_AreReportedNotFatal`,
`ExternalEvaluationSubsystem_Evaluate_NonAsciiSentence_IsEvaluatedAtTheTarget`,
`ExternalEvaluationSubsystem_CommittedBenchMapping_ResolvesToInventoryReadings`

Fake rows with an unmapped label and a word that is not a form of the inventory are read together with usable rows: the
command exits 0, reports each skip under its reason, evaluates the usable rows and the record counts the skips.
Sentences with non-ASCII characters before the target, whose offsets are in bytes, are converted and evaluated with no
row lost. The committed benchmark mapping has one row for each reading key of each word, every identifier in it is a
reading and a candidate of its word, and the two keys of a word map to different readings.

#### Seed study and no dataset text

**Tests**: `ExternalEvaluationSubsystem_Seeds_ReportSpreadAndIntervals`,
`ExternalEvaluationSubsystem_Output_HoldsNoDatasetText`

With `--seeds` the command retrains with each evaluation seed and prints, for every set and seed, the accuracy and the
interval of the improvement; the seeds table of the record has the per-seed rows and a mean, minimum and maximum that
are
consistent with them. The sentences and labels of the fake sources occur nowhere in the training fixture or the
command's fixed text, and after a `--write` run none of them occurs in the console output or in any record file.

#### Command contract

**Tests**: `ExternalEvaluationSubsystem_Failures_ExitTwoWithNoFigures`,
`ExternalEvaluationSubsystem_CommittedRecord_HasExpectedFilesAndNumericGrammar`

A missing cache that may not be fetched, a tampered cache, an unknown set and a mapping with an unknown reading
identifier each exit 2, name the problem on the error stream and print nothing on the output stream; a run with a good
cache exits 0 and the check of a record that was never written exits 1. The committed external record folder has the
seven record tables, and every row matches the grammar of its columns and agrees with the committed pins; the test
verifies the shape of the committed files, not the correctness of the figures.

See also *SpeechTextEn System Verification* and the unit documents `external-source-cache.md`,
`external-row-conversion.md`, `wikipedia-homograph-adapter.md`, `heteronym-bench-adapter.md`,
`external-evaluation-record.md` and `evaluate-external-command.md`.
