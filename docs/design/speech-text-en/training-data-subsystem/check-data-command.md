### CheckDataCommand

**Purpose**: Be the command line entry of the trainer's `check-data` command: parse the options, find the data folder,
run every check in a fixed order and turn the outcome into an exit code. The `Program.cs` entry point is glue that
dispatches the first argument to this unit and prints the usage of the trainer for no command, for `--help` or for an
unknown command (the last two as a usage error).

**Data Model**: `CheckDataCommand` is a static class that keeps no state; everything it uses is passed in, so tests run
it with in-memory writers: the arguments, the output and error writers, the current directory, the tool's own directory
and an environment lookup. `CheckDataOptions` is a read-only record of the parsed options: `--data <folder>`,
`--eval-sets <folder>`, `--leakage-report <file>`, `--generalization <file>`, `--max-findings <n>` (a positive integer,
25 by default), `--counts` and `--help`; an option value may follow after a space or an equals sign. The data folder is,
in order, the `--data` option, else the first folder `data/text-en` found by walking up from the current directory, else
the one found by walking up from the tool's directory. The evaluation folder is the `--eval-sets` option, else the value
of the environment variable `TEXTEN_EVAL_SETS`, else none.

**Key Methods**:

- **Run(args, output, error, currentDirectory, baseDirectory, getEnvironmentVariable)**: parses the options first, so a
  bad command line is a usage error before any file is touched; prints the usage for `--help`; resolves the data folder
  (a folder that cannot be found is a usage error); and rejects `--leakage-report` without evaluation sets or with a
  path inside the data folder, and a `--generalization` file that does not exist. It then creates the `CheckReport` and
  runs, in this order, `DataManifest.Verify`, `TextEnDataSet.Load`, `SchemaChecker.Run`, `LicenseChecker.Run`,
  `LeakageChecker.CheckInternal` and the overlay checks, and then the external leakage check; every check runs even
  after findings of an earlier one. The overlay checks run `EvaluationOverlayChecker.CheckRegression` and then
  `CheckGeneralization` on the committed set with matches revealed, or, with `--generalization`, on the staged file
  (loaded by `TextEnDataSet.LoadGeneralization` under the name `staged:<file name>`) with generic reasons and an
  information line naming the staged file; the committed set is then not overlay-checked. Without an evaluation folder
  the external check is replaced by the information line `external leakage check skipped (no evaluation sets supplied)`;
  an evaluation folder inside the data folder is a finding and is not read; otherwise
  `LeakageChecker.LoadEvaluationSets`, `CheckExternal` and, when asked, `WriteReport` run. It then writes the report and
  the summary (and, for `--counts`, the per-reading table) and returns the exit code of the report.
- **TryParse(args, out error)**: the option parser; **FindDataRoot(startDirectory)**: the walk upward for
  `data/text-en`.

**Error Handling**: a usage error writes `check-data: error: <message>` and a hint to the error writer and returns 2; so
does an input or output error. Only `IOException`, `UnauthorizedAccessException`, `InvalidDataException` (which
includes the evaluation folder and evaluation file errors of `LeakageChecker`), `ArgumentException` and
`NotSupportedException` (a malformed `--data`, `--eval-sets` or `--leakage-report` path, for example one with a null
character) are caught, with the message but no stack trace; the paths are resolved inside the guarded block. No
catch-all handler is used, so a programming error still fails loudly.

**Dependencies**: `DataManifest`, `TextEnDataSet`, `SchemaChecker`, `LicenseChecker`, `LeakageChecker`,
`EvaluationOverlayChecker` and `CheckReport`; the .NET base class library only.

**Callers**: `Program.Run` of the trainer, `TrainCommand`, `EvaluateCommand` and `EvaluateExternalCommand` (through
`FindDataRoot`, the data folder discovery they share) and the unit tests.
