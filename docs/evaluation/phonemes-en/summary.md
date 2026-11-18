# Phonemes.En, Kokoro and Piper: Gate Summary

This is the aggregate scoreboard for the English phonemization work: the shared `DemaConsulting.Speech.Phonemes.En`
package, its adoption by the Kokoro model and the new raw ONNX Piper model. It lists each gate that the owner set
(report-then-ratchet), the value measured at the final state of the implementation, and whether the gate is met. It
holds aggregate numbers only. The reference engines (espeak-ng and misaki) were used locally as measurement oracles;
none of their output is stored in the repository.

The numbers come from the tools project `tools/DemaConsulting.Speech.Phonemes.En.Tools` (commands `measure-oracle`,
`measure-morphology` and `propose-readings --check`) and the local comparison script described in
[renderers.md](renderers.md); the per-pass table is [renderers.tsv](renderers.tsv). A second measurement over the same
corpora, after the Kokoro and Piper packages were finished, gave figures identical to the first one.

## Gates

| Gate | Target | Measured | Result |
| --- | --- | --- | --- |
| Reading ids with a pronunciation | 100% | 853 of 853 | Pass |
| Piper symbols in the voice's map | 100% | 100% | Pass |
| Kokoro symbols in the vocabulary | 100% | 100% | Pass |
| Piper word match, sentences | 85% or more | 88.05% (held out 90.09%) | Pass |
| Piper word match, isolated words | 85% or more | 78.63% (held out 75.27%) | Below |
| Piper phoneme error rate | 8% or less | 3.14% (held out 2.58%) | Pass |
| Piper primary stress agreement | 90% or more | 94.34% (held out 97.05%) | Pass |
| Kokoro phoneme error rate | 6% or less | 1.99% weak, 4.88% full | Pass |

Notes on the gates:

- Reading ids: the 395 readings of the text analyzer give 853 form and reading pairs; every one resolves to a
  pronunciation and the committed list of gaps is empty (checked by `propose-readings --check` and by a test).
- Symbol coverage: every symbol the renderers can emit is in the Piper `phoneme_id_map` and in the Kokoro
  vocabulary. The coverage tests cover all 133,971 dictionary pronunciations, every phone triple, the weak forms, the
  output of the letter-to-sound model, the 150 golden sentences, and, through the Piper engine path, all 300 corpus
  and 60 validation sentences (zero unknown symbols, and the engine's ids equal the renderer's).
- Piper against espeak-ng en-us: the error rate excludes stress marks. The sentence figures use the 150 golden
  sentences plus the 300 own-written sentences of `data/phonemes-en/renderer-corpus.tsv` (450 sentences, 4,276 word
  pairs); the held-out figures use the 60 sentences of `data/phonemes-en/renderer-validation.tsv`, written after the
  rules were frozen. The word passes use the 3,000 most frequent words of the text-analysis training sentences (error
  rate 4.49%, stress agreement 95.39%) and the next 3,000 words as the held-out pass (4.39% and 96.73%).
- Kokoro against misaki US: the shipped default speaks function words in their weak forms (1.99% on the tuning
  sentences, 1.43% held out). With full forms the figures are 4.88% and 4.50%. On the word passes they are 2.93% and
  2.81%. Weak forms also raise primary stress agreement from 71.4% to 94.2%; owner listening decides the default.

## Supporting Measurements

| Measurement | Value |
| --- | --- |
| Plural and third-person `s` | 98.81% of 13,397 |
| Past `ed` | 99.58% of 4,317 |
| Present participle `ing` | 99.59% of 4,109 |
| Possessive `s` | 99.39% of 6,023 |
| Comparative `er` | 99.18% of 2,925 |
| Superlative `est` | 96.07% of 229 |
| Adverb `ly` | 97.46% of 1,143 |
| Classifier hit rate, readings golden | 385 of 395 (97.5%) |

Notes:

- The morphology rows are the agreement of the suffix sound class with the lexicon for words whose stem is listed; a
  test enforces at least 98% for the main endings. For `est` the exact figure is 78.17% because the lexicon mixes two
  reduced vowels. The measurement tool prints the full table.
- Classifier hit rate: the real text analyzer over the own-written sentences of the readings golden; the test gate is
  95%. The 10 misses are pinned as forced readings and the golden was not bent to hide them.
- Out-of-vocabulary words: our own trained model has 11.59% phoneme error rate against 25.53% for the rules baseline
  (45% of it); see [oov.md](oov.md).
- Kokoro old lexicon against the new pipeline: 59,940 words compared, 68.75% identical, 5.91% phoneme error rate; see
  [kokoro-adoption.md](kokoro-adoption.md).

## Real Audio Through the CLI

The real `speech-cli speak` command, built from this repository, produced valid mono 16-bit WAV files from the
installed models for a heteronym sentence and a numbers and dates sentence. All files are audible (root mean square
level 0.06 to 0.11) and have the expected sample rate: 24,000 Hz for Kokoro and 22,050 Hz for Piper. A published
build of the tool does the same. The files and their checks are outside the repository. A Sherpa synthesis model and
the Sherpa and raw ONNX recognition models still work in the same build, so raw ONNX Runtime models and sherpa-onnx
coexist in one process folder.

## Known Gaps

- Text analysis (`Text.En`) does not handle URLs, e-mail addresses or Markdown marks the way the old Kokoro
  normalizer did: the marks are dropped, a URL is spoken as letters and parts, an e-mail loses its `@`. A small
  normalization hook in the Kokoro model is a possible fix.
- `12-year-old` is spoken as "one two" followed by the words, `10K` as "one zero K", a lone capital `A` as in `A1` is
  read as the article, and `sq. ft.` is not expanded.
- The classifier sometimes labels both occurrences of a same-part-of-speech heteronym alike (`read ... read`,
  `lead ... lead`, `content ... content`); the 10 forced rows of the readings golden record the other misses.
- Piper isolated-word agreement with espeak-ng (78.63%) is below the 85% target. The difference is the
  spelling-driven choice of the unstressed vowel, which cannot be derived from the phones alone; the sentence gate is
  met.
- Kokoro British voices speak US phonemes, so they have an American pronunciation.
- The Piper voice could not be verified at its original host (huggingface.co is unreachable from the build machine,
  TLS interception), so the download descriptor pins the existing SHA-256 verified Sherpa archive of the same voice and
  extracts only the model and its configuration file.
- The renderers are rule based on ARPAbet; they are measured against espeak-ng and misaki, not against listeners
  (see the pending verdict below).

## Owner Listening Verdict

PENDING. The blinded listening packs are outside the repository:

- Kokoro: `C:\Users\mnixon\.copilot\session-state\b3581152-65fd-4a2a-a4f8-3432d8fd8a03\files\listening\kokoro\`
  (30 sentences; the old pipeline, the new pipeline with weak forms and the new pipeline with full forms, shuffled per
  sentence; the answer key is a separate file).
- Piper: `C:\Users\mnixon\.copilot\session-state\b3581152-65fd-4a2a-a4f8-3432d8fd8a03\files\listening\piper\`
  (the same 30 sentences; the new pipeline against the old Sherpa and espeak-ng path, shuffled; separate answer key).

The verdict, with the weak-form decision for Kokoro, will be recorded here.
