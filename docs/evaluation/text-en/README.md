# Text.En Heteronym Model: Evaluation Record

This folder is the committed, deterministic evaluation record of the heteronym model that the
`DemaConsulting.Speech.Text.En` package embeds (`src/DemaConsulting.Speech.Text.En/Resources/heteronyms.bin`). It is
written by `evaluate --write` and verified by `evaluate --check` of the trainer
(`tools/DemaConsulting.Speech.Text.En.Trainer`), which recomputes every table from the committed model and data and
compares the bytes. The tables are integers (counts and basis points, where 10,000 basis points are 100 percent), so
the record is identical on every operating system. The protocol, the choices made on the development set and every run
of the gate are in `tools/DemaConsulting.Speech.Text.En.Trainer/RUN-LOG.md`; the settings and the model hash are in
`tools/DemaConsulting.Speech.Text.En.Trainer/training.json`.

The numbers below are for the model with the uncompressed image SHA-256
`73de5a3f74e34b9373d165e4bedd603988e5d1d81bab36fb6c3ab6444f603df0` (also `gate.tsv`). If the model changes, the record
is regenerated and these headline numbers must be updated with it.

## What Is Measured

Each labeled sentence has one target word, a heteronym of the reading inventory, and the reading that word has there.
The evaluation runs the real analyzer on the sentence (`EnglishTextAnalyzer` with the embedded model, the same path a
host uses) and takes the `en.reading` annotation on the target word. A word for which the analyzer emits no reading
counts as wrong ("end to end"). Rows whose target is not exactly one token of the analyzer's own tokenization are
therefore counted wrong too; the holdout rows are all aligned.

Baselines:

- **Own part of speech.** The reading decided by the part of speech that the analyzer's tagger gives the word: the
  unique candidate whose part-of-speech hint contains the tag, otherwise the first candidate of the form. This is what
  a reader gets for free from the tagger, and it is the baseline the quality gate must beat.
- **Majority reading.** The reading that the form has most often in the training data (ties: the first candidate).
- **First candidate.** The first candidate of the form in the inventory order.

The improvement over the own-part-of-speech baseline comes with a 95 percent interval from a paired, document-clustered
bootstrap (10,000 resamples, seed 7): rows of one source document are resampled together, and each independent or
synthetic sentence is its own cluster. Every harvested sentence must name its source document; a harvested sentence
without one stops the evaluation instead of counting as its own cluster.

## Files

- `summary.tsv`: per set, the rows, accuracy, the three baselines, the improvement, its interval and the parity check
  of the trainer's own decisions against the analyzer's (0 mismatches on every set)
- `gate.tsv`: the quality gate (at least 93.0 percent pooled accuracy on the natural holdout, and an interval whose
  lower bound is above zero) and its verdict
- `by-source.tsv`: accuracy by the `source` of the rows (strata of the natural data)
- `per-reading.tsv`: accuracy by reading, lowest first
- `confidence.tsv`: accuracy by the confidence the analyzer reports
- `seeds.tsv`: holdout accuracy of models trained with five seeds and the same settings, with mean, minimum and maximum
- `overlay-rows.tsv`: every row of the two evaluation overlays (see below) with its labeled reading, the reading of the
  shipped model with its cue priors, the reading of the model alone and whether a cue prior acted on the row
- `curated-ablation.tsv`: per overlay slice, the accuracy with the cue priors and of the model alone, each with its
  clustered 95 percent interval, the paired cue effect with its interval, and the rows on which a cue acted and whose
  reading it changed

`summary.tsv`, `by-source.tsv`, `per-reading.tsv` and `confidence.tsv` list the overlays `generalization` and
`regression` after the three data sets, so the lines of the data sets are the same with or without the overlays; the
`source` of an overlay row is its stratum (for example `regression/listening/unseen` or
`generalization/novel-vocabulary/cue-unsolvable`).

## Headline Results

| Set | Words | Model | Own tag | Majority |
| --- | ---: | ---: | ---: | ---: |
| `holdout-natural` (266 documents; the gate) | 1,167 | 96.23% (1,123) | 90.66% (1,058) | 82.01% (957) |
| `holdout-independent` (not gating) | 1,972 | 95.44% (1,882) | 91.23% (1,799) | 51.37% (1,013) |
| `dev` (used to choose settings; optimistic) | 3,235 | 95.30% (3,083) | 88.56% (2,865) | 65.87% (2,131) |

The columns "Own tag" and "Majority" are the own-part-of-speech and majority-reading baselines, and the percent signs
are percentages of the words of the set.

- **Gate: passed.** The improvement over the own-part-of-speech baseline on the natural holdout is +5.57 points, with a
  95 percent interval of +3.16 to +8.15 points (266 clusters). The accuracy threshold was 93.0 percent. The gate was
  fixed before any holdout was read (see `RUN-LOG.md`).
- **Adjective cue.** The classifier adds a fixed prior to an adjective reading after a degree adverb or next to a
  coordinated adjective (see *HeteronymClassifier Design*; added 2026-11-20 for the flagged *very minute and clear*).
  For the shipped model it changed no decision on the development set (it raised the confidence of 22 words, all already
  right) or on either holdout; the accuracies above are unchanged, and the record changed only in the confidence strata
  and in three seed models' independent-holdout counts (one word fewer each).
- **Noun-form, past-time and context cues.** Three more priors of the classifier (see *HeteronymClassifier Design*;
  added 2026-11-20 for the flagged *a knife wound or a deep muscle tear*). For the shipped model they changed no
  decision on the development set or on either holdout; externally `whd-eval` gained 1 row, `whd-train` 8 and `bench` 1
  (`tear.rip` and `wound.injury`), and no reading lost a row (see `RUN-LOG.md`).
- **Seeds.** Five training runs with seeds 1 to 5 and the same settings give 95.46 to 97.00 percent on the natural
  holdout (mean 96.43) and 95.44 to 97.06 percent on the independent holdout (mean 96.34). The shipped model is seed 1,
  chosen before any holdout was evaluated, not the best seed.
- **Confidence.** On the natural holdout the reading was right for 99.33 percent of the words at a confidence of 0.90 or
  more (1,035 of 1,042), for 75.00 percent between 0.60 and 0.90 (78 of 104) and for 47.62 percent below 0.60 (10 of
  21).
- **Weakest readings** (at least five words), natural holdout: `live.adj` 3 of 5, `read.past` 11 of 17,
  `analysis.plural` 4 of 6, `august.month` 4 of 6, `reading.act` 21 of 29, `row.line` 6 of 8, `use.habitual` 6 of 8.
  Independent holdout: `bass.fish` 0 of 5, `sewer.seamstress` 0 of 5, `number.noun` 1 of 5, `august.month` 2 of 5,
  `bow.weapon` 2 of 5, `frequent.verb` 2 of 5, `put.verb` 2 of 5. The complete ordered list is `per-reading.tsv`.

## Limits of This Evidence

State these with every figure from this record:

1. **The labels are not human-verified.** The reading of each natural sentence was chosen by a rule from the part of
   speech (rows whose `label_method` is `upos`) or by a language model (`llm`), and the model-written and model-labeled
   rows were audited blind by a second model. No human reviewed them. The accuracy is agreement with these labels.
2. **The tagger saw part of the natural holdout.** The rows labeled by rule come from sentences of the corpora on which
   the part-of-speech tagger itself was trained, so the tagger is nearly perfect on them and the features that depend on
   the tag are better there than on new text. The natural holdout therefore flatters the tag-based features. The
   independent holdout (raw sentences written blind by a different model family, never seen by the tagger) agrees
   within about one point, but it is synthetic text, not natural text.
3. **The improvement comes from the hard rows.** On the rule-labeled rows the baseline is near perfect by construction
   (the label is a function of the tag), so the whole improvement over the tag comes from the language-model-labeled
   rows, where the words need meaning rather than grammar (for `harvest-masc-llm`: 84.46 percent against 68.92 percent
   for the tag alone). Where the tag alone decides the reading, the model is sometimes worse than the tag:
   `read.past` (11 of 17, the tag alone gets 17) and `reading.act` (21 of 29, the tag alone gets 29).
4. **Small counts.** A reading has five rows in the independent holdout and few in the natural one; a per-reading
   figure is a few rows and is noisy. The pooled figures weigh the common readings most.
5. **Selection effects.** The settings (the pruning threshold, the weight of the natural rows, the number of epochs and
   the confidence calibration) were chosen on the development set, so its figures are optimistic. The selection was a
   pre-registered rule that picked its winner by one row (1,267 against 1,266 of 1,301 natural development rows); a
   neighboring setting is a little better on all development rows. The rule was applied as written.
6. **Coverage.** The model decides 395 readings of 207 words of US English, one sentence at a time. A heteronym that is
   not in the inventory gets no reading, and the model does not read beyond the sentence.

## Evaluation Overlays

Two sets beside the data files measure what the flagged sentences and unseen words and constructions get (see the data
README, "Evaluation Overlays"): the **regression overlay** (`regression.tsv`: owner-flagged sentences, analyzer and
golden test targets and selected training and development rows, each marked as seen or unseen in training) and the
**generalization set** (`generalization.tsv`: 242 rows written blind by another model family, frozen before any model
output on it existed, and checked new against the training and development data). They are not part of the gate.

Each overlay is measured twice with the same shipped model on the same path:

- **with the cue priors**: the analyzer's reading, as for the data sets;
- **model only**: the reading the model's own weights choose for the same features without the four hand-written cue
  priors (adjective, noun-form, past-time and context cues). This is computed by the evaluation with the classifier's
  own scoring; the classifier and the model are unchanged.

Intervals are 95 percent percentile intervals of the clustered bootstrap (10,000 resamples, seed 7). A generalization
row resamples with its family (the cue or template family it was written from), a regression row alone. The cue effect
is the paired difference of the two accuracies. Every generalization row marked `cue_unsolvable=yes` is verified: none
of the adjective, past-time or context cue tests may fire on it and both readings must agree; otherwise the evaluation
stops with a data error.

### Baseline of the Shipped Model (PR A)

Measured on 2026-11-21 with the shipped model (image SHA-256 `73de5a3f...3df0`, no word clusters) by
`evaluate --set regression --set generalization`, once, after the generalization set was frozen and its one defect fix
(the flag of `gen-0060`) was made; the figures are the ones of `curated-ablation.tsv`. This is the PR A baseline of the
pre-registered criterion below. Percentages with their 95 percent intervals in brackets; the cue effect is the shipped
accuracy minus the model-only accuracy, in points.

**Generalization set** (242 rows; intervals clustered by family):

| Slice | Rows | Families | Correct | With cues | Model only | Cue effect | Cues |
| --- | ---: | ---: | ---: | --- | --- | --- | ---: |
| all | 242 | 64 | 177 / 178 | 73.14 [67.05, 79.10] | 73.55 [67.51, 79.34] | -0.41 [-1.29, 0.00] | 13, 1 |
| `novel-vocabulary` | 135 | 51 | 100 / 100 | 74.07 [65.22, 82.17] | 74.07 [65.22, 82.17] | 0.00 [0.00, 0.00] | 8, 0 |
| `novel-construction` | 107 | 39 | 77 / 78 | 71.96 [63.71, 80.00] | 72.90 [64.56, 81.00] | -0.93 [-3.30, 0.00] | 5, 1 |
| cue-unsolvable | 80 | 51 | 65 / 65 | 81.25 [71.79, 89.87] | 81.25 [71.79, 89.87] | 0.00 [0.00, 0.00] | 0, 0 |
| cue-solvable | 162 | 64 | 112 / 113 | 69.14 [61.44, 76.47] | 69.75 [62.33, 76.82] | -0.62 [-2.01, 0.00] | 13, 1 |

"Correct" is with cues / model only; "Cues" is the rows a cue prior acts on, then the rows whose reading it changes.
The own-part-of-speech baseline gets 157 of 242 (64.88 percent); the shipped model's improvement over it is +8.26
points [0.00, +16.53] over 64 families. Parity: 242 compared, none mismatched.
The one row whose reading a cue prior changes is `gen-0060` (*Beneath the pulley, the cord wound.*): the noun-form
cue turns the model's right `wind.coil` into `wound.injury`. The weakest readings are `tear.rip` (0 of 9), `bass.fish`
and `bass.music` (4 of 9 each) and `bow.weapon` and `content.satisfied` (5 of 9 each); the full list is
`per-reading.tsv`. The pre-registered guard of PR B is this shipped figure: 177 of 242 (73.14 percent).

**Regression overlay** (519 rows; each row its own cluster):

| Slice | Rows | Correct | With cues | Model only | Cue effect | Cues |
| --- | ---: | ---: | --- | --- | --- | ---: |
| all | 519 | 519 / 515 | 100.00 [100.00, 100.00] | 99.23 [98.46, 99.81] | +0.77 [+0.19, +1.54] | 37, 4 |
| seen in training | 25 | 25 / 24 | 100.00 [100.00, 100.00] | 96.00 [88.00, 100.00] | +4.00 [0.00, +12.00] | 18, 1 |
| unseen | 494 | 494 / 491 | 100.00 [100.00, 100.00] | 99.39 [98.58, 100.00] | +0.61 [0.00, +1.42] | 19, 3 |
| `listening` | 7 | 7 / 4 | 100.00 [100.00, 100.00] | 57.14 [14.29, 85.71] | +42.86 [+14.29, +85.71] | 6, 3 |
| `data-row` | 21 | 21 / 20 | 100.00 [100.00, 100.00] | 95.24 [85.71, 100.00] | +4.76 [0.00, +14.29] | 20, 1 |
| `analyzer-test` | 11 | 11 / 11 | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 0.00 [0.00, 0.00] | 2, 0 |
| `golden-pipeline` | 37 | 37 / 37 | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 0.00 [0.00, 0.00] | 2, 0 |
| `golden-readings` | 382 | 382 / 382 | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 0.00 [0.00, 0.00] | 4, 0 |
| `golden-sentences` | 61 | 61 / 61 | 100.00 [100.00, 100.00] | 100.00 [100.00, 100.00] | 0.00 [0.00, 0.00] | 3, 0 |

The own-part-of-speech baseline gets 471 of 519 (90.75 percent; improvement +9.25 [+6.74, +11.75]); parity 519
compared, none mismatched. The four rows the cue priors change are three owner-flagged listening sentences
(`minute.tiny`, `wound.injury` and `tear.rip`, the sentences the priors were written for) and one `read.past` data row;
the model alone gets each of them wrong.

**Reading the baseline.** The cue priors were written for the flagged sentences and fix exactly those; on the blind
generalization set they act on 13 rows and change one, for the worse. The rows the curated cues cannot solve score
higher (81.25 percent) than the others (69.14 percent), so that subset is not harder for the model alone. The
generalization accuracy of the shipped model (73.14 percent) is far below its holdout accuracy (96.23 percent): new cue
words and constructions are where the model is weak, which is what the word-cluster features of PR B are meant to
address.

## Pre-Registered Criterion for Word Clusters

Recorded on 2026-11-20, before the generalization set was written and before any model with word clusters was
trained; the same text, with the protocol of the generalization set, is in `RUN-LOG.md`. PR A adds the regression
overlay, the blind generalization set and the word-cluster table without changing the model; PR B trains with the
clusters and is judged by this criterion.

- **Models.** A is PR B's final pipeline with the committed 512-class word table. B is the identical pipeline (same
  data, settings, curated-feature configuration and training seed 1) with an empty table. Both are evaluated end to end
  through the analyzer on `generalization.tsv`.
- **Primary endpoint.** The difference of accuracy, A minus B, in basis points, with a 95 percent interval from the
  paired percentile bootstrap that resamples whole families (the `family` column, the cue and template family of a row;
  10,000 resamples, seed 7).
- **Pass** if and only if the difference is at least +300 basis points (3.0 points) and the lower bound of the interval
  is above zero; the mean difference over training seeds 1 to 5 must also be above zero.
- **Reported, not gating.** The difference on the `novel-vocabulary`, `novel-construction` and cue-unsolvable subsets,
  and a 2 by 2 ablation (clusters off or on, by the curated cue features off or on) on the generalization set, the
  regression overlay (seen and unseen rows) and the development set, with the main effect of the clusters and their
  interaction with the curated cues.
- **Guards.** The legacy natural holdout stays at or above 1,123 of 1,167; every golden test passes, including the 385
  end-to-end rows of `readings-golden.tsv`; the final model's generalization accuracy is at least the PR A baseline of
  the shipped model.
- **Discipline.** The generalization set is read once for the comparison, with all four models in one run. A data or
  setting change after that read voids the result. More than 512 clusters need a development-set benefit measured
  before the read. A procedural amendment logged in `RUN-LOG.md` on 2026-11-21, before any PR B training, makes this
  concrete without changing the criterion: during PR B development, candidate models live at scratch paths and are
  evaluated only on the development set and the regression overlay
  (`evaluate --model <file> --set dev --set regression`), never on the generalization set or the natural holdout and
  never with `evaluate --write` or `--check`.
  No candidate model is committed before the read is logged, because CI's `evaluate --check` evaluates the committed
  model on every set. The read is one run of the four ablation models and seeds 1 to 5; the final commit's
  `evaluate --write` must reproduce its figures, and an accidental early read is logged and disclosed.

## Planned Fresh Natural Audit Set

The natural holdout above was seen by the upstream part-of-speech tagger, and earlier gate runs informed decisions. A
fresh audit set is planned (not built yet): about 1,200 natural rows stratified by lemma, drawn with a recorded seed
from public-domain documents published in 1929 or earlier that are in neither the tagger's corpora nor the
word-cluster corpus, labeled blind by two language-model families with adjudication, frozen before PR B training and
read once. The existing natural holdout stays as the legacy non-regression gate (at least 1,123 of 1,167).
