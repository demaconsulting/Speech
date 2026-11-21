<!-- markdownlint-disable MD013 MD060 table-too-wide -->

# Speech-to-Text Evaluation Data (English)

License: CC BY 4.0 (LibriSpeech subset) and CC0 1.0 (poem recitation); see [ATTRIBUTION.md](ATTRIBUTION.md)

The real English speech recordings and reference transcripts used to measure speech-to-text accuracy and segmentation.
They are the evidence behind the real-model tests of `DemaConsulting.Speech.Onnx.NemotronStt` and the input of the
evaluation tool `tools/DemaConsulting.Speech.SttEvaluation`; the Sherpa, core Speech and CLI tests use the poem
recitation. Nothing here is legal advice.

## Rules

- **Never packed**: the data sits outside every `src/` project and is consumed only by test projects and developer
  tools, all of which are `IsPackable=false`. No file of this folder is ever part of a NuGet package.
- **Never mixed**: no other data, generated audio or engine output is added to this folder. The files are the corpus
  files unchanged (LibriSpeech) or the single recitation already used by the tests (poem).
- **No engine output committed**: transcripts, reports and comparisons produced by the evaluation tool are written only
  under the git-ignored `artifacts/` folder.
- **Verified**: every file is pinned by SHA-256 and size in [MANIFEST.tsv](MANIFEST.tsv); the evaluation tool and the
  data-set tests refuse a file whose checksum or size does not match.

## Contents

| File | What |
| --- | --- |
| [MANIFEST.tsv](MANIFEST.tsv) | one row per audio file: `file`, `set`, `sha256`, `bytes`, `seconds`, `license`, `source`, `reference` (the reference transcript) |
| [ATTRIBUTION.md](ATTRIBUTION.md) | the attribution and license of each source (LibriSpeech CC BY 4.0, poem CC0) |
| `librispeech-test-clean/*.flac` | 24 utterances of the LibriSpeech `test-clean` set (set `librispeech-test-clean`), byte-identical to the archive: 16 kHz, mono, 16-bit FLAC, 211 s and 3.9 MB in total |
| `crossing-the-bar-16k-mono.wav` | a 50 s recitation of Tennyson's "Crossing the Bar" (set `crossing-the-bar`), 16 kHz, mono, 16-bit PCM WAV |

## LibriSpeech Subset

- **Archive**: <https://www.openslr.org/resources/12/test-clean.tar.gz>, SHA-256
  `39fde525e59672dc6d1551919b1478f724438a95aa55f874b576be21967e6c23`, 346,663,984 bytes.
- **Reference text**: the line of the chapter's `*.trans.txt` file, verbatim (upper case, no punctuation, apostrophes
  kept). The British spellings of some transcripts are kept as they are.
- **Selection rule** (deterministic; applied once, the result is pinned by the manifest):
  1. take the speakers of `test-clean` from `SPEAKERS.TXT` and keep the first 12 female and the first 12 male speakers in
     numeric id order (24 speakers);
  2. for each speaker, walk the utterances in numeric chapter and utterance order and keep the first one that lasts
     7.0 to 11.0 seconds and whose transcript contains no number word (any cardinal or ordinal number word, and
     `DOZEN`) and no spoken-out title (`MISTER`, `MISSUS`, `MISSES`, `DOCTOR`, `SAINT`),
     because a recognizer that writes digits or abbreviations would otherwise be counted wrong for a correct word;
  3. copy the FLAC file unchanged into `librispeech-test-clean/`.

To rebuild or audit the subset, download and extract the archive outside the repository (for example under the
temporary folder), apply the rule above and compare the result with the manifest; the files and their checksums must be
identical. The archive is not kept.

## Poem Recitation

`crossing-the-bar-16k-mono.wav` is the recording the recognition tests have always used (formerly
`test/DemaConsulting.Speech.Sherpa.Tests/TestData/` and a duplicate in `test/DemaConsulting.Speech.Tests/TestData/`; both
copies were identical and are replaced by this single file). Its reference is the published text of the poem on one line,
with its punctuation; the archaic spelling `crost` is kept.

## Consumers

| Consumer | Uses |
| --- | --- |
| `tools/DemaConsulting.Speech.SttEvaluation` | every file, through the manifest (repository root found by `Speech.slnx`, or `--data <dir>`) |
| `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests` | every file, copied to `TestData/stt-en/` beside the test assembly |
| `test/DemaConsulting.Speech.Sherpa.Tests`, `test/DemaConsulting.Speech.Tests` | the poem, copied to `TestData/crossing-the-bar-16k-mono.wav` beside the test assembly |
| `test/DemaConsulting.Speech.Cli.Tests` | the poem, read from this folder by its repository path |
