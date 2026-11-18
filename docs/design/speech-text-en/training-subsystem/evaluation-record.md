### EvaluationRecord

**Purpose**: Render an evaluation as a small set of deterministic tab-separated files, and compare such files with a
folder, so that a committed evaluation record can be regenerated and verified byte for byte. The record is the durable,
reviewable evidence of what the model measured and whether the quality gate passed.

**Data Model**: `EvaluationReport` (an internal sealed record) holds the SHA-256 of the uncompressed image of the
evaluated model, the `EvaluationSettings` (with the gate threshold), the evaluated `SetEvaluation`s in reporting order
and the `SeedResult`s of the multi-seed study (empty when it was not run). `EvaluationRecord` is an internal static
class, stateless and thread-safe, with the six file names as constants.

**The files.** All six have a header row, a fixed column order, LF line ends, no byte order mark and a final newline.
Every number is an integer or an integer ratio in basis points (hundredths of a percent) rounded half away from zero;
no floating-point value is ever formatted into a file, and there is no time stamp, host name or duration, so the files
are identical on every platform and rerun.

- `summary.tsv`: one row per set: the rows, the aligned, single-candidate and covered rows, the rows decided correctly
  and the accuracy, each baseline's correct count and accuracy, the difference, the interval bounds and whether the
  lower bound is positive, the cluster count and the parity counts
- `gate.tsv`: key and value rows: the image hash, the gate threshold, the confidence level, the resamples and the
  bootstrap seed, and, when the natural holdout was evaluated, the two conditions and the verdict (`pass` or `fail`;
  `not evaluated` otherwise)
- `by-source.tsv`: per set and source, the rows, the rows correct, the accuracy and the part-of-speech baseline's
  correct count
- `per-reading.tsv`: the same by labeled reading, worst first
- `confidence.tsv`: the same by confidence bucket
- `seeds.tsv`: one row per seed of the study (the natural and the independent rows, the rows correct and the accuracy),
  then the mean (rounded half up with integers), the minimum and the maximum of the accuracies

**Why tab-separated text and not Markdown.** The repository's formatters reflow and normalize Markdown tables, which
would change the bytes of a committed record and break the byte comparison. Plain tab-separated files are left alone by
them, so a record can be compared exactly.

**Key Methods**:

- **Render(report)**: the content of the six files by name, in ordinal name order
- **Write(report, directory)**: creates the folder when needed and writes the six files as UTF-8 without a byte order
  mark
- **Compare(report, directory)**: renders and compares each file with the one in the folder byte for byte, and returns
  the names of the files that are missing or differ, in ordinal order; an empty list means the record is current
- **FormatPercent(basisPoints, signed)**: formats basis points for the console as a percentage with two decimals and an
  optional plus sign, with the invariant culture (a console format, not used in the files)

**Error Handling**: a null report or directory throws `ArgumentNullException`. File-system errors (an unwritable folder,
for instance) propagate as `IOException` or `UnauthorizedAccessException` and are reported by the command as an I/O
error. A missing or different file is a result of `Compare`, not an error.

**Dependencies**: `HeteronymEvaluator` (the result types, `RoundBasisPoints` and `DecideGate`), `EvaluationSettings`
(from `TrainingRecord`) and the framework.

**Callers**: `EvaluateCommand` and the unit tests.
