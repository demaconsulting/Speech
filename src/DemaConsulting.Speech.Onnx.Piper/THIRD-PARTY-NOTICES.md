# Third-Party Notices for the DemaConsulting.Speech.Onnx.Piper Package

This package embeds no third-party data. It installs, on demand, a voice that comes from the projects
listed below, and this file gives the attribution that the voice's license requires and states what the
package changes. The MIT License in the repository `LICENSE` file, and in `PACKAGE-LICENSE.md`, covers
only the code of this repository. It does not cover the voice described here.

## Piper Voice `en_US-libritts_r-medium` (trained on LibriTTS-R)

- **What it is**: the Piper (VITS) English (US) voice `libritts_r`, quality `medium`, 22,050 Hz,
  904 speakers. Its `MODEL_CARD` states: language `en_US`, 904 speakers, dataset
  <http://www.openslr.org/141/>, "License: CC BY 4.0", and "Fine-tuned from English lessac medium on
  train-clean-360".
- **License**: Creative Commons Attribution 4.0 International (CC BY 4.0),
  <https://creativecommons.org/licenses/by/4.0/>. The license applies to the voice's dataset as the
  voice's model card states it; this package follows the model card.
- **Training data**: LibriTTS-R, published through OpenSLR as resource SLR141
  (<https://www.openslr.org/141/>, license CC BY 4.0): Yuma Koizumi, Heiga Zen, Shigeki Karita,
  Yifan Ding, Kohei Yatabe, Nobuyuki Morioka, Michiel Bacchiani, Yu Zhang, Wei Han and Ankur Bapna,
  "LibriTTS-R: A Restored Multi-Speaker Text-to-Speech Corpus", arXiv, 2023. LibriTTS-R is a
  sound-quality-improved version of the LibriTTS corpus (<http://www.openslr.org/60/>); the
  maintainers of the corpus ask that the dataset paper is cited when the corpus is used in a work.
- **Voice project**: Piper, <https://github.com/OHF-Voice/piper1-gpl> (the voices are distributed through
  <https://huggingface.co/rhasspy/piper-voices>).
- **Where this package gets the voice**: the archive `vits-piper-en_US-libritts_r-medium.tar.bz2` that the
  sherpa-onnx project publishes at
  <https://github.com/k2-fsa/sherpa-onnx/releases/tag/tts-models> (82,038,311 bytes, SHA-256
  `10dc268f3e371696d721486123e2705a9fc1faa113491979fde4d88dba1f1b1c`, pinned in the model's download
  descriptor). The voice's own Hugging Face files could not be reached from the machine this package was
  developed on, so their hashes were not verified and the sherpa-onnx archive is used instead.
- **Changes**: none to the voice's weights or configuration. The sherpa-onnx project converted the voice
  for its own runtime (the graph in the archive carries added metadata). This package keeps only the
  archive's `en_US-libritts_r-medium.onnx` and `en_US-libritts_r-medium.onnx.json` files, unchanged, and
  discards the rest of the archive (the espeak-ng data, which has its own license, and a token table).
- **Warranty**: the voice is provided as is, without warranty of any kind, as stated by CC BY 4.0.

### Suggested credit when you share audio or the voice

```text
Voice: Piper "en_US-libritts_r-medium", trained on the LibriTTS-R corpus (Koizumi et al., 2023;
OpenSLR SLR141), licensed under CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/).
Run with DemaConsulting.Speech.Onnx.Piper.
```

## English Text Analysis and Pronunciation Data

The English text analysis and the pronunciation data (the CMU Pronouncing Dictionary and the trained
letter-to-sound model) are not part of this package. They come from the
`DemaConsulting.Speech.Text.En` and `DemaConsulting.Speech.Phonemes.En` packages, which this package depends
on, and their notices are in those packages' own `THIRD-PARTY-NOTICES.md` files. This package reproduces
none of that data.
