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
| [MANIFEST.tsv](MANIFEST.tsv) | pins: SHA-256, size and row count of the CMUdict input and of the uncompressed lexicon image; verified by `build-lexicon --check` |
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

## Measurement Oracles (local only)

espeak-ng (GPL-3) and misaki may be installed on a developer machine **outside the repository** purely to measure the renderers
that come later. No project references them, and their outputs and any data derived from them are never committed; only aggregate
numbers and this project's own golden files are.
