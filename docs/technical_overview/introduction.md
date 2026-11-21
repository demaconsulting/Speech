# Introduction

This document explains how the Speech library turns English text into the phonemes that its direct ONNX Runtime voices
speak, and how the quality of that speech is improved over time. It is a theory-of-operation document: it describes the
English front end, the models that use it, the loop by which listening finds and fixes errors, and the evidence that
measures the result. It is written for technical users and reviewers who want to judge where the spoken output can be
trusted and why it sounds the way it does.

The English front end is two packages. `DemaConsulting.Speech.Text.En` analyzes the text: it decides how numbers,
abbreviations, symbols and web addresses are spoken and which reading each heteronym has (`record` the noun or the
verb). `DemaConsulting.Speech.Phonemes.En` then gives every word a pronunciation and renders it in the phoneme symbols
of the voice. Both packages are pre-release.

## Purpose

The purpose of this document is to make the behavior of the English front end understandable and predictable. A voice
graph takes phonemes, not text, so every word that a voice says wrong was either read wrong (normalization or
heteronym reading), pronounced wrong (lexicon or letter-to-sound) or rendered wrong (stress, weak forms or pauses).
Knowing which component makes which decision tells a reader where an error comes from and where it is fixed.

This document is informational. It is not a requirement, a design specification or a compliance record. The structure
of the code is in the Speech Software Design document, the API and its usage are in the Speech User Guide, and the
measured results are in the evaluation records of the repository. This document summarizes them and names the source of
every figure it repeats.

## Scope

This document covers:

- The English front end: text normalization, the heteronym classifier and its quality gate, the pronunciation lexicon,
  the letter-to-sound model and the renderers
- The model families of the library and which of them use the English front end
- The listening-improvement loop: how errors are heard, flagged, triaged, fixed and kept fixed
- The accuracy and evaluation figures recorded in the repository, with their limits
- In brief, the licenses and the provenance of the data behind the front end

This document does not cover the public API, audio devices, model download and management, speech-to-text engines,
voice cloning, or the build and release process. Those are described in the user guide and the design document.

## References

- [REF-1] Speech User Guide, DEMA Consulting (installation, usage, the text analysis chapter and the command-line tool)
- [REF-2] Speech Software Design, DEMA Consulting (structure and algorithms of each software item)
- [REF-3] Text.En Heteronym Model: Evaluation Record, `docs/evaluation/text-en/README.md` in the repository (the gate,
  the held-out results and the evaluation overlays)
- [REF-4] Text.En Heteronym Model: External Evaluation Record, `docs/evaluation/text-en/external/README.md` in the
  repository (results on evaluation-only external datasets)
- [REF-5] Phonemes.En, Kokoro and Piper: Gate Summary, `docs/evaluation/phonemes-en/summary.md` in the repository (the
  renderer, morphology and letter-to-sound measurements)
- [REF-6] Text.En Heteronym Data, `data/text-en/README.md` in the repository (training, holdout and overlay data with
  their provenance and licenses)
- [REF-7] Phonemes.En Data, `data/phonemes-en/README.md` in the repository (the pronunciation tables and their pins)
- [REF-8] DemaConsulting.Speech.Proofreader, `tools/DemaConsulting.Speech.Proofreader/README.md` in the repository (the
  listening tool and its flag export)
- [REF-9] CMU Pronouncing Dictionary, <https://github.com/cmusphinx/cmudict>
