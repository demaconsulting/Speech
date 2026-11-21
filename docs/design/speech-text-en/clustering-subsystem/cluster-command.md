### ClusterCommand

**Purpose**: The command line entry of `cluster`: parse the options, resolve the folders, run one mode and report the
result with an exit code. `Program` dispatches the `cluster` command name to it.

**Data Model**:

- `ClusterOptions`: the mode (`Fetch`, `Audit`), whether to write pins or removals, the threads, the delay, the mirror,
  the cache and data folders, and whether help was requested
- `ClusterEnvironment`: the fetcher and sleeper seams (tests replace them; null means the real network and pause)
- `DefaultDelayMs` 2,000 and `MinimumDelayMs` 1,000

**Options.** Exactly one of `--fetch` and `--audit`. `--write-pins` only with `--fetch`; `--write` only with `--audit`
(writing the removals); `--threads <n>` (at least 1, default the processor count); `--delay-ms <n>` (at least 1,000);
`--mirror <host>` (an allowed mirror); `--cache <dir>` (default `<data>/external/cluster-corpus`; it must be inside
`<data>/external` or outside the repository, compared on real paths); `--data <dir>`; `--help`. Values may also be given
as `--name=value`.

**Key Methods**:

- **Run(args, output, error, currentDirectory, baseDirectory, environment)**: parses, resolves the data folder (the
  option, or `data/text-en` searched upward from the current and then the tool's folder) and runs the mode:
  - **fetch**: `ClusterCorpusCache.Fetch`; prints a line per book and a summary with the number of requests and the
    time; with `--write-pins` writes the new pins into `cluster-sources.tsv`; exit 1 when a book failed;
  - **audit**: reads the pin-verified bodies, tokenizes them with `ClusterCorpusReader`, runs the audit and prints the
    counts per evaluation file and rule; with `--write` writes `cluster-removals.tsv`, otherwise exit 1 when it is
    missing or differs from the removals the audit computes
  Every stage prints its time, so a run can be benchmarked
- **TryParse(args, out error)**: the option rules above

**Error Handling**: a usage error, a missing data folder, a refused cache folder, and any I/O, data or consistency
exception of the units (`IOException`, `UnauthorizedAccessException`, `InvalidDataException`, `ArgumentException`,
`NotSupportedException`, `InvalidOperationException`) print `cluster: <message>` on the error stream and exit 2.

**Dependencies**: every other unit of the subsystem, the TrainingDataSubsystem (`CheckDataCommand.FindDataRoot`,
`LeakageChecker` real-path helpers) and the .NET base class library.

**Callers**: `Program` (the trainer's entry point) and the unit and subsystem tests.
