### ExternalEvaluationRecord

**Purpose**: Render the external evaluation as a small set of deterministic tab-separated files, and compare such files
with a folder, so that a committed record of the external figures can be regenerated and verified byte for byte. It
follows the conventions of the evaluation record of the committed sets and reuses its group tables.

**Data Model** (internal types):

- `ExternalSetResult`: the end-to-end `SetEvaluation` of one set, with its accuracy interval, and the
  `ConversionCounts` of its conversion
- `ExternalSeedResult`: a training seed and the direct-path `SetEvaluation` of each set for the model retrained with it
- `ExternalEvaluationReport`: the SHA-256 of the uncompressed image of the evaluated model, the `EvaluationSettings`,
  the `ExternalSourcePins`, the evaluated sets in reporting order and the seed results (empty when no study was run)
- `ExternalEvaluationRecord`: an internal static class, stateless and thread-safe, with the seven file names as
  constants

**The files.** All seven have a header row, a fixed column order, LF line ends, no byte order mark and a final newline.
Every number is an integer or an integer ratio in basis points rounded half away from zero; no floating-point value is
formatted into a file, and there is no time stamp, host name or duration, so the files are identical on every platform
and rerun.

- `sources.tsv`: key and value rows: the image hash, the resamples, the confidence level, the bootstrap seed and the
  seeds, then for each source in id order its repository, commit, file count, total size, listing hash and license
  statement
- `conversion.tsv`: one row per set: the rows read and used, the rows skipped under each reason in the fixed order, the
  rows with a non-ASCII sentence, the offsets converted and repaired, the used rows that equal a training or development
  sentence, and the aligned, single-candidate and covered rows of the evaluation
- `summary.tsv`: one row per set: the rows, the rows correct, the accuracy and the bounds of its interval, each of the
  three baselines' correct count and accuracy, the improvement over the own-part-of-speech baseline with its interval
  and whether its lower bound is positive, the cluster count and the parity counts
- `by-source.tsv`, `per-reading.tsv` and `confidence.tsv`: the tallies by stratum, by labeled reading (worst first, as
  our own reading identifiers) and by confidence bucket, rendered by the group renderer of `EvaluationRecord`
- `seeds.tsv`: for each set one row per seed of the study (rows, rows correct, accuracy, the improvement and the bounds
  of its interval), then the mean (the rounding of `EvaluationRecord`), minimum and maximum of the accuracy, the
  improvement and both bounds

**No dataset text.** The files hold only numbers, the set and stratum names, our reading identifiers, the pins and the
image hash. The code writes no sentence, target word, item identifier or dataset label.

**Key Methods**:

- **Render(report)**: the content of the seven files by name, in ordinal name order
- **Write(report, directory)**: creates the folder when needed and writes the files as UTF-8 without a byte order mark
- **Compare(report, directory)**: renders and compares each file with the one in the folder byte for byte, and returns
  the names of the files that are missing or differ, in ordinal order; an empty list means the record is current

**Error Handling**: a null report or directory throws `ArgumentNullException`. File-system errors (an unwritable folder,
for instance) propagate as `IOException` or `UnauthorizedAccessException` and are reported by the command as an I/O
error. A missing or different file is a result of `Compare`, not an error.

**Dependencies**: `ExternalSourceCache` (the pins), `ExternalRowConversion` (`ConversionCounts` and `SkipReasons`) and,
from the TrainingSubsystem, `HeteronymEvaluator` (the result types and `RoundBasisPoints`), `EvaluationSettings` and
`EvaluationRecord` (the group renderers, the row writer and the mean).

**Callers**: `EvaluateExternalCommand` and the unit tests.
