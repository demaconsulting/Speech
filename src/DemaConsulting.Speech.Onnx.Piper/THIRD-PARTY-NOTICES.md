# Third-Party Notices for the DemaConsulting.Speech.Onnx.Piper Package

This package embeds no third-party data. It installs, on demand, a voice that comes from the projects listed below, and
this file gives the attribution that the voice's license requires, records the voice's lineage and states what the
package changes. The MIT License in the repository `LICENSE` file, and in `PACKAGE-LICENSE.md`, covers only the code of
this repository. It does not cover the voice described here.

## Piper Voice `en_US-libritts_r-medium` (trained on LibriTTS-R)

- **What it is**: the Piper (VITS) English (US) voice `libritts_r`, quality `medium`, 22,050 Hz,
  904 speakers. Its `MODEL_CARD` states: language `en_US`, 904 speakers, dataset
  <http://www.openslr.org/141/>, "License: CC BY 4.0", and "Fine-tuned from English lessac medium on
  train-clean-360".
- **License**: Creative Commons Attribution 4.0 International (CC BY 4.0),
  <https://creativecommons.org/licenses/by/4.0/>, as declared by the voice's model card.
- **Training data**: LibriTTS-R, published through OpenSLR as resource SLR141
  (<https://www.openslr.org/141/>, license CC BY 4.0): Yuma Koizumi, Heiga Zen, Shigeki Karita,
  Yifan Ding, Kohei Yatabe, Nobuyuki Morioka, Michiel Bacchiani, Yu Zhang, Wei Han and Ankur Bapna,
  "LibriTTS-R: A Restored Multi-Speaker Text-to-Speech Corpus", arXiv, 2023. LibriTTS-R is a
  sound-quality-improved version of the LibriTTS corpus (<http://www.openslr.org/60/>); the
  maintainers of the corpus ask that the dataset paper is cited when the corpus is used in a work.
- **Voice project**: Piper (Rhasspy), <https://github.com/rhasspy/piper> (continued at
  <https://github.com/OHF-Voice/piper1-gpl>); the voices are distributed through
  <https://huggingface.co/rhasspy/piper-voices>.
- **Lineage notice**: the voice was fine-tuned from the English `lessac` medium Piper voice
  (<https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/lessac/medium>), which was trained on
  the Blizzard Challenge 2013 Lessac data (audio book recordings of Voice Factory International, Inc. and
  associated data of Lessac Technologies, Inc.). That data is licensed for non-commercial research use
  only (<https://www.cstr.ed.ac.uk/projects/blizzard/2013/lessac_blizzard2013/license.html>). DEMA
  Consulting hosts the voice with its declared license and this complete attribution; you must review the
  upstream terms and decide whether the voice fits your use.
- **Where this package gets the voice**: the bundle `piper-en-us-libritts-r-medium-20261122-fp32-b1.zip`
  on this project's `models` release (<https://github.com/demaconsulting/Speech/releases/tag/models>,
  78,627,157 bytes, SHA-256 `5196a8aa97f9053e0bdbbc2299007d48d55f37a74e88199869daebb710d8bd0f`, pinned in
  the model). It holds the voice's three original files from
  <https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/libritts_r/medium>, unmodified:
  `en_US-libritts_r-medium.onnx` (SHA-256
  `10bb85e071d616fcf4071f369f1799d0491492ab3c5d552ec19fb548fac13195`),
  `en_US-libritts_r-medium.onnx.json` (SHA-256
  `b471dc60d2d8335e819c393d196d6fbf792817f40051257b269878505bc9afb3`) and `MODEL_CARD` (SHA-256
  `0ccde6927e5bb4d743f4ea39618a9387ba18cca3351220a8a9cfdbc68b30fcb9`), together with the CC BY 4.0
  text (`LICENSE`), this attribution and lineage (`NOTICE`), a `README.txt` and a `model.json`. These four
  files are installed with the voice under `licenses/piper-en-us-libritts-r-medium-20261122-fp32-b1/`.
- **Changes**: none. The voice's files are installed byte for byte as published.
- **Warranty**: the voice is provided as is, without warranty of any kind, as stated by CC BY 4.0.

### Suggested credit when you share audio or the voice

```text
Voice: Piper "en_US-libritts_r-medium" (Rhasspy), trained on the LibriTTS-R corpus (Koizumi et al., 2023;
OpenSLR SLR141), licensed under CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/). Fine-tuned from
the Piper "lessac" voice, whose Blizzard Challenge 2013 Lessac training data is licensed for non-commercial
research use only. Run with DemaConsulting.Speech.Onnx.Piper.
```

## English Text Analysis and Pronunciation Data

The English text analysis and the pronunciation data (the CMU Pronouncing Dictionary and the trained
letter-to-sound model) are not part of this package. They come from the
`DemaConsulting.Speech.Text.En` and `DemaConsulting.Speech.Phonemes.En` packages, which this package depends
on, and their notices are in those packages' own `THIRD-PARTY-NOTICES.md` files. This package reproduces
none of that data.
