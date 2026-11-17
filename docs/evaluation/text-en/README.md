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
- **Seeds.** Five training runs with seeds 1 to 5 and the same settings give 95.46 to 97.00 percent on the natural
  holdout (mean 96.43) and 95.44 to 97.11 percent on the independent holdout (mean 96.37). The shipped model is seed 1,
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
