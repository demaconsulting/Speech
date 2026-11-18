# DemaConsulting.Speech.Phonemes.En.Tools

Developer tool for the data and measurements behind `DemaConsulting.Speech.Phonemes.En`. It is never packed or shipped.
It references the library (whose internals are visible to it) so the embedded lexicon, reading tables and
letter-to-sound model are built, verified and measured with the library's own phone model, image format and morphology,
not copies of them. The data folder is `data/phonemes-en/` (see its `README.md` for provenance, pins and licenses).

Run every command from the repository root:

```text
dotnet run -c Release --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- <command> [options]
```

## Reproducibility gates

Each of these has a `--check` mode that exits 1 when the committed artifact is stale; run them after any change to the
inputs or to the library code they depend on.

- `build-lexicon [--check]`: the CMUdict-derived lexicon resource and its `MANIFEST.tsv` rows, from the pinned, fetched
  CMUdict. `--check` compares the source hash, the image hash and size and the decompressed committed resource, enforces
  the size budget and round-trips every entry.
- `train-lts [--check]`: the letter-to-sound model resource (about 8 minutes). The training is deterministic, so
  `--check` retrains and compares the image hash with the manifest and the decompressed committed resource.
- `propose-readings --check`: the frozen reading table and form overrides against the reading inventory of
  `DemaConsulting.Speech.Text.En` and the lexicon: every reading id has a row, every `cmu#N` row is that variant, every
  (spelling, reading) pair resolves, and the committed gap list is empty. Without `--check` it proposes rows into the
  git-ignored `data/phonemes-en/work/` for human review.
- `regenerate-goldens [--sentences] [--readings] [--oov] [--renderers]`: the expected columns of the golden corpora in
  `test/DemaConsulting.Speech.Phonemes.En.Tests/TestData/` (sentences, readings, out-of-vocabulary words, and the Piper
  and Kokoro renderings). A changed line is a behavior change: review the diff line by line, and fix wrong output in the
  code, never in the golden. The pipeline goldens of the model packages (`kokoro-pipeline-golden.tsv`,
  `piper-pipeline-golden.tsv`) are pinned by those packages' tests, not generated here: when the renderer goldens
  change, review the failing pipeline tests' actual output and update them by hand.

## Measurements

- `measure-morphology`: how well the morphology rules reproduce the lexicon's own inflected entries (also a gate in the
  tests).
- `benchmark-lts [--variants small,shipped,large] [--write]`: the held-out letter-to-sound benchmark behind
  `docs/evaluation/phonemes-en/oov.md` (about 25 minutes for the three variants; the `small` and `large` variants exist
  only to reproduce that document's comparison).
- `measure-oracle`: exports our renderings of the measurement corpora for the local oracle comparison below.

## Oracle measurements are local only

The renderers are measured against two reference engines, espeak-ng (GPL-3.0) and misaki, to report aggregate agreement
numbers. These engines are **measurement oracles on a developer machine only**:

- Never reference them from a project, never copy their code, data or output into the repository, and **never commit
  oracle output** (their phoneme strings, per-word results or caches). Only the aggregate numbers recorded in
  `docs/evaluation/phonemes-en/` and this project's own golden files are committed.
- `measure-oracle` writes our renderings to a folder **outside the repository** (its default is a folder in the
  temporary directory; the only folder it accepts inside the repository is the git-ignored `data/phonemes-en/work/`).
  The comparison script and the oracles' outputs also stay outside the repository.

To set up an oracle environment, create a Python virtual environment in a folder outside the repository and install only
binary wheels (a source build of spaCy's dependencies is not needed):

```text
python -m venv <oracle-folder>/venv
<oracle-folder>/venv/bin/python -m pip install --only-binary=:all: spacy num2words addict regex espeakng-loader phonemizer
<oracle-folder>/venv/bin/python -m pip install --no-deps misaki
```

(On Windows the interpreter is `<oracle-folder>\venv\Scripts\python.exe`.) `espeakng-loader` supplies the espeak-ng
library and data to `phonemizer`; misaki downloads the spaCy `en_core_web_sm` model on first use. Write a small script
in the oracle folder that reads the exported `ours*.jsonl`, runs each sentence through the oracles
(`misaki.en.G2P(trf=False, british=False, fallback=None)` for Kokoro and `phonemizer` with the `en-us` espeak voice for
Piper) and prints only aggregates: word match, phoneme error rate without stress, and stress agreement.
