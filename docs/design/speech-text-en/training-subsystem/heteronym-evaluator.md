### HeteronymEvaluator

**Purpose**: Measure a trained model on a labeled data set through the product's own path, and decide the quality
gate: the end-to-end accuracy, three baselines, strata by source, by reading and by confidence, a paired clustered
bootstrap interval of the improvement over the part-of-speech baseline, and a parity check between the analyzer and the
trainer's direct path. Everything is integer arithmetic, so the numbers are identical on every platform.

**Data Model** (the result types are internal sealed records, immutable):

- `SetEvaluation`: the set name; the rows; the rows that are aligned (the target is exactly one runtime token); the rows
  of a one-candidate form; the rows **covered** (a reading was emitted); the rows decided correctly end to end; the rows
  each of the three baselines decides correctly; the tallies by source, by reading and by confidence; the bootstrap
  interval; the parity counts; and, only for an end-to-end evaluation of caller-supplied rows, the bootstrap interval of
  the accuracy itself (null otherwise)
- `GroupTally`: a group name, its rows, the rows the model decides correctly and the rows the part-of-speech baseline
  decides correctly (0 for a confidence bucket)
- `BootstrapInterval`: the number of clusters, the point estimate of the difference (model minus baseline) and the lower
  and upper bounds, each in basis points rounded half away from zero, and whether the **exact** lower bound is strictly
  greater than zero
- `ClusterStats`: a cluster id, its rows and the rows the model and the baseline decide correctly
- `ParityCounts`: the aligned rows compared, the rows on which both paths decided a reading and the two differ
  (**mismatched**), and the rows on which the direct path decided a reading and the analyzer emitted none
  (**suppressed**: the word is covered by a replacement, a pronunciation or an override)
- `SeedResult`: the accuracy of one model of the multi-seed study on the natural and the independent holdout
- `GateVerdict`: whether the accuracy condition holds, whether the interval condition holds, and `Passed` when both hold
- `HeteronymEvaluator` (an internal sealed class, not thread-safe): the corpus, the data folder, the evaluation
  settings, the majority reading of every form seen in the training file, the prepared sets it has already read, and the
  cluster ids of the harvest provenance table once a harvested row needs it. `SetFiles` names the four sets: `train`
  (`train.tsv`, used only for the parity check of the training rows), `dev` (`dev.tsv`), `holdout-natural`
  (`holdout-natural.tsv`) and `holdout-independent` (`holdout-independent.tsv`); `ReportSets` lists the three of the
  evaluation record (`dev` and the two holdouts). This unit is the only place a holdout file is opened, and only for a
  set name that a command passes

**Evaluation path (end to end).** `Evaluate(set, image)` prepares the rows of the set once with `RowPreparer.Prepare`
and keeps the classifier context. It builds a real `EnglishTextAnalyzer` (default options, the shared part-of-speech
adapter) with a `HeteronymModelProvider` over the model under test and the corpus inventory, runs it over each row's
sentence and reads the reading annotation on exactly the row's span, with its confidence. A row for which the analyzer
emits no reading counts as **wrong**: the metric is what a user gets, not what a classifier would get on rows it was
shown. The **direct path** is the runtime `HeteronymClassifier` over the precomputed context of the row (the model
loaded with `SparsePerceptronModel.Load` and the corpus inventory); the two paths are compared for every aligned row for
the parity counts. A mismatch means the features the model was trained on differ from what the analyzer sees, which is
exactly the drift the parity check exists to find. `EvaluateDirect(set, image)` uses the direct path only, as the
multi-seed study does, and reports zero parity counts.

**Caller-supplied rows (external sets).** `EvaluateRows(set, rows, image, clusterOf)` and `EvaluateRowsDirect(set, rows,
image, clusterOf)` take labeled rows and a clustering function from the caller instead of reading a file of the data
folder, so the external evaluation (see *ExternalEvaluationSubsystem Design*) is measured by exactly the same code as
the committed sets. The prepared rows are kept under the set name for the length of one run, so one name must always
mean the same rows. The clustering function replaces the file-and-provenance rule of the committed sets: rows that it
gives the same name resample together, and no provenance table is read for such rows. No number of a file set changes.
A row whose target is not a form of the inventory or whose label is not one of its candidates still stops the
evaluation with an `InvalidDataException`, because `RowPreparer.Prepare` rejects it; the external adapters therefore
skip and count such rows before they call the evaluator.

**Accuracy interval.** The end-to-end path of `EvaluateRows` also reports an interval of the accuracy itself: the same
paired bootstrap, with the baseline counts of every cluster taken as zero, so the interval is that of the summed
correct rows over the summed rows, in basis points, with the same resamples, confidence level, seed and integer
ordering. Its point estimate is the accuracy. The interval is computed only on that path: `Evaluate` and the direct path
leave it null, so the committed record is byte for byte unchanged and the seed study stays cheap.

**Overlay evaluation.** `EvaluateOverlay(set, rows, image)` evaluates the rows of an evaluation overlay (see
*EvaluationOverlay Design*) end to end exactly as `EvaluateRows` does, with the accuracy interval, each row in the
cluster the overlay gives it, and adds, per row:

- the **shipped** reading: the analyzer's, with the classifier's curated cue priors (the adjective, noun-form,
  past-time and context cues);
- the **model-only** reading: the candidate with the highest integer score of the row's own features over the loaded
  weights, through `AveragedPerceptronTrainer.Decide` (the classifier's `ScoreCandidate` and its tie rule) and no
  prior, on the same path: a row the analyzer leaves without a reading has none, and a row whose target is not one
  runtime token keeps the analyzer's reading, as its context is not available;
- whether a **cue fires**: a prior is added to at least one candidate (the adjective cue with an adjective candidate,
  the past-time cue with a past-tense candidate, a context cue for a candidate, or the noun-form cue).

The noun-form test is private to the classifier, so it is mirrored from the inventory facts it reads (`CanBeNoun`,
`IsNounOfItsLemma`); the other tests are the classifier's own internal methods. For an aligned row on which no cue
fires, the classifier's reading and the model-only reading must be equal; a difference stops the evaluation with an
`InvalidDataException`, so the mirror cannot drift unseen. The **slices** of the rows (`all` first, then the others in
ordinal order) each get the rows, the shipped and the model-only correct counts, the clustered bootstrap intervals of
both accuracies (the baseline counts taken as zero, as for the accuracy interval), the paired interval of shipped minus
model-only (the model-only counts in the place of the baseline), and the counts of rows on which a cue fires and of
rows whose two readings differ. The model, the classifier and the analyzer are not changed by any of this.

**Cue-unsolvable rows.** A row marked `cue_unsolvable=yes` must be one runtime token on which none of
`HasAdjectiveCue`, `HasPastTimeCue` and `HasContextCue` (for any candidate) fires and whose shipped and model-only
readings agree (which covers the noun-form cue). Every row that fails is collected with its reason (the cue test that
fires, a changed reading, with the noun-form cue named when it applies, or "not one runtime token"), and the evaluation
then stops with one `InvalidDataException` that names them all, before any figure of the overlay is returned.

**Baselines.** The **own-part-of-speech** baseline picks the one candidate whose `pos_hint` list (in `readings.tsv`)
holds the tagger's part of speech of the word; with no tag, no such candidate or several, it picks the first candidate.
The **majority** baseline picks the reading most often labeled for the form in the training file (a tie, or a form never
seen, goes to the first candidate). The **first-candidate** baseline picks the first candidate of the form in the order
of `forms.tsv`.

**Strata.** The tallies by `source` (ordinal order), by labeled reading (worst accuracy first, compared exactly by cross
multiplication, ties by the ordinal name) and by the reported confidence of the emitted reading in four buckets:
below 0.600, 0.600 to 0.899, at least 0.900, and none (no reading was emitted). The bucket boundaries are applied to the
confidence rounded to thousandths, the value the annotation carries.

**Paired clustered bootstrap.** Rows resample in **clusters**: the source document of every harvested row (a source
that is `harvest-*`), taken from its record in `harvest-provenance.tsv`, and each synthetic or independent-holdout row
alone, so the several rows that one document supplies, which are not independent, move together. The source column
decides, never the row id. A harvested row with no document, a missing or malformed provenance table, or a row of an
unknown source is an error, not an independent cluster: the evaluation stops (see *Error Handling*). For each of the
`Resamples` resamples, a generator seeded with the bootstrap seed draws as
many clusters as there are, with replacement, and the resample yields the integer pair of the summed difference (model
correct minus baseline correct) and the summed rows. The pairs are ordered by **exact cross multiplication** of
(difference, rows), and the interval is read at the index `R (100 - confidence) / 200` from each end (R is the number of
resamples); the bounds and the point estimate are converted to basis points with integers only, rounded half away from
zero. No floating-point arithmetic is involved. A set with no cluster has the all-zero interval.

**The quality gate.** `DecideGate(natural, settings)` passes when the end-to-end accuracy on the natural holdout is at
least the threshold, tested exactly as `correct * 1000 >= threshold * rows` (and there is at least one row), **and** the
lower bound of the interval is exactly greater than zero. Both conditions are required; the independent holdout is
reported but is not part of the gate. The thresholds are the evaluation settings of the training record (see
*TrainingRecord Design*).

**Key Methods**: `Evaluate`, `EvaluateDirect` and `DecideGate` as above; `EvaluateRows` and `EvaluateRowsDirect` for
caller-supplied rows; `EvaluateOverlay` for an overlay (returning `OverlayEvaluation` with `OverlayRowOutcome`s and
`OverlaySlice`s); `Bootstrap(clusters, settings)` (public static, the interval); `RoundBasisPoints(numerator,
denominator)` (public static, half away from zero, integers only, 0 for a non-positive denominator).

**Error Handling**: null arguments throw `ArgumentNullException`; settings out of range, an unknown set, a data file
that the strict reader rejects (a table finding, a span outside its sentence, a target or label the inventory does not
have) and a model that fails the loader's or the classifier's checks are `InvalidDataException`s that propagate to the
command, which reports them as an input error. The evaluator **fails closed** on the provenance it relies on: a
harvested row without a source document in `harvest-provenance.tsv` (including a folder without the table) is an
`InvalidDataException` whose message names the set file and the row, or the missing table, and tells the owner to run
`check-data`; a row of an unknown source is one too. An overlay whose row id repeats, a cue-unsolvable row on which a
cue acts, and a row without a detected cue whose reading the priors change are `InvalidDataException`s too. The table is
read on the first harvested row, so a set of synthetic or independent rows needs no table, and a failed read is not
cached. The evaluator does not verify data hashes; `check-data` is the validator of the data folder.

**Dependencies**: `TrainingCorpus` and `RowPreparer` (rows, inventory, hints), `EvaluationSettings` (from
`TrainingRecord`), `DeterministicRandom`, `AveragedPerceptronTrainer` (the integer decision), `EvaluationOverlay` (the
row type), and from the TrainingDataSubsystem `TsvTable`,
`CheckReport` and `TextEnDataSet` (the provenance table and its header). From the library it uses `EnglishTextAnalyzer`
and `EnglishTextAnalyzerOptions`, `TextEnAnnotations` (the typed readers of the reading annotations),
`PartOfSpeechAdapter`, `HeteronymModelProvider`, `HeteronymClassifier` (including its internal cue tests and weight
source interface), `HeteronymContext` (the tag codes), `ReadingInventory` and `SparsePerceptronModel`.

**Callers**: `EvaluateCommand`, `EvaluateExternalCommand` (the caller-supplied rows), `EvaluationRecord` (the result
types and `RoundBasisPoints`, `DecideGate`), `ExternalEvaluationRecord` (the result types) and the unit tests.
