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

## Adjective cue (2026-11-20)

**Trigger.** A listener flagged *these little boats were grey and very minute and clear*: the analyzer chose
`minute.time` at 0.59 although the tagger tagged *minute* `ADJ`. The construction (a degree adverb before the word, an
adjective coordinated with it) is grammar that the sparse features see too rarely to weigh.

**Change.** No data, setting or model changed. `HeteronymClassifier` adds a fixed prior of
`round(scale * temperature * 2.2)` (odds of about 9 to 1 on the model's calibrated scale) to every candidate whose
`pos_hint` includes `ADJ` when the tagger tags the word `ADJ` and a degree adverb tagged `ADV` stands right before it or
a coordinating conjunction joins it to a word tagged `ADJ`. The prior, the list of 29 degree adverbs and the two
constructions were fixed from the flag and from linguistic reasoning before any set was read; the size (ln 9) was not
tuned. `train --check` exits 0: the training never sees the prior, so the model and `training.json` are unchanged (image
SHA-256 `73de5a3f74e34b9373d165e4bedd603988e5d1d81bab36fb6c3ab6444f603df0`).

**Development set** (read first, the only set that could have changed the design): the cue fired on 22 words, all
already right, raising their confidence; no decision changed (3,083 of 3,235 before and after). Nothing was changed
after this run.

**Gate run** (the only holdout read for this change, then the record writer): `evaluate --gate` passed with the same
figures as before, 96.23 percent (1,123 of 1,167) on the natural holdout, improvement +5.57 points (+3.16 to +8.15). The
independent holdout is unchanged for the shipped model (1,882 of 1,972); seeds 3 to 5 each read one word fewer
(`seeds.tsv`), and the confidence strata moved (`confidence.tsv`). `evaluate --write` and `evaluate --check` exit 0.

**External record** (regenerated because the analyzer changed; informational, nothing chosen from it): `whd-eval` 1,502
to 1,501 of 1,615 (93.00 to 92.94 percent; one `frequent.verb` row on which the cue fired, the tagger having tagged the
verb `ADJ`), `whd-train` 13,270 of 14,487 unchanged (one `mobile.movable` row gained, one `live.verb` row lost), `bench`
unchanged.

**Flagged sentence after the change:** `minute.tiny` at 0.861. *They were very close* (`close.adj-noun`), *She was so
content* (`content.satisfied`), *Wait a minute* (`minute.time`) and *Most content is free* (`content.noun`) are read as
before.

## Noun-form, past-time and context cues (2026-11-20)

**Trigger.** A listener flagged *It has been compared to a knife wound or a deep muscle tear*: the analyzer chose
`wind.coil` for *wound* at 0.55 (the tagger tagged it `NOUN`) and `tear.drop` for *tear* at 0.72.

**Experiments, not kept.** (1) A part-of-speech cue for every word whose `NOUN` or `VERB` tag allows exactly one
candidate, at log-odds 2.2, 4.6 and a hard constraint: development 3,083 to 3,050, 3,028 and 3,017 of 3,235, and
`whd-eval` 1,501 to 1,490 of 1,615 (tagging errors on noun/verb heteronyms); `NOUN` only 3,049, `VERB` only 3,062. (2)
The same cue limited to forms that rule a reading out by themselves (*wound* under `wind.coil`) at log-odds 4.6: one
development row lost (*The tape wound onto the spool*, the verb tagged `NOUN`). (3) A fourth synthetic round of 110
reviewed rows (noun uses of `tear.rip`, *wound* after a modifier, *tear gas*) and retraining: development 3,091, natural
holdout 1,128 (seed mean 96.43 to 97.02 percent), independent holdout 1,894, but the retrained single-epoch model
re-rolled readings of the golden sentences (*The bass player caught a bass*, *He had a tear in his eye*, *I read every
night*, *this minute detail*) and of the analyzer test *Yesterday I read the whole book*; 30 more rows for *read* made
it worse. The data, the model and the records were restored.

**Change.** No data, setting or model changed (`train --check` exits 0, image SHA-256
`73de5a3f74e34b9373d165e4bedd603988e5d1d81bab36fb6c3ab6444f603df0`). `HeteronymClassifier` adds three priors on the
model's calibrated scale: the noun-form cue (log-odds 1.0) to the only candidate that can be a noun of the form when
the word is tagged `NOUN` and the form rules out another candidate's noun use (`ReadingInventory.CanBeNoun`); the
past-time cue (2.2) to a past-tense candidate when the sentence has *yesterday*, *ago*, *previously* or *last* before
a word of time and the word does not follow an auxiliary or a particle; and context cues (2.2) to `tear.rip` when one
of 23 words of the body or of materials is within three words.

**Development set and gate run:** 3,083 of 3,235 unchanged; `evaluate --gate` passed with the same figures, 96.23
percent (1,123 of 1,167) on the natural holdout; the independent holdout is 1,882 of 1,972 unchanged; the confidence
strata moved by one development word. `evaluate --write` and `evaluate --check` exit 0.

**External record** (informational): `whd-eval` 1,501 to 1,502 (`tear.rip` 3 to 4 of 9), `whd-train` 13,270 to 13,278
(`tear.rip` 39 to 44 of 57, `wound.injury` 56 to 59 of 61), `bench` 278 to 279 (`tear.rip` 7 to 8 of 8); no reading
lost a row.

**Flagged sentence after the change:** `wound.injury` at 0.689 and `tear.rip` at 0.774. *A tear ran down her cheek*
(`tear.drop`), *The tape wound onto the spool* (`wind.coil`) and *Yesterday I read the whole book* (`read.past`) are
read as expected.

## Word-cluster subsystem (PR A): protocol and pre-registration (2026-11-20)

Written on 2026-11-20, before the generalization set was written, before any cluster corpus was fetched and before any
model with word clusters was trained. Evaluations of the generalization set so far: none (the set does not exist yet).
Sets read for this entry: none.

**Purpose.** The cue priors of the two entries above were written for single flagged sentences. Word classes induced
from a large public-domain corpus (hierarchical clusters of words that occur in similar contexts) could let the model
generalize from *muscle tear* to *hamstring tear* without a hand-written word list. PR A builds the evaluation sets and
the clustering subsystem only; it changes no feature, setting or model, so `train --check` and `evaluate --check --gate`
stay exactly as they are. PR B trains with the clusters and is judged by the criterion pre-registered below.

### Decisions fixed for PR A

1. **Corpus.** The 269 Project Gutenberg books of the PartOfSpeech corpora (with their recorded US public-domain
   checks), minus every book that holds a development or natural-holdout sentence (26 dev and 19 holdout books, 224
   remain), plus about 50 domain books published in 1929 or earlier (anatomy and first aid, textiles, sport, music,
   clocks), each with a recorded license check. No US-government source and no CC0 training sentence is part of the
   corpus.
2. **Leakage audit.** Before clustering, every corpus sentence is compared with every evaluation sentence
   (development, natural holdout, independent holdout, regression overlay and generalization set): exact, near
   duplicate and contained passages are removed and counted.
3. **Fetching.** Only from an official Project Gutenberg mirror by automated means (never `www.gutenberg.org`), with a
   polite rate limit; every book body is pinned by its SHA-256, and the normalized corpus is also kept as a
   byte-reproducible snapshot archive whose SHA-256 is recorded (not published).
4. **Clustering.** Our own deterministic Brown-style agglomerative clustering in C# with **C = 512 clusters**: integer
   counts, the trainer's software logarithm, a fixed iteration order, ties broken by the lowest index and explicit
   barriers, so the result is the same for any thread count and on every operating system. 1,000 clusters are allowed
   later only after a measured development-set benefit, read before any generalization read.
5. **Runtime lookup.** When two vocabulary words share a runtime hash, every entry of that hash is dropped, so a word
   never inherits another word's cluster.
6. **No feature or model change in PR A.** The classifier, its weights and the cue priors stay exactly as they are.

### Vocabulary preflight rule (fixed before the preflight runs)

- The minimum corpus count of a vocabulary word is 5.
- The candidate vocabulary sizes are 30,000, 40,000, 50,000, 60,000 and 75,000 words.
- The vocabulary size is the smallest candidate that contains every nominated cue word with a corpus count of at least
  5, capped at 75,000. A nominated word beyond the cap is reported as "not in cut", and one with a count below 5 as
  "too rare"; neither moves the cap.
- The preflight reports, per candidate, the token coverage and, per nominated word, its count, rank and whether it is
  in the cut. It never reads `generalization.tsv`.

**Nominated cue words** (fixed list, lower case):

- the 23 context-cue words of the classifier: muscle, muscles, ligament, ligaments, tendon, tendons, hamstring,
  meniscus, cartilage, rotator, cuff, acl, labrum, groin, fabric, cloth, canvas, sail, seam, sleeve, lining, denim,
  wear;
- injuries, materials, instruments and other domain words: gash, gashes, laceration, lacerations, sprain, bruise, stab,
  knife, bullet, sinew, calf, thigh, shin, bandage, garment, linen, silk, satin, tarpaulin, curtain, paper, clock,
  watch, spool, reel, bobbin, coil, fiddle, cello, viola, drum, trout, perch, pike, angler, rod, microscopic,
  infinitesimal, hour, second;
- the 29 degree adverbs of the classifier (very, so, too, quite, extremely, rather, most, more, less, really,
  incredibly, exceedingly, remarkably, fairly, somewhat, highly, utterly, terribly, awfully, perfectly, completely,
  entirely, absolutely, particularly, especially, unusually, surprisingly, equally, pretty) and astonishingly,
  uncommonly, infinitely, singularly, wonderfully;
- the past-time words yesterday, ago, previously, formerly, earlier, beforehand.

### Generalization set protocol

1. The writer prompt (`data/text-en/prompts/generalization.md`) is committed first. It fixes the lemmas and readings,
   about 9 rows per reading (about 240 rows, to allow drops), the kind (`novel-vocabulary` or `novel-construction`),
   family and cue-form designation of every row, the rows that the curated cues cannot solve (they avoid the degree
   adverbs, the past-time words, *last* before a word of time, the context-cue words and a coordinating conjunction
   joining the target to an adjective), 6 to 30 words, one target occurrence, original text and no quotations, the
   output schema and a self-validation checklist.
2. A folder outside the repository holds only `readings.tsv`, `forms.tsv` and the prompt. One `general-purpose`
   sub-agent with the model `gpt-5.6-luna` (a different family from the developer, the family that wrote the
   independent holdout) writes the set there. It may not read any other file, repository, web page, model, evaluation
   or cluster output.
3. `check-data --generalization <file>` checks the staged copy (schema, the novelty rules N1 to N3 against the final
   training, development, holdout and regression data, and the shape quotas). Failing row ids go back to the same
   writer with a generic reason only; the writer replaces the rows. No model output is ever consulted.
4. A blind label review by a second sub-agent (`claude-sonnet-5.5`) sees the sentence, the target span and the
   candidate readings with their descriptions, not the writer's label. A disagreement drops the row; nothing is
   relabeled. The review is committed as `audit/generalization-review.tsv`.
5. Freeze: the set, the review and the manifest are committed and the commit SHA, the model names and the dispatch
   times are recorded here. Only then is the baseline measured, the corpus audited and the clustering run.
6. A later edit only fixes a defect, is logged here, and is never informed by a model result.

### Pre-registered PR B acceptance criterion (2026-11-20)

Fixed before any PR B training and before the generalization set exists.

- **Models.** A is PR B's final pipeline with the committed 512-class word table. B is the identical pipeline (same
  data, settings, curated-feature configuration and training seed 1) with an empty table (`--clusters none`). Both
  are evaluated end to end through the analyzer on `generalization.tsv`.
- **Primary endpoint.** Delta = accuracy(A) minus accuracy(B), in basis points. Its uncertainty is the evaluator's
  integer paired percentile bootstrap that resamples whole families (the `family` column, the cue and template
  family) with replacement: 10,000 resamples, generator seed 7, 95 percent interval.
- **Pass** if and only if Delta is at least +300 basis points (3.0 points) **and** the lower bound of the 95 percent
  interval is above 0 basis points. Also required for robustness: the mean Delta over training seeds 1 to 5 is above
  zero.
- **Secondary, reported, not gating.** Delta with intervals on the `novel-vocabulary`, `novel-construction` and
  `cue_unsolvable=yes` subsets; a 2 by 2 ablation {clusters off, on} by {curated features off, on}: four models trained
  identically and evaluated in one run on the generalization set, the regression overlay (seen and unseen rows) and the
  development set (trainer level and end to end), with the main effect of clusters (the mean over the curated levels)
  and the interaction (on,on) minus (on,off) minus (off,on) plus (off,off), with family-clustered intervals on the
  generalization set.
- **Guards, gating in PR B.** The legacy natural holdout stays at or above 1,123 of 1,167 (read once); every golden
  test passes, including the 385 end-to-end rows of `readings-golden.tsv`; the final model's generalization accuracy
  is at least the PR A baseline (the shipped model with its cue priors).
- **Discipline.** The generalization set is read once for this comparison, all four models in the same run, and the
  read is logged here. Any data or setting change after the read voids the result and needs a new pre-registration.
  Moving from 512 to 1,000 clusters needs a development-set benefit measured before the read.
- **PR A baseline cells.** The shipped model (clusters off, curated on, the cue priors) and the model alone (clusters
  off, curated off), on both overlays, are recorded in `docs/evaluation/text-en/curated-ablation.tsv` and here.

### Fresh natural audit set (plan only; not built in PR A)

The natural holdout was seen by the upstream part-of-speech tagger, and earlier gate runs informed decisions. A fresh
natural audit set is planned:

- about 1,200 natural rows, stratified by lemma;
- drawn from public-domain documents (published in 1929 or earlier, fetched from the mirror) that are in none of the
  PartOfSpeech corpora (the 269 books, MASC, the community sentence collection and the US-government sets) and not
  in `cluster-sources.tsv`;
- the draw is fixed in advance from the mirror catalog with a recorded seed;
- labels from two blind language-model families, with adjudication of disagreements;
- frozen before PR B training and read once, as PR B's final natural audit.

The existing natural holdout stays as the legacy non-regression gate (at least 1,123 of 1,167).

## Word-cluster subsystem (PR A): generalization set frozen (2026-11-21)

Written when the set was frozen (2026-11-21, 01:48 UTC), before any model, evaluation or cluster output on it existed.
Evaluations of the generalization set so far: none. Sets read for this entry: none. The freeze commit SHA is recorded in
the next entry (a commit cannot name itself).

**Writing.** The writer prompt (`data/text-en/prompts/generalization.md`) was committed first (`abae8f2`). The staging
folder outside the repository held only `readings.tsv`, `forms.tsv` and the prompt; the corpus had not been fetched and
no clustering had been run. Times are UTC, from the files of the staging folder.

| Round | Writer instance | Rows written or replaced | `check-data --generalization` of the staged copy after |
| --- | --- | ---: | --- |
| 1 | `gpt-5.6-luna` (first output 2026-11-20, about 23:46) | 243 written | 77 findings on 61 rows (45 N2, 16 N3) |
| 2 | `gpt-5.6-luna`, new instance | 61 replaced | 20 rows (13 N2, 7 N3) |
| 3 | `gpt-5.6-luna`, new instance | 20 replaced | 2 rows (`gen-0130` N2, `gen-0160` N3) |
| 4 | `gpt-5.6-luna`, new instance | 2 replaced | no finding |
| 5 | `gpt-5.6-luna`, new instance (2026-11-21, 01:40 to 01:42) | 11 rewritten after review 1 | `gen-0125` N3: dropped |

**Deviations from the protocol above.**

1. *Same writer* (step 3): the sub-agent tool runs a writer to completion and cannot send it a later message, so each
   replacement round used a new `gpt-5.6-luna` instance under the same isolation rules. Each round received only row
   ids and the generic reason, never a training sentence or the matching cue word.
2. *A disagreement drops the row* (step 4): the 11 rows that review 1 did not confirm were rewritten instead of dropped.
   They were writing defects found before the freeze and before any model output was seen (a bare *read* without a sign
   of its tense, *bow* without a sign of its sense, one *tear* and one *minute* whose context pointed the other way).
   The new writer made the intended reading clear and kept the labels; nothing was relabeled. The rewritten rows went
   through the novelty check again and a second blind review.

**Blind label review** (`claude-sonnet-5.5`; it saw the id, sentence, span, target and the candidate readings with their
descriptions in a shuffled order with seed 20261120, not the writer's label):

- review 1 (2026-11-21, output 01:39), 243 rows: 232 agree; 11 disagree or `unclear`: `gen-0009`, `gen-0067`,
  `gen-0082`, `gen-0083`, `gen-0085`, `gen-0086`, `gen-0087`, `gen-0088`, `gen-0093`, `gen-0122`, `gen-0125`;
- review 2 (a new instance, 2026-11-21, output 01:43), the 11 rewritten rows: 11 agree, high confidence;
- `gen-0125` is dropped (its rewritten sentence failed N3) and not rewritten again.

**Frozen set.** `data/text-en/generalization.tsv`: 242 rows, every label confirmed by a blind reviewer (review 1 for 232
rows, review 2 for the 10 rewritten rows kept); the bytes of every row are the writer's. 64 families (3 to 9 rows), 135
`novel-vocabulary` and 107 `novel-construction` rows, 81 `cue_unsolvable=yes` rows over 13 targets; 9 rows per reading
except `bow.bend` (8) and `wind.coil` (27). `check-data` passes with the shape quotas. The review is
`data/text-en/audit/generalization-review.tsv` (254 lines: one per row and review round).

## Word-cluster subsystem (PR A): freeze commit and first overlay evaluation (2026-11-21)

**Freeze commit.** The generalization set, its blind review and the manifest were frozen in commit
`2869a472fb2a3b638190394a71b2f8ba4f210316` ("Text.En: blind generalization set, frozen").

**Overlay evaluation, first run.** With the overlay evaluation of the trainer (`evaluate --set regression --set
generalization`, the shipped model with image SHA-256 `73de5a3f...3df0`; the code was not yet committed), run once:

- Regression overlay (519 rows, each its own cluster; 95 percent intervals): with the cue priors 519 of 519 (100.00
  percent); the model alone 515 of 519 (99.23 percent, 98.46 to 99.81); cue effect +0.77 points (+0.19 to +1.54); a
  cue prior acts on 37 rows and changes the reading of 4. Seen rows (25): 25 against 24 (+4.00, 0.00 to +12.00).
  Unseen rows (494): 494 against 491 (+0.61, 0.00 to +1.42). By origin, with cues against model alone: `listening` 7
  against 4 of 7 (+42.86, +14.29 to +85.71), `data-row` 21 against 20 of 21, `analyzer-test` 11 and 11 of 11,
  `golden-pipeline` 37 and 37 of 37, `golden-readings` 382 and 382 of 382, `golden-sentences` 61 and 61 of 61. Parity
  519 compared, 0 mismatched, 0 suppressed; own-part-of-speech baseline 471 of 519.
- Generalization set: **stopped by the cue-unsolvable check before any figure of the set was printed or seen**:
  `gen-0060` (*Beneath the pulley, the cord wound.*, `wind.coil`, `cue_unsolvable=yes`) is one the curated cues can
  act on: the tagger tags *wound* there as a noun, so the noun-form prior is added and the reading with the priors
  differs from the model alone. This is a data error of the frozen set (the row does not belong to the cue-unsolvable
  subset), found by a structural check, not by an accuracy. It is reported to the owner for a decision under protocol
  step 6 (a defect fix, logged here); the row was not edited. No accuracy of the generalization set has been read.

## Word-cluster subsystem (PR A): defect fix of the generalization set, gen-0060 (2026-11-21)

**Defect fix (protocol step 6).** The `cue_unsolvable` flag of `gen-0060` (*Beneath the pulley, the cord wound.*,
`wind.coil`, `novel-construction`, family `wound:cordage`) was corrected from `yes` to `no`, because the noun-form cue
detector fires on the row (the cue-unsolvable validity check of `evaluate`, exit code 2). A row on which a curated cue
acts is by definition not cue-unsolvable. Only the flag changed: the sentence, label, kind and family are unchanged and
the row stays in the set. No accuracy figure of the generalization set had been produced or seen. The set keeps 242
rows; it now has 80 `cue_unsolvable=yes` rows (54 `novel-vocabulary`, 26 `novel-construction`) over all 13 targets, and
the shape quotas still hold. The fix is also recorded in the prompt's record (`data/text-en/prompts/generalization.md`)
and the data README; `MANIFEST.tsv` was regenerated.

## Word-cluster subsystem (PR A): baseline of the shipped model on the overlays (2026-11-21)

**Run.** With the overlay evaluation committed in `0f12855` ("Text.En: overlay evaluation with and without the curated
cue priors") and the generalization set as fixed in `10ac6ad` (the `gen-0060` flag), `evaluate --set regression --set
generalization` was run once with the shipped model (image SHA-256 `73de5a3f...3df0`, no word clusters), exit 0; the
cue-unsolvable check passed on all 80 marked rows. Then `evaluate --write` wrote the record: the data-set lines of
`summary.tsv`, `by-source.tsv`, `per-reading.tsv` and `confidence.tsv` are byte-identical and the overlays are
appended after them (`git diff` shows only added lines: 2, 12, 411 and 8), `gate.tsv` and `seeds.tsv` are unchanged,
and `overlay-rows.tsv` and `curated-ablation.tsv` are new. `evaluate --check --gate` then passed with the same figures
(dev 3,083 of 3,235; natural holdout 1,123 of 1,167, 96.23 percent, +5.57 [+3.16, +8.15]; independent holdout 1,882 of
1,972) and the record current. These holdout reads repeat the decision for the same model; they are not gate runs and
nothing was chosen from them.

**Generalization set** (242 rows, 64 families; 95 percent intervals clustered by family; with the cue priors against
the model alone; cue effect in points; rows a cue prior acts on / rows whose reading it changes):

- all: 177 against 178 of 242, 73.14 [67.05, 79.10] against 73.55 [67.51, 79.34]; cue effect -0.41 [-1.29, 0.00];
  13 / 1.
- `novel-vocabulary` (135 rows, 51 families): 100 against 100, 74.07 [65.22, 82.17] both; 0.00 [0.00, 0.00]; 8 / 0.
- `novel-construction` (107 rows, 39 families): 77 against 78, 71.96 [63.71, 80.00] against 72.90 [64.56, 81.00];
  -0.93 [-3.30, 0.00]; 5 / 1.
- `cue_unsolvable=yes` (80 rows, 51 families): 65 against 65, 81.25 [71.79, 89.87] both; 0.00 [0.00, 0.00]; 0 / 0.
- `cue_unsolvable=no` (162 rows, 64 families): 112 against 113, 69.14 [61.44, 76.47] against 69.75 [62.33, 76.82];
  -0.62 [-2.01, 0.00]; 13 / 1.
- Own-part-of-speech baseline 157 of 242 (64.88 percent); improvement +8.26 [0.00, +16.53] over 64 families. Parity
  242 compared, 0 mismatched, 0 suppressed. The one changed row is `gen-0060`: the noun-form cue turns the model's
  right `wind.coil` into `wound.injury`. Weakest readings: `tear.rip` 0 of 9, `bass.fish` and `bass.music` 4 of 9,
  `bow.weapon` and `content.satisfied` 5 of 9.
- The PR B guard "generalization accuracy at least the PR A baseline of the shipped model" is therefore 177 of 242
  (73.14 percent).

**Regression overlay** (519 rows, each its own cluster): identical to the first run logged above: all 519 against 515
(100.00 [100.00, 100.00] against 99.23 [98.46, 99.81]; +0.77 [+0.19, +1.54]; 37 / 4); seen 25 against 24 (96.00
[88.00, 100.00]; +4.00 [0.00, +12.00]; 18 / 1); unseen 494 against 491 (99.39 [98.58, 100.00]; +0.61 [0.00, +1.42];
19 / 3); by origin `listening` 7 against 4 (57.14 [14.29, 85.71]; +42.86 [+14.29, +85.71]; 6 / 3), `data-row` 21
against 20 (95.24 [85.71, 100.00]; +4.76 [0.00, +14.29]; 20 / 1), `analyzer-test` 11 and 11 (2 / 0),
`golden-pipeline` 37 and 37 (2 / 0), `golden-readings` 382 and 382 (4 / 0), `golden-sentences` 61 and 61 (3 / 0).
Own-part-of-speech baseline 471 of 519; parity 519 compared, 0 mismatched. The four changed rows are the listening
sentences of `minute.tiny`, `wound.injury` and `tear.rip` and the data row of `read.past`; the model alone gets each
wrong.

These are the PR A baseline cells of the pre-registered 2 by 2 ablation (clusters off, curated cues on and off). No
model, classifier or cue was changed, and the generalization set was not changed after this read.

## Word-cluster subsystem (PR A): corpus pins and leakage audit (2026-11-21)

Written after the corpus was pinned and audited and **before the vocabulary preflight was run**. No cluster table, model
or evaluation output existed. Sets read for this entry: none (the audit compares sentence text only; no model reads
them).

**Pins (`data/text-en/cluster-sources.tsv`, 336 rows, SHA-256 `749286ba...a8c0`).**

- *Core*: the 269 Project Gutenberg books of the PartOfSpeech corpora (`data/open/sources.tsv`: 175,
  `data/silver/sources.tsv`: 94, read at PartOfSpeech `faae816`), each with its recorded US public-domain basis copied
  verbatim. The development and natural-holdout books were recomputed from `harvest-provenance.tsv` (the source document
  of every `dev.tsv` and `holdout-natural.tsv` row): 26 development books (`excluded-dev`) and 19 natural-holdout books
  (`excluded-holdout`), the 45 books of the plan, disjoint, all among the 269. **224 core books are used.** The
  independent holdout has no harvest provenance and derives from no book. The RDF metadata of every used core book was
  fetched and re-checked: the rights statement of all 224 is "Public domain in the USA.", so no core book was dropped.
- *Domain*: the 67 candidates of the plan (anatomy and first aid 18, textiles and sewing 14, sport 16, music 15, clocks
  4), each checked with the rules R1 to R8 from its RDF and body. Publication-year evidence: `marc906` or `marc260` when
  the RDF has it (7 of the accepted books), otherwise the latest year printed in the first 200 body lines (arabic or
  roman numerals); in that case the `year` column is `-` and the basis states the evidence. **56 accepted** (anatomy 16,
  textiles 10, sport 14, music 13, clocks 3); **11 rejected** with the reason recorded and no pin:
  - `rejected-license` (8): R3, no publication year found: 6129 (*Healthful Sports for Boys*), 20776 (*Encyclopedia of
    Needlework*), 59425 (*The Seaman's Medical Instructor*), 65987 (*Strength and How to Obtain It*); R7, a year from
    1930 to 2099 in the first 200 body lines: 15147 (*Beeton's Book of Needlework*, a 1986 facsimile edition), 17571
    (*Piano tuning*, a 1975 Dover republication), 40540 (*The Complete Opera Book*, an introduction naming 1997), 25731
    (*Ancient Egyptian and Greek Looms*: the year is "B.C. 2000", a false positive of the mechanical rule, applied as
    specified);
  - `rejected-content` (3): R6, a body under 50,000 bytes: 57273 (45,451) and 69222 (42,267); 17576 (*A
    Rudimentary Treatise on Clocks, Watches and Bells*): no plain-text file on the mirror (TeX and PDF only).
  - No candidate failed R1, R2, R4 (no creator died after 1929), R5 (no government creator or printer) or R8 (no
    duplicate edition of a core or another selected book; the three volumes of *General Anatomy* and of the
    history of British boxing are separate volumes).
- **280 books are used** (224 core, 56 domain). No US-government source and no CC0 training sentence is in the corpus.

**Fetch.** `cluster --fetch --write-pins --delay-ms 2000` from `https://gutenberg.pglaf.org` only (never the main web
site): 582 requests (the text and RDF of the 291 books then listed as used, sequential, at least 2 seconds apart) in
51 minutes 54 seconds; one 404 (17576). The rejected domain books were then set to their rejected status with `-` pins.
A second `cluster --fetch --delay-ms 2000` reported all 280 used books `cached` against their pins with 0 requests. The
raw downloads were deleted from the cache afterwards (the normalized bodies, 150 MB, and the RDF files are kept in the
git-ignored `data/text-en/external/cluster-corpus/`).

**Content-level leakage audit.** `cluster --audit --write --threads 28` (exit 0, 7.9 seconds wall clock: tokenization of
31,363,332 tokens in 3.0 seconds, audit in 3.5 seconds) compared every corpus sentence with all 7,135 evaluation
sentences (`dev.tsv` 3,235, `holdout-natural.tsv` 1,167, `holdout-independent.tsv` 1,972, `regression.tsv` 519,
`generalization.tsv` 242). Removed corpus sentences / matched evaluation sentences / books, per file and rule:

| Evaluation file | exact | near | contains |
| --- | --- | --- | --- |
| `dev.tsv` | 1 / 1 / 1 | 0 / 0 / 0 | 1 / 1 / 1 |
| `holdout-natural.tsv` | 1 / 1 / 1 | 0 / 0 / 0 | 1 / 1 / 1 |
| `holdout-independent.tsv` | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 |
| `regression.tsv` | 1 / 1 / 1 | 0 / 0 / 0 | 1 / 1 / 1 |
| `generalization.tsv` | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 |

6 sentences were removed from 5 books (`cluster-removals.tsv`, SHA-256 `84794fd8...e77b`). All six were checked by hand
against the evaluation sentence and the corpus paragraph: two sentences of *Jude the Obscure* (153) that a development
row and a natural-holdout row repeat through community-collection quotations of Hardy; a short holdout sentence ("If so,
what does it mean?") inside a sentence of *Dracula* (345); a Bible quotation of a development row in *Gleanings by the
Way* (38644); "Water is a compound of hydrogen and oxygen." of a regression row in *The Gospel of Evolution* (36270); a
regression data row's sentence in *The Rise of the Dutch Republic* (4823). Each removal is a genuine repetition or
quotation; none of the generalization set or the independent holdout occurs in the corpus.

**Next, fixed before running it.** The vocabulary preflight follows the rule pre-registered above ("Vocabulary preflight
rule"): minimum count 5; the smallest of 30,000, 40,000, 50,000, 60,000 and 75,000 that contains every nominated cue
word with a count of at least 5, capped at 75,000; the fixed nominated list; the generalization set is not read.

## Word-cluster subsystem (PR A): vocabulary preflight, induction and re-induction check (2026-11-21)

**Vocabulary preflight** (run after the entry above, by the pre-registered rule): `cluster --preflight --min-count 5
--threads 28` (exit 0, 12.3 seconds). The corpus after the removals has 31,363,141 tokens, 222,326 eligible types and
66,794 words with a count of at least 5. Token coverage: 30,000 words 97.99 percent, 40,000 98.56, 50,000 98.89,
60,000 99.11, 75,000 99.22. Nominated words (count, rank):

Each item gives the word, its count and its rank; all words of an item enter the cut at 30,000 unless stated.

- muscle 1,790 (1,401), muscles 3,613 (681), ligament 407 (5,235), ligaments 470 (4,683), tendon 403 (5,275), tendons
  479 (4,612), cartilage 362 (5,722), cuff 325 (6,196), groin 85 (15,122);
- **hamstring 7 (rank 53,818): in the cut from 60,000, the word that sets the size**;
- fabric 491 (4,498), cloth 2,182 (1,120), canvas 401 (5,289), sail 901 (2,707), seam 802 (2,987), sleeve 674 (3,490),
  lining 466 (4,721), wear 1,335 (1,889);
- gash 42 (22,527), gashes 13 (40,219; from 50,000), laceration 88 (14,813), lacerations 12 (41,986; from 50,000),
  sprain 41 (22,949), bruise 99 (13,759), stab 55 (19,509), knife 1,689 (1,490), bullet 419 (5,123), sinew 25 (29,612);
- calf 603 (3,841), thigh 643 (3,633), shin 71 (16,908), bandage 545 (4,143);
- garment 341 (5,995), linen 1,133 (2,197), silk 1,852 (1,346), satin 357 (5,796), tarpaulin 21 (32,373; from 40,000),
  curtain 530 (4,239), paper 3,608 (683);
- clock 1,106 (2,233), watch 2,735 (898), spool 30 (27,008), reel 137 (11,242), bobbin 264 (7,197), coil 218 (8,241);
- fiddle 234 (7,855), cello 33 (25,488), viola 174 (9,607), drum 347 (5,906);
- trout 1,027 (2,384), perch 365 (5,691), pike 200 (8,741), angler 276 (6,968), rod 790 (3,030);
- microscopic 105 (13,281), infinitesimal 51 (20,298), hour 8,137 (324), second 9,537 (275);
- the 29 degree adverbs and the five added ones: counts 44 (astonishingly) to 81,575 (so), ranks 48 (so) to 21,886
  (astonishingly);
- yesterday 1,532 (1,638), ago 3,643 (675), previously 1,462 (1,716), formerly 1,306 (1,921), earlier 1,258 (1,988),
  beforehand 384 (5,479);
- too rare (count below 5): meniscus 4, rotator 1, acl 0, labrum 4, denim 1.

Every nominated word with a count of at least 5 is in the cut at 60,000 and not at 50,000 (hamstring), so by the rule
**the vocabulary size is 60,000**. The five modern words (meniscus, rotator, acl, labrum, denim) are "too rare" in
pre-1930 text, as the plan expected; they do not move the size. The generalization set was not read.

**Induction.** `cluster --write --clusters 512 --max-words 60000 --min-count 5 --threads 28` (exit 0; i7-13850HX, 28
logical processors): tokenization 3.1 seconds, audit 4.3 seconds (removals current), counting 4.0 seconds (60,000 words,
3,141,914 bigram types), induction 1 minute 10.7 seconds (the first progress line, after 1,000 words, estimated 3
minutes 6 seconds left), table, snapshot and record about 1 second; **total 1 minute 23.2 seconds** (1 minute 25.3
seconds wall clock with the process start).

- Corpus: **280 books** (224 core, 56 domain), 468,898 paragraphs, 1,340,479 sentences, **31,363,141 tokens** (after
  removing 6 sentences of 191 tokens), 222,326 types.
- Table `data/text-en/word-clusters.tsv`: **60,000 words**, 512 distinct paths (path length 3 to 17 bits; cluster sizes
  1 to 1,936, median 25), **1,410,769 bytes**, SHA-256
  `aaa3fe0c024bb666cc6b92ec489da997e5e51b20ab21da01b83a6288d28cca38`; token coverage 99.11 percent. **Shared lookup
  hashes: 0** (no word was dropped by the drop-all rule), ineligible words: 0.
- Record `data/text-en/clustering.json`: tokenizer version `0.1.0-beta.5`, sources `749286ba...a8c0`, removals
  `84794fd8...e77b`, evaluation listing `f5e5f6ee...bcc0`.
- Snapshot `cluster-corpus.tar` (in the git-ignored cache, not committed): **153,272,320 bytes**, 282 files (the 280
  normalized bodies, `cluster-sources.tsv` and `NOTICE.txt`), SHA-256
  `cdefcfa2eff5fb4594b068588302454df3061d9f5234009b4db0af937f0670ca`.

**Manual re-induction check and determinism** (the one-time `cluster --check` evidence of PR A; both runs from the cache
with the settings of the committed record):

| `cluster --check` | Tokenize | Audit | Count | Induce | Total | Result |
| --- | --- | --- | --- | --- | --- | --- |
| `--threads 1` | 5.4 s | 13.9 s | 6.4 s | 5 min 52.9 s | **6 min 19.4 s** | all current |
| `--threads 28` | 3.1 s | 3.9 s | 5.2 s | 1 min 20.2 s | **1 min 33.6 s** | all current |

Both reproduced the committed table byte for byte (SHA-256 `aaa3fe0c...cca38`), the record byte for byte and the
snapshot SHA-256 `cdefcfa2...70ca`: **one thread and 28 threads give identical output**. The single-thread run took far
less than the 3-hour limit of the plan, so no reduced configuration was needed. The arithmetic has no platform-dependent
part (integer counts, the trainer's own logarithm, basic operations in a fixed order); the golden tests of the subsystem
compare Windows, Linux and macOS in CI.

**Snapshot publication (documented only; not published).** The proposal for the owner: attach the archive as a release
asset of `demaconsulting/Speech` named `text-en-cluster-corpus-cdefcfa2.tar`, then put its URL in `snapshotUrl` of
`clustering.json` (a later `cluster --check` keeps the URL while the snapshot hash matches). Anyone can then verify the
archive by its SHA-256 and re-run `cluster --check --from-snapshot <file>` without the mirror. The archive holds only
US public-domain texts with the Project Gutenberg header, footer and every paragraph naming Project Gutenberg removed,
and a `NOTICE.txt`. Where to host it remains an open question for the owner.

**Offline gate.** `check-data` now verifies on every run that `clustering.json` matches the committed
`cluster-sources.tsv`, `cluster-removals.tsv` and `word-clusters.tsv` (SHA-256), the tokenizer version of the build,
the book, word and path counts, and that no used book is a development or natural-holdout document. Nothing in the
classifier, its features, `heteronyms.bin`, `training.json` or the cue priors was changed; PR B consumes the table.

## Word-cluster subsystem (PR A): corpus revision after the publication-year review (2026-11-21)

This entry revises the corpus of the two entries above; they stay as they were written. No model, classifier, cue prior,
`heteronyms.bin` or `training.json` changed, and the generalization set was not read.

**Why.** For 49 of the 56 accepted domain books the earlier year rule took the latest year printed in the first 200 body
lines, which can be a year mentioned in the text (for example 1814 in *A Book of Operas*), so it did not establish the
publication year. The owner's condition is a pre-1930 book with a license check recorded for each.

**Review.** For each domain book, the title page, imprint, copyright or entry line, edition statement and preface dates
were read by hand. The whole body was also scanned for every year from 1500 to 2099. A book was kept only if both hold:

- the evidence shows the first publication, or the transcribed edition, in 1929 or earlier;
- the RDF rights read "Public domain in the USA.".

The `year` column now holds that year for every domain book, and `license_basis` states the evidence, for example
"title page: New York, Harper & Brothers, 1891; copyright 1891". Five books have no imprint year in the transcription:

- 4523 (*On Conducting*): the translator's note gives Leipzig, 1869. The translation cites works up to 1883, and the
  translator died in 1905.
- 4939 (*Chopin*): the bibliography runs to February 1900. The publisher's list quotes a review of June 1915, and the
  author died in 1921.
- 5724 (*A Book of Operas*): the latest year in the text is 1909, and the author died in 1923.
- 19975 (*Base-ball*): the text cites the rules of 1887 and looks ahead to the season of 1888. The author died in 1925.
- 54537 (*Musical Instruments*): the series editor's note is dated August 1875, and the PG title gives 1876.

For each of these five, the basis gives the latest year in the text and the death year.

- **Rejected: 13291** (*The World's Great Men of Music*). Its title page reads 1922, but the transcribed text has
  chapters on three conductors, Toscanini among them. Those chapters cite 1924 and 1926 and give Toscanini's age as
  seventy-five (about 1942), years after the author's death in 1928. The transcribed edition cannot be shown to be from
  1929 or earlier, so the book is now `rejected-license` with that reason and no pin.
- **Accepted: 25731** (*Ancient Egyptian and Greek Looms*, Roth, died 1925). Its earlier R7 rejection was a false
  positive of the mechanical rule ("B.C. 2000"). The title page and preface name a museum in Halifax and the date April
  1913, and the only years from 1930 to 2099 in the body are B.C. dates. The body is 87,589 bytes, 96 percent prose,
  and it is not an edition of another listed book. The one request was `cluster --fetch --write-pins --delay-ms 2000`
  from `gutenberg.pglaf.org`; the RDF was already cached. The body is pinned as `08b29477...928b`.
- The other 55 books are confirmed. Their years are 1805 to 1922: 15 before 1870, 22 from 1870 to 1899, 18 from 1900
  to 1922. The full-body scan found no year from 1930 to 2099 that is a date, except the B.C. dates of 25731 and 54537;
  the other matches are counts and measures.

**Corpus.** 280 books are still used (224 core, 56 domain): anatomy and first aid 16, textiles and sewing 11, sport 14,
music 12, clocks 3. There are 11 rejected candidates (8 `rejected-license`, 3 `rejected-content`), and
`cluster-sources.tsv` has 336 rows (SHA-256 `18f44377...a820`). The corpus has 467,991 paragraphs, 1,336,563
sentences, 31,278,545 tokens before the removals and 31,278,354 after, and 222,070 types.

**Leakage audit.** `cluster --audit --write --threads 28` (exit 0; 9.5 seconds wall clock, audit 3.8 seconds) gives the
same result as before: the same 6 sentences from the same 5 books. The per-file and per-rule counts are unchanged.
`cluster-removals.tsv` is byte-identical (SHA-256 `84794fd8...e77b`).

**Preflight** (the pre-registered rule, unchanged). `cluster --preflight --min-count 5 --threads 28` (exit 0, 13.2
seconds) found 66,710 words with a count of at least 5. Token coverage is the same to two decimals: 97.99, 98.56, 98.89,
99.11 and 99.22 percent for 30,000 to 75,000 words.

- **hamstring** (count 7, rank 53,751) is still the only word that needs 60,000, so **V stays 60,000**.
- No nominated word changed its cut. gashes 13 and lacerations 12 are in from 50,000, and tarpaulin 21 from 40,000.
  meniscus 4, rotator 1, acl 0, labrum 4 and denim 1 are still too rare.
- Counts moved slightly with the swap of the two books. The changes of 1 percent or more are fabric 491 to 505, cloth
  2,182 to 2,221, linen 1,133 to 1,153, spool 30 to 40, rod 790 to 805, cello 33 to 28 and viola 174 to 169; every other
  nominated count moved by less than 1 percent.

**Induction.** `cluster --write --clusters 512 --max-words 60000 --min-count 5 --threads 28` (exit 0) ran on the same
i7-13850HX with 28 logical processors. The stages took: tokenization 3.4 seconds, audit 3.9 seconds, counting 4.6
seconds (3,136,242 bigram types), induction 1 minute 11.0 seconds. The **total was 1 minute 24.0 seconds** (1 minute
28.5 seconds wall clock).

- Table: **60,000 words**, 512 paths (3 to 17 bits; cluster sizes 1 to 1,887, median 24), **1,403,795 bytes**, SHA-256
  `17e94d11a69627513a92679370432697d452011b6ffb3168ecc35f5643d5007b`. Token coverage is 99.11 percent. There are 0
  shared lookup hashes and 0 ineligible words.
- Snapshot: **152,852,480 bytes**, 282 files, SHA-256
  `3d1e4e8cb64e19b926385483e55d495eba11f6c9258ba7fcb87a6522173f471d`. It is not published; the proposed asset name
  becomes `text-en-cluster-corpus-3d1e4e8c.tar`.
- Record: sources `18f44377...a820`, removals `84794fd8...e77b`, evaluation listing `f5e5f6ee...bcc0` (unchanged),
  tokenizer `0.1.0-beta.5`, 280 used and 56 other books.
- Spot checks look sensible: {very, extremely, comparatively, exceedingly, remarkably, …}; {to-day, to-morrow,
  yesterday, to-night, …}; {year, week, century, minute, month, …}. *hamstring* is again with "lower, upper,
  external, internal".

**Manual re-induction check and determinism** (both from the cache, with the settings of the committed record):

| `cluster --check` | Tokenize | Audit | Count | Induce | Total | Wall clock | Result |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `--threads 1` | 5.2 s | 21.5 s | 7.8 s | 5 min 33.1 s | **6 min 8.6 s** | 6 min 11.1 s | all current |
| `--threads 28` | 2.5 s | 3.2 s | 4.6 s | 1 min 7.4 s | **1 min 18.5 s** | 1 min 20.5 s | all current |

Both runs reproduced the committed table byte for byte (`17e94d11...d007b`), the record byte for byte and the snapshot
(`3d1e4e8c...f471d`). One thread and 28 threads give identical output.

## Word-cluster subsystem (PR A): amendment to the PR B read discipline (2026-11-21)

This entry amends only the procedure of the pre-registered criterion for word clusters (entry "protocol and
pre-registration", 2026-11-20); that entry stays as it was written. It is recorded before any PR B training and before
any model with word clusters exists. No model, classifier, cue prior, `heteronyms.bin` or `training.json` changed, and
the generalization set was not read.

**Why.** `evaluate --write` and `evaluate --check` evaluate every set, the generalization set included, because the
committed evaluation record holds those figures by design, and CI runs `evaluate --check` on whatever model is
committed. Without a rule, a candidate model committed during PR B development would therefore show its generalization
figure before the single pre-registered read.

**No change to the criterion.** Models A and B, the primary endpoint, the +300 basis-point threshold, the
family-clustered interval, the seed-mean rule, the 2 by 2 ablation and the guards are all unchanged.

- **Candidate models.** During PR B development, candidates are written to scratch paths (outside the repository or
  git-ignored). They are evaluated only with `evaluate --model <scratch> --set dev` and `--set regression`, plus the
  trainer's own development figures. Never `--set generalization`; never `--gate` or `--set holdout-natural` (read-once
  as well); never `evaluate --write` or `--check` on a candidate.
- **No commits before the read.** No candidate `heteronyms.bin` or `training.json` is committed or pushed before the
  read is logged here. Until then the committed model stays the shipped PR A model, whose CI figures are the PR A
  baseline already recorded above, so they are not a read.
- **The single read.** The pre-registered read is one run: the four ablation models and seeds 1 to 5 for the seed-mean
  rule, all from scratch paths. Its figures are logged here first.
- **After the read.** Only then is the final model committed and `evaluate --write` run. That write re-evaluates the
  same model A and must reproduce the logged figures; it informs no decision, and any difference is a defect.
- **Accidental early read.** If a candidate's generalization figure is ever seen before the read, the event is logged
  here with the model and the figure, and disclosed with PR B's result.

## Word-cluster subsystem (PR A): commit references after the rebase onto main (2026-11-21)

The branch was rebased onto `main` (the base PR was squash-merged as `cc03fca`, whose tree equals the old base
`6be4450`), and two commit pairs were folded so that every commit passes the checks. The tree after the rebase is
byte-identical to the validated tree. Commit ids named in the entries above map as follows:

- `abae8f2` is now `136f2045cabf3e36ff112d7582634ba656ebab36` (Text.En: prompt of the blind generalization set);
- `2869a472fb2a3b638190394a71b2f8ba4f210316` is now `ae439189000de1297e321facecc75a2554b54412` (Text.En: blind
  generalization set, frozen);
- `10ac6ad` is now `b1e2a143f0fde7def91c2ee4af310bf526765f39` (Text.En: generalization gen-0060 is not
  cue-unsolvable);
- `0f12855`, together with its record in the next commit, is now `a87692a39e04a72b6ce43df8a566816f9311fae3`
  (Text.En: overlay evaluation with and without the curated cue priors, and the baseline).

Commit ids may change again if the branch is squash-merged, so the frozen content is also pinned by SHA-256:

- the writer prompt as committed before the writing: `c558809c7a4afd1ce533bda04563709e14979124aceea48339747e75995e9cef`;
- `generalization.tsv` at the freeze: `ce27cae8ff559d8ce6fbbd195b5afb3403c7853e2e408dd794d41f9d307ed16a`;
- `generalization.tsv` after the `gen-0060` flag fix (current, as in `MANIFEST.tsv`):
  `b2b33140fefbe408198477ff92cc6c5666e4af360890019c97ecb9dd9902937e`;
- `audit/generalization-review.tsv` (unchanged since the freeze):
  `ccd81a2be89e326477ff8cb4fa0837c10bb444e3b85ce8159d26515879c434c3`.
