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
- **Word-cluster corpus.** The corpus for the word-cluster table is 280 Project Gutenberg books that are in the public
  domain in the United States, each pinned in `data/text-en/cluster-sources.tsv` by the SHA-256 of its text with the
  basis of its public-domain status. The book texts are never committed. The resulting table is CC0 1.0 and is not yet
  used by any model.

## Voices

- **Piper.** The Piper LibriTTS-R voice is licensed CC BY 4.0 and requires attribution of the LibriTTS-R corpus and the
  Piper project. Only the voice graph and its configuration are extracted from the downloaded archive; the espeak-ng
  data in the archive is discarded and never installed.
- **Kokoro.** The Kokoro v1.0 voice is licensed Apache-2.0, and the Kokoro package embeds its phoneme vocabulary under
  the same license.

The voices are downloaded on demand and verified by SHA-256; no voice data is embedded in a package.
