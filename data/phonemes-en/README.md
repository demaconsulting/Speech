<!-- markdownlint-disable MD013 MD060 table-too-wide -->

# Phonemes.En Data

License: BSD-style (CMUdict); nothing is committed from CMUdict itself

The inputs and pins behind the embedded pronunciation lexicon of `DemaConsulting.Speech.Phonemes.En`. The lexicon is derived
from the CMU Pronouncing Dictionary (CMUdict) and nothing else: no misaki, WikiPron or espeak-ng data is used anywhere in this
repository. This folder holds only the pins; the dictionary itself is fetched into a local, git-ignored cache and the derived
resource lives in the library. Nothing here is legal advice.

## Contents

| File | What |
| --- | --- |
| [MANIFEST.tsv](MANIFEST.tsv) | pins: SHA-256, size and row count of the CMUdict input and of the uncompressed lexicon and letter-to-sound model images; verified by `build-lexicon --check` and `train-lts --check` |
| [reading-pronunciations.tsv](reading-pronunciations.tsv) | the reviewed, frozen ARPAbet pronunciation of every heteronym reading id of `DemaConsulting.Speech.Text.En` (395 rows: `reading_id`, `arpabet`, `source`); embedded in the library |
| [form-overrides.tsv](form-overrides.tsv) | the few inflected spellings the morphology rules cannot derive from a reading's headword (`does`, `lives`, `dove` ...); embedded in the library |
| [reading-gaps.tsv](reading-gaps.tsv) | the committed list of (spelling, reading id) pairs that cannot be resolved; expected to be empty (header only) |
| `external/` | git-ignored local cache: `external/cmudict/cmudict.dict` and its `LICENSE` (never committed) |
| `work/` | git-ignored scratch for measurements (never committed) |

## CMUdict (the only pronunciation source)

- **Source**: <https://github.com/cmusphinx/cmudict>, file `cmudict.dict` (the same pin as `data/text-en/README.md`).
- **Pin**: SHA-256 `81917843c7f44ce2b094ac63873c2c7a4cf802040792c455ba3ca406891c3d22`, 3,618,488 bytes, 135,166 lines.
- **License**: BSD-style, copyright 1993-2015 Carnegie Mellon University. The notice is reproduced in
  `src/DemaConsulting.Speech.Phonemes.En/PACKAGE-LICENSE.md` and `THIRD-PARTY-NOTICES.md` and travels with the package.
- **Fetch** (from the repository root; the build verifies the hash and refuses any other file):

```pwsh
New-Item -ItemType Directory -Force data/phonemes-en/external/cmudict | Out-Null
curl.exe -sSL -o data/phonemes-en/external/cmudict/cmudict.dict https://raw.githubusercontent.com/cmusphinx/cmudict/master/cmudict.dict
curl.exe -sSL -o data/phonemes-en/external/cmudict/LICENSE https://raw.githubusercontent.com/cmusphinx/cmudict/master/LICENSE
```

## The Embedded Lexicon

`src/DemaConsulting.Speech.Phonemes.En/Resources/cmudict-en.bin.gz` is built from the pinned file by the tools project:

```pwsh
dotnet run --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- build-lexicon           # writes the resource and the image row of MANIFEST.tsv
dotnet run --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- build-lexicon --check   # verifies them (exit 1 when stale)
```

The rules (implemented in `tools/DemaConsulting.Speech.Phonemes.En.Tools/LexiconSubsystem/`):

- Only headwords of the letters a to z and apostrophes are kept: 133,973 of the 135,166 entries (hyphenated words and words with
  periods are dropped).
- `word(2)`, `word(3)` ... are ordered variants of `word`; the first is the default. Two identical repeated variants are dropped.
  Result: 124,926 words, 133,971 pronunciations.
- Each ARPAbet phone is one byte (consonants 1 to 24, each vowel with its stress digit 0, 1 or 2 from 25 to 69); words are sorted
  by ordinal order and front-coded in blocks of 32; the image is gzip-compressed. Image 1,675,386 bytes, resource about 0.8 MB (budget 1 MB).
- `--check` re-derives the image from the pinned source and compares it with the committed resource decompressed (the compressed
  bytes depend on the runtime's deflate and are never compared) and with `MANIFEST.tsv`, and proves that every entry reads back
  identical through the library's lookup.
- The morphology rules that derive words the lexicon lacks are measured against the lexicon itself with `measure-morphology`.

## Reading Pronunciations

The English text analysis labels a heteronym such as `record` with a reading id (`record.noun`, `record.verb`); the phonemizer needs
a pronunciation for every one of the 395 ids. CMUdict lists the variants of a heteronym but not which belongs to which sense, and
about a quarter of the heteronyms lack a variant altogether, so the mapping is a reviewed table, built like this:

- `propose-readings` proposes a row per reading id from CMUdict (variants matched by stress signature, so the noun gets the initial
  stress and the verb the final stress; `-ate` and `-ment` endings by sound rule) into the git-ignored `work/` folder.
- The proposals were reviewed and corrected by hand, then the whole table was audited a second time against the resolved
  pronunciation of every (spelling, reading id) pair. No misaki, WikiPron or espeak-ng data was consulted for any row.
- Each row's `source` says where it came from: `cmu#N` (variant `N` of the lemma in CMUdict), `rule:<name>` (derived from a CMUdict
  entry by a named sound rule) or `authored` (written from the CMUdict base entry and phonetic knowledge; this includes the three
  lemmas CMUdict lacks, `analyse`, `pasty` and `rerelease`).
- A row is the pronunciation of the reading's own headword. An inflected spelling (`records`, `abused`, `abusing`) is that
  pronunciation plus the sound of the ending by the morphology rules; `form-overrides.tsv` holds the spellings that rule cannot give.
- `propose-readings --check` verifies the table: exactly the inventory's reading ids, every `cmu#N` row equal to the lexicon's
  variant, every one of the 853 (spelling, reading id) pairs of `data/text-en/forms.tsv` resolves, and the pairs that do not are
  exactly the committed `reading-gaps.tsv` (there are none). The library's tests assert the same, and the readings golden
  corpus (`test/DemaConsulting.Speech.Phonemes.En.Tests/TestData/readings-golden.tsv`, one sentence per reading id) pins the
  pronunciation of every reading id end to end.

```pwsh
dotnet run --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- propose-readings            # writes work/reading-review.tsv and a proposed table
dotnet run --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- propose-readings --check    # verifies the committed tables (exit 1 when inconsistent)
```

## The Embedded Letter-To-Sound Model

`src/DemaConsulting.Speech.Phonemes.En/Resources/lts-en.bin.gz` pronounces a word that neither the lexicon nor the morphology
rules know. It holds the weights of three small neural networks (a phone network, a primary-stress network and a
secondary-stress network, quantized to integers) that the tools project trains from the pinned CMUdict alone:

```pwsh
dotnet run --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- train-lts           # trains on all entries (about 8 minutes), writes the resource and the lts-en.bin row of MANIFEST.tsv
dotnet run --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- train-lts --check   # retrains and verifies them (exit 1 when stale)
dotnet run --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- benchmark-lts       # the held-out benchmark behind docs/evaluation/phonemes-en/oov.md
```

The training is deterministic (seeded, fixed-order arithmetic, no dependence on the number of cores), so `--check` compares the
re-derived image with the committed resource decompressed and with the image row of `MANIFEST.tsv`. The shipped model is trained
on all entries with exactly the settings of the model that `benchmark-lts` trains on the training part of a stem-family
split to measure held-out quality; the evaluation record `docs/evaluation/phonemes-en/oov.md` states the numbers, the split and
the decision against the rules and g2p_en alternatives.

## Measurement Oracles (local only)

espeak-ng (GPL-3) and misaki may be installed on a developer machine **outside the repository** purely to measure the renderers
that come later. No project references them, and their outputs and any data derived from them are never committed; only aggregate
numbers and this project's own golden files are.
