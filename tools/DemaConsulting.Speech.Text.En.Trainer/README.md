# DemaConsulting.Speech.Text.En.Trainer

Developer tool for the Text.En training data in `data/text-en/` and for the heteronym model that is trained from it.
It is never packed or shipped. The `check-data` units use only the base class library; the `train` and `evaluate`
units reference the Text.En library (its internals are visible to this tool) so a model is trained with the
analyzer's own tokenization, part-of-speech tagging, feature extractor, scoring and model format, not a copy of them.
The `evaluate-external` units read the pinned external datasets only through a SHA-256 verified cache. The `corpus`
units fetch a pinned public-domain corpus and audit it against the evaluation sets with the analyzer's own tokenizer.

Commands:

- `check-data`: verify the data folder
- `train`: train the heteronym model, or verify a committed one with `--check`
- `evaluate`: measure a model on the development and holdout sets and decide the quality gate
- `evaluate-external`: measure the embedded model on external, evaluation-only datasets fetched at evaluation time
- `corpus`: fetch the pinned public-domain corpus and audit it against every evaluation set

## check-data

```text
dotnet run --project tools/DemaConsulting.Speech.Text.En.Trainer -- check-data [options]
```

Verifies the data folder and reports every violation as `check-data: <check>: <file>[:<line>]: <message>`:

- `manifest`: `MANIFEST.tsv` hashes, sizes and row counts, sorted and complete listing
- `format`: UTF-8 without BOM, LF, final newline, no empty lines, constant columns, NFC, no quoting
- `schema`: readings, mappings, forms, decisions, labeled rows with strict whole-word spans, provenance ids, counts
- `license`: open licenses only, per-source license policy, attribution, no evaluation-only material
- `leakage`: the splits stay disjoint (sentences, source documents, synthetic templates)
- `overlay`: the regression overlay (references, seen-in-training marks, no holdout contact) and the generalization
  set (novelty rules N1 to N3 against the training and development data, and its quotas once it has rows)
- `corpus`: the corpus files, offline: the schema of `cluster-sources.tsv` and `cluster-removals.tsv` and a pin and
  a public-domain basis for every used book (a `leakage` finding when a used corpus book is a development or
  natural-holdout document); skipped when the folder has neither file

Options:

- `--data <dir>`: the data folder (default: `data/text-en`, searched upward from the current directory)
- `--eval-sets <dir>`: local evaluation sets for the optional external leakage check (or set `TEXTEN_EVAL_SETS`).
  Each `*.txt` file is one set (one sentence per line); each `*.tsv` file is one set (a header row and a `sentence`
  or `text` column, no quoting). Evaluation data must never be committed.
- `--leakage-report <file>`: write the per-set, per-split counts (requires evaluation sets supplied via `--eval-sets` or
  `TEXTEN_EVAL_SETS`; outside the data folder)
- `--generalization <file>`: check a staged copy of the generalization set in place of the committed one; its novelty
  findings give a generic reason and never name a training row, so they can go back to the blind writer
- `--max-findings <n>`: findings printed per check (default 25)
- `--counts`: print the per-reading row counts

Exit codes: `0` no findings, `1` findings, `2` usage or I/O error.

## train

```text
dotnet run -c Release --project tools/DemaConsulting.Speech.Text.En.Trainer -- train [options]
dotnet run -c Release --project tools/DemaConsulting.Speech.Text.En.Trainer -- train --check [options]
```

Trains the heteronym model (a candidate-restricted averaged perceptron on the hashed features of the runtime's own
feature extractor, in integer arithmetic) and writes the model file `heteronyms.bin` and the training record
`training.json`. With `--check` it retrains with the settings in the record and fails when the committed model or the
record is stale, naming what differs: the data, the tagger, the feature stream, the choices or the weights.

Options:

- `--data <dir>`: the data folder (default: `data/text-en`, searched upward from the current directory)
- `--model-dir <dir>`: the folder of `heteronyms.bin` and `training.json` (default: the committed locations)
- `--check`: verify instead of write
- `--seed`, `--hash-bits`, `--prune`, `--natural-weight`, `--max-epochs`: experiment settings; they require
  `--model-dir` and cannot be combined with `--check`, so an experiment never overwrites the committed files
- `--row-order positional|keyed`: experiment setting, the order in which the training rows are visited in each epoch.
  `positional` (the default) is a seeded shuffle of the row positions; `keyed` sorts the visits by a seeded key of the
  row id, so adding rows never changes the relative order of the existing rows. Recorded as `rowOrder` in
  `training.json`

Exit codes: `0` trained or current, `1` stale, `2` usage, I/O or data error.

### What is read and what is committed

- **Isolation.** Training and every choice made while training read only `train.tsv`, `dev.tsv`, `readings.tsv` and
  `forms.tsv`. The code that loads the training data opens exactly those four files and never lists the folder, so
  the holdout sets cannot influence a model. The epoch count and the calibration temperature are chosen on the
  development set; the pruning threshold and the natural-row weight were chosen on it too (see `RUN-LOG.md`).
- **Inputs hash.** `training.json` records `dataSha256`, the hash of the four input files, and `featuresSha256`, the
  hash of the ordered feature stream of the training and development rows, so a drift of the tagger or the
  extractor is visible even when the data files are unchanged. The tagger package version is recorded too.
- **Determinism contract.** One thread; features precomputed once in file order; integer weights, integer exact
  averaging and integer rounding; a fixed SplitMix64 generator with a Fisher-Yates shuffle (positional order) or
  SplitMix64 keys of the FNV-1a hash of the row ids (keyed order); the calibration
  temperature chosen from a fixed grid with a software exponential and logarithm; ordinal sorting everywhere;
  invariant-culture formatting. The image is compared uncompressed, because the bytes of a Deflate stream depend on
  the runtime. The same inputs give the same model on every operating system.

## evaluate

```text
dotnet run -c Release --project tools/DemaConsulting.Speech.Text.En.Trainer -- evaluate [options]
```

Measures a model on labeled sets end to end through the real analyzer (a row for which the analyzer emits no reading
counts as wrong), with the own-part-of-speech, majority-reading and first-candidate baselines, a paired
document-clustered bootstrap interval of the improvement over the own-part-of-speech baseline, strata by source and
by reading, accuracy by confidence, and a parity check of the trainer's own predictions against the analyzer's.

Options:

- `--data <dir>`, `--model-dir <dir>`: as for `train`
- `--model <file>`: evaluate this model file instead of the one in the model folder
- `--set <name>`: `dev`, `holdout-natural`, `holdout-independent` or `all` (repeatable; default `dev`); `regression`
  and `generalization` evaluate the evaluation overlays (not part of `all`) with and without the curated cue priors
- `--gate`: decide the statistical quality gate on its four scored sets (`regression`, `generalization`,
  `holdout-natural`, `holdout-independent`): each must reach its recorded threshold; prints each set's accuracy with its
  95% clustered interval and threshold, and the flips of the model under test against the committed record
  (`evaluate: FLIP <set> <id> <gold> <previous>-><current> <change>`); exit `0` for a pass, `1` for a failure and `2`
  when a gate set has no file. Together with `--check` (or `--write`) the exit code is `0` only when the gate passes and
  the record is current. `evaluate --gate --model <candidate>` shows a later change's flips before anything is written
- `--seeds`: retrain with each evaluation seed, score each model end to end on every gate set and print the counts and
  the threshold the gate's rule gives (aggregates only, never a per-row result)
- `--write`, `--check`, `--record-dir <dir>`: write or verify the deterministic evaluation record (default folder
  `docs/evaluation/text-en`); the record includes the overlays whose files exist, after the data sets

Exit codes: `0` done or gate passed, `1` gate failed or record stale, `2` usage, I/O or data error.

**The statistical gate.** The learned classifier is judged on four scored sets, not per golden sentence. A set passes
when `correct × 10,000 ≥ threshold × rows`; the gate passes when every set passes. The thresholds are in
`EvaluationSettings.Default` (`TrainingRecord.cs`) and mirrored in `training.json`; `train --check` reports a
difference. Each threshold is the floor of the mean minus two sample standard deviations of the current recipe's correct
counts at the seeds 1 to 5 (`evaluate --seeds` prints the value by this rule), so every seed of the recipe passes and a
clearly worse model fails:

| Gate set | Rows | Threshold | Shipped model |
| --- | ---: | ---: | ---: |
| `generalization` | 242 | 69.02% | 73.14% |
| `holdout-independent` | 1,972 | 94.83% | 95.44% |
| `holdout-natural` | 1,167 | 95.20% | 96.23% |
| `regression` | 529 | 96.01% | 98.30% |

To change the recipe or the data: run `evaluate --gate --model <candidate>` and review every `broken` flip of a
`listening` or golden row; to re-derive the thresholds after a deliberate recipe change, run `evaluate --seeds` once,
record the counts in `RUN-LOG.md` before anything else, and update `EvaluationSettings.Default`, `train` and
`evaluate --write`. The record's `gate-rows.tsv` and `flips.tsv` hold every gate row's labeled, previous and current
reading and the rows that changed (ids only).

Every harvested row needs an entry in `harvest-provenance.tsv`, because the bootstrap resamples the rows of one source
document together. Run `check-data` first: `evaluate` does not validate the data folder, but it stops with exit code `2`
instead of guessing when a harvested row has no source document.

Reading a holdout set is the one irreversible act of model development: a set that has guided a choice no longer
measures anything. The tool says so when it reads one. The holdouts are now repeated-use gate sets (non-regression), so
only fresh sets give unbiased estimates for a later comparison.

**Overlays.** An overlay (`regression.tsv` or `generalization.tsv`) is reported like a set and then by slice (all
rows; seen and unseen rows and each origin of the regression overlay; each kind, cue-solvable class and family of the
generalization set) with the accuracy with the cue priors and of the model alone, each with a clustered 95% interval
(a generalization row resamples with its family), the paired cue effect and the rows on which a cue acts. The
model-only reading is computed by the evaluation with the classifier's own scoring and no prior; the classifier and the
model are not changed. A generalization row marked `cue_unsolvable=yes` on which the adjective, past-time or a context
cue test fires, or whose reading the priors change, is a data error (exit `2`) that names the row and the cue.

## evaluate-external

```text
dotnet run -c Release --project tools/DemaConsulting.Speech.Text.En.Trainer -- evaluate-external [options]
```

Measures the embedded model on two external, evaluation-only datasets: Google's WikipediaHomographData (WHD; the `eval`
split is the headline set `whd-eval`, the `train` split is `whd-train`) and the English read set of heteronym-bench
(`bench`). The datasets are fetched on demand from commit-pinned archives into the git-ignored cache
`data/text-en/external/`, each file verified against its SHA-256 pin; nothing of them is committed, embedded or used for
training or for any choice, and no setting may ever be chosen from these numbers. Rows are converted at evaluation time
(WHD byte offsets become character offsets, bench offset defects are repaired), mapped to our reading ids by the id-only
files `data/text-en/whd-mapping.tsv` and `data/text-en/bench-mapping.tsv`, and evaluated with the same end-to-end
evaluator as `evaluate`. Rows that cannot be used are skipped and reported by reason (`bad-offsets`, `duplicate-of-whd`,
`reading-not-candidate`, `unknown-form`, `unmapped-label`); the 267 bench items copied from WHD are skipped as
`duplicate-of-whd`. The report has, per set, accuracy with a clustered bootstrap interval, the always-default, majority
and own-part-of-speech baselines, the improvement with its interval, accuracy by reading and by confidence, and, with
`--seeds`, the model retrained with each of the five evaluation seeds. The console output and the record hold only
counts, our own ids, set names and pins, never a sentence, target word or external label.

Options:

- `--fetch`: download the pinned archives that are missing or fail verification (HTTPS; published into the cache only
  when every pinned file matches), then evaluate. Without it the network is never used and a missing or modified cache
  stops the command with exit code `2`
- `--set <name>`: `whd-eval`, `whd-train`, `bench` or `all` (repeatable; default `all`)
- `--cache <dir>`: the cache folder (default `<data>/external`); it must be that folder or outside the repository
- `--pins <file>`: the pins (default `external-sources.json` in this folder, with the sibling `external-files.tsv`)
- `--data <dir>`, `--model-dir <dir>`, `--model <file>`: as for `evaluate`
- `--seeds`: also retrain with each evaluation seed (implied by `--write` and `--check`)
- `--write`, `--check`, `--record-dir <dir>`: write or verify the deterministic external evaluation record (default
  folder `docs/evaluation/text-en/external`)

Exit codes: `0` done or record current, `1` record stale, `2` usage, I/O, pin, hash or data error (no figures are
printed).

To reproduce the committed record: `evaluate-external --fetch --write` (network once, about a minute), then
`evaluate-external --check`. The command is never run by `build.ps1`, `lint.ps1` or a workflow: it needs the network and
third-party data, so it is a manual, local procedure; there is deliberately no `workflow_dispatch` workflow for it.

### How the pins were made

`external-sources.json` names each source (repository, commit, archive URL, license statement, file count, byte total
and the SHA-256 of the listing of its files). `external-files.tsv` lists every pinned file as `source`, `path`,
`sha256`, `bytes`, sorted by source and path in ordinal order. They were generated once with a throwaway script that
downloaded the archive of each commit (`https://codeload.github.com/<repository>/tar.gz/<commit>`), took the pinned
files (WHD: `LICENSE`, `data/eval/*.tsv`, `data/train/*.tsv`; heteronym-bench: `LICENSE`, `LICENSE-DATA` and the English
read set file of the benchmark) and hashed them. The listing hash is the SHA-256 of the lines `path TAB sha256 LF` in
ordinal path order. Changing a pin is a reviewed change: a different commit means a different evaluation.

### License note

WHD is Apache-2.0 code with Wikipedia text (CC BY-SA 3.0); the heteronym-bench data is CC BY-SA 4.0. Neither is
committed, embedded or used to train. The record and the two mapping files contain no text of either dataset, only
numbers, the pins and our own ids.

## corpus

```text
dotnet run -c Release --project tools/DemaConsulting.Speech.Text.En.Trainer -- corpus <mode> [options]
```

Fetches the US public-domain books listed in `data/text-en/cluster-sources.tsv` and audits them against every
evaluation set. It changes no model, and no model uses the corpus. Give exactly one mode:

- `--fetch [--write-pins]`: fetch the plain text and metadata of every `used` book that is missing from the cache or
  fails its pin, only over HTTPS from an official mirror (`gutenberg.pglaf.org`; `aleph.gutenberg.org` with
  `--mirror`), never from the main web site and never through a redirect (a redirect fails that book), one request at a
  time with `--delay-ms` (default 2,000, at least 1,000) between requests, at most three retries after 5, 15 and 45
  seconds, and a stop on HTTP 403 or 429. The body (the text between the start and end markers, without credits, license
  text or any paragraph naming the archive) is pinned by size and SHA-256; `--write-pins` fills the pins of rows that
  have none and never changes a pin. Exit `1` when a book failed
- `--audit [--write]`: compare every corpus sentence with every sentence of `dev.tsv`, `holdout-natural.tsv`,
  `holdout-independent.tsv`, `regression.tsv` and `generalization.tsv` (rules `exact`, `near`, `contains`) and print the
  removals per file and rule; `--write` writes `cluster-removals.tsv` (ids and hashes only), otherwise exit `1` when it
  is missing or differs

Options: `--threads <n>` (default the processor count; the output does not depend on it); `--delay-ms <n>`;
`--mirror <host>`; `--cache <dir>` (default `data/text-en/external/cluster-corpus`, git-ignored; it must be inside
`data/text-en/external` or outside the repository); `--data <dir>`.

Exit codes: `0` done or current, `1` stale removals or a book failed, `2` usage, I/O or data error.

**Determinism.** Books are tokenized and audited in parallel into separate slots and merged in ordinal source-id order,
and every attribution and sort uses integer and ordinal comparisons only, so the removals are byte-identical for any
`--threads` and on every operating system. Each stage prints its time.

**Drift and re-pinning.** The pins of `cluster-sources.tsv` are the identity of the corpus; there is no snapshot
archive (owner decision). `--fetch` into an empty
cache reproduces it bit for bit; a book whose mirror text no longer matches its pin fails (`failed: the mirror's text
differs from the pin`, exit `1`) and its pin is never overwritten. To keep such a book after review, write `-` in its
`body_sha256` and `body_bytes` and run `corpus --fetch --write-pins` (both edits are committed together, because
`check-data` rejects a used book without a pin); to drop it, set its status to `rejected-content` with no pin. Then run
`corpus --audit`, review the removals, run `corpus --audit --write`, regenerate `MANIFEST.tsv`, run `check-data` and
record the re-pin in `RUN-LOG.md`.

**Adding or removing a book.** Edit its row in `cluster-sources.tsv` (a domain book is used only with its publication
year and the evidence for it read from the book itself), run `corpus --fetch --write-pins`, then
`corpus --audit --write`, and record the run in `RUN-LOG.md`.

The command is never run by `build.ps1`, `lint.ps1` or a workflow; only its tests run in CI, offline, on generated
books. CI instead runs the cheap offline gate of `check-data` (check `corpus`): the source list and the removals must
be well formed, and every used book needs a pin. Whether the removals are still current needs the corpus cache, so
`check-data` cannot tell; `corpus --audit` compares them byte for byte and is re-run before the corpus is next used.

## Files

- `training.json`: the committed training record of the shipped model: the settings, the hash of the inputs and of
  the feature stream, the tagger and format versions and the SHA-256 of the uncompressed model image. `train --check`
  verifies it, with `src/DemaConsulting.Speech.Text.En/Resources/heteronyms.bin` (the model the package embeds), by
  retraining. A model or a record is only ever written by `train`; do not edit either by hand.
- `RUN-LOG.md`: the protocol, the development-set exploration, and every run of the gate with its numbers, the
  public-domain corpus and its audit, and the word-class attempt (PR B) and its negative result
- `docs/evaluation/text-en/` (in the repository root): the committed evaluation record that `evaluate --write` writes
  and `evaluate --check` verifies, with the headline results and the limits of the evidence in its `README.md`
- `external-sources.json`, `external-files.tsv`: the pins of the external evaluation datasets (see `evaluate-external`)
- `docs/evaluation/text-en/external/` (in the repository root): the committed external evaluation record that
  `evaluate-external --write` writes and `evaluate-external --check` verifies, with its results and caveats in its
  `README.md`

## Licensing of the model

The model file contains weights, not text. Its training rows come from three license classes: sentences adapted
from MASC (CC BY 3.0 US), public-domain sentences and CC0 sentences; the labels generated or chosen by language models
are dedicated to the public domain under CC0 1.0. Because the weights were trained in part on data adapted from MASC,
the package that embeds them provides them under the terms of that license and carries the attribution (see
`src/DemaConsulting.Speech.Text.En/PACKAGE-LICENSE.md` and `THIRD-PARTY-NOTICES.md`, and `data/text-en/ATTRIBUTION.md`).
