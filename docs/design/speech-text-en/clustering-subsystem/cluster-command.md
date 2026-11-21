### ClusterCommand

**Purpose**: The command line entry of `cluster`: parse the options, resolve the folders, run one mode and report the
result with an exit code. `Program` dispatches the `cluster` command name to it.

**Data Model**:

- `ClusterOptions`: the mode (`Fetch`, `Audit`, `Preflight`, `Write`, `Check`), whether to write pins or removals, the
  preflight words, the clusters, vocabulary limit and minimum count when given, the threads, the delay, the output,
  cache, snapshot and data folders, and whether help was requested
- `ClusterEnvironment`: the fetcher and sleeper seams (tests replace them; null means the real network and pause)
- `DefaultClusters` 512, `DefaultMinCount` 5, `DefaultDelayMs` 2,000, `MinimumDelayMs` 1,000; `PreflightSizes`
  30,000, 40,000, 50,000, 60,000 and 75,000; `NominatedWords`, the pre-registered cue-word list of the run log

**Options.** Exactly one of `--fetch`, `--audit`, `--preflight`, `--write` and `--check` (with `--audit`, `--write`
means writing the removals). `--write-pins` only with `--fetch`; `--words a,b,...` only with `--preflight`;
`--clusters <n>` (at least 2) only with `--write`; `--max-words <n>` (at least 0) and `--min-count <n>` (at least 1)
only with `--write` and `--preflight` (`--check` takes all three from the record); `--threads <n>` (at least 1,
default the processor count); `--delay-ms <n>` (at least 1,000); `--mirror <host>` (an allowed mirror); `--out <dir>`
(default the data folder); `--cache <dir>` (default `<data>/external/cluster-corpus`; it must be inside
`<data>/external` or outside the repository, compared on real paths); `--from-snapshot <file>`; `--data <dir>`;
`--help`. Values may also be given as `--name=value`.

**Key Methods**:

- **Run(args, output, error, currentDirectory, baseDirectory, environment)**: parses, resolves the data folder (the
  option, or `data/text-en` searched upward from the current and then the tool's folder) and runs the mode:
  - **fetch**: `ClusterCorpusCache.Fetch`; prints a line per book and a summary with the number of requests and the
    time; with `--write-pins` writes the new pins into `cluster-sources.tsv`; exit 1 when a book failed;
  - **audit**: tokenizes, runs the audit, prints the counts per evaluation file and rule; with `--write` writes
    `cluster-removals.tsv`, otherwise exit 1 when it is missing or differs;
  - **preflight**: tokenizes and counts with the committed removals (if any) and the given minimum count, prints the
    token coverage of each candidate size and, per nominated word, its count, rank and cut status or "too rare";
  - **write** and **check**: tokenize, audit (exit 1 when the committed removals are missing or differ), count, induce
    with progress lines that estimate the time left from the rate so far, build the table, the snapshot (in the cache)
    and the record (keeping the recorded snapshot URL when the snapshot hash is unchanged); print the table and snapshot
    hashes and the total time with the thread count; **write** writes the table and the record to the output folder;
    **check** reads the settings from the record in the output folder and reports the table, the record and the snapshot
    as current or stale
  Every stage prints its time, so a run can be benchmarked
- **TryParse(args, out error)**: the option rules above

**Error Handling**: a usage error, a missing data folder or record, a refused cache folder, and any I/O, data or
consistency exception of the units (`IOException`, `UnauthorizedAccessException`, `InvalidDataException`,
`ArgumentException`, `NotSupportedException`, `InvalidOperationException`) print `cluster: <message>` on the error
stream and exit 2.

**Dependencies**: every other unit of the subsystem, the TrainingDataSubsystem (`CheckDataCommand.FindDataRoot`,
`LeakageChecker` real-path helpers), the TrainingSubsystem (`TrainingCorpus.TaggerVersion`) and the .NET base class
library.

**Callers**: `Program` (the trainer's entry point) and the unit and subsystem tests.
