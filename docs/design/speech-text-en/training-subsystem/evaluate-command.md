### EvaluateCommand

**Purpose**: Be the command line entry of the `evaluate` command: measure a trained model on the development and holdout
sets, decide the quality gate, run the multi-seed study, and write or verify the evaluation record. Evaluating a holdout
set is the one irreversible act of model development (a set that has guided a choice no longer measures anything), so
the command reads a holdout file only when the gate, an explicit holdout set or the record asks for it, and says so in
its output. The command is a pure function of its arguments, its two writers and the directories.

**Data Model**: `EvaluateCommand` is an internal static class with `Name` (`evaluate`). `EvaluateOptions` is the parsed
command line: the data folder, the model directory, the model file, the requested sets, and the flags for the gate, the
multi-seed study, writing the record, verifying the record and usage, and the record folder. The command keeps no state
between runs.

**Command line.** `evaluate [options]` with `--data <dir>`, `--model-dir <dir>`, `--model <file>`, `--set <name>`
(`train`, `dev`, `holdout-natural`, `holdout-independent` or `all`, which is the last three; repeatable; `dev` by
default; `train` exists for the parity check of the training rows; `regression` and `generalization` are the evaluation
overlays, which `all` does not include), `--gate`, `--seeds`, `--write`, `--check`,
`--record-dir <dir>` and `--help`. The exit codes are 0 (done, or the gate passed), 1 (the gate failed, or the
evaluation record is stale) and 2 (a usage, input, I/O or data error). With `--gate` and `--check` (or `--write`)
together the exit code is the more severe of the two results, so 0 needs a passing gate and a current record. `--write`
and `--check` cannot be combined; an unknown option or set name, a missing value, and a model file that does not exist
are usage or input errors.

**Locations.** The data folder is found as for `train`. The model under test is `--model`, or else `heteronyms.bin` in
`--model-dir`, or else the committed location `src/DemaConsulting.Speech.Text.En/Resources/heteronyms.bin`. The
training record is the `training.json` in the model directory or at the committed location
`tools/DemaConsulting.Speech.Text.En.Trainer/training.json`; when it exists, its training and evaluation settings are
used (the settings the model was trained with and the gate that was committed with it), and otherwise the defaults. The
evaluation record folder is `--record-dir` or else `docs/evaluation/text-en` of the repository.

**What it does.**

1. Resolves the locations and settings, reads the model file and computes the SHA-256 of its uncompressed image (a
   compressed or an uncompressed file gives the same hash).
2. Chooses the sets: all three, in ordinal order, followed by the overlays whose files exist (`generalization`, then
   `regression`; an absent file is named and left out), when the record is written or checked; the natural holdout
   alone for `--gate`; otherwise the requested sets, `dev` by default.
3. Loads the corpus and creates the evaluator. When a holdout set is among the sets it prints a line that says a holdout
   set is being read.
4. Evaluates each set end to end through the analyzer (see *HeteronymEvaluator Design*) and prints the accuracy, the
   three baselines, the improvement and its interval, the parity counts, the strata and the worst readings of at least
   five rows. An overlay is loaded with `EvaluationOverlay` (an overlay without rows is named and skipped) and evaluated
   with `HeteronymEvaluator.EvaluateOverlay`: the same figures for the shipped classifier, then a line confirming the
   verified cue-unsolvable rows and one line per slice with the accuracy with and without the curated cue priors, both
   intervals, the cue effect and its interval, and the rows on which a cue acts and whose reading it changes.
5. With `--seeds`, or when the record is written or checked, retrains with each seed of the evaluation settings (the
   same training settings and the same development-set choices, only the seed changes) and evaluates each model on the
   natural and the independent holdout through the direct path.
6. With `--gate`, prints the verdict on the natural holdout from `HeteronymEvaluator.DecideGate` and returns 0 for a
   pass and 1 for a fail.
7. With `--write`, writes the evaluation record (`EvaluationRecord.Write`), the overlays appended after the data sets;
   with `--check`, compares it
   (`EvaluationRecord.Compare`), names every file that is missing or differs and returns 1 when any is. The check never
   lowers a result: the exit code is the larger of the gate result of step 6 and the check result, so a failed gate
   stays 1 when the record is current, and a stale record gives 1 when the gate passed.

**Error Handling**: `IOException`, `UnauthorizedAccessException`, `InvalidDataException` (data, settings, record and
model errors, a harvested row that has no source document in `harvest-provenance.tsv`, reported before any gate verdict
is printed, a missing or malformed overlay file asked for by name, and a generalization row marked `cue_unsolvable=yes`
on which a curated cue acts, reported before any figure of that overlay is printed), `ArgumentException` and
`NotSupportedException` are caught by `Run` and reported on the error writer as `evaluate: error: <message>` with exit
code 2; a failed gate and a stale record are results (exit code 1), not errors. Nothing else is caught.

**Dependencies**: `HeteronymEvaluator`, `EvaluationRecord` and `EvaluationOverlay`, `TrainingCorpus`, `ModelSelection`
(the retraining of the study), `TrainingRecord` and its settings, `TrainCommand` (the file-name constants),
`CheckDataCommand` (the data folder discovery, from the TrainingDataSubsystem) and `SparsePerceptronModelWriter` (the
uncompressed image used for the hash).

**Callers**: `Program.cs` (the dispatch of the `evaluate` command), `EvaluateExternalCommand` (which reuses the
resolution of the model file) and the unit tests.
