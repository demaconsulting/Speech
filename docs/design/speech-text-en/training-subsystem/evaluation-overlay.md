### EvaluationOverlay

**Purpose**: Load the two evaluation overlays of the data folder, the regression overlay (`regression.tsv`) and the
blind generalization set (`generalization.tsv`), as rows the evaluator can score, each with its stratum, its report
slices and its bootstrap cluster. The overlays are not splits: only the evaluation reads them, never training or model
selection.

**Data Model**: `OverlayRow` (an internal sealed record) holds the `LabeledRow` (id, reading, sentence, offsets and a
source), the bootstrap cluster, the group (the origin of a regression row or the family of a generalization row), the
kind (`-` for a regression row), the seen-in-training mark (`-` for a generalization row), whether the row is marked
`cue_unsolvable=yes`, and the slices. `EvaluationOverlay` is an internal static class, stateless and thread-safe, with
the set names `regression` and `generalization` and their ordinal order `Sets`.

**The source is the stratum.** The evaluator tallies rows by their source, so the loader puts the stratum there:
`regression/<origin>/seen` or `regression/<origin>/unseen`, and `generalization/<kind>/cue-unsolvable` or
`generalization/<kind>/cue-solvable`. The by-source table of an overlay is then its table of strata, with no second
tally.

**Clusters (decision).** A generalization row resamples with its family (`family:<family>`), the cue or template idea
it was written from, as the pre-registered criterion of the word-cluster work requires; rows of one family are not
independent evidence. A regression row is its own cluster (`row:<id>`), because its rows come from unrelated places.

**Slices.** Every row is in `all`. A regression row is also in `seen` or `unseen` and in `origin:<origin>`; a
generalization row in `kind:<kind>`, `cue_unsolvable:yes` or `cue_unsolvable:no`, and `family:<family>`.

**Key Methods**:

- **Load(dataRoot, set)**: reads the overlay with the strict loader of `TextEnDataSet` (`LoadRegression` or
  `LoadGeneralization`), stops on the first finding, checks every span and converts the records in file order; a file
  with its header only gives no rows
- **FileOf(set)**: the file name of an overlay; another name throws `ArgumentException`
- **IsOverlay(set)**: whether a set name is an overlay

**Error Handling**: a missing file, a format or header defect or an offset that is not an integer (a finding of the
loader) throws `InvalidDataException` with the location and message of the first finding and the hint to run
`check-data`; a span that is not inside its sentence throws `InvalidDataException` naming the row. Everything else about
the overlays (references, novelty, quotas) is the data check's, not the evaluation's.

**Dependencies**: `TextEnDataSet`, `CheckReport`, `RegressionRecord` and `GeneralizationRecord` of the
TrainingDataSubsystem, `LabeledRow` of `TrainingCorpus`, and the framework.

**Callers**: `EvaluateCommand`, and the unit tests; `HeteronymEvaluator` and `EvaluationRecord` use `OverlayRow`.
