# Licensing and Data Provenance

This section summarizes where the data behind the English front end comes from and under which terms it is used. The
authoritative statements are the package license and notice files and the provenance records of the data folders; this
summary is not legal advice.

## Pronunciations

- **CMU Pronouncing Dictionary only.** The lexicon of `DemaConsulting.Speech.Phonemes.En` is derived from the CMU
  Pronouncing Dictionary (BSD-style license), pinned by SHA-256. The dictionary file is fetched into a git-ignored cache
  and is not committed; the derived lexicon is embedded in the package, which declares `MIT AND LicenseRef-CMUdict` and
  carries the dictionary's notice.
- **Own work on top of it.** The letter-to-sound model was trained by DEMA Consulting from that dictionary alone. The
  reading pronunciations, the corrections and the additions are reviewed tables written from the dictionary and
  phonetic knowledge.
- **No espeak-ng or misaki data.** No data of espeak-ng, misaki or WikiPron is used anywhere in the repository. The
  Piper renderer approximates the espeak-ng style by rules; espeak-ng and misaki are used only as local measurement
  oracles outside the repository, and their outputs are never committed.

## Text Analysis

- **Package.** `DemaConsulting.Speech.Text.En` declares `MIT AND CC-BY-3.0-US`. Its code and its abbreviation and
  initialism tables, written from scratch, are under the MIT License; its reading inventory is dedicated to the public
  domain under CC0 1.0.
- **Heteronym model.** The embedded model is a set of trained weights without any sentence text. Because it was trained
  in part on sentences adapted from the Manually Annotated Sub-Corpus (MASC, CC BY 3.0 US), the weights are provided
  under that license, with the attribution in the package's notice file.
- **Training data.** Every row of `data/text-en/` carries its own license: CC0 1.0, public domain or CC BY 3.0 US
  (MASC). The sentences written or labeled by language models for this repository are dedicated to the public domain
  under CC0 1.0 by DEMA Consulting, and they are marked as model-written in the provenance files.
- **Evaluation-only datasets.** The external datasets (WikipediaHomographData and heteronym-bench) are fetched at
  evaluation time and never committed; only id-to-id mapping tables are in the repository.
- **Public-domain corpus.** The corpus kept for later features is 280 Project Gutenberg books that are in the public
  domain in the United States, each pinned in `data/text-en/cluster-sources.tsv` by the SHA-256 of its text with the
  basis of its public-domain status. The book texts are never committed, and no model uses the corpus.

## Voices

- **Piper.** The Piper LibriTTS-R voice is licensed CC BY 4.0, as its model card declares, and requires attribution of
  the LibriTTS-R corpus (Koizumi et al., 2023; CC BY 4.0; <https://www.openslr.org/141/>) and the Piper project
  (Rhasspy). Its model card also states that it was fine-tuned from the English Lessac medium voice, which was trained
  on the Blizzard Challenge 2013 Lessac data; that data is licensed for non-commercial research use only. Consumers must
  review the upstream terms and decide whether the voice fits their use.
- **Kokoro.** The Kokoro v1.0 voice is licensed Apache-2.0, and the Kokoro package embeds its phoneme vocabulary under
  the same license.
- **Chatterbox Turbo.** The Chatterbox Turbo graphs are licensed MIT, Copyright (c) 2025 Resemble AI. The package
  embeds only the tokenizer; reference voices come from the user or from the separately installed CC0 voice pack.

The voices are downloaded on demand and verified by SHA-256; no model weights or voice data are embedded in a package.

## Model Hosting and Redistribution

- **The `models` release.** DEMA Consulting redistributes the ONNX models it can legally host as zip bundles on one
  long-lived pre-release of the project, the `models` release
  (<https://github.com/demaconsulting/Speech/releases/tag/models>).
  Hosting on GitHub lets users behind a firewall that blocks Hugging Face install the models. A published bundle is
  never replaced: a change gets a new bundle revision (`-b2`, `-b3`, …) or a new upstream release in its name.
- **Bundle contents.** Each bundle holds the vendor files **unmodified**, byte for byte, plus four files added by DEMA
  Consulting: `LICENSE` (the original license text or texts), `NOTICE` (the attribution and notices of origin),
  `model.json` (the original source URL, size and SHA-256 of every file, the upstream repository and the license) and
  `README.txt` (a plain-text summary stating that the files are unmodified). The library verifies the zip and every
  extracted file against pins compiled into the package, and installs the four added files with the model under
  `licenses/<bundle>/`. The bundles are built reproducibly by the non-shipped `tools/ModelBundler` tool.
- **Kokoro** (`kokoro-en-v1.0-int8-b1`): Apache-2.0. The files are the `onnx-community/Kokoro-82M-v1.0-ONNX` export of
  `hexgrad/Kokoro-82M` v1.0; the attribution credits hexgrad and the onnx-community organization.
- **Nemotron** (`nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1`): the model weights
  (`nvidia/nemotron-3.5-asr-streaming-0.6b`, by NVIDIA Corporation) are licensed under the OpenMDW License Agreement
  1.1, which allows redistribution when its text and the notices of origin are kept; the int4 ONNX conversion by the
  onnx-community organization is tagged MIT. The bundle carries both texts and a notice naming both sources.
- **Chatterbox Turbo** (`chatterbox-turbo-lm-20261014-fp16-b1` and `chatterbox-turbo-aux-20261014-int4-b1`): MIT,
  Copyright (c) 2025 Resemble AI. Only this one set of graphs (the half-precision language model with the 4-bit
  auxiliary graphs) is offered; no other precision is downloaded from anywhere.
- **Piper** (`piper-en-us-libritts-r-medium-20261122-fp32-b1`): the voice's three original `rhasspy/piper-voices`
  files (the graph, its configuration and its `MODEL_CARD`), under the declared CC BY 4.0. The bundle carries the CC BY
  4.0 legal code and a `NOTICE` with the complete attribution: LibriTTS-R (Koizumi et al., 2023; CC BY 4.0; OpenSLR
  141), the Piper project (Rhasspy), and the lineage of the voice, fine-tuned from the English Lessac medium voice,
  trained on the Blizzard Challenge 2013 Lessac data, which is licensed for non-commercial research use only. DEMA
  Consulting hosts the voice with correct and complete attribution; consumers decide whether the license fits their
  use.
- **Every ONNX model comes from the release.** The Kokoro, Nemotron, Chatterbox Turbo and Piper packages download only
  their `models` bundles; none falls back to Hugging Face. The Hugging Face URLs that remain in the packages are the
  original source of each vendor file, recorded in the bundle's `model.json` and `README.txt` as provenance.
- **Sherpa models and the voice pack.** The Sherpa models download their archives from the sherpa-onnx GitHub
  releases, and the CC0 reference-voice pack from its own GitHub release; neither is re-hosted.
