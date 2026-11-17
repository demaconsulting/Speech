# DemaConsulting.Speech.Text.En.Trainer

Developer tool for the Text.En training data in `data/text-en/`. It is never packed or shipped and has no dependency on
the Speech or Text.En libraries.

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
