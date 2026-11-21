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

## PR B: order-only baseline (keyed visiting order, feature set 1, no new data) (2026-11-21)

Written before any model with word classes exists and before any PR B data was written. Sets read for this entry: the
development set (trainer level and end to end) and the regression overlay only. **No generalization set and no holdout
(natural or independent) was read for any model in this entry**: `evaluate` was run only as `--set dev --set
regression`, never with `--gate`, `--seeds`, `--write`, `--check` or another set.

**Change.** The trainer gained the keyed visiting order as an experiment option (`train --row-order keyed`), recorded
as `rowOrder` in `training.json`. Each epoch, every visit (a used row and its repeat number) gets a key mixed from the
seed, the epoch, the 64-bit FNV-1a hash of the row id and the repeat number, and visits are taken in key order (ties:
ordinal id, then repeat). Adding rows therefore never changes the relative order of the existing rows. The default stays
positional, so the committed model is unchanged: `train` reproduced the image `73de5a3f...3df0` (120,090 weights,
253,643 bytes compressed, the same file), and `training.json` differs only by the added `"rowOrder": "positional"`.

**Runs.** For seeds 1 to 5 and both orders: `train --model-dir <scratch> --row-order <order> --seed <n>` with otherwise
the committed settings (feature set 1, the committed data, the cue priors in the classifier), then `evaluate
--model-dir <scratch> --set dev --set regression`. Trainer level is the development figure of the kept epoch (natural
rows, all used rows); end to end is the development set through the analyzer (with the cue priors); the regression
overlay is reported with the cue priors and for the model alone. Positional seed 1 is the shipped model.

| Order | Seed | Epochs | Dev natural | Dev all | Dev end to end | Regression, cues | Model only | Weights | Image |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| positional | 1 | 1 | 1,267/1,301 | 2,885/3,034 | 3,083/3,235 | 519/519 | 515 | 120,090 | `73de5a3f` |
| positional | 2 | 4 | 1,266/1,301 | 2,911/3,034 | 3,108/3,235 | 514/519 | 513 | 150,435 | `52eec716` |
| positional | 3 | 11 | 1,268/1,301 | 2,915/3,034 | 3,113/3,235 | 513/519 | 513 | 148,475 | `22d11bac` |
| positional | 4 | 8 | 1,266/1,301 | 2,918/3,034 | 3,116/3,235 | 513/519 | 511 | 148,991 | `3d619d1f` |
| positional | 5 | 1 | 1,265/1,301 | 2,889/3,034 | 3,087/3,235 | 505/519 | 499 | 117,597 | `e53e5d70` |
| keyed | 1 | 2 | 1,266/1,301 | 2,900/3,034 | 3,097/3,235 | 511/519 | 511 | 136,768 | `80031288` |
| keyed | 2 | 3 | 1,261/1,301 | 2,898/3,034 | 3,095/3,235 | 515/519 | 511 | 144,715 | `0347ce24` |
| keyed | 3 | 2 | 1,273/1,301 | 2,912/3,034 | 3,110/3,235 | 514/519 | 510 | 137,345 | `e4c70117` |
| keyed | 4 | 1 | 1,268/1,301 | 2,894/3,034 | 3,092/3,235 | 506/519 | 501 | 118,212 | `5b95d7bf` |
| keyed | 5 | 2 | 1,265/1,301 | 2,905/3,034 | 3,103/3,235 | 513/519 | 509 | 137,508 | `9c78b8f1` |

Regression overlay by origin, with the cue priors / the model alone (rows: `listening` 7, `analyzer-test` 11,
`golden-pipeline` 37, `golden-sentences` 61, `golden-readings` 382, `data-row` 21):

| Order | Seed | listening | analyzer-test | golden-pipeline | golden-sentences | golden-readings | data-row |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| positional | 1 | 7 / 4 | 11 / 11 | 37 / 37 | 61 / 61 | 382 / 382 | 21 / 20 |
| positional | 2 | 5 / 4 | 10 / 10 | 36 / 36 | 60 / 60 | 382 / 382 | 21 / 21 |
| positional | 3 | 5 / 5 | 11 / 11 | 35 / 35 | 60 / 60 | 381 / 381 | 21 / 21 |
| positional | 4 | 5 / 3 | 11 / 11 | 35 / 35 | 59 / 59 | 382 / 382 | 21 / 21 |
| positional | 5 | 5 / 2 | 10 / 10 | 33 / 32 | 58 / 58 | 378 / 378 | 21 / 19 |
| keyed | 1 | 5 / 6 | 10 / 10 | 35 / 35 | 60 / 60 | 380 / 380 | 21 / 20 |
| keyed | 2 | 6 / 4 | 11 / 11 | 35 / 34 | 61 / 61 | 381 / 381 | 21 / 20 |
| keyed | 3 | 6 / 4 | 10 / 10 | 35 / 34 | 60 / 60 | 382 / 382 | 21 / 20 |
| keyed | 4 | 6 / 3 | 11 / 11 | 32 / 32 | 58 / 58 | 378 / 378 | 21 / 19 |
| keyed | 5 | 6 / 4 | 11 / 11 | 35 / 34 | 59 / 59 | 381 / 381 | 21 / 20 |

**Seed spread** (mean, minimum to maximum over seeds 1 to 5):

- positional: dev natural 1,266.4 (1,265 to 1,268); dev all 2,903.6 (2,885 to 2,918); dev end to end 3,101.4 (3,083
  to 3,116); regression with the cues 512.8 (505 to 519), model alone 510.2 (499 to 515).
- keyed: dev natural 1,266.6 (1,261 to 1,273); dev all 2,901.8 (2,894 to 2,912); dev end to end 3,099.4 (3,092 to
  3,110); regression with the cues 511.8 (506 to 515), model alone 508.4 (501 to 511).

**Reading.** The visiting order alone does not change the level: the two orders' means differ by less than the spread of
either over seeds, on every measure. The seed spread is the noise floor of the PR B iteration on the development set
and the regression overlay: up to 12 natural development rows, 33 development rows end to end and 16 regression rows for
the model alone, with feature set 1 and no new data. With the cue priors only the shipped model (positional, seed 1)
gets every regression row right; no feature-set-1 model gets every `listening` row right without them. Keyed seed 1 is
the order-only reference for the PR B candidates (the same data and features as the shipped model, the keyed order):
dev natural 1,266, dev end to end 3,097, regression 511 with the cues and 511 alone. The models are in scratch folders
outside the repository and were not committed.

## PR B: procedure fixed before any training with word classes (2026-11-21)

Written before any model with word classes or the new features exists, before any synthetic-v4 row was written, and
before any PR B candidate was trained. Committed together with the writer and review prompts of the data round
(`data/text-en/prompts/generation-synthetic-v4.md` and `review-synthetic-v4.md`). Sets read for this entry: none. The
pre-registered criterion (entry "protocol and pre-registration", 2026-11-20) and its amendment (2026-11-21) are
unchanged; this entry fixes how PR B gets to the single read.

**Feature specification** (decided now, implemented before the first candidate):

- **Model file format 2** (`HETW` version 2; the 80-byte header keeps its offsets): byte 13 holds the feature options
  (bit 0 the curated layer, other bits zero), the 32-bit field at offset 44 holds the section count (1 to 8), bytes 14
  and 15 stay zero. After inflating, a directory of `(u16 id, u16 zero, u32 length)` entries with strictly increasing
  ids is followed by the sections in id order, whose lengths sum exactly to the payload. Section 1 holds the weights in
  today's layout (required); section 2 holds the word classes (optional: absent means a model without word classes,
  model B); ids 3 and up are reserved and rejected, as are a duplicate id, a missing weights section, a length mismatch
  and trailing bytes. A version 1 file is rejected. The word-class section: a cluster count C (1 to 4,096), C entries of
  path length (1 to 32) and path bits, a word count W (1 to 262,144), then W gap-coded 32-bit lookup hashes (strictly
  increasing), W 16-bit fingerprints and W cluster indexes below C; every count is checked before any allocation.
- **Lookup fingerprint: 16 bits.** Each token carries, beside its 32-bit FNV-1a lookup hash, a 16-bit fingerprint (a
  Murmur3-style 32-bit mix with its own seed over the same normalized code units, folded as `x ^ (x >> 16)`). A word
  gets a table word's class only when both the hash and the fingerprint match, so a word outside the table borrows a
  class with a probability of about 60,000 / 2^48 per token. 16 rather than 32 bits keeps about 120 KB of
  incompressible bytes out of the model. The width is fixed here and is not changed after the read.
- **Feature set 2**: the 32 templates of feature set 1 unchanged, plus: the classes of the words at offsets -2, -1, +1
  and +2 at the prefixes of 4, 6 and 10 bits and the full path (conjoined); the classes of the neighbors at -1 and +1 at
  4 and 6 bits (shared); a bag of the classes of the content words within 8 tokens (6 and 10 bits; conjoined); the 6-bit
  class next to the target with the target's part of speech (shared); the target form with its part of speech
  (conjoined). A word without a class emits no class feature, so an empty table is exactly "without word classes".
- **Curated layer** (features, no longer priors; only when the model's options enable it): a degree adverb right before
  the target (the 29 adverbs of the classifier), a coordinated adjective on either side, and a past-time word
  (*yesterday*, *ago*, *previously*, or *last* with one of the 13 time nouns) in the target's own clause (bounded by a
  coordinating or subordinating conjunction or a semicolon, not by a comma), with its side and whether an auxiliary or
  particle stands right before the target. The lists move from the classifier into the extractor unchanged; the 23
  context-cue words of *tear* are not carried over. The four cue priors are removed from the classifier.
- **Primary part-of-speech sharing**: every shared feature is scored through the reading's role salt and through the
  salt of the first tag of its `pos_hint`, in the classifier and the trainer alike.
- **Keyed visiting order** becomes the default (trainer version 2); positional stays an experiment option.
- **Variants** for the criterion: `train --clusters none` (no word-class section) and `--curated off` (options bit 0
  clear), both experiment options; the 2 by 2 ablation runs every cell end to end through the analyzer from the model
  file alone.

**Settings.** Training starts from the committed settings (seed 1, 22 hash bits, scale 64, prune threshold 2, natural
weight 3, at most 12 epochs, the temperature grid) with the keyed order. Before the read the only knobs are:

- the prune threshold, for size, by the development rule (the highest number of development natural rows decided
  correctly, ties to the smaller model);
- data replacement and top-up through the data-round procedure below;
- defect fixes.

Nothing else (hash bits, scale, natural weight, epochs, seed, the feature templates, the fingerprint width, the
512-class table) changes before the read.

**Data round (synthetic-v4).** 465 rows by the committed prompts, balanced per reading and written as contrastive pairs
(`quotas-v4.tsv` in the prompt): part 0 tear, wound, minute and read (285 rows); part 1 bass, lead, close, live, wind
and content (180 rows). This follows the owner's split, which puts *read* in part 0 (the plan had it in part 1); the
quotas are those of the plan.

- **Model families.** Writers `claude-sonnet-5.5` (one `general-purpose` instance per part; top-ups by new instances of
  the same model); blind reviewer `gpt-6.1-sol`.
- **Stage.** Outside the repository, holding only copies of `readings.tsv`, `forms.tsv`, `train.tsv`, `dev.tsv`, the
  quota file and the prompts, the prompt text exactly as committed with only the staging folder substituted.
- **Mechanical filters** (a script; nobody reads generalization rows): a row is dropped when it fails its offsets, form
  or single-target check; repeats exactly or nearly (the "Leakage" rule of the data README) a sentence of train, dev,
  either holdout, `regression.tsv` or `generalization.tsv`; breaks the novelty rule N2 or N3 of any generalization row
  of its lemma group; would make a regression row seen in training; or has a (reading, template) pair already in train
  or dev.
- **Top-ups.** At most 2 rounds for readings under quota, with a generic note only (never a training, regression or
  generalization sentence), then filtered again.
- **Blind review.** Every surviving row; a disagreement or `unsure` drops the row, never relabels it. A reading below 90
  percent of its quota gets one further top-up and a second review instance on the new rows only. Recorded as
  `audit/review-synthetic-v4.tsv` with round and decision columns.
- **Development rows.** The round-1 rule: per reading, the templates in SHA-1 order, about 12 percent of the reading's
  new rows to dev, never splitting a (reading, template) pair.
- **Gate of the data.** `check-data` exit 0 (with the generalization novelty rules against the final train and dev
  data) and the external leakage check over the cached evaluation sets.

**Freeze checklist** (readiness, not binding; the read is taken also when it is not met, and the result says so):

- the regression overlay is 100 percent on the `listening`, `analyzer-test` and every `golden-*` origin for model A at
  seed 1, and each such row is right on at least 4 of the 5 seeds;
- the model file is at most 0.70 MB;
- the development set end to end is reported against the shipped 3,083 of 3,235 (not a gate).

**The read.** One run of `evaluate --ablation --ablation-dir <scratch>`: it trains the four cells (clusters off and on
by curated layer off and on) at seed 1 and models A and B at seeds 2 to 5, all from scratch paths, evaluates them as
pre-registered and exits 0 on a pass and 1 on a fail. Its console output for the generalization set is aggregate only
(the pre-registered slices, the per-seed figures and the image hashes; no per-row or per-reading generalization detail),
so that a second candidate, if the failure path needs one, stays as uninformed as possible. Its figures are logged here
before anything else is done.

**Retirement of the cue-unsolvable verification.** The evaluator's check that no cue prior acts on a
`cue_unsolvable=yes` overlay row is retired together with the priors: its referent no longer exists in PR B's models.
The 80 marked rows were verified against the priors on 2026-11-21 (entry "baseline of the shipped model on the
overlays"), the set is pinned by SHA-256 (`b2b33140...937e`), and the mark stays as a frozen stratum label for the
pre-registered subset analysis. The overlays are then reported for the model under test only, by slice, with clustered
intervals.

**Not built in PR B.** The "fresh natural audit set" planned in PR A is outside PR B's scope (owner decision); the
legacy natural holdout is the natural guard, as the criterion states.

**Failure path.**

1. A failed criterion or guard at the read (difference below +300 basis points, a lower bound at or below 0, a seed mean
   at or below 0, or generalization A below 177 of 242) is logged. No tuning on the generalization set or a holdout:
   the diagnosis uses the development set and the regression overlay only. At most one further frozen candidate, after
   an entry "PR B: second pre-registration" that states the change, its development and regression basis, that the
   generalization set has been read once, and that the next read is read 2 (both reads are reported).
2. A failed gate (natural holdout below 1,123 of 1,167) or a failed golden after a passing read is treated the same way;
   no golden is edited or demoted.
3. Still failing: stop with the result incomplete and all figures; nothing is pushed and the word-class and cue-removal
   commits are not made. The branch keeps the commits up to the read log (the shipped model and the cue priors intact),
   and the working tree is saved for the owner outside the repository.

**Discipline until the read** (the amendment, applied): candidates live in scratch folders and are evaluated only with
`evaluate --set dev --set regression` and the trainer's development figures; never `--set generalization`,
`--set holdout-natural`, `--gate`, `--seeds`, `--write` or `--check` on a candidate; from the first change of the
runtime until the read, neither `build.ps1` nor the trainer tests whose names contain `Committed` are run (they evaluate
the holdouts and the generalization set of the working-tree model); no candidate model or record is committed before the
read is logged here.

## PR B: synthetic-v4 data round (2026-11-21)

Written after the data round and before any candidate was trained on its rows. Sets read for this entry: nobody read a
generalization or holdout sentence. The mechanical filter script reads the overlays and both holdouts and prints only
rule names, counts and stage row ids; the only model evaluated is the shipped model, on the regression overlay (allowed,
see below). No candidate model was trained or evaluated for this entry.

**Prompts and stage.** The committed prompts of entry "PR B: procedure fixed before any training with word classes"
(`data/text-en/prompts/generation-synthetic-v4.md` and `review-synthetic-v4.md`, committed in C2 before any row was
written), given verbatim with only the staging folder substituted. Stage `C:\work\sp-clusters-b-stage\` (outside the
repository) with the copies named in the procedure entry; the writer files (outputs, notes, validators and the writers'
scratch) were moved into `writers\` after each round. One more folder there holds an obsolete copy of the
review inputs made before top-up round 1; it was never reviewed.

**Writers** (`claude-sonnet-5.5`, `general-purpose`, dispatched by the orchestrator with the stage prompt text
verbatim):

- Round 1: part 0 (tear, wound, minute, read; 285 rows) and part 1 (bass, lead, close, live, wind, content; 180 rows),
  both dispatched 2026-11-21 10:13:22 UTC; part 1 finished about 10:20, part 0 about 10:24.
- Top-up round 1: two new instances (one per part) with the part's prompt and a generic first line naming only the
  reading ids, the row counts and the quota-line text; dispatched about 11:48, finished about 11:51; 14 rows (8 and 6).
- Top-up round 2 (after the review, see below): one new part-0 instance, about 11:55 to 11:57; 5 `read.present` rows
  (habitual frames).

**Mechanical filters** (`filter_v4.py`, scratch; rules of the procedure entry, the C# leakage and overlay rules ported
exactly; it reproduces all 14,998 committed provenance templates). 484 rows written, **16 dropped**, 468 kept:

- generalization novelty N2 (a cue word of a `novel-vocabulary` row of the lemma group): 12 (`syn4-0-7`, `-17`, `-23`,
  `-54`, `-81`, `-103`, `-126`; `syn4-1-44`, `-65`, `-80`, `-103`; `syn4-1-t1-1`);
- generalization novelty N3 (a construction trigram of a `novel-construction` row): 3 (`syn4-1-52`, `-107`,
  `syn4-1-t1-4`);
- label defect: 1 (`syn4-0-264`, `read.present` in "can be read", a participle);
- format, offsets, form and single target; exact and near repeats of train, dev, both holdouts, the regression overlay,
  the generalization set and the listening sentences L01 to L16 (both directions, cumulative); a regression or listening
  row made seen in training; a (reading, template) pair already in train or dev: 0 each.

After top-up round 1 every reading was at 90 percent of its quota or more (`lead.guide` and `close.verb` 14 of 15), so
the blind review followed.

**Blind review** (`gpt-6.1-sol`, `general-purpose`, the committed review prompt verbatim, stage file SHA-256
`26d2d70cf09a5bdb4b84fd7273d8494e50b1fa8eca3398cac87581aa0db59071`; judgments frozen by SHA-256 before the labels were
opened; a disagreement or `unsure` drops a row, never relabels it):

- Round 1, 2026-11-21 11:52:12 to 11:54:34 UTC: 463 rows, **457 agree**, 0 disagreements other than `unsure`, 6
  `unsure`, all dropped: `syn4-0-35` (`tear.rip`) and `syn4-0-251`, `-252`, `-254`, `-255`, `-257` (`read.present`
  with only a habitual adverbial: the tense is not fixed). Judgments SHA-256
  `0e7f773d3b62b0f0079fa912e059433a6f2b229de0404eb759309c4338af0369`. Files: stage `review-round-1\` (inputs in
  `review-round-1-inputs\`).
- `read.present` fell to 30 of 35 (under 90 percent), hence top-up round 2 (0 rows filtered) and review round 2 by a
  new `gpt-6.1-sol` instance on those 5 rows only, about 11:57 to 11:59 UTC: **5 of 5 agree**. Judgments SHA-256
  `ba20fb491222e9298620cdab5c23ae60102c04f491ac5d0590eff9c9abcc7500`. Files: stage `review\` (inputs
  `review-blind.tsv` and `review-labels.tsv` at the stage root).
- Recorded in `data/text-en/audit/review-synthetic-v4.tsv` (468 lines: one per reviewed row and round; `decision`
  `keep` 462, `drop` 6).

**Final rows: 462** (468 filter survivors minus 6 `unsure`), every reading at its quota except `tear.rip` 44 of 45,
`lead.guide` 14 of 15 and `close.verb` 14 of 15:

| Reading | Quota | Written | Filtered | Review dropped | Kept | Train | Dev |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `tear.rip` | 45 | 48 | 3 | 1 | 44 | 39 | 5 |
| `tear.drop` | 45 | 47 | 2 | 0 | 45 | 40 | 5 |
| `wound.injury` | 33 | 34 | 1 | 0 | 33 | 29 | 4 |
| `wind.coil` (both parts) | 47 | 48 | 1 | 0 | 47 | 41 | 6 |
| `minute.tiny` | 30 | 30 | 0 | 0 | 30 | 26 | 4 |
| `minute.time` | 30 | 30 | 0 | 0 | 30 | 26 | 4 |
| `read.past` | 35 | 35 | 0 | 0 | 35 | 31 | 4 |
| `read.present` | 35 | 41 | 1 | 5 | 35 | 31 | 4 |
| `bass.fish` | 15 | 15 | 0 | 0 | 15 | 13 | 2 |
| `bass.music` | 15 | 15 | 0 | 0 | 15 | 13 | 2 |
| `lead.guide` | 15 | 16 | 2 | 0 | 14 | 12 | 2 |
| `lead.metal` | 15 | 16 | 1 | 0 | 15 | 13 | 2 |
| `close.adj-noun` | 15 | 16 | 1 | 0 | 15 | 13 | 2 |
| `close.verb` | 15 | 16 | 2 | 0 | 14 | 12 | 2 |
| `live.adj` | 15 | 16 | 1 | 0 | 15 | 13 | 2 |
| `live.verb` | 15 | 16 | 1 | 0 | 15 | 13 | 2 |
| `wind.air` | 15 | 15 | 0 | 0 | 15 | 13 | 2 |
| `content.noun` | 15 | 15 | 0 | 0 | 15 | 13 | 2 |
| `content.satisfied` | 15 | 15 | 0 | 0 | 15 | 13 | 2 |
| **Total** | **465** | **484** | **16** | **6** | **462** | **404** | **58** |

**Development rows.** The round-1 rule: per reading, the round's templates (two words each side of the target,
lowercased) in the order of `SHA-1("text-en-split-v1|syn|<reading_id>|<template>")` go to dev until
`round(0.12 x rows)` of the reading's new rows are in dev; a (reading, template) pair is never split (the rule
reproduces the first round's assignment for 285 of its 289 readings; the other 4 changed later by relabels and moves).
404 rows to `train.tsv`, 58 to `dev.tsv`.

**Merge** (`merge_v4.py`, scratch; it checks the frozen judgment hashes, that every filter survivor was reviewed exactly
once and that no (reading, template) pair is already in train or dev):

- `train.tsv` 19,865 to 20,269 rows and `dev.tsv` 3,235 to 3,293 rows (appended after the third-round rows, ids in the
  order part, round, number); `synthetic-provenance.tsv` 14,998 to 15,460 (`id - <batch> <template>`, the writer's batch
  `syn4-<part>-<reading>[-t<r>]-b<k>`).
- Source `synthetic-v4` added to the closed source vocabulary (`TextEnDataSet`, generated, `CC0-1.0`), with the test
  `SchemaChecker_Run_FourthRoundSyntheticRow_ReportsNoFindings`.
- `gaps-v4.tsv` regenerated from `check-data --counts` (19 readings changed; 13 readings now one row short of 40, was
  14); `MANIFEST.tsv` regenerated; data README, `audit/audit-summary.md`, the prompts README and both prompt records
  completed.
- SHA-256: `train.tsv` `3a9e5b653d0e0cc6fb5687ac21fd05ead21e083b8ca6d17ae175cb2b1af36738`, `dev.tsv`
  `cde600b95b668cc695851337ba7b59fb1f9f464fd788da6633f287a9efbc9b81`, `synthetic-provenance.tsv`
  `6a38e9836b1409714b45bc5143c93bf5bd81600394c094cd473058c0dbb2ce02`, `audit/review-synthetic-v4.tsv`
  `de2e2bfecb861479d61697154f8c6ee9e784b78e8b2dc5a80aa662eedd280ea8`.

**New regression rows.** `reg-0520` to `reg-0524` (origin `analyzer-test`, `ref SpeechTextEnTests.cs:1118` to `:1122`,
the system theory of the readings the model makes alone, `seen_in_training no`, `CC0-1.0`): the listening sentences L07
"Last night, I read it." (`read.past`), L08 "Two days ago she read it." (`read.past`), L09 "She read it two days ago."
(`read.past`), L10 "Yesterday I wanted to read." (`read.present`) and L11 "Last night I did not read."
(`read.present`). The overlay has 524 rows (`analyzer-test` 16); `regression.tsv` SHA-256
`877bdf03bfaf543a8d37754197712f01a70320212fc91f10735e1013738bde39`. Every test reference of the five rows resolves (the
line holds the sentence and the reading; checked by script, because the trainer test that checks it has `Committed` in
its name and is not run before the read).

**Extended PR A baseline** (the shipped model `73de5a3f...3df0` with the cue priors, PR A's trainer from the "before"
worktree at `0034231`, `evaluate --model-dir <scratch>\shipped --data <copy of PR A's data with the new regression.tsv>
--set regression`, the only set read): **524 of 524 with the cue priors, 520 of 524 for the model alone**; the five new
rows are right both ways (`analyzer-test` 16 of 16 with and without the priors; a prior fired on 5 of the 16 rows and
changed none). The four rows the model alone gets wrong are unchanged from the PR A baseline (all `listening` and
`data-row`).

**Gate of the data.**

- `check-data`: **exit 0, 0 findings**, including the generalization novelty rules N1 to N3 against the final train and
  dev data, `seen_in_training` of every regression row recomputed (25 seen, 499 unseen; the five new rows unseen) and
  the manifest. Informational only: the word-cluster removals were audited against the evaluation files before this
  change (`evaluationListingSha256` `f5e5f6ee...c0` then, `156fa76c...d5` now, because `dev.tsv` and `regression.tsv`
  grew); the corpus cache needed for `cluster --audit` is not available in this worktree, so the audit was not re-run.
  The table was induced before and independently of these rows and uses no labels.
- **External leakage**: `check-data --eval-sets <scratch>\eval-sets` over the locally cached evaluation sets
  (WikipediaHomographData train 14,487 and eval 1,615 sentences, heteronym-bench 566 items; 16,668 sentences, 16,383
  distinct keys): **exit 0, 0 exact matches and 0 near duplicates** in all four data files. Rows sharing at least one
  word 5-gram with these sets: train 364 of 20,269, dev 53 of 3,293, holdout-natural 23 of 1,167, holdout-independent 3
  of 1,972 (10 fourth-round rows among them). The other 21 sets of the full check are not cached, so
  `leakage-report.tsv` (all 24 sets, before this round) was not regenerated; the data README says so.

**Order of the steps.** As fixed: one top-up round after the filters (of the two allowed), then the blind review of
all filter survivors, then the review's refill path (one further top-up and a second review instance on the new rows
only) for the one reading that the review left under 90 percent of its quota.

## PR B: candidate log, development set and regression overlay only (2026-11-21)

Sets read for every model in this entry: the development set (trainer level and end to end) and the regression overlay
(524 rows) only. **No generalization set and no holdout (natural or independent) was read for any candidate**:
`evaluate` was run only as `--set dev --set regression` or `--set regression`, from a script that refuses any other
set; never `--gate`, `--seeds`, `--write`, `--check` or `--ablation`, and neither `build.ps1` nor a trainer test with
`Committed` in its name was run. Every model is in a scratch folder outside the repository
(`C:\work\sp-clusters-scratch\pr-b\`, one folder per model, named by the candidate number, or by the prune
threshold and seed); the committed `heteronyms.bin` and `training.json` are unchanged.

**Settings.** Feature set 2, model format 2, trainer version 2, the committed settings (seed 1, 22 hash bits, scale 64,
natural weight 3, at most 12 epochs, the temperature grid), keyed order, the 512-class table, curated layer on; only the
prune threshold varies (the size knob of the procedure entry). Data: the committed data plus the synthetic-v4 rows
(entry "PR B: synthetic-v4 data round"; `dataSha256` `84e71e20...990e`), except candidate 0 (the Phase 4 smoke
candidate, trained before the merge on the data of PR A: 3,235 development rows, 519 regression rows).

**Candidates at seed 1.** Trainer level: the kept epoch's development natural rows (of 1,301) and all used development
rows (of 3,034 for candidate 0, 3,092 after the merge). End to end: the development set through the analyzer (of 3,235
for candidate 0, 3,293 after the merge; the shipped model has 3,083 of 3,235). Regression rows by origin: `listening` 7,
`analyzer-test` 16 (11 for candidate 0), `golden-pipeline` 37, `golden-sentences` 61, `golden-readings` 382,
`data-row` 21. Size: `heteronyms.bin` bytes (Deflate).

Development set, size and image:

| Model | Prune | Epochs | Dev natural | Dev all | Dev end to end | Bytes | Image |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| candidate 0 | 2 | 2 | 1,263 | 2,907 | 3,105 | 734,185 | `3bb84f76` |
| candidate 1 | 2 | 12 | 1,268 | 2,973 | 3,171 | 771,491 | `cdc31683` |
| prune 28 | 28 | 2 | 1,268 | 2,953 | 3,151 | 671,964 | `7ddbc366` |
| prune 32 | 32 | 2 | 1,269 | 2,953 | 3,151 | 660,287 | `1f83c5d0` |
| candidate 2 (prune 40) | 40 | 2 | **1,269** | 2,954 | 3,152 | **618,493** | `4ac85243` |
| prune 48 | 48 | 2 | 1,267 | 2,955 | 3,153 | 569,415 | `2446dcec` |

Regression overlay by origin, and the number of weights:

| Model | Regression | Listening | Analyzer | Pipeline | Sentences | Readings | Data rows | Weights |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| candidate 0 | 510/519 | 5 | 9/11 | 35 | 60 | 380 | 21 | 187,774 |
| candidate 1 | 520/524 | 5 | 16 | 36 | 60 | 382 | 21 | 216,943 |
| prune 28 | 516/524 | 6 | 14 | 34 | 61 | 380 | 21 | 160,034 |
| prune 32 | 517/524 | 6 | 14 | 35 | 61 | 380 | 21 | 154,872 |
| candidate 2 (prune 40) | 516/524 | 6 | 14 | 35 | 61 | 379 | 21 | 134,104 |
| prune 48 | 517/524 | 6 | 14 | 35 | 61 | 380 | 21 | 108,365 |

Trainer level only (not evaluated end to end), the rest of the prune grid: prune 3, 4, 5, 6, 8, 12, 16 and 24 all keep
12 epochs with 1,268 development natural rows (2,972 or 2,973 all) and 767,999 down to 740,121 bytes (215,284 down to
203,563 weights); prune 64 keeps 1 epoch with 1,263 (2,954), 21,858 weights and 407,987 bytes. From prune 28 on, epoch 2
scores at least as many development natural rows as any later epoch, so the selection (ties to the smaller model) keeps
it.

Regression rows wrong at seed 1:

- Candidate 0: `reg-0002`, `-0004`, `-0013`, `-0014`, `-0050`, `-0051`, `-0115`, `-0162`, `-0166` (Phase 4 report).
- Candidate 1: `reg-0004` (L03 *muscle tear*, `tear.rip` read as `tear.drop`), `reg-0006` (L05 *The tape wound onto the
  spool*, `wind.coil` as `wound.injury`), `reg-0050` (*I read every night before bed*, `read.present` as `read.past`),
  `reg-0088` (*He had a tear in his eye as he began to tear the letter*, the first *tear*, `tear.drop` as `tear.rip`).
- Candidate 2 (prune 40): `reg-0004`, `reg-0013` (*He caught a large bass in the lake*, `bass.fish` as `bass.music`),
  `reg-0014` (*He drew the bow and shot an arrow*, `bow.weapon` as `bow.bend`), `reg-0024` (*... but he will read it
  tomorrow*, `read.present` as `read.past`), `reg-0050`, `reg-0162` (*The actors take a bow at the end*, `bow.bend` as
  `bow.weapon`), `reg-0166` (*Celtic music uses fiddles and flutes*, `celtic.culture` as `celtic.team`), `reg-0339`
  (*The church honors Jesus every Sunday*, `jesus.english` as `jesus.spanish`).
- Prune 28: as candidate 2 with `reg-0055` (*The bass swam deep in the lake*) and without `reg-0339`; prune 32:
  `reg-0004`, `-0013`, `-0014`, `-0050`, `-0055`, `-0162`, `-0166`; prune 48: `reg-0004`, `-0013`, `-0014`, `-0024`,
  `-0050`, `-0166`, `-0339`.
- The new rows `reg-0520` to `reg-0524` (L07 to L11) are right for every candidate after the merge.

**The prune threshold by the development rule.** Over the grid 2, 3, 4, 5, 6, 8, 12, 16, 24, 28, 32, 40, 48 and 64 the
highest number of development natural rows is 1,269 (prune 32 and 40); the tie goes to the smaller model: **prune 40**,
candidate 2, 618,493 bytes (section 1, weights: 404,440 bytes raw, 266,688 deflated alone; section 2, word classes:
410,109 raw, 348,452 deflated alone). The winning margin over prune 2 is one development natural row, within the seed
noise; prune 2 (candidate 1, epoch 12) is 20 rows better end to end on the development set (3,171 against 3,151 to 3,153
for the epoch-2 models) and 4 rows better on the regression overlay, but it is 771,491 bytes, over the 0.70 MB of the
freeze checklist. The development rule decides; the difference is recorded here.

**Seed diagnostics** (regression overlay only; trainer-level development figures from the `train` output):

Development set, size and image:

| Prune | Seed | Epochs | Dev natural | Dev all | Bytes | Image |
| ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 40 | 1 | 2 | 1,269 | 2,954 | 618,493 | `4ac85243` |
| 40 | 2 | 7 | 1,264 | 2,975 | 707,218 | `83d1a0eb` |
| 40 | 3 | 2 | 1,271 | 2,967 | 623,121 | `ce314339` |
| 40 | 4 | 6 | 1,274 | 2,986 | 703,996 | `404769eb` |
| 40 | 5 | 3 | 1,263 | 2,957 | 659,495 | `8b6f6c8f` |
| 2 | 1 | 12 | 1,268 | 2,973 | 771,491 | `cdc31683` |
| 2 | 2 | 3 | 1,264 | 2,971 | 770,607 | `ba272cb1` |
| 2 | 3 | 2 | 1,272 | 2,967 | 758,080 | `eabed8eb` |
| 2 | 4 | 7 | 1,274 | 2,983 | 786,760 | `efda05a3` |
| 2 | 5 | 8 | 1,265 | 2,969 | 783,592 | `1d6583f3` |

Regression overlay by origin:

| Prune | Seed | Regression | Listening | Analyzer | Pipeline | Sentences | Readings |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 40 | 1 | 516 | 6 | 14 | 35 | 61 | 379 |
| 40 | 2 | 518 | 6 | 15 | 36 | 59 | 381 |
| 40 | 3 | 518 | 6 | 15 | 34 | 61 | 381 |
| 40 | 4 | 518 | 5 | 16 | 34 | 60 | 382 |
| 40 | 5 | 519 | 5 | 15 | 35 | 61 | 382 |
| 2 | 1 | 520 | 5 | 16 | 36 | 60 | 382 |
| 2 | 2 | 517 | 6 | 15 | 35 | 59 | 381 |
| 2 | 3 | 518 | 6 | 15 | 34 | 61 | 381 |
| 2 | 4 | 519 | 5 | 16 | 35 | 60 | 382 |
| 2 | 5 | 519 | 5 | 16 | 35 | 60 | 382 |

Regression rows wrong:

- prune 40, seed 1: 0004, 0013, 0014, 0024, 0050, 0162, 0166, 0339
- prune 40, seed 2: 0004, 0013, 0050, 0088, 0111, 0162
- prune 40, seed 3: 0004, 0013, 0026, 0050, 0055, 0339
- prune 40, seed 4: 0001, 0004, 0024, 0034, 0050, 0115
- prune 40, seed 5: 0004, 0006, 0014, 0034, 0050
- prune 2, seed 1: 0004, 0006, 0050, 0088
- prune 2, seed 2: 0004, 0013, 0050, 0055, 0088, 0111, 0162
- prune 2, seed 3: 0004, 0013, 0026, 0050, 0055, 0339
- prune 2, seed 4: 0001, 0004, 0024, 0050, 0115
- prune 2, seed 5: 0004, 0006, 0034, 0050, 0111

The `data-row` origin is 21 of 21 for every model. Over seeds 1 to 5 at prune 40: regression mean 517.8 (516 to 519),
development natural 1,268.2 (1,263 to 1,274). Rows wrong on 2 or more of the 5 seeds at prune 40: `reg-0004` and
`reg-0050` (5 of 5), `reg-0013` (3), `reg-0014`, `reg-0024`, `reg-0034`, `reg-0162` and `reg-0339` (2 each).

**Freeze checklist** (readiness, not binding): not met. `listening`, `analyzer-test` and the golden origins are not 100
percent at seed 1 for any candidate, and `reg-0004` (L03 *tear*) and `reg-0050` (*I read every night before bed*) are
wrong on every seed at both prune thresholds. Candidate 2 is 618,493 bytes (within 0.70 MB); its development set end to
end is 3,152 of 3,293 (3,105 of the 3,235 rows that the shipped model's 3,083 refers to; candidate 1 3,126).

**Diagnosis of the two persistent rows** (training data only, no evaluation set): in `train.tsv` and `dev.tsv` the
target *tear* ends the sentence in 16 `tear.drop` rows and 1 `tear.rip` row, and *muscle* occurs in 2 `tear.rip` rows;
L03 has a body-part noun modifier before a sentence-final *tear* ("a deep muscle tear."), a construction the contrastive
rows ("a tear in his calf") do not cover. For *I read every night before bed*, the blind review dropped five
`read.present` rows with only a habitual adverbial as `unsure` (the tense is not fixed), so the habitual first-person
present without another tense cue is barely in the data. Both point to data, not to a defect.

**Defect checks (no defect found, no code changed).** The word-class lookup agrees between training and the analyzer
(end-to-end parity: 3,290 compared, 0 mismatched); the table covers 95.5 percent of the word tokens of the development
sentences (a rough regular-expression count); the class features, the curated features (the clause rule, the
auxiliary-before rule) and the primary-hint salt were read against the procedure entry and match it. A compact encoding
of the word-class section was estimated (9-bit packed class indexes save about 7.5 KB; Rice-coded hash gaps a further
19 KB): not enough to bring prune 2 under 0.70 MB, and not made.

**Diagnostic without word classes** (prune 40, `--clusters none`; not a decision input): epoch 4, dev
natural 1,263 and all 2,948, dev end to end 3,146, regression 517 (wrong `reg-0004`, `-0006`, `-0013`, `-0050`, `-0088`,
`-0166`, `-0339`), 122,901 weights, 253,934 bytes, image `7c3e8784`.

## PR B: procedure amendment before the read (targeted supplement and prune selection) (2026-11-21)

Written by the orchestrator before the pre-registered read, after the candidate log above. Sets read before this entry:
the development set and the regression overlay only; no generalization or holdout figure of any PR B model has been
seen. The pre-registered criterion (2026-11-20) and its read-discipline amendment (2026-11-21) are unchanged; this entry
amends only the pre-read knobs of the entry "PR B: procedure fixed before any training with word classes", which may
change freely before the read because nothing has been read.

**Why.** No candidate meets the freeze checklist. Two guarded rows (`reg-0004`, L03 *a deep muscle tear*; `reg-0050`,
*I read every night before bed*) are wrong on all 5 seeds, and the diagnosis on training data only (candidate log)
points to data gaps among the round's own lemmas: a body-part noun before a clause-final *tear* is almost absent for
`tear.rip`, and the habitual present of *read* without another tense cue does not survive the blind review. The
development rule picks prune 40 by one development natural row, within the seed noise, while the guarded origins vary
with the prune threshold (`bow`, `celtic` and `jesus` rows outside the round's lemmas).

**Amendment 1: one targeted supplement round.** Through the data-round procedure unchanged (the committed writer prompt
with a generic first line naming only reading ids, counts and a quota-line text; new `claude-sonnet-5.5` writer
instances; the same mechanical filters, including the near-repeat check against `regression.tsv` and the listening
sentences and the generalization novelty rules; a blind review by new `gpt-6.1-sol` instances on the new rows only; a
disagreement or `unsure` drops the row; the round-1 development rule). It is numbered as the next top-up round of each
part. It is limited to the round's ten lemmas and to readings whose guarded rows fail on development or regression
diagnosis, each with a balancing contrast reading, about 60 rows: `tear.rip` (a body-part noun or modifier right
before the target, the target ending its clause), `tear.drop` (the same frame, balancing), `read.present` (the habitual
present fixed by another present-tense verb or a present time frame in the same sentence), `read.past` (the habitual
past fixed the same way, balancing), `wind.coil` with *wound* (with a particle or preposition: up, onto, around,
round, into, tight), `wound.injury` (balancing), `bass.fish` (fishing, lakes, rivers, catching, cooking) and
`bass.music` (balancing). The quota-line texts are written from the training-data diagnosis, never from a regression
or generalization sentence. The rule "at most 2 top-up rounds" of the procedure entry is lifted for this one round only.

**Amendment 2: the prune threshold by the guarded regression rows.** The prune threshold is chosen, at seed 1 and after
the supplement, as the one with the most regression rows of the guarded origins (`listening`, `analyzer-test` and every
`golden-*` origin) right, among the thresholds whose model A is at most 700,000 bytes; ties go to the most development
natural rows, then to the smaller model. The development rule of the procedure entry remains reported. This uses the
regression overlay for iteration, as the read discipline allows; the generalization set and the holdouts stay unread.

**Unchanged.** Hash bits, scale, natural weight, epochs, seed, the feature templates, the fingerprint width, the
512-class table, the criterion, the guards and the failure path.

## PR B: targeted supplement (amendment 1) (2026-11-21)

Written after the supplement was merged and before any candidate was trained on its rows. Sets read for this entry:
nobody read a generalization or holdout sentence; the filter script reads the overlays and both holdouts and prints
only rule names, counts and stage row ids. No model was trained or evaluated for this entry.

**Writers** (amendment 1 of the entry "PR B: procedure amendment before the read"; new `claude-sonnet-5.5`
`general-purpose` instances, the committed prompt of each part verbatim with only the staging folder substituted and a
generic first line naming reading ids, row counts and a quota-line text written from the training-data diagnosis of
the candidate log, never from a regression or generalization sentence), dispatched by the orchestrator about
2026-11-21 12:46:40 UTC, finished about 12:50 UTC (the writers' notes give 12:47 to 12:55):

- Part 0, numbered top-up round 3 (stage `writers\part-0-top-up-3.tsv`, 48 rows):
  - `tear.rip` 12: "noun: a body-part noun or modifier right before the target (a muscle, hamstring, rotator cuff,
    ligament, cartilage, skin, retinal ... tear), with the target ending its clause or the sentence; vary the body
    parts and the frames widely";
  - `tear.drop` 6: "noun: the same frame for a teardrop (a modifier right before the target, the target ending its
    clause or the sentence), with crying, emotion or a face nearby, balancing the tear.rip rows";
  - `read.present` 12: "the habitual present, fixed by another present-tense verb or a present time frame in the same
    sentence (nowadays, these days, still, each night ... and another verb in the present tense); vary subjects,
    including I, we and you";
  - `read.past` 6: "the habitual past, fixed the same way by another past-tense verb or a past time frame in the same
    sentence (back then, as a child, in those years, used to ... and another verb in the past tense), balancing the
    read.present rows";
  - `wind.coil` 8: "past tense or participle of wind with a particle or preposition right after the target: wound up,
    onto, around, round, into, tight, back, along";
  - `wound.injury` 4: "noun or verb of injury in frames that also fit the coiling sense (a wound on, around, along
    something), balancing the wind.coil rows".
- Part 1, numbered top-up round 2 (stage `writers\part-1-top-up-2.tsv`, 12 rows):
  - `bass.fish` 8: "the fish: fishing, lakes, rivers and ponds, catching, landing, releasing, swimming and cooking
    bass, with the target as subject or object and in short plain sentences";
  - `bass.music` 4: "the low sound or instrument in the same kinds of plain frames (the target as subject or object of
    an everyday verb), balancing the bass.fish rows".

**Mechanical filters** (`filter_v4.py` on the two new files only, so against the merged train and dev data of the main
round; every rule of the entry "PR B: synthetic-v4 data round", including the near-repeat checks against the
regression overlay and the listening sentences, the generalization novelty rules N2 and N3 and the (reading, template)
pairs of train and dev): 60 written, **0 dropped**.

**Rule for this supplement only** (its quota lines were written after the regression diagnosis): a row is dropped when
one of its construction trigrams around the target (the generalization rule N3: words -2..0, -1..+1 or 0..+2) equals
one of a `regression.tsv` row of its lemma group. **6 dropped**: `syn4-0-t3-19` (`read.present`) and `syn4-0-t3-31`
(`read.past`), trigram "i _ the" (as `reg-0005`, `reg-0051`, `reg-0504`); `syn4-0-t3-39` (`wind.coil`), "the tape _"
and "tape _ onto" (as `reg-0006`); `syn4-1-t2-4` (`bass.fish`), `syn4-1-t2-10` and `syn4-1-t2-11` (`bass.music`),
"_ in the" (as `reg-0013`). The merge script recomputes the rule and checks that it drops exactly these six.

**Blind review round 3** (a new `gpt-6.1-sol` `general-purpose` instance, the committed review prompt verbatim; about
12:52 to 12:55 UTC; the earlier review folders moved aside to `review-round-1\` and `review-round-2\` with their inputs
in `review-round-1-inputs\` and `review-round-2-inputs\`; inputs `review-blind.tsv` and `review-labels.tsv` at the stage
root, files in `review\`): the 54 remaining rows, **54 agree**, 0 disagreements, 0 `unsure`. Judgments SHA-256
`31d06086d03525c1a58bbd2e5d4e41f896675eb1e755af047050d6f62eeb21e3` (frozen before the labels were opened; the merge
script checks it). Recorded as round 3 in `data/text-en/audit/review-synthetic-v4.tsv` (522 lines: 516 `keep`, 6
`drop`).

**Development rows.** The round-1 rule applied to the supplement's own rows per reading (templates in the order of
`SHA-1("text-en-split-v1|syn|<reading_id>|<template>")` to dev until `round(0.12 x rows)` are in dev, never splitting a
(reading, template) pair; no pair already in train or dev). Dev: `syn4-0-t3-7` (`tear.rip`), `-18` (`tear.drop`),
`-24` (`read.present`), `-33` (`read.past`), `-40` (`wind.coil`) and `syn4-1-t2-3` (`bass.fish`).

| Reading | Written | Regression trigram rule | Review dropped | Kept | Train | Dev |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `tear.rip` | 12 | 0 | 0 | 12 | 11 | 1 |
| `tear.drop` | 6 | 0 | 0 | 6 | 5 | 1 |
| `read.present` | 12 | 1 | 0 | 11 | 10 | 1 |
| `read.past` | 6 | 1 | 0 | 5 | 4 | 1 |
| `wind.coil` | 8 | 1 | 0 | 7 | 6 | 1 |
| `wound.injury` | 4 | 0 | 0 | 4 | 4 | 0 |
| `bass.fish` | 8 | 1 | 0 | 7 | 6 | 1 |
| `bass.music` | 4 | 2 | 0 | 2 | 2 | 0 |
| **Total** | **60** | **6** | **0** | **54** | **48** | **6** |

**Merge** (`merge_supp.py`, scratch; the same checks as `merge_v4.py`): `train.tsv` 20,269 to **20,317** rows and
`dev.tsv` 3,293 to **3,299** (appended after the main round's rows, ids in the order part, round, number);
`synthetic-provenance.tsv` 15,460 to 15,514. The `synthetic-v4` source now has 516 rows (452 train, 64 dev).
`gaps-v4.tsv` regenerated from `check-data --counts` (8 readings changed; still 13 readings one row short of 40);
`MANIFEST.tsv` regenerated; the data README, `audit/audit-summary.md`, the prompts README and both prompt records
completed (the prompt text is unchanged).

- SHA-256: `train.tsv` `1eed0dd91918df1f40a6eb5b5f1289844e524a0d4db843fe3c4694da297cebe0`, `dev.tsv`
  `200031813a5740c83d685c812246d020044e694b7fea9076b5493bb275351388`, `synthetic-provenance.tsv`
  `4319fa6b423ae39c6b99c39dcd927c9ca69bb1a199ae9fb69b8c5664d8b53284`, `audit/review-synthetic-v4.tsv`
  `dfdd61a0683397f941500530a79b1fa7f64f24c37e0bd8befb663fe2ca1477dd`; `regression.tsv` and `generalization.tsv`
  unchanged.

**Gate of the data.**

- `check-data`: **exit 0, 0 findings**, including the generalization novelty rules N1 to N3 against the final train and
  dev data and `seen_in_training` of every regression row recomputed (25 seen, 499 unseen, unchanged). Informational
  only, as before: the evaluation listing hash of the word-cluster audit changed again (`cb03a58f...4d37` now) because
  `dev.tsv` grew; `cluster --audit` needs the corpus cache, which is not in this worktree.
- **External leakage** (`check-data --eval-sets <scratch>\eval-sets`, the same three cached sets): **exit 0, 0 exact
  matches and 0 near duplicates** in all four data files. Rows sharing a word 5-gram: train 365 of 20,317 (one
  supplement row), dev 53 of 3,299, holdout-natural 23 of 1,167, holdout-independent 3 of 1,972. `leakage-report.tsv`
  not regenerated (as in the main round).

## PR B: candidate log after the supplement and the prune threshold by amendment 2 (2026-11-21)

Continues the entry "PR B: candidate log, development set and regression overlay only" on the data with the targeted
supplement (entry "PR B: targeted supplement (amendment 1)"; `dataSha256` `774b6a27...93c1`). Sets read for every model
in this entry: the development set (trainer level and end to end) and the regression overlay (524 rows) only. **No
generalization set and no holdout (natural or independent) was read for any candidate**: `evaluate` was run only as
`--set dev --set regression` (or `--set regression` for the seed diagnostics) from the same guarded script; never
`--gate`, `--seeds`, `--write`, `--check` or `--ablation`, and neither `build.ps1` nor a trainer test with `Committed`
in its name was run. Models in scratch folders `supp-pNN` and `supp-p32-sK`; the committed `heteronyms.bin` and
`training.json` are unchanged.

**Settings.** As in the earlier candidate log (feature set 2, model format 2, trainer version 2, seed 1, 22 hash bits,
scale 64, natural weight 3, at most 12 epochs, the temperature grid, keyed order, the 512-class table, curated layer
on); only the prune threshold varies.

**Prune grid at seed 1** (amendment 2). Development natural rows of 1,301 and all used rows of 3,098 at the trainer
level; end to end of 3,299 (in brackets: the 64 fourth-round development rows). Regression rows by origin: `listening`
7, `analyzer-test` 16, `golden-pipeline` 37, `golden-sentences` 61, `golden-readings` 382, `data-row` 21; "Guarded" is
the sum of the first five (503 rows). Every model keeps epoch 2.

Development set, size and image:

| Prune | Dev natural | Dev all | Dev end to end | Weights | Bytes | Image |
| ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 2 | 1,269 | 2,971 | 3,169 (52) | 198,707 | 757,345 | `60723c2e` |
| 16 | 1,270 | 2,973 | 3,171 (53) | 178,052 | 710,451 | `51d68ffc` |
| 20 | 1,270 | 2,973 | 3,171 (53) | 172,537 | 699,087 | `b907e097` |
| 24 | 1,271 | 2,972 | 3,170 (53) | 166,277 | 684,839 | `a0d57628` |
| 26 | 1,271 | 2,974 | 3,172 (54) | 163,069 | 677,963 | `6b788537` |
| 28 | 1,271 | 2,973 | 3,171 (54) | 160,143 | 672,229 | `f0980e40` |
| 30 | 1,271 | 2,972 | 3,170 (54) | 157,861 | 667,074 | `15ff5b03` |
| **32** | **1,271** | 2,973 | 3,171 (54) | 154,403 | **659,333** | `a65f0c29` |
| 36 | 1,271 | 2,971 | 3,169 (54) | 144,225 | 639,045 | `5d867ae9` |
| 40 | 1,271 | 2,975 | 3,173 (55) | 132,247 | 615,655 | `91f641c0` |
| 44 | 1,270 | 2,971 | 3,169 (53) | 121,346 | 594,705 | `c824ddc7` |
| 48 | 1,270 | 2,973 | 3,171 (53) | 109,409 | 571,115 | `305fddd9` |
| 56 | 1,269 | 2,967 | 3,165 (53) | 78,921 | 514,775 | `94be0dde` |
| 64 | 1,267 | 2,964 | 3,162 (54) | 34,135 | 435,886 | `ec0589d2` |

Regression overlay by origin:

| Prune | Regression | Guarded | Listening | Analyzer | Pipeline | Sentences | Readings | Data rows |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 2 | 518 | 497 | 7 | 14 | 35 | 61 | 380 | 21 |
| 16 | 517 | 496 | 7 | 14 | 35 | 60 | 380 | 21 |
| 20 | 517 | 496 | 7 | 14 | 35 | 60 | 380 | 21 |
| 24 | 517 | 496 | 7 | 14 | 35 | 60 | 380 | 21 |
| 26 | 517 | 496 | 7 | 14 | 35 | 60 | 380 | 21 |
| 28 | 518 | 497 | 7 | 14 | 35 | 60 | 381 | 21 |
| 30 | 519 | 498 | 7 | 14 | 36 | 60 | 381 | 21 |
| **32** | **520** | **499** | 7 | 14 | 36 | 61 | 381 | 21 |
| 36 | 518 | 497 | 7 | 14 | 35 | 61 | 380 | 21 |
| 40 | 518 | 497 | 7 | 14 | 35 | 61 | 380 | 21 |
| 44 | 518 | 497 | 7 | 14 | 35 | 61 | 380 | 21 |
| 48 | 518 | 497 | 7 | 14 | 35 | 61 | 380 | 21 |
| 56 | 518 | 497 | 7 | 14 | 35 | 61 | 380 | 21 |
| 64 | 518 | 497 | 7 | 14 | 35 | 61 | 380 | 21 |

Guarded rows wrong:

- prune 2: 0013, 0014, 0050, 0055, 0162, 0166
- prune 16: 0013, 0014, 0050, 0055, 0088, 0162, 0166
- prune 20: 0013, 0014, 0050, 0055, 0088, 0162, 0166
- prune 24: 0013, 0014, 0050, 0055, 0088, 0162, 0166
- prune 26: 0013, 0014, 0050, 0055, 0088, 0162, 0166
- prune 28: 0013, 0014, 0050, 0055, 0088, 0166
- prune 30: 0013, 0014, 0050, 0088, 0166
- prune **32**: 0013, 0014, 0050, 0166
- prune 36: 0013, 0014, 0050, 0055, 0166, 0339
- prune 40: 0013, 0014, 0050, 0055, 0166, 0339
- prune 44: 0013, 0014, 0050, 0055, 0166, 0339
- prune 48: 0013, 0014, 0050, 0055, 0166, 0339
- prune 56: 0013, 0014, 0050, 0055, 0166, 0339
- prune 64: 0013, 0014, 0050, 0055, 0157, 0339

The `data-row` origin is 21 of 21 for every model, so every wrong row is a guarded row. Prune 16 (710,451 bytes) and 20
(699,087 bytes) bracket the 700,000-byte limit. The supplement changed the guarded rows that failed on every seed
before it: `reg-0004` (L03 *a deep muscle tear*) is right for every model of this grid and `listening` is 7 of 7;
`reg-0050` (*I read every night before bed*) is still wrong everywhere.

**The prune threshold by amendment 2: 32.** Among the thresholds whose model is at most 700,000 bytes (20 to 64), prune
32 has the most guarded rows right, 499 of 503 (the next best, prune 30, 498), so no tie rule is needed: 659,333 bytes
(section 1, weights: 464,583 bytes raw, 306,476 deflated alone; section 2, word classes: 410,109 raw, 348,446 deflated
alone), image `a65f0c29574dfe3bca5a1bbac94fd5e196630242fd4bd295405a597bf4932d44`, development end to end 3,171 of 3,299
(3,117 of the 3,235 rows that the shipped model's 3,083 refers to). Wrong at seed 1: `reg-0013` (*He caught a large bass
in the lake*, `analyzer-test`), `reg-0014` (*He drew the bow and shot an arrow*, `analyzer-test`), `reg-0050`
(`golden-pipeline`) and `reg-0166` (*Celtic music uses fiddles and flutes*, `golden-readings`).

**The development rule, reported** (the procedure entry's rule, not the decision): the most development natural rows
over the grid is 1,271 (prune 24, 26, 28, 30, 32, 36 and 40); the tie goes to the smaller model: **prune 40** (615,655
bytes, guarded 497 of 503). The two rules differ by two guarded rows (`reg-0055`, `reg-0339` wrong at prune 40, right at
32, with `reg-0055` the more frequent failure over seeds, see below) and by no development natural row.

**Seed diagnostics at prune 32** (regression overlay only; trainer-level development figures from the `train` output):

Development set, size and image:

| Seed | Epochs | Dev natural | Dev all | Bytes | Image |
| ---: | ---: | ---: | ---: | ---: | --- |
| 1 | 2 | 1,271 | 2,973 | 659,333 | `a65f0c29` |
| 2 | 8 | 1,263 | 2,975 | 735,671 | `61eb6d9a` |
| 3 | 7 | 1,269 | 2,985 | 736,287 | `ee96913d` |
| 4 | 8 | 1,267 | 2,983 | 738,848 | `15a9ca6b` |
| 5 | 4 | 1,266 | 2,980 | 708,518 | `8fab25f2` |

Regression overlay by origin:

| Seed | Regression | Guarded | Listening | Analyzer | Pipeline | Sentences | Readings |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 520 | 499 | 7 | 14 | 36 | 61 | 381 |
| 2 | 517 | 496 | 7 | 15 | 35 | 58 | 381 |
| 3 | 518 | 497 | 7 | 15 | 34 | 60 | 381 |
| 4 | 521 | 500 | 7 | 16 | 36 | 61 | 380 |
| 5 | 520 | 499 | 7 | 15 | 34 | 61 | 382 |

Rows wrong:

- seed 1: 0013, 0014, 0050, 0166
- seed 2: 0013, 0050, 0055, 0059, 0088, 0111, 0162
- seed 3: 0013, 0024, 0050, 0055, 0107, 0162
- seed 4: 0050, 0162, 0458
- seed 5: 0014, 0034, 0050, 0055

Over seeds 1 to 5: regression mean 519.2 (517 to 521), development natural mean 1,267.2 (1,263 to 1,271); `listening` 7
of 7 on every seed. Guarded rows wrong on 2 or more seeds: `reg-0050` (5 of 5), `reg-0013`, `reg-0055` (*The bass swam
deep in the lake*) and `reg-0162` (*The actors take a bow at the end*) 3 each, `reg-0014` 2; every other guarded row is
right on at least 4 of the 5 seeds. Seeds 2 to 5 keep later epochs and are over 700,000 bytes; only model A at seed 1 is
the model that would ship.

**Freeze checklist** (readiness, not binding): **not met**. At seed 1 `analyzer-test` is 14 of 16, `golden-pipeline` 36
of 37 and `golden-readings` 381 of 382 (`listening` and `golden-sentences` complete); `reg-0050`, `reg-0013`,
`reg-0055` and `reg-0162` are right on fewer than 4 of the 5 seeds. Size: 659,333 bytes, within 0.70 MB. Development end
to end: 3,171 of 3,299 (3,117 of 3,235 against the shipped 3,083).

**Default changed.** `TrainingSettings.Default.PruneThreshold` 2 to 32 (with its remarks and the training-record
design text). `train --model-dir <scratch>\freeze-check` with no option reproduces image `a65f0c29...2d44` (the same
file bytes as `supp-p32`). The pinned fixture image of the trainer tests is unchanged by the new default (656 of 656
trainer tests pass with `FullyQualifiedName!~Committed`).

## PR B: freeze (2026-11-21)

Written before the pre-registered read. Sets read before this entry: the development set and the regression overlay
only (entries "PR B: synthetic-v4 data round", "PR B: candidate log", "PR B: targeted supplement (amendment 1)" and "PR
B: candidate log after the supplement and the prune threshold by amendment 2"); no generalization or holdout figure of
any PR B model has been seen, and there was no accidental early read.

**Frozen state.** A local WIP commit (never pushed as is) of everything in the working tree except
`src/DemaConsulting.Speech.Text.En/Resources/heteronyms.bin`,
`tools/DemaConsulting.Speech.Text.En.Trainer/training.json` and `docs/evaluation/text-en/*` (all three still the shipped
PR A state). The commit holds this entry; its SHA and its `git write-tree` hash are recorded in the paragraph "Commit
references of the freeze" below, appended by a follow-up local commit that changes only this log, so the code and the
data of the read are exactly the WIP commit's tree.

**Data** (SHA-256):

- `data/text-en/train.tsv` `1eed0dd91918df1f40a6eb5b5f1289844e524a0d4db843fe3c4694da297cebe0` (20,317 rows)
- `data/text-en/dev.tsv` `200031813a5740c83d685c812246d020044e694b7fea9076b5493bb275351388` (3,299 rows)
- `data/text-en/word-clusters.tsv` `17e94d11a69627513a92679370432697d452011b6ffb3168ecc35f5643d5007b` (60,000 words,
  512 classes)
- `data/text-en/readings.tsv` `b8f7bbc3394322f53b76ef5107138535a9c8775b39c6030a744b0a203d63da8e`
- `data/text-en/forms.tsv` `7deefce9c61c8bc236a0acbac0f9a4ec5e3235dee9b872fea7a423b86987ea48`
- for reference: `regression.tsv` `877bdf03bfaf543a8d37754197712f01a70320212fc91f10735e1013738bde39` (524 rows) and
  `generalization.tsv` `b2b33140fefbe408198477ff92cc6c5666e4af360890019c97ecb9dd9902937e` (unchanged); the training
  record's `dataSha256` `774b6a27df26e6be91b86a671b0360bf863204920d5941aa9958e2a2d46793c1` and `featuresSha256`
  `b8a5d573b3c72bb8f1e8d878ff8d4421800e3303be2bcd6f51ef1bb93ea85b94`

**Settings** (`TrainingSettings.Default` at the freeze): seed 1, 22 hash bits, scale 64, **prune threshold 32** (by
amendment 2; the development rule would give 40), natural weight 3, at most 12 epochs, temperature grid of 1,000 steps
of 1/20, keyed order, word classes `table`, curated layer on; feature set 2, model format 2, trainer version 2, 16-bit
lookup fingerprint. Evaluation: seeds 1 to 5, 10,000 resamples, bootstrap seed 7, 95 percent.

**Model A at seed 1**: image SHA-256 `a65f0c29574dfe3bca5a1bbac94fd5e196630242fd4bd295405a597bf4932d44`, 2 epochs,
154,403 weights, 659,333 bytes (874,788 uncompressed); development natural 1,271 of 1,301, all 2,973 of 3,098, end to
end 3,171 of 3,299; regression 520 of 524. `train --model-dir <scratch>` with no option reproduces it. The read's A at
seed 1 must have this image hash.

**Freeze checklist** (readiness, not binding): **not met**. At seed 1 `reg-0013`, `reg-0014` (`analyzer-test`),
`reg-0050` (`golden-pipeline`) and `reg-0166` (`golden-readings`) are wrong; over seeds 1 to 5 `reg-0050` is wrong on
all 5 and `reg-0013`, `reg-0055` and `reg-0162` on 3. `listening` is 7 of 7 on every seed. Size within 0.70 MB
(659,333 bytes). Development end to end 3,117 of the shipped model's 3,235 rows against its 3,083 (not a gate). The read
is taken anyway, as the procedure fixes.

**The read** (next, once, from the WIP commit's tree): `evaluate --ablation --ablation-dir
C:\work\sp-clusters-scratch\pr-b\read-1`.

**Commit references of the freeze** (appended by a follow-up local commit that changes only this log): WIP commit
`dded64974b57716aa9a23d8dcdfb7ca8a719d1ca`, `git write-tree` hash (its tree)
`9585af89bd628fe56691a404b3bae65dc583352b`, parent `dfb90c9` (C2). Neither commit is pushed.

## PR B: the pre-registered read (read 1) (2026-11-21)

Written immediately after the read, before anything else was done. The read was run once, at HEAD `f97a0ad` (the
freeze's follow-up commit; code and data exactly tree `9585af89bd628fe56691a404b3bae65dc583352b` of the WIP commit
`dded649`), with a clean working tree and a current Release build, from `C:\work\sp-clusters-b`:
`dotnet run --no-build -c Release --project tools/DemaConsulting.Speech.Text.En.Trainer -- evaluate --ablation
--ablation-dir C:\work\sp-clusters-scratch\pr-b\read-1`. It started 2026-11-21T08:24:56-05:00, took 55 seconds and
exited 1 (fail). The ablation folder did not exist before the run. The console output (92 lines, SHA-256
`ef2b11db1eb7b315d640d681823035c3892d0f49341eb1c0e14fd1e847691303`) is kept outside the repository as
`C:\work\sp-clusters-scratch\pr-b\read-1-console.txt`. Only the aggregate figures that the command prints were seen;
no per-row or per-reading generalization detail was looked at, and `generalization.tsv` was not opened. No holdout was
read (no `--gate`, `--write` or `--check`).

**Image hash check.** Model A at seed 1 (`clusters-on-curated-on-seed-1`) has image SHA-256
`a65f0c29574dfe3bca5a1bbac94fd5e196630242fd4bd295405a597bf4932d44`, 154,403 weights, 659,333 bytes: **equal to the
frozen hash**. The A images of seeds 2 to 5 (`61eb6d9a…`, `ee96913d…`, `15a9ca6b…`, `8fab25f2…`) also equal those of
the seed diagnostics before the freeze.

**Primary endpoint** (generalization set, 242 rows, end to end; B is clusters off with the curated layer on, seed 1):

| Measure | Value |
| --- | --- |
| A (clusters on, curated on, seed 1) | 179 of 242 (73.97 %) |
| B (clusters off, curated on, seed 1) | 186 of 242 (76.86 %) |
| Delta A minus B | **-289 basis points** (-7 rows) |
| 95 % family-clustered interval (64 families, 10,000 resamples, seed 7) | **[-661, +79] basis points** |

| Seed | A | B | Delta (rows) | Delta (basis points) |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 179 | 186 | -7 | -289 |
| 2 | 187 | 179 | +8 | +331 |
| 3 | 184 | 178 | +6 | +248 |
| 4 | 184 | 177 | +7 | +289 |
| 5 | 179 | 182 | -3 | -124 |
| Mean | | | +2.2 (sum +11) | **+91** |

**Criterion.**

| Condition | Required | Read 1 | Met |
| --- | --- | --- | --- |
| Delta | at least +300 basis points | -289 basis points | **no** |
| Lower bound of the 95 % interval | above 0 | -661 basis points | **no** |
| Mean delta over seeds 1 to 5 | above 0 | +91 basis points | yes |
| Guard: generalization A | at least 177 of 242 | 179 of 242 | yes |

**Verdict: FAIL** (the evaluator prints `CRITERION: FAIL` and exits 1). The delta and the interval conditions fail; the
seed mean and the generalization guard hold.

**Subsets** (A minus B at seed 1, family-clustered 95 % intervals):

| Subset | Rows | Families | A | B | Delta | Interval |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| `novel-vocabulary` | 135 | 51 | 100 | 104 | -296 bp | [-709, +78] bp |
| `novel-construction` | 107 | 39 | 79 | 82 | -280 bp | [-988, +330] bp |
| `cue_unsolvable=yes` | 80 | 51 | 61 | 61 | 0 bp | [-380, +366] bp |
| `cue_unsolvable=no` (printed, not pre-registered) | 162 | 64 | 118 | 125 | -432 bp | [-983, +65] bp |

**2 by 2 ablation at seed 1** (clusters {off, on} by curated layer {off, on}; all four cells trained and evaluated in
this run). Image hashes: on/on `a65f0c29…`, on/off `75ecc873…`, off/on `8999933d…`, off/off `08340753…`.

| Set | on/on (A) | on/off | off/on (B) | off/off |
| --- | ---: | ---: | ---: | ---: |
| Generalization, all (242) | 179 | 182 | 186 | 183 |
| Generalization, `novel-vocabulary` (135) | 100 | 99 | 104 | 102 |
| Generalization, `novel-construction` (107) | 79 | 83 | 82 | 81 |
| Generalization, `cue_unsolvable=yes` (80) | 61 | 63 | 61 | 61 |
| Generalization, `cue_unsolvable=no` (162) | 118 | 119 | 125 | 122 |
| Regression, all (524) | 520 | 515 | 520 | 516 |
| Regression, seen (25) | 25 | 24 | 25 | 25 |
| Regression, unseen (499) | 495 | 491 | 495 | 491 |
| Development, trainer natural (1,301) | 1,271 | 1,268 | 1,267 | 1,264 |
| Development, trainer all (3,098) | 2,973 | 2,954 | 2,969 | 2,958 |
| Development, end to end (3,299) | 3,171 | 3,152 | 3,167 | 3,156 |

Main effect of clusters (mean over the curated levels) and the interaction (on,on) minus (on,off) minus (off,on) plus
(off,off); intervals are family-clustered on the generalization set and row-clustered on the regression overlay (the
development figures are printed without intervals):

| Set | Main effect of clusters | Interval | Interaction | Interval |
| --- | ---: | --- | ---: | --- |
| Generalization, all | -165 bp | [-525, +169] bp | -248 bp | [-631, +125] bp |
| Generalization, `novel-vocabulary` | -259 bp | [-620, +73] bp | -74 bp | [-543, +441] bp |
| Generalization, `novel-construction` | -47 bp | [-758, +586] bp | -467 bp | [-1,100, 0] bp |
| Generalization, `cue_unsolvable=yes` | +125 bp | [-122, +390] bp | -250 bp | [-800, +233] bp |
| Generalization, `cue_unsolvable=no` | -309 bp | [-847, +182] bp | -247 bp | [-753, +208] bp |
| Regression, all | -10 bp | [-105, +95] bp | +19 bp | [-115, +153] bp |
| Regression, seen | -200 bp | [-600, 0] bp | +400 bp | [0, +1,200] bp |
| Regression, unseen | 0 bp | [-100, +110] bp | 0 bp | [-140, +140] bp |
| Development, trainer natural | +31 bp | | 0 bp | |
| Development, trainer all | 0 bp | | +26 bp | |
| Development, end to end | 0 bp | | +24 bp | |

**Next.** The criterion failed, so the binding failure path of the entry "PR B: procedure fixed before any training
with word classes" applies: no tuning on the generalization set or a holdout, diagnosis on the development set and the
regression overlay only, and at most one further frozen candidate after an entry "PR B: second pre-registration" (read
2). The failure path was not started with this entry. No candidate model or record is committed, and nothing is pushed.

## PR B: failure-path diagnosis after read 1 (development set and regression overlay only) (2026-11-21)

Written after read 1 and before any second pre-registration. This entry is a diagnosis only: it freezes nothing, commits
no model, record or setting, and takes no read. Sets read for this entry: the development set (trainer level and end to
end) and the regression overlay (524 rows) only. No generalization set or holdout was evaluated for any model. The only
generalization information used is the aggregate read-1 figures already logged above. No `evaluate` was run with
`--set generalization`, `--set holdout-*`, `--gate`, `--seeds`, `--write`, `--check` or `--ablation`. Neither
`build.ps1` nor a trainer test with `Committed` in its name was run.

The work used a diagnostic program outside the repository (`C:\work\sp-clusters-scratch\pr-b\diag\`). It is linked
against the trainer and library binaries of the frozen tree (copied, unchanged), and the only overlay it loads is the
regression overlay. It does two things:

- It retrains with the trainer's own `AveragedPerceptronTrainer`, scores every epoch with the trainer's own decision
  rule, and builds the image with the trainer's own writer. With the frozen settings it reproduces the image hashes of
  model A at seeds 1 to 5 (`a65f0c29…`, `61eb6d9a…`, `ee96913d…`, `15a9ca6b…`, `8fab25f2…`) byte for byte.
- It breaks a candidate's score down by template and by channel. The channels are the label salt, the role salt and
  the primary part-of-speech (hint) salt. A copy of the feature extractor records the template of each feature, and
  for every row explained it was checked to emit exactly the runtime extractor's hashes.

A second small program, linked against the PR A binaries, lists the regression rows that the feature-set-1 models of
the entry "PR B: order-only baseline" get wrong. It does so with and without the cue priors, on a copy of PR A's data
that has the current `regression.tsv`. Margins below are integer score differences at scale 64: gold minus the
strongest other candidate.

**Wrong guarded rows of model A at seed 1, by root cause.**

- **`reg-0050`** (*I read every night before bed*, `read.present` read as `read.past`, `golden-pipeline`): a modeling
  and data outcome, not a defect.
  - **How often it is wrong.**
    - Feature-set-2 models: on every model of PR B, and on every epoch of every model inspected. That covers all 12
      epochs of seeds 1 to 5 at prune 32 and at prune 52, and all 12 epochs at seed 1 at prune 40, 48, 56, 60 and 64:
      0 of 180 epoch snapshots are right.
    - The read-1 models: all four cells of the 2 by 2 at seed 1, and both A and B at seeds 2 to 5.
    - The PR B trainer on PR A's data, before any synthetic-v4 row, with clusters on and curated on, clusters off, and
      both off: wrong on all 5 seeds each.
    - Feature set 1 (the order-only baseline models): wrong in 9 of 10 models, with the cue priors and without them.
      The only exception is the shipped model (positional order, seed 1). So this row has been right only for the
      shipped seed and order, never as a stable property of the method.
  - **Why.** The sentence has no tense cue except the habitual adverbial *every night*. In `train.tsv`, *read* with no
    auxiliary, modal, *to* or negation in the three words before it is `read.past` 116 times and `read.present` 68
    times (plus 20 sentence-initial imperatives). A pronoun right before it is 28 past and 23 present. *every* or *each*
    before a word of time occurs in 6 present and 2 past rows. The blind review of the data round drops a row whose only
    tense cue is a habitual adverbial as `unsure`: 5 such `read.present` rows were dropped in round 1. The amendment-1
    supplement's habitual rows fix the tense with another present-tense verb.
  - **Margins.** A: −5,824. B (clusters off): −1,552. Clusters on with the curated layer off: −3,600. Both off: −3,492.
  - **Contributions in A.** Feature-set-1 templates give −3,009. Those are mostly frame features: no modal within three
    words, no punctuation before, position 1, a pronoun at −1 and a preposition at +3. Feature-set-2 templates give
    −2,815:
    - the bag of classes (the broad 6-bit noun class of *night* and *bed*): −726;
    - the classes of the words at −2 to +2: −702;
    - the shared neighbor classes: −631;
    - the class next to the target with its tag: −504;
    - the target form with its tag: −252.
  - **Curated layer and hint salt.** No curated feature fires on the sentence. The hint channel contributes exactly 0,
    because both readings of *read* have the primary tag `VERB`, so a hint-salted feature adds the same weight to both.
- **`reg-0013`** (*He caught a large bass in the lake*, `bass.fish` read as `bass.music`, `analyzer-test`) and
  **`reg-0166`** (*Celtic music uses fiddles and flutes*, `celtic.culture` read as `celtic.team`, `golden-readings`):
  rare lemmas not yet learned at the epoch the selection keeps. The prune threshold is not the cause.
  - **The epoch choice.** The selection keeps epoch 2 because it is the strict maximum of natural development rows:
    1,271, against 1,264 to 1,268 later. That is a difference of 3 rows, within the seed noise. It is not a tie. These
    lemmas have few natural development rows.
  - **Later epochs fix both rows.** At seed 1 and prune 32, `reg-0013` is right from epoch 8 and `reg-0166` from epoch
    10. At prune 2 and epoch 2 both are also wrong (entry "candidate log after the supplement").
  - **`reg-0013`.** Margin −832. The largest pulls toward music are *the* at +2 (word, function word and its word
    classes, −332 each) and a 6-bit class in the bag (−412). The hint channel is 0, because both readings are
    `NOUN`.
  - **`reg-0166`.** Margin −3,547. The label and role channels give −3,890 and −2,210. They come from the
    position and shape features (sentence-initial, position 0, no punctuation before, the shapes of the neighbors),
    which the training data ties to the team name. The hint channel (`ADJ` against `PROPN`) gives +2,553 in
    favor of the gold reading, so the primary part-of-speech salt helps this row rather than causing the error.
- **`reg-0014`** (*He drew the bow and shot an arrow*, `bow.weapon` read as `bow.bend`, `analyzer-test`): the word
  classes at epoch 2. The margin is −1,608. Feature-set-1 templates favor the gold reading by +2,628, and feature-set-2
  templates oppose it by −4,236:
  - the classes of *the* at −1, word and shared, −322 each;
  - the 10-bit verb class of *drew* and *shot* in the bag, −392 twice.

  The row is right from epoch 4 at seed 1. The hint channel is 0 (both `NOUN`).
- **Other guarded rows that flicker across seeds.** `reg-0055` (*The bass swam deep in the lake*), `reg-0162` (*The
  actors take a bow at the end*), `reg-0339` (*jesus*), `reg-0024`, `reg-0088`, `reg-0111` and `reg-0458` change with
  the seed and the epoch. No epoch and prune combination keeps them all right.

**Defects.** None was found. The checks:

- The image is reproduced bit for bit, and the extractor copy emits exactly the runtime hashes.
- The curated past-time feature fires and is learned (`reg-0520`, *Last night, I read it*: +382 for `read.past`).
- The hint salt cancels exactly when two candidates share their primary tag (`reg-0013`, `-0014` and `-0050`: 0).
- The training update of a shared hint bucket nets to zero in that case (the same bucket gets +1 and −1).
- End-to-end parity holds: 0 mismatched rows, as logged.

The natural-row epoch rule and its tie to the earlier epoch work as specified. They are a modeling choice that
disfavors lemmas with few natural development rows, not a bug.

**Candidates** (each a single change to the frozen settings; data, features and every other setting unchanged). The
diagnostic program trained them into `C:\work\sp-clusters-scratch\pr-b\fp-NN-sK\`:

- **`fp-00`**: the frozen A (reference, reproduced).
- **`fp-01`**: the epoch chosen by all development rows instead of the natural ones (ties to the earlier epoch). The
  prune threshold is chosen by the amendment-2 rule over 40, 48, 52, 56, 60 and 64, which gives 52. Prune 40 and 48
  exceed 700,000 bytes at seed 1 (728,993 and 705,384).
- **`fp-02`**: no early stopping (always 12 epochs) at the same prune threshold, 52.

Development figures are at seed 1. The trainer-level figures are of 1,301 natural and 3,098 used rows, and end to end
of 3,299 rows. Regression is of 524 rows.

| Candidate | Prune | Epoch | Dev natural | Dev all | Dev end to end | Bytes | Image |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `fp-00` | 32 | 2 | 1,271 | 2,973 | 3,171 | 659,333 | `a65f0c29` |
| `fp-01` | 52 | 12 | 1,266 | 2,987 | 3,185 | 687,723 | `86c7112d` |
| `fp-02` | 52 | 12 | 1,266 | 2,987 | 3,185 | 687,723 | `86c7112d` |

Regression at seed 1, by origin (`listening` 7, `analyzer-test` 16, `golden-pipeline` 37, `golden-sentences` 61,
`golden-readings` 382, `data-row` 21):

| Candidate | Regression | Listening | Analyzer | Pipeline | Sentences | Readings | Data rows | Guarded wrong |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `fp-00` | 520 | 7 | 14 | 36 | 61 | 381 | 21 | 0013, 0014, 0050, 0166 |
| `fp-01` | 521 | 7 | 16 | 35 | 60 | 382 | 21 | 0024, 0050, 0088 |
| `fp-02` | 521 | 7 | 16 | 35 | 60 | 382 | 21 | 0024, 0050, 0088 |

At seed 1, `fp-01` and `fp-02` are the same model, because epoch 12 has the most development rows. Seeds 1 to 5
(regression only; `L/A/P/S/R` = listening, analyzer, pipeline, sentences, readings):

| Candidate | Seed | Epoch | Regression | L/A/P/S/R | Bytes | Guarded wrong |
| --- | ---: | ---: | ---: | --- | ---: | --- |
| `fp-00` | 1 | 2 | 520 | 7/14/36/61/381 | 659,333 | 0013, 0014, 0050, 0166 |
| `fp-00` | 2 | 8 | 517 | 7/15/35/58/381 | 735,671 | 0013, 0050, 0055, 0059, 0088, 0111, 0162 |
| `fp-00` | 3 | 7 | 518 | 7/15/34/60/381 | 736,287 | 0013, 0024, 0050, 0055, 0107, 0162 |
| `fp-00` | 4 | 8 | 521 | 7/16/36/61/380 | 738,848 | 0050, 0162, 0458 |
| `fp-00` | 5 | 4 | 520 | 7/15/34/61/382 | 708,518 | 0014, 0034, 0050, 0055 |
| `fp-01` | 1 | 12 | 521 | 7/16/35/60/382 | 687,723 | 0024, 0050, 0088 |
| `fp-01` | 2 | 6 | 517 | 7/15/34/59/381 | 651,464 | 0013, 0034, 0050, 0055, 0088, 0111, 0162 |
| `fp-01` | 3 | 6 | 520 | 7/14/35/61/382 | 657,222 | 0013, 0014, 0050, 0055 |
| `fp-01` | 4 | 12 | 522 | 7/16/36/61/381 | 687,651 | 0050, 0458 |
| `fp-01` | 5 | 5 | 521 | 7/15/35/61/382 | 634,102 | 0014, 0050, 0055 |
| `fp-02` | 2 | 12 | 516 | 7/15/34/58/381 | 687,384 | 0013, 0034, 0050, 0055, 0059, 0088, 0111, 0162 |
| `fp-02` | 3 | 12 | 518 | 7/14/34/60/382 | 693,204 | 0013, 0014, 0024, 0050, 0055, 0107 |
| `fp-02` | 5 | 12 | 519 | 7/14/35/61/381 | 689,074 | 0013, 0014, 0050, 0055, 0458 |

`fp-02` at seeds 1 and 4 is the same model as `fp-01`. Guarded rows wrong on 2 or more of the 5 seeds:

- `fp-00`: `reg-0050` 5, `reg-0013`, `-0055` and `-0162` 3 each, `reg-0014` 2.
- `fp-01`: `reg-0050` 5, `reg-0055` 3, `reg-0013`, `-0014` and `-0088` 2 each.
- `fp-02`: `reg-0050` 5, `reg-0013` and `-0055` 3 each, `reg-0014`, `-0024`, `-0088` and `-0458` 2 each.

Regression means over seeds 1 to 5: 519.2, 520.2 and 519.2. The data-row origin is 21 of 21 throughout.

**The orchestrator's bar for read 2 is not met by any candidate.** The bar requires every guarded row right at seed 1
and on at least 4 of 5 seeds, at most 700,000 bytes. Selecting the epoch on all development rows, or not stopping early,
fixes the three rare-lemma rows that A gets wrong at seed 1 and improves the development set end to end by 14 rows. But
it does not reach `reg-0050`, and it trades in `reg-0024` and `reg-0088`. Across seeds, several rare-lemma rows still
fail on 2 or 3 of 5.

For comparison, the feature-set-1 method of the shipped model would not meet this bar either: positional order, seeds 1
to 5, with the cue priors. `reg-0050`, `reg-0004` and `reg-0006` are each wrong on 4 of the 5 seeds.

`reg-0050` needs either data (read is a round lemma) or an owner decision. `reg-0014` and `reg-0162` (*bow*), `reg-0166`
(*celtic*) and `reg-0339` (*jesus*) are outside the round's lemmas, so the data-round procedure cannot reach them. No
data was written, nothing was frozen, and read 2 was not taken. The diagnostic programs, logs and models are in
`C:\work\sp-clusters-scratch\pr-b\` (`diag\`, `diag-pra\`, `fp-*`); nothing of them is in the repository.

## PR B: result — criterion not met at read 1; stopped without read 2 (2026-11-21)

Written after the failure-path diagnosis, as the last PR B entry. Sets read for this entry: none. No model was trained
or evaluated for it, and the generalization set and the holdouts were not read again.

**Result: the pre-registered criterion is not met. PR B stops, and its result is incomplete.**

- **Read 1** (the only read; entry "PR B: the pre-registered read (read 1)"): A 179 of 242, B 186 of 242. The
  difference A minus B is −289 basis points (−7 rows), with the 95 % family-clustered interval [−661, +79] basis
  points. The mean difference over seeds 1 to 5 is +91 basis points, and generalization A is 179, at least 177. The
  difference and the lower bound fail the criterion; the seed mean and the generalization guard hold.
- **Guards.** Model A at seed 1 (image `a65f0c29…`, the frozen model) gets four guarded regression rows wrong:
  `reg-0013` and `reg-0014` (`analyzer-test`), `reg-0050` (`golden-pipeline`) and `reg-0166` (`golden-readings`). As
  shipped, it would fail the analyzer tests and the goldens that these rows come from. The natural holdout gate was not
  evaluated, because the read failed first.

**Read 2 was not taken.** The failure path allows one further frozen candidate after an entry "PR B: second
pre-registration". The orchestrator's bar for that candidate was every guarded regression row right at seed 1 and on at
least 4 of the 5 seeds, with the model at most 700,000 bytes, judged on the development set and the regression overlay
only. The diagnosis (entry above) found no defect and no candidate that meets the bar. `reg-0050` (*I read every night
before bed*) is never right for feature set 2: 0 of 180 epoch snapshots, on every PR B model and seed. Read 2 would
have spent the generalization set on a candidate that could not ship.

**State of this branch.** Two commits before this log: C1 (the keyed row-visiting order as an experiment option; the
committed `training.json` only gains `"rowOrder": "positional"`, and the image stays
`73de5a3f74e34b9373d165e4bedd603988e5d1d81bab36fb6c3ab6444f603df0`) and C2 (the PR B procedure and the synthetic-v4
prompts). The shipped PR A model and its four cue priors stay in place: `heteronyms.bin`, `training.json`'s inputs and
result, `HeteronymClassifier` and `ReadingInventory` are those of PR A. Nothing of feature set 2, model format 2, the
synthetic-v4 data or the prune default 32 is on this branch.

**Where the attempt is kept** (not in the repository, never pushed):

- the local branch `wip/text-en-cluster-features-attempt-1` at `c609af6` (the freeze `dded649`, its follow-up `f97a0ad`,
  the read-1 log `bdfb1de` and the diagnosis `c609af6`);
- `C:\work\sp-clusters-scratch\pr-b\failed-attempt\`: the patches `0034231..c609af6` and a README;
- the stage `C:\work\sp-clusters-b-stage\` (writers, reviews), and the scratch models in
  `C:\work\sp-clusters-scratch\pr-b\` (frozen A in `supp-p32\`, the read-1 models in `read-1\`, `fp-01` in
  `fp-01-s1\` to `fp-01-s5\`).

The repository files that the attempt changed live on that local branch only. They are the 115 files, other than this
log, that differ between this branch and `c609af6`. `heteronyms.bin` and `training.json` are not among them; the frozen
model A is kept in the scratch folder only.

- **Data** (12, in `data/text-en/`): the synthetic-v4 rows of `train.tsv`, `dev.tsv` and `synthetic-provenance.tsv`;
  the `regression.tsv` rows `reg-0520` to `reg-0524`; the added `audit/review-synthetic-v4.tsv`;
  `audit/audit-summary.md`, `gaps-v4.tsv`, `MANIFEST.tsv` and `README.md`; and the completed records in
  `prompts/README.md`, `prompts/generation-synthetic-v4.md` and `prompts/review-synthetic-v4.md`.
- **Runtime code** (9, in `src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/`): `FeatureHashing.cs`,
  `HeteronymClassifier.cs`, `HeteronymContext.cs`, `HeteronymFeatureExtractor.cs`, `ModelBinaryFormat.cs`,
  `ReadingInventory.cs`, `SparsePerceptronModel.cs`, `SparsePerceptronModelWriter.cs` and the added
  `WordClusterMap.cs`.
- **Trainer code** (11, in `tools/DemaConsulting.Speech.Text.En.Trainer/`): `TrainingDataSubsystem/TextEnDataSet.cs`
  (the `synthetic-v4` source) and, in `TrainingSubsystem/`, `AveragedPerceptronTrainer.cs`, `EvaluateCommand.cs`,
  `EvaluationOverlay.cs`, `EvaluationRecord.cs`, `HeteronymEvaluator.cs`, `ModelSelection.cs`, `TrainCommand.cs`,
  `TrainingCorpus.cs`, `TrainingRecord.cs` (the prune default 32) and the added `ClusterAblation.cs`.
- **Tests** (21): in `test/DemaConsulting.Speech.Text.En.Tests/`, `Fakes/HeteronymFixtureModel.cs`,
  `SpeechTextEnTests.cs`, `TestData/heteronym-feature-vectors.tsv` and, in `HeteronymSubsystem/`,
  `HeteronymClassifierTests.cs`, `HeteronymFeatureExtractorTests.cs`, `HeteronymSubsystemTests.cs`,
  `ReadingInventoryTests.cs`, `SparsePerceptronModelTests.cs` and the added `WordClusterMapTests.cs`; in
  `test/DemaConsulting.Speech.Text.En.Trainer.Tests/`, `Fakes/TrainingFixture.cs`, `TrainingSubsystemTests.cs`,
  `TrainingDataSubsystem/SchemaCheckerTests.cs` and, in `TrainingSubsystem/`, `AveragedPerceptronTrainerTests.cs`,
  `EvaluateCommandTests.cs`, `EvaluationRecordTests.cs`, `HeteronymEvaluatorTests.cs`, `ModelSelectionTests.cs`,
  `TrainCommandTests.cs`, `TrainingCorpusTests.cs`, `TrainingRecordTests.cs` and the added `ClusterAblationTests.cs`.
- **Documents** (62): `requirements.yaml` and `.reviewmark.yaml`; `definition.yaml`, `introduction.md` and
  `speech-text-en.md` in both `docs/design/` and `docs/verification/`, and `docs/reqstream/speech-text-en.yaml`; the
  design, requirement and verification documents of the heteronym subsystem and its units `heteronym-classifier`,
  `heteronym-feature-extractor`, `reading-inventory` and `sparse-perceptron-model`; the same three documents of the
  training subsystem and its units `averaged-perceptron-trainer`, `evaluate-command`, `evaluation-overlay`,
  `evaluation-record`, `heteronym-evaluator`, `model-selection`, `train-command`, `training-corpus`, `training-record`
  and the added `cluster-ablation`; the design documents of the training-data subsystem and its units
  `license-checker` and `text-en-data-set`; and the requirement and verification documents of `schema-checker`.

**The generalization set has been read once** (read 1) for the comparison of word classes against no word classes. The
criterion's discipline applies to any future attempt: a data or setting change after a read voids that read's result,
so a new attempt needs a new pre-registration entry. That entry states the change, its development and regression
basis, and that the set has been read once; its read is read 2, and both reads are reported.

**Open questions for the owner.**

1. **The guard definition.** Should the guarded origins judge the one shipped seed, as the analyzer tests and goldens
   do today, or a seed-stable property (right on at least 4 of the 5 seeds)? The shipped feature-set-1 method fails the
   seed-stable form too: `reg-0050`, `reg-0004` and `reg-0006` are each wrong on 4 of its 5 seeds.
2. **The `reg-0050` golden.** *I read every night before bed* has no tense cue besides the habitual adverbial, and the
   blind review drops such rows as `unsure`. Is the present reading the only acceptable one, or is the sentence
   ambiguous? The failure path does not allow editing or demoting a golden without the owner.
3. **A retry.** Whether to try again with the epoch chosen on all development rows (as `fp-01`) and targeted data: the
   habitual present of *read*, and the rare lemmas outside the round's ten (*bow*, *celtic*, *jesus*). It would go
   under a new pre-registration, as stated above.

## Cleanup after PR B: statistical gate, golden forks and corpus tooling, fixed before any evaluation (2026-11-21)

Written after the PR B result entry and before anything of this cleanup is evaluated. No seed study has run, no fork
candidate has been written into a golden or evaluated by any model, and no blind screen of the regression overlay has
run. Sets read for this entry: none. No model was trained or evaluated for it.

The owner answered the open questions of the PR B result entry with these decisions:

- **D1:** the quality gate becomes a statistical gate over four scored sets, with recorded thresholds, intervals and a
  flip report, in place of the D13 gate and the PR B guards. Classifier correctness is judged by this gate, not by
  per-sentence test assertions.
- **D2:** a golden whose sentence admits two readings is forked into one unambiguous sentence per reading.
- **D3:** the word clusters are removed. The corpus downloader, the per-book pins, the leakage audit and the
  evaluation-set tooling are kept under neutral names for later features.
- **No corpus snapshot:** the deterministic tar snapshot of the corpus and its `--from-snapshot` option are removed,
  and no snapshot is published (item 6).

The rules below are fixed now, before any evaluation they govern.

**1. PR B is closed.** The generalization set's role as the pre-registered comparison set ended with read 1. By D1 it
becomes a gate (non-regression) set. Later comparisons (learned cue features, word vectors) use a fresh set and a new
pre-registration.

**2. The gate.** The scored sets:

| Gate set | File | Rows (expected) | Cluster for the interval |
| --- | --- | ---: | --- |
| `regression` | `regression.tsv`, all origins, after the forks (item 4) | about 530 | each row its own cluster |
| `holdout-natural` | `holdout-natural.tsv` | 1,167 | source document (266) |
| `holdout-independent` | `holdout-independent.tsv` | 1,972 | each row |
| `generalization` | `generalization.tsv` (frozen, unchanged) | 242 | `family` (64) |

- **Metric:** end-to-end accuracy through `EnglishTextAnalyzer` with the shipped classifier, cue priors included. A
  missing reading counts as wrong, the same path as the evaluation record. The value is in basis points.
- **Interval:** the existing integer percentile clustered bootstrap of the accuracy itself, 95 percent, 10,000
  resamples, seed 7. It is reported and recorded for every set, but it does not gate.
- **Decision rule:** the gate passes if and only if, for every gate set S, `correct_S × 10,000 ≥ T_S × rows_S`, an
  integer comparison where `T_S` is the threshold of S in basis points. Any failing set fails the gate, and the output
  names the set. The point estimate decides, not an interval bound: a lower bound at the threshold would fail the
  shipped model itself on the small sets, and an upper bound would accept almost anything on 242 rows.
- **Threshold rule ("typical score minus a margin"):** for each S, let `c₁…c₅` be the correct counts of the current
  recipe at seeds 1 to 5 (the committed `training.json` settings, positional order, feature set 1, the cue priors; only
  the seed changes). With `m` their mean and `s` their sample standard deviation (divisor 4):

  ```text
  T_S = floor( 10,000 × (m − 2·s) / rows_S )      [basis points]
  ```

  In exact integer arithmetic, with `S₁ = Σc`, `S₂ = Σc²` and `N = rows_S`, `T_S` is the largest integer `t ≥ 0` with
  `D = 10,000·S₁ − 5·t·N ≥ 0` and `D² ≥ 5·10⁸·(5·S₂ − S₁²)`.
- **Why this margin:** a model of the same quality falls below the mean minus two seed standard deviations in about 2.5
  percent of training runs (normal approximation). For five values every sample lies within `s·(n−1)/√n = 1.789·s` of
  the mean (Samuelson's inequality), so each of the five seeds, the shipped seed 1 included, is strictly above
  `m − 2·s`, and the floor only lowers `T_S`. The shipped model therefore passes by construction. The regression set is
  selected for seed 1's correctness (its rows are goldens the shipped model gets right); the seed spread measures how a
  model of equal quality scores on such a set.
- **Expected values** for the sets with known seed figures (gate run 1; the same models, re-evaluated): natural
  holdout (1,123, 1,114, 1,129, 1,132, 1,129) 9,520 basis points; independent holdout (1,882, 1,912, 1,914, 1,905,
  1,886) 9,483 basis points. The seed study must reproduce both; any difference is a defect. The regression threshold
  is computed after the forks; the generalization threshold is computed in the seed study.
- **Retired** when the new gate is in force: the D13 gate (93.0 percent pooled on the natural holdout, plus an
  improvement interval above zero) and the PR B guards (natural at least 1,123 of 1,167, every golden right on the
  shipped seed, generalization at least 177). The improvement over the own-tag baseline and its interval stay
  reported.

**3. Reads.** One seed study, `evaluate --seeds`, over seeds 1 to 5 of the current recipe on the four gate sets, run
once after the forks.

- It prints aggregate figures only. No per-row or per-reading output of seeds 2 to 5 is printed or opened.
- Seed 1 is the shipped model, whose per-row overlay results are already public in `overlay-rows.tsv`.
- This is a threshold derivation, not a comparison read. The holdout figures repeat gate run 1's models.
- Its figures are logged in an entry written immediately after the run, before anything else.

**4. The forks and the screen.** The known ambiguous golden is `reg-0050`, *I read every night before bed.*
(`read.present`; `piper-pipeline-golden.tsv:e05` and `kokoro-pipeline-golden.tsv:e05`). Its candidate forks, in this
order; the first candidate a blind reviewer confirms wins:

- **Present** (replaces the sentence of e05; `reg-0050` keeps its id and ref, with new offsets):
  1. *I read every night before bed, and I still do.* (the owner's example; "and I still do" may lean toward
     `read.past`, and the blind reviewer decides);
  2. *Nowadays I read every night before bed.*;
  3. *These days I read every night before bed, and my sister reads in the morning.*
- **Past** (new id e11 in both pipeline goldens; new overlay row `reg-0525`, `read.past`, `golden-pipeline`,
  `piper-pipeline-golden.tsv:e11`, `seen_in_training=no`, CC0-1.0):
  1. *When I was a child, I read every night before bed.* (the owner's example);
  2. *Back then I read every night before bed.*
- If no candidate of a side is confirmed, that fork is dropped. If both sides fail, e05 and `reg-0050` are dropped.
- New overlay ids start at `reg-0525`; `reg-0520` to `reg-0524` were used by the failed attempt and are referred to
  above.

The 10 `forced` rows of `readings-golden.tsv` (the shipped model's misses, lines 35, 43, 46, 80, 241, 257, 265, 320,
330 and 362) join the overlay as `golden-readings` rows, so every golden is scored and the suite is not selected only
for seed 1.

**The blind ambiguity screen.** No blind reviewer has judged the regression overlay or the goldens before.

- Scope: every `regression.tsv` row except `data-row`, the 10 `forced` rows and all fork candidates.
- The reviewer sees the id, sentence, span, target and the candidate readings with their `readings.tsv` descriptions,
  shuffled with a recorded seed, and never the label. The answer is one reading or `unsure`, with a confidence and a
  reason.
- The judgments are staged outside the repository and frozen by SHA-256 before they are compared with the labels. They
  are recorded in `data/text-en/audit/regression-review.tsv`.
- Decisions: agree keeps the row. An `unsure`, or a disagreement that makes the sentence two-way, forks a golden: the
  sentence is replaced by the fork of its labeled reading, the other fork is added under a new id, and both forks are
  reviewed again. A `listening` row is never edited; it goes to the owner. A disagreement where the label is plainly
  right is kept with the reasoning noted. A golden without a clean fork is dropped with its overlay row.
- The shipped model's readings on the fork rows are recorded and are never used to choose a fork.

**5. No change** to `train.tsv`, `dev.tsv`, the inventory (`readings.tsv`, `forms.tsv`), the training settings, the
model (`heteronyms.bin` and the `inputs` and `result` of `training.json`) or the cue priors.

**6. The corpus without the word clusters.**

- `clustering.json` and `word-clusters.tsv` are deleted. They stay in git history at `0034231` (the PR A merge).
- By owner decision there is no corpus snapshot. The publication proposal of the entry "Snapshot publication
  (documented only; not published)" above is withdrawn.
- The identity of the corpus is the per-book `body_sha256` and `body_bytes` pins of `cluster-sources.tsv`. Drift is
  reported by `corpus --fetch` (a book whose mirror text differs from its pin fails, exit 1) and handled by re-pinning
  under review.
- `cluster-removals.tsv` must be re-audited with `corpus --audit` before the corpus is next used. The audit needs the
  local cache, and no cache is present on this machine, so it is not re-run in this cleanup.
