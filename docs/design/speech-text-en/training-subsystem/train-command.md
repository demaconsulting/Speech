### TrainCommand

**Purpose**: Be the command line entry of the `train` command: train the heteronym model from the training data and
write the model file and the training record, or, with `--check`, verify that the committed model and record are what
retraining produces. The command is a pure function of its arguments, its two writers and the directories, so the tests
run it in memory.

**Data Model**: `TrainCommand` is an internal static class. `Name` is `train`, `ModelFileName` is `heteronyms.bin` and
`RecordFileName` is `training.json`. `TrainOptions` is the parsed command line: the data folder (or null to discover
it), the model directory (or null for the committed locations), whether to verify, the experiment overrides (a
dictionary from option name to a non-negative integer), the row visiting order override (or null for the default) and
whether usage was requested. The command keeps no state
between runs.

**Command line.** `train [options]` and `train --check [options]`, with the options `--data <dir>`, `--model-dir <dir>`,
`--check`, the experiment options `--seed`, `--hash-bits`, `--prune`, `--natural-weight` and `--max-epochs` (each takes
a non-negative integer, as `--option value` or `--option=value`), the experiment option `--row-order positional|keyed`
(the visiting order of the training rows; see *AveragedPerceptronTrainer*) and `--help`. The exit codes are 0 (trained,
or the committed model is current), 1 (the committed model or record is stale or missing) and 2 (a usage, input or I/O
error, including a data or setting error). Usage errors go to the error writer as `train: error: <message>`; progress
and results go to the output writer as lines that start with `train:`.

**Committed locations.** The data folder is the one given or the first `data/text-en` found by searching upward from the
current directory and then from the tool's own folder (the discovery of `CheckDataCommand`). The committed locations
are derived from the data folder's repository, two levels above it: the model at
`src/DemaConsulting.Speech.Text.En/Resources/heteronyms.bin` and the record at
`tools/DemaConsulting.Speech.Text.En.Trainer/training.json`. With `--model-dir` both files are read and written in that
folder instead.

**Experiment guard.** An experiment option changes the settings of the default configuration, so it is accepted only
together with `--model-dir` and never with `--check`; otherwise the command line is a usage error. An experiment can
therefore never overwrite the committed files, and the committed configuration is always the default one.

**Key Methods**:

- **Run(args, output, error, currentDirectory, baseDirectory)**: parses the options, answers `--help`, finds the data
  folder, resolves the two locations and runs the training or the check; the exceptions listed below become exit code 2
- **TryParse(args, out parseError)** (internal): the option parser; an unknown option, a missing value, a value that is
  not a non-negative integer, a row order other than `positional` or `keyed`, or an experiment option without
  `--model-dir` or with `--check` gives no options and a message
- **Train and write** (private): validates the settings (the default ones with the overrides applied), reads the
  evaluation settings of an existing record so that retraining never moves the gate, loads the corpus
  (`TrainingCorpus.Load`), trains (`ModelSelection.Train`), writes the compressed model and the record
  (`TrainingRecord.ToJson`, UTF-8 without a byte order mark), creating the folders, and prints the number of rows, the
  seed and row visiting order, the development accuracy of each epoch, the epoch and temperature kept, the number of
  weights, the sizes and the image hash. The time taken is printed for information only and is never written to a file.
  It returns 0
- **Check** (private): the verification. A missing or unparsable record is stale (the record is parsed with
  `TrainingRecord.Parse`). Otherwise it retrains with the settings in the record and compares, naming every difference:
  the data hash, the tagger version, the feature-set, inventory and trainer versions, the feature stream hash and the
  recorded row counts (the inputs); the epochs and the temperature step and the image hash (the result); and the
  committed model file, which is turned into an uncompressed image (`SparsePerceptronModelWriter.ToUncompressedImage`)
  and compared byte for byte with the retrained uncompressed image. The compressed bytes are never compared, because
  they depend on the runtime's Deflate implementation. A missing model file or one that is not a valid model is stale.
  It prints `train: --check: OK (...)` and returns 0, or one `STALE` line per difference and a `FAILED` line and returns
  1

**Error Handling**: `IOException`, `UnauthorizedAccessException`, `InvalidDataException` (data, settings and record
errors), `ArgumentException` and `NotSupportedException` are caught by `Run` and reported as a usage error with exit
code 2; `train --check` reports a damaged record or model as stale (exit code 1) rather than as an error, because that
is the condition the check exists to find. Nothing else is caught, so a defect surfaces as a crash and not as a wrong
model.

**Dependencies**: `ModelSelection`, `TrainingCorpus`, `TrainingRecord` and its settings, `CheckDataCommand` (the data
folder discovery, from the TrainingDataSubsystem) and `SparsePerceptronModelWriter` (the uncompressed image of the
committed file).

**Callers**: `Program.cs` (the dispatch of the `train` command), `EvaluateCommand` (the file-name constants) and the
unit tests.
