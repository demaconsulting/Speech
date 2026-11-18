# RUN-LOG: Text.En heteronym model training and quality gate

This log records how the heteronym model was configured and measured, in the order it happened. It is written
by hand, never rewritten, and corrected only by an appended entry. Numbers are copied from the output of the
commands named next to them.

## Protocol (fixed before any holdout was evaluated)

1. The model is trained on `data/text-en/train.tsv` and nothing else; the inventory is `readings.tsv` and
   `forms.tsv`. The training code cannot open a holdout file (see `TrainingCorpus`).
2. Every choice (epoch count, calibration temperature, pruning threshold, natural-row weight) is made on
   `data/text-en/dev.tsv` only.
3. The quality gate (decision D13 of the Text.En plan) is evaluated on `holdout-natural.tsv` and has two
   conditions, both required:
   - the pooled end-to-end reading accuracy is at least 93.0% (930 per mille), where a row for which the analyzer
     emits no reading counts as wrong; and
   - the lower bound of the two-sided 95% percentile interval of the paired, document-clustered bootstrap of
     (model accuracy minus own-part-of-speech baseline accuracy) is above zero (10,000 resamples, seed 7).
4. The thresholds live in code (`EvaluationSettings.Default` in `TrainingSubsystem/TrainingRecord.cs`) and are
   recorded in `training.json` once a model is committed. They were written before the first holdout run and are
   not changed by results.
5. `holdout-independent.tsv` is reported but is not part of the gate.
6. The configuration is fixed on the development set first; only then is the gate run on the natural holdout.
   Every run of the gate is counted below. If the configuration changes after a holdout number has been seen,
   the change is recorded here as an additional gate run, and the earlier number stays in the log.

## Pre-registration of the gate (entry 1)

Written on 2026-11-17 at about 12:58 (UTC-05:00), before the first holdout evaluation. Holdout evaluations so
far: none.

- Gate thresholds in code: `GateMinPooledAccuracyPerThousand = 930` (named differently when first written; the value
  never changed), `ConfidencePercent = 95`, `Resamples = 10000`,
  `BootstrapSeed = 7`, lower bound strictly greater than zero.
- Source file that holds them, `TrainingSubsystem/TrainingRecord.cs`, SHA-256 at this moment:
  `4c63eef584ddbb69dd5671eb95301d719a0957c2b273aeb0679b8cd6ae4e870e`.
- Evaluator source, `TrainingSubsystem/HeteronymEvaluator.cs`, SHA-256 at this moment:
  `3e76aeeeecc2af3bec8c7c903da5cff1a53b4d3ea13056a364180e5f70471ccb`.
- Multi-seed study seeds: 1, 2, 3, 4, 5 (the committed seed is 1 and is never chosen from a holdout number).

### Development-set exploration (dev only; no holdout was read)

The pre-registered grid was the pruning threshold 0, 1, 2 times the natural-row weight 1, 2, 3, with the seed
(1), hash bits (22), scale (64), at most 12 epochs, and the epoch count and temperature chosen on the development
natural rows, as `train --model-dir` prints. The score is the number of the 1,301 development natural rows
(scored from the aligned, ambiguous rows only) the kept epoch decides correctly; ties go to the smaller model.

| nat. weight | prune | epochs | dev natural | dev all (of 3,034) | temp. | weights | bytes (deflate) |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 0 | 3 | 1266 | 2915 | 21.1 | 143,392 | 299,439 |
| 1 | 1 | 3 | 1266 | 2915 | 21.1 | 142,408 | 297,324 |
| 1 | 2 | 3 | 1266 | 2914 | 21.1 | 141,367 | 294,991 |
| 2 | 0 | 6 | 1264 | 2913 | 25.3 | 151,835 | 311,746 |
| 2 | 1 | 6 | 1264 | 2913 | 25.3 | 150,697 | 309,472 |
| 2 | 2 | 6 | 1264 | 2913 | 25.3 | 149,873 | 307,591 |
| 3 | 0 | 1 | 1267 | 2885 | 19.55 | 123,098 | 260,018 |
| 3 | 1 | 1 | 1267 | 2885 | 19.55 | 121,591 | 256,936 |
| 3 | 2 | 1 | 1267 | 2885 | 19.6 | 120,090 | 253,643 |

Chosen by the rule above: natural weight 3, prune 2 (the highest development natural score, 1,267, and the
smallest model among the ties). Honest remark: the difference between 1,267 and 1,266 is one row of 1,301, which
is within noise, and the chosen configuration keeps only one epoch and decides fewer development rows overall
(2,885) than the others (about 2,915). The rule was written before the grid was run and is applied as written.

Final configuration (now the defaults in `TrainingSettings.Default`): seed 1, 22 hash bits, scale 64, prune 2,
natural weight 3, up to 12 epochs, 1 epoch kept, temperature 19.6 (step 392 of 20). Image SHA-256 (uncompressed)
`73de5a3f74e34b9373d165e4bedd603988e5d1d81bab36fb6c3ab6444f603df0`; 120,090 weights; 253,643 bytes compressed,
363,326 bytes uncompressed; training and selection took 1.2 s (the data preparation, tokenizing and tagging
23,100 rows, 0.9 s). `train --check` on it exits 0. Development end-to-end accuracy through the analyzer:
95.30% (3,083 of 3,235 rows; own-part-of-speech baseline 88.56%, majority 65.87%, first candidate 60.28%;
improvement +6.74% with a 95% interval of +5.59% to +7.90% over 2,252 clusters); parity of the trainer with the
analyzer: 3,232 aligned rows compared, 0 mismatches, 0 suppressed. (The first grid cell, natural weight 1 and
prune 0, scored 96.23% on the same rows; it was not chosen because the pre-registered rule ranks by the
development natural rows, not by all development rows.)

## Gate runs

### Gate run 1 (the only gate configuration)

Run on 2026-11-17 at about 12:58 (UTC-05:00), after the configuration above was fixed on the development set,
with `evaluate --gate --seeds --model-dir data/text-en/work/final` (image SHA-256
`73de5a3f74e34b9373d165e4bedd603988e5d1d81bab36fb6c3ab6444f603df0`). Exit code 0.

Natural holdout (`holdout-natural.tsv`, 1,167 rows from 266 documents, all rows aligned and covered):

| measure | value |
| --- | --- |
| pooled end-to-end accuracy | 96.23% (1,123 of 1,167); threshold 93.0%: met |
| own-part-of-speech baseline | 90.66% (1,058 of 1,167) |
| majority-reading-by-form baseline | 82.01% (957 of 1,167) |
| first-candidate baseline | 67.78% (791 of 1,167) |
| improvement over own-part-of-speech | +5.57% |
| 95% paired interval, clustered by document | +3.16% to +8.15%; lower bound above zero: yes |
| trainer-versus-analyzer parity | 1,167 compared, 0 mismatched, 0 suppressed |
| confidence <0.600 / 0.600-0.899 / >=0.900 | 10 of 21 / 78 of 104 / 1,035 of 1,042 right |

Verdict: **PASS** (both conditions). By source: `harvest-masc` 98.64% (364 of 369), `harvest-masc-llm` 84.46% (125
of 148; own-part-of-speech 102), `harvest-silver-llm` 90.74%, `harvest-open-train-llm` 96.67%, the other strata 80%
to 100% on small counts. The 148 LLM-labeled MASC rows are where the improvement comes from, as the plan predicted.

Worst readings on the natural holdout (at least 5 rows): `live.adj` 3 of 5, `read.past` 11 of 17 (own-part-of-speech
17 of 17), `analysis.plural` 4 of 6, `august.month` 4 of 6, `reading.act` 21 of 29 (own-part-of-speech 29 of 29),
`row.line` 6 of 8, `use.habitual` 6 of 8, `insert.verb` 4 of 5, `put.verb` 4 of 5, `mobile.movable` 5 of 6,
`read.present` 17 of 20, `increase.noun` 7 of 8, `increase.verb` 8 of 9, `minute.time` 10 of 11,
`present.adj-noun` 15 of 16. Several readings where the own-part-of-speech baseline is exact (`read.past`,
`reading.act`) are decided worse by the model: the part-of-speech tag alone is a stronger cue there than the
context the model learned, which is the first thing to look at in a later improvement.

Multi-seed variance (same settings and the same development-set choices, only the seed changes):

| seed | natural | independent |
| ---: | ---: | ---: |
| 1 (committed) | 96.23% | 95.44% |
| 2 | 95.46% | 96.96% |
| 3 | 96.74% | 97.11% |
| 4 | 97.00% | 96.65% |
| 5 | 96.74% | 95.69% |
| mean / min / max | 96.43% / 95.46% / 97.00% | 96.37% / 95.44% / 97.11% |

Every seed clears 93.0% on the natural holdout, so the pass does not hinge on the committed seed.

Independent holdout (`holdout-independent.tsv`, 1,972 raw sentences written blind, reported, not part of the
gate), same model: 95.44% (1,882 of 1,972); own-part-of-speech baseline 91.23%, majority 51.37%, first candidate
51.17%; improvement +4.21%, 95% interval +3.04% to +5.43%; 0 parity mismatches. Worst readings (5 rows each):
`bass.fish` 0, `sewer.seamstress` 0, `number.noun` 1, `august.month` 2, `bow.weapon` 2, `frequent.verb` 2,
`put.verb` 2.
The tagger sees the part-of-speech-derived natural rows in its own training data, so the natural holdout flatters the
tag-dependent features; the independent holdout is the more realistic view of the tagger side, and the two numbers
agree to within a point.

Training time: 0.9 s to prepare (tokenize, tag, extract features of) 23,100 rows and 1.2 s to train 12 epochs and
select, on one thread (Release build, Windows, .NET 10). Model size: 120,090 weights, 253,643 bytes with Deflate,
363,326 bytes uncompressed, 22 hash bits (a 512 KiB occupancy bitmap at run time).

Evaluation record drafts: `evaluate --write` and `--check` into the git-ignored `data/text-en/work/eval-record` for
the same model (13.8 s and 13.1 s, mostly five training runs), the record is byte-identical on repeat. These re-read the
holdouts for the same model and configuration and are reports, not gate runs: nothing was changed after any holdout
number.

### Confirmation after code-only changes (not a new gate run)

After the gate run, three changes were made that cannot change any number: a rename of the gate threshold field and
its JSON key (the stored value is unchanged), the removal of hexadecimal literals and of a few words that the
spell checker flags, and a correction of the command so that `--gate` together with `--write` decides on the natural
holdout instead of the first set. The final model was then retrained from scratch (`train` gives image SHA-256
`73de5a3f74e34b9373d165e4bedd603988e5d1d81bab36fb6c3ab6444f603df0` again, `train --check` exits 0) and the gate
command was run once more on it with identical output (96.23%, lower bound +3.16%, PASS). So the natural holdout has
been read by the gate command twice for one configuration and one model; the configuration was not changed in
between and is not changed by this log.

### Confirmation after review fixes (not a new gate run)

Two corrections of the evaluate command followed a review, and neither can change a number: `evaluate --gate --check`
now exits `0` only when the gate passes and the record is current (before, a current record hid a failed gate), and the
evaluator stops with a data error (exit code `2`) when a harvested row has no source document in
`harvest-provenance.tsv`, instead of treating the row as an independent cluster. No model, number, setting or record
file changed. `train --check`, `evaluate --check`, `evaluate --gate` and `evaluate --gate --check` were run on the
committed files and exit `0` with identical output (266 clusters, lower bound +3.16%, upper bound +8.15%). These are
regression runs of the same model and configuration, not gate runs: the gate-run count stays at one configuration, and
no other reading of a holdout is recorded here.

### Full-corpus tokenization and parity check

`evaluate --set train` (and the development and holdout sets above) scores every row end to end through the analyzer
and through the trainer's own path and compares them. The stored sentences are space-tokenized MASC-style text; the
runtime tokenizer re-tokenizes them.

| set | rows | target is exactly one runtime token | single-candidate | parity compared / mismatched / suppressed |
| --- | ---: | ---: | ---: | --- |
| train | 19,865 | 19,857 (8 not aligned) | 1,387 | 19,857 / 0 / 0 |
| dev | 3,235 | 3,232 (3 not aligned) | 198 | 3,232 / 0 / 0 |
| holdout-natural | 1,167 | 1,167 | 0 | 1,167 / 0 / 0 |
| holdout-independent | 1,972 | 1,972 | 23 | 1,972 / 0 / 0 |

The 11 not-aligned rows of training and development are skipped and counted by the trainer and count as wrong in an
evaluation; no holdout row is affected. No row is suppressed (a reading covered by a normalization or an override), and
the accuracy of the final model on the training rows themselves is 98.12%.

Gate runs so far: 1 configuration, evaluated twice with identical output (see above).

## Shipping the model (phase B)

The quality gate passed, so the model was shipped with no change of any setting, choice or code that influences
training:

- `train` with the defaults wrote the committed locations: `src/DemaConsulting.Speech.Text.En/Resources/heteronyms.bin`
  (253,643 bytes with Deflate, 363,326 bytes uncompressed; embedded in the library) and
  `tools/DemaConsulting.Speech.Text.En.Trainer/training.json`. The image SHA-256 is again
  `73de5a3f74e34b9373d165e4bedd603988e5d1d81bab36fb6c3ab6444f603df0` (identical to the gated model), 1.0 s to prepare
  the rows and 1.1 s to train and select; `train --check` exits 0 (about 3 s).
- `evaluate --write` wrote the committed evaluation record in `docs/evaluation/text-en/` (7.4 s with five training
  runs); `evaluate --check` exits 0. Writing the record read both holdout sets once more for the same model. That is a
  report, not a gate run: no number was used to change anything.
- The shipped default analyzer was also exercised on 14 clear sentences written for the unit tests (not taken from any
  data set), covering `record`, `lead`, `read`, `wind`, `tear`, `bass` and `bow`. All 14 readings were as expected on
  the first run; no sentence was replaced.

Gate runs, final count: 1 configuration; the holdouts were read by the gate command twice and by the record writer for
the same model; the configuration was not changed after any holdout number. From phase B on the gate command is also run
as a regression check of the committed model (the trainer test
`TrainingSubsystem_Evaluate_CommittedModelAndData_PassesGate`, and the check of the committed evaluation record, read
the holdouts on every test run). Those runs repeat the same decision for the same model; they are not gate runs in the
sense of this log and nothing is chosen from them.

## External evaluation (P2-8)

**Protocol.** The embedded model is evaluated on two external, evaluation-only datasets: Google's WikipediaHomographData
(WHD) and the English read set of heteronym-bench. They are fetched on demand into the git-ignored cache
`data/text-en/external/` from commit-pinned archives and verified per file against SHA-256 pins; nothing of them is
committed, embedded or read by the training (the training loader opens exactly its four files). **No setting, row,
threshold or model is ever chosen from these numbers.** The run was done once, after the model was frozen and after the
gate. If a later result prompts a retrain, WHD and the benchmark are contaminated for that model and the record must
say so.

**The run (2026-11-17).** Command: `evaluate-external --fetch --write`, then `evaluate-external --check` (exit 0).
Model: the embedded model, image SHA-256 `73de5a3f74e34b9373d165e4bedd603988e5d1d81bab36fb6c3ab6444f603df0` (seed 1 of
the five-seed study; the retrained seed 1 model has the same image). Pins: WHD commit
`8f008f021e88f8b71118a27ae655f1f3121162bc` (325 files, 2,097,865 bytes), heteronym-bench commit
`064163ef097ece80d9b8319abf5612b2b8a3968b` (3 files, 169,232 bytes). Mapping: `whd-mapping.tsv` (326 ids) and
`bench-mapping.tsv` (60 keys, each confirmed against the benchmark's own description of the key, including the subtle
pairs for `axes`, `does`, `dove`, `wound` and `bass`).

| set | rows read | used | skipped | accuracy (95% interval) | own tag | majority | always default |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: |
| whd-eval (headline) | 1,615 | 1,615 | 0 | 93.00% (91.70 to 94.18) | 90.59% | 69.23% | 66.75% |
| whd-train | 14,487 | 14,487 | 0 | 91.60% (91.14 to 92.05) | 89.74% | 69.55% | 68.04% |
| bench | 566 | 299 | 267 `duplicate-of-whd` | 92.98% (89.97 to 95.65) | 80.27% | 40.13% | 43.81% |

- The improvement over the own-part-of-speech baseline is +2.41 points on `whd-eval` (interval +1.05 to +3.78), +1.86
  on `whd-train` (+1.37 to +2.35) and +12.71 on `bench` (+8.36 to +17.06). The parity check has 0 mismatches and 0
  suppressed rows on every set; 25 `whd-eval` rows (314 on `whd-train`) are not exactly one analyzer token and count as
  wrong.
- Seeds 1 to 5: `whd-eval` mean 92.72% (92.38 to 93.00), `whd-train` mean 91.70% (91.48 to 91.94), `bench` mean 94.65%
  (91.97 to 96.66). The per-seed intervals are in `docs/evaluation/text-en/external/seeds.tsv`.
- Skipped rows: none for an unmapped label, an unknown form, a non-candidate reading or bad offsets; 267 bench items are
  copies of WHD sentences (241 of WHD train, 26 of WHD eval) and are evaluated as part of WHD. No evaluated sentence
  equals a training or development sentence.
- The numbers are informational, not a gate. They are lower than the in-domain holdouts (96.23% natural, 95.44%
  independent), which is expected for Wikipedia prose and WHD's label conventions; the caveats (domain, our mapping,
  coverage of 162 plus 30 of 207 words, the unverified bench labels, sentence-level clusters) are in the record README.
  The low-confidence threshold of 0.60 is not changed by this run; `confidence.tsv` is the evidence for a later
  decision.
