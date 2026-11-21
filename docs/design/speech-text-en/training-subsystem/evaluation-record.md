### EvaluationRecord

**Purpose**: Render an evaluation as a small set of deterministic tab-separated files, and compare such files with a
folder, so that a committed evaluation record can be regenerated and verified byte for byte. The record is the durable,
reviewable evidence of what the model measured and whether the quality gate passed.

**Data Model**: `EvaluationReport` (an internal sealed record) holds the SHA-256 of the uncompressed image of the
evaluated model, the `EvaluationSettings` (with the gate thresholds), the evaluated `SetEvaluation`s in reporting order
and the `SeedResult`s of the multi-seed study (empty when it was not run), and the `OverlayEvaluation`s of the
evaluation overlays (none when no overlay was evaluated; their shipped evaluations are also among the sets, after the
data sets). `GateRowState` is one gate row (set, id, labeled, previous and current reading) and `CommittedGate` the gate
state read from a committed record folder (its image, its previous image and its gate rows). `EvaluationRecord` is an
internal static class, stateless and thread-safe, with the ten file names as constants.

**The files.** All ten have a header row, a fixed column order, LF line ends, no byte order mark and a final newline.
Every number is an integer or an integer ratio in basis points (hundredths of a percent) rounded half away from zero;
no floating-point value is ever formatted into a file, and there is no time stamp, host name or duration, so the files
are identical on every platform and rerun.

- `summary.tsv`: one row per set: the rows, the aligned, single-candidate and covered rows, the rows decided correctly
  and the accuracy, each baseline's correct count and accuracy, the difference, the interval bounds and whether the
  lower bound is positive, the cluster count and the parity counts
- `gate.tsv`: key and value rows: the image hash, the previous image hash, the confidence level, the resamples and the
  bootstrap seed; then, when every gate set of the thresholds was evaluated, per set (`<set>_` prefix) the rows,
  clusters, correct rows, accuracy, interval bounds, threshold, fixed and broken flips and `pass`; then the verdict
  (`pass` or `fail`; `not evaluated` when a gate set is missing)
- `gate-rows.tsv`: every row of the evaluated gate sets, ordered by set and then id (ordinal): the set, the id, the
  labeled reading, the previous reading (`-` for a row the previous record does not have) and the current reading
  (`none` for no reading); ids only, never a sentence
- `flips.tsv`: the gate rows whose previous and current readings differ, with the change (`fixed`, `broken` or
  `changed`, from `HeteronymEvaluator.ClassifyFlip`); the header only when nothing flipped
- `by-source.tsv`: per set and source, the rows, the rows correct, the accuracy and the part-of-speech baseline's
  correct count
- `per-reading.tsv`: the same by labeled reading, worst first
- `confidence.tsv`: the same by confidence bucket
- `seeds.tsv`: one row per seed of the study with the rows, the rows correct and the accuracy of every gate set, then
  the mean (rounded half up with integers), the minimum and the maximum of the accuracies and the threshold by the
  gate's rule (`HeteronymEvaluator.DeriveGateThreshold`)
- `overlay-rows.tsv`: one row per overlay row: the set, the id, the group (origin or family), the kind, the
  seen-in-training mark, the cue-unsolvable mark (`-` where it does not apply), the labeled, shipped and model-only
  readings (`none` for no reading) and whether a cue fired
- `curated-ablation.tsv`: one row per overlay slice: the rows and clusters, the shipped correct count, accuracy and
  interval, the same for the model alone, the paired cue effect with its interval, and the rows on which a cue fired and
  whose reading changed

**Appending (decision).** The overlays come after the data sets in every shared file, so adding them leaves every
earlier line of those files byte-identical; the two overlay files have their header only when no overlay was evaluated.

**Rotation of the previous column (decision).** `Write` and `Compare` render against the committed gate state of the
folder. On a first write (no committed `gate-rows.tsv`) the previous reading is the current one and the previous image
is the image, so nothing flips. When the committed record is of the same model image, its previous column and previous
image are kept and a new row gets `-`, so a check of an unchanged model gives the same files. When the image changed,
the committed current column becomes the previous one and the committed image the previous image, so the flip report
compares the new model with the one before it. Every gate row is listed, so the flip report is complete.

**Why tab-separated text and not Markdown.** The repository's formatters reflow Markdown tables, which would change the
bytes of a committed record and break the byte comparison; plain tab-separated files are left alone.

**Key Methods**:

- **Render(report)** and **Render(report, committed)**: the content of the ten files by name, in ordinal name order, as
  a first write or rotated from a committed gate state
- **ReadCommitted(directory)**: the committed gate state of a folder, or null when `gate.tsv` or `gate-rows.tsv` is
  missing or not of the expected form
- **Write(report, directory)**: renders against the folder's committed state, creates the folder when needed and writes
  the ten files as UTF-8 without a byte order mark
- **Compare(report, directory)**: renders against the folder's committed state and compares each file with the one in
  the folder byte for byte, and returns
  the names of the files that are missing or differ, in ordinal order; an empty list means the record is current
- **FormatPercent(basisPoints, signed)**: formats basis points for the console as a percentage with two decimals and an
  optional plus sign, with the invariant culture (a console format, not used in the files)

**Error Handling**: a null report or directory throws `ArgumentNullException`. File-system errors (an unwritable folder,
for instance) propagate as `IOException` or `UnauthorizedAccessException` and are reported by the command as an I/O
error. A missing or different file is a result of `Compare`, not an error.

**Dependencies**: `HeteronymEvaluator` (the result types, `RoundBasisPoints`, `DecideGate`, `DeriveGateThreshold` and
`ClassifyFlip`), `EvaluationOverlay`
(the row type), `EvaluationSettings`
(from `TrainingRecord`) and the framework.

**Callers**: `EvaluateCommand`, `ExternalEvaluationRecord` (which reuses the group renderers, the row writer and the
mean of this unit for its by-source, per-reading, confidence and seeds tables, so that the committed record and the
external record have one layout) and the unit tests.
