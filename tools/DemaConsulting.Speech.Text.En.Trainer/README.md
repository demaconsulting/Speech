# DemaConsulting.Speech.Text.En.Trainer

Developer tool for the Text.En training data in `data/text-en/` and for the heteronym model that is trained from it.
It is never packed or shipped. The `check-data` units use only the base class library; the `train` and `evaluate`
units reference the Text.En library (its internals are visible to this tool) so a model is trained with the
analyzer's own tokenization, part-of-speech tagging, feature extractor, scoring and model format, not a copy of them.

Commands:

- `check-data`: verify the data folder
- `train`: train the heteronym model, or verify a committed one with `--check`
- `evaluate`: measure a model on the development and holdout sets and decide the quality gate

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

Options:

- `--data <dir>`: the data folder (default: `data/text-en`, searched upward from the current directory)
- `--eval-sets <dir>`: local evaluation sets for the optional external leakage check (or set `TEXTEN_EVAL_SETS`).
  Each `*.txt` file is one set (one sentence per line); each `*.tsv` file is one set (a header row and a `sentence`
  or `text` column, no quoting). Evaluation data must never be committed.
- `--leakage-report <file>`: write the per-set, per-split counts (requires evaluation sets supplied via `--eval-sets` or
  `TEXTEN_EVAL_SETS`; outside the data folder)
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
  averaging and integer rounding; a fixed SplitMix64 generator and Fisher-Yates shuffle; the calibration
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
- `--set <name>`: `dev`, `holdout-natural`, `holdout-independent` or `all` (repeatable; default `dev`)
- `--gate`: evaluate `holdout-natural` and decide the quality gate (at least 93.0% pooled accuracy and a 95% interval
  lower bound of the improvement above zero); exit `0` for a pass and `1` for a failure. Together with `--check` (or
  `--write`) the exit code is `0` only when the gate passes and the record is current
- `--seeds`: retrain with each evaluation seed and report the holdout accuracy of each
- `--write`, `--check`, `--record-dir <dir>`: write or verify the deterministic evaluation record (default folder
  `docs/evaluation/text-en`)

Exit codes: `0` done or gate passed, `1` gate failed or record stale, `2` usage, I/O or data error.

Every harvested row needs an entry in `harvest-provenance.tsv`, because the bootstrap resamples the rows of one source
document together. Run `check-data` first: `evaluate` does not validate the data folder, but it stops with exit code `2`
instead of guessing when a harvested row has no source document.

Reading a holdout set is the one irreversible act of model development: a set that has guided a choice no longer
measures anything. The tool says so when it reads one, and `RUN-LOG.md` counts the runs of the gate.

## Files

- `training.json`: the committed training record of the shipped model: the settings, the hash of the inputs and of
  the feature stream, the tagger and format versions and the SHA-256 of the uncompressed model image. `train --check`
  verifies it, with `src/DemaConsulting.Speech.Text.En/Resources/heteronyms.bin` (the model the package embeds), by
  retraining. A model or a record is only ever written by `train`; do not edit either by hand.
- `RUN-LOG.md`: the protocol, the development-set exploration, and every run of the gate with its numbers
- `docs/evaluation/text-en/` (in the repository root): the committed evaluation record that `evaluate --write` writes
  and `evaluate --check` verifies, with the headline results and the limits of the evidence in its `README.md`

## Licensing of the model

The model file contains weights, not text. Its training rows come from three license classes: sentences adapted
from MASC (CC BY 3.0 US), public-domain sentences and CC0 sentences; the labels generated or chosen by language models
are dedicated to the public domain under CC0 1.0. Because the weights were trained in part on data adapted from MASC,
the package that embeds them provides them under the terms of that license and carries the attribution (see
`src/DemaConsulting.Speech.Text.En/PACKAGE-LICENSE.md` and `THIRD-PARTY-NOTICES.md`, and `data/text-en/ATTRIBUTION.md`).
