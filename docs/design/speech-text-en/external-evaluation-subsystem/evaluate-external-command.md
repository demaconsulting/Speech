### EvaluateExternalCommand

**Purpose**: The command line entry of `evaluate-external`: parse the options, guard the cache location, make the
external sources present and verified, convert and evaluate the requested sets, run the seed study, print the results
and write or verify the external evaluation record. It orchestrates the other units of the subsystem and owns the exit
codes.

**Data Model**: `EvaluateExternalCommand` is an internal static class with the command name `evaluate-external`, the
names of the sets in report order (`whd-eval`, `whd-train` and `bench`; `whd-eval` is the headline set), the usage text
and `Run`. `EvaluateExternalOptions` (an internal sealed record) holds the parsed options. The command keeps no state
between runs.

**Options and exit codes.** `--fetch` allows the download of a source that is missing or fails verification;
`--set <name>` selects `whd-eval`, `whd-train`, `bench` or `all` (repeatable; the default is all); `--cache <dir>` is
the cache folder; `--pins <file>` is the pins file (default: `external-sources.json` in the trainer folder);
`--data <dir>`, `--model-dir <dir>` and `--model <file>` are as in `evaluate`; `--seeds` runs the seed study; `--write`
writes the record and `--check` verifies it (the two cannot be combined, and either one implies all sets and the seed
study); `--record-dir <dir>` is the record folder (default: `docs/evaluation/text-en/external`); `--help` prints the
usage and exits 0. An option may be written `--name value` or `--name=value`. The exit code is 0 when the command did
its work and, with `--check`, the record is current; 1 when `--check` finds the record stale; and 2 for a usage, I/O,
pin, hash or data error, with a message on the standard error that starts with `evaluate-external: error:` and no figure
on the standard output.

**The flow.** `Run` parses the options (a bad option, a missing value, an unknown set or the combination of `--write`
and `--check` is exit 2), prints the usage for `--help`, finds the data folder (the option, or searched upward from the
current directory and then from the tool's folder) and calls the evaluation inside a handler that turns every I/O,
format, pin, hash and argument error into exit 2. The evaluation:

1. resolves the repository (two folders above the data folder), the default cache (`<data>/external`), the cache, the
   pins file, the model file (with the resolution that `evaluate` uses), the training record (for the settings, with the
   defaults when it is absent) and the record folder; a cache folder that is inside the repository but is not the
   default cache is refused, and a model file that does not exist is an error
2. loads the pins and ensures each needed source is cached and verified through `ExternalSourceCache`, where the needed
   sources are `whd` for either WikipediaHomographData set and `bench` for the benchmark, so a run with one set neither
   downloads nor verifies the other source
3. loads the training corpus and converts each requested set: the mapping file (`whd-mapping.tsv` or
   `bench-mapping.tsv`)
   is loaded with `ReadingIdMap`, the adapter reads the rows from the verified cache and `ExternalRowConverter.Convert`
   maps and filters them; WikipediaHomographData sets cluster by sentence and the benchmark's items are clusters of
   their own
4. **only now** prints the model, the cache and each source with its pin, evaluates each set through
   `HeteronymEvaluator.EvaluateRows` and prints it, runs the seed study when asked (or when writing or checking) and
   prints it, and writes or compares the record

**The printed result of a set**: the rows read and used and the skips by reason, the counters of the conversion
(including the sentences equal to a training or development sentence, which are kept), the aligned, single-candidate and
covered rows, the accuracy with its interval and cluster count, the own-part-of-speech, majority and first-candidate
baselines, the improvement over the own-part-of-speech baseline with its interval, the parity counts, the tallies by
stratum, the lowest-accuracy readings that have at least five rows (at most fifteen) and the tallies by confidence
bucket. Only counts, set and stratum names, our reading identifiers and the pins are printed.

**The seed study.** For each evaluation seed the command retrains a model with `ModelSelection.Train` from the training
corpus with that seed, evaluates every set with the direct path and prints the model hash, each set's accuracy,
improvement and interval, and then the mean, minimum and maximum of each set's accuracy. The corpus holds only training
and development rows, so the retrained models cannot depend on the external content.

**Key Methods**: **Run(args, output, error, currentDirectory, baseDirectory, fetcher)** (the entry; the fetcher is the
test seam for the network) and **TryParse(args, out error)** (the options).

**Error Handling**: every `IOException`, `UnauthorizedAccessException`, `InvalidDataException`, `ArgumentException` and
`NotSupportedException` of the flow is reported as one error line and exit 2; a stale record is exit 1, not an error.
The messages come from the units and never quote the content of a dataset. Nothing is written to the cache, the record
folder or the standard output before the inputs are settled, except the record that was asked for after the evaluation.

**Dependencies**: every other unit of the subsystem; from the TrainingDataSubsystem `CheckDataCommand` (the data folder
discovery) and `LeakageChecker` (the path test); from the TrainingSubsystem `TrainingCorpus`, `HeteronymEvaluator`,
`ModelSelection`, `TrainingRecord`, `EvaluationRecord` and `EvaluateCommand` (the model file resolution); and the
library's model writer for the hash of the uncompressed image.

**Callers**: `Program.cs` (the dispatch of the `evaluate-external` command) and the unit tests.
