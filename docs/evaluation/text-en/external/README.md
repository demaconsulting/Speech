# Text.En Heteronym Model: External Evaluation Record

This folder is the committed, deterministic record of the evaluation of the embedded heteronym model
(`src/DemaConsulting.Speech.Text.En/Resources/heteronyms.bin`) on two **external, evaluation-only** datasets: Google's
WikipediaHomographData (WHD) and the English read set of heteronym-bench. It is written by
`evaluate-external --fetch --write` and verified by `evaluate-external --check` of the trainer
(`tools/DemaConsulting.Speech.Text.En.Trainer`). The tables are integers (counts and basis points, where 10,000 basis
points are 100 percent), so the record is identical on every operating system. It holds **only numbers, the pinned
revisions, set and stratum names and our own reading ids**. No sentence, target word, label or other text of either
dataset is in this folder, in the repository or in the model. The model under test is the embedded model with the
uncompressed image SHA-256 `73de5a3f74e34b9373d165e4bedd603988e5d1d81bab36fb6c3ab6444f603df0` (`sources.tsv`); if the
model changes, the record is regenerated and the numbers below must be updated with it. The record was regenerated on
2026-11-20 for the classifier's adjective cue (same model, see `RUN-LOG.md`): the shipped model lost 1 `whd-eval` row
(1,502 to 1,501 right) and on `whd-train` gained one row and lost one; the cue was designed from a listening flag and
the development set, and nothing was chosen from these numbers. It was regenerated again on 2026-11-20 for the
noun-form, past-time and context cues (same model): `whd-eval` 1,501 to 1,502, `whd-train` 13,270 to 13,278, `bench`
278 to 279 right (the cues were designed from the listening flag and the development set; see `RUN-LOG.md`).

The numbers here are **informational**. They never gate a build, and no setting of the model was chosen from them (see
"Discipline" below). The in-domain evidence and the quality gate are in `docs/evaluation/text-en/README.md`.

## Reproducing the Record

The datasets are fetched on demand into the git-ignored cache `data/text-en/external/` from commit-pinned archives and
verified against per-file SHA-256 pins (`tools/DemaConsulting.Speech.Text.En.Trainer/external-sources.json` and
`external-files.tsv`). Nothing is downloaded without `--fetch`, and a file that does not match its pin is never
evaluated.

```text
dotnet run -c Release --project tools/DemaConsulting.Speech.Text.En.Trainer -- evaluate-external --fetch --write
dotnet run -c Release --project tools/DemaConsulting.Speech.Text.En.Trainer -- evaluate-external --check
```

The first command needs network access once, takes about a minute and writes this folder.
The second verifies the committed files against a fresh evaluation and exits with code 1 when they differ. Neither
command runs in the build or in continuous integration.

## What Is Measured

The evaluation converts each external row to the trainer's own row format at evaluation time, maps its label to one of
our reading ids, and then evaluates exactly as the in-domain record does: the real analyzer runs on the sentence with
the embedded model, and the `en.reading` annotation on the target word is compared with the mapped reading. A word for
which the analyzer emits no reading counts as wrong.

- **WHD.** The `train` and `eval` files of the pinned revision. The offsets in these files are UTF-8 byte offsets and
  the fields are quoted; both are handled (the converter checks that every converted span is the row's homograph). The
  label of a row is its WHD word id, mapped by the committed id-to-id file `data/text-en/whd-mapping.tsv`.
  **`whd-eval` is the headline set** (1,615 rows). `whd-train` (14,487 rows) is a second, larger sample of the same
  distribution: it is the training split of WHD, but the model never saw it, so it is evaluation data here too.
- **heteronym-bench.** The English read set of the pinned revision (566 items). The 267 items that the benchmark copied
  from WHD are skipped as `duplicate-of-whd` (they are WHD sentences and are already in the WHD sets), leaving the
  299 items written for the benchmark (`bench-v1` and `bench-claude_v2` strata). The benchmark labels its readings with
  its own keys; `data/text-en/bench-mapping.tsv` maps each key of its 30 words to one of our reading ids.
- **Clusters.** WHD rows of one sentence resample together in the bootstrap (WHD has no article id). Each bench item is
  its own cluster.
- **Baselines.** The own-part-of-speech, majority-reading and first-candidate ("always default") baselines are those
  of the in-domain record. The majority baseline is computed from our training file, not from WHD.
- **Intervals.** 95 percent intervals from the same bootstrap (10,000 resamples, seed 7): one for the accuracy itself
  and one for the improvement over the own-part-of-speech baseline.
- **Seeds.** The model is retrained with each of the five evaluation seeds (same settings, same training and
  development files; external rows are never read by the training) and every external set is evaluated with each.
  Seed 1 is the shipped model, chosen before any holdout was read, not the best seed.

## Files

- `sources.tsv`: the model image, the bootstrap settings and, per source, the repository, the pinned commit, the number
  and size of the pinned files, the listing hash and the license statement
- `conversion.tsv`: per set, the rows read and used, the rows skipped by reason, the non-ASCII rows, the converted and
  repaired offsets, the rows whose sentence equals a training or development sentence (kept), and the aligned and
  covered counts
- `summary.tsv`: per set, the accuracy with its interval, the three baselines, the improvement over the
  own-part-of-speech baseline with its interval, and the parity of the trainer's own decisions against the analyzer's
- `by-source.tsv`, `per-reading.tsv`, `confidence.tsv`: accuracy by stratum, by reading (lowest first; our reading ids)
  and by the confidence the analyzer reports
- `seeds.tsv`: per set and seed, the accuracy and the improvement with its interval, then the mean, minimum and maximum

## Headline Results

| Set | Rows | Model (95% interval) | Own tag | Majority | Always default |
| --- | ---: | ---: | ---: | ---: | ---: |
| `whd-eval` (headline) | 1,615 | 93.00% (91.70 to 94.18), 1,502 right | 90.59% | 69.23% | 66.75% |
| `whd-train` | 14,487 | 91.65% (91.20 to 92.10), 13,278 right | 89.74% | 69.55% | 68.04% |
| `bench` (299 items) | 299 | 93.31% (90.30 to 95.99), 279 right | 80.27% | 40.13% | 43.81% |

- **WHD eval.** The embedded model reads 93.00 percent of the 1,615 rows as the mapped reading, 2.41 points more than
  the own-part-of-speech baseline (95 percent interval +1.05 to +3.84 points; the lower bound is above zero) and 23.8
  points more than always choosing the majority reading. The model is lower than on our in-domain holdouts (96.23
  percent natural, 95.44 percent independent): this is the cost of a change of domain and of label conventions (see
  the limits), and it is the number to quote for text that does not look like our training data.
- **Seeds.** Over the five seeds, `whd-eval` gives a mean of 92.72 percent (minimum 92.45, maximum 93.00), `whd-train` a
  mean of 91.73 percent (91.51 to 91.97) and `bench` a mean of 95.12 percent (92.64 to 96.99). The shipped model is
  seed 1, which is the top of the WHD range and in the lower half of the bench range. The bench range is about 4.4
  points wide on 299 items, so treat the bench figure as indicative only. The per-seed intervals of the improvement are
  in `seeds.tsv`; the lower bound is above zero for every seed on every set (+0.50 points for seed 4 on `whd-eval`).
- **Rows with no reading.** 25 of the 1,615 WHD eval rows (1.5 percent; 314 of 14,487 on `whd-train`) are not exactly
  one token of the analyzer's own tokenization, so no reading is emitted and they count as wrong. On the 1,590 aligned
  rows the accuracy is 94.4 percent. The parity check has 0 mismatches and 0 suppressed rows on every set.
- **Confidence.** On `whd-eval` the reading is right for 98.38 percent of the rows at a confidence of 0.90 or more
  (1,274 of 1,295), for 80.93 percent between 0.60 and 0.90 (208 of 257) and for 50.00 percent below 0.60 (19 of 38).
  The same picture holds on `whd-train` and `bench` (`confidence.tsv`); it is the evidence for the provisional
  low-confidence threshold of 0.60, which this change does not alter.
- **Weakest readings** on `whd-eval` (at least five rows): `minute.time` 2 of 9 (the tag baseline gets all 9),
  `tear.rip` 3 of 9, `age.verb` 4 of 9, `rodeo.event` 5 of 10, `conjugate.adj-noun` 5 of 9, `pasty.noun` 4 of 7,
  `read.past` 4 of 7 (the tag baseline gets all 7), `wind.coil` 3 of 5. The complete ordered list is `per-reading.tsv`.
  Several of these (`minute.time`, `read.past`) are words where the own-part-of-speech baseline is better than the
  model; the in-domain record shows the same for `read.past`.
- **Skipped rows.** The committed mappings cover every row: 0 rows skipped for an unmapped label, an unknown form, a
  reading that is not a candidate of the form or bad offsets, in every set. The only skips are the 267 benchmark items
  that duplicate WHD. The skip machinery exists to catch drift of the upstream files or of the mappings, which a
  `--check` would then report.
- **Overlap with training.** 0 evaluated sentences equal a training or development sentence after normalization
  (`conversion.tsv`).

## Limits of This Evidence

State these with every figure from this record:

1. **WHD is Wikipedia text.** The sentences are Wikipedia prose; our training and development text is MASC, other open
   sentence corpora, synthetic and language-model-written sentences. The figures measure a shift of domain, and the
   model was not tuned for it. They do not say how the model performs on speech-style, conversational or
   domain-specific text.
2. **The mapping is ours.** WHD labels are human-annotated word ids; the mapping to our reading ids is ours. It maps
   326 WHD ids to 318 readings (8 pairs of ids share a reading), so the accuracy is accuracy at the granularity of our
   readings, not of WHD's word ids. A different split of the readings would give different figures.
3. **Coverage.** WHD covers 162 homographs and heteronym-bench 30 words; our inventory has 207 words. The external
   figures say nothing about the 45 extension words that neither dataset has, and `bench` contains 59 of the readings.
4. **Label quality differs.** WHD labels are human-annotated. The 299 evaluated bench items were written by a language
   model and their labels were not human-verified, and the benchmark is recent and small: its interval is wide.
5. **Intervals are by sentence.** WHD has no article id, so the bootstrap resamples whole sentences rather than
   articles. Sentences of one article are correlated, so the WHD intervals may be a little too narrow.
6. **WHD train and eval are not independent samples of the model's performance.** Both come from one source and one
   annotation process; `whd-train` mostly serves as a larger sample with a tighter interval.
7. **The tagger is a dependency.** The part-of-speech tagger the analyzer uses was trained on its own corpora, which are
   not WHD; its provenance is outside this evaluation.
8. **No dataset is in the repository.** The sentences are CC BY-SA licensed text; the record is a set of numbers
   derived from them and quotes none. The two mapping files are our own ids (CC0).

## Discipline

External numbers are never used to choose a setting, a training row, a threshold or a model. This record was produced
once, after the model was frozen (run date and command in `tools/DemaConsulting.Speech.Text.En.Trainer/RUN-LOG.md`).
If a later change is motivated by these figures, the external sets are no longer independent for that model, and the
record must say so.
