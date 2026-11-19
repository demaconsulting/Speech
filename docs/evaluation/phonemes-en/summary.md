# Phonemes.En, Kokoro and Piper: Gate Summary

This is the aggregate scoreboard for the English phonemization work: the shared `DemaConsulting.Speech.Phonemes.En`
package, its adoption by the Kokoro model and the new raw ONNX Piper model. It lists each gate that the owner set
(report-then-ratchet), the value measured at the final state of the implementation, and whether the gate is met. It
holds aggregate numbers only. The reference engines (espeak-ng and misaki) were used locally as measurement oracles;
none of their output is stored in the repository.

The numbers come from the tools project `tools/DemaConsulting.Speech.Phonemes.En.Tools` (commands `measure-oracle`,
`measure-morphology` and `propose-readings --check`) and the local comparison script described in
[renderers.md](renderers.md); the per-pass table is [renderers.tsv](renderers.tsv). They are the figures after the
refinement pass described below; before it a repeat measurement over the same corpora had given figures identical to
the first one.

## Gates

| Gate | Target | Measured | Result |
| --- | --- | --- | --- |
| Reading ids with a pronunciation | 100% | 853 of 853 | Pass |
| Piper symbols in the voice's map | 100% | 100% | Pass |
| Kokoro symbols in the vocabulary | 100% | 100% | Pass |
| Piper word match, sentences | 85% or more | 86.12% (held out 89.31%) | Pass |
| Piper word match, isolated words | 85% or more | 78.70% (held out 75.37%) | Below |
| Piper phoneme error rate | 8% or less | 3.01% (held out 2.47%) | Pass |
| Piper primary stress agreement | 90% or more | 95.90% (held out 98.10%) | Pass |
| Kokoro phoneme error rate | 6% or less | 2.00% weak, 4.88% full | Pass |

Notes on the gates:

- Reading ids: the 395 readings of the text analyzer give 853 form and reading pairs; every one resolves to a
  pronunciation and the committed list of gaps is empty (checked by `propose-readings --check` and by a test).
- Symbol coverage: every symbol the renderers can emit is in the Piper `phoneme_id_map` and in the Kokoro
  vocabulary. The coverage tests cover all 133,971 dictionary pronunciations, every phone triple, the weak forms, the
  output of the letter-to-sound model, the 150 golden sentences, and, through the Piper engine path, all 300 corpus
  and 60 validation sentences (zero unknown symbols, and the engine's ids equal the renderer's).
- Piper against espeak-ng en-us: the error rate excludes stress marks. The sentence figures use the 150 golden
  sentences plus the 300 own-written sentences of `data/phonemes-en/renderer-corpus.tsv` (450 sentences, 4,279 word
  pairs); the held-out figures use the 60 sentences of `data/phonemes-en/renderer-validation.tsv`, written after the
  rules were frozen. The word passes use the 3,000 most frequent words of the text-analysis training sentences (error
  rate 4.47%, stress agreement 95.39%) and the next 3,000 words as the held-out pass (4.38% and 96.76%).
- Kokoro against misaki US: the shipped default speaks function words in their weak forms (2.00% on the tuning
  sentences, 1.43% held out). With full forms the figures are 4.88% and 4.50%. On the word passes they are 2.91% and
  2.80%. Weak forms also raise primary stress agreement from 71.5% to 94.1%; the owner's listening kept them on.
- Against the first end-to-end figures (Piper 88.05% word match, 3.14% error rate, 94.34% stress agreement; held out
  90.09%, 2.58%, 97.05%) the refinement pass improved every Piper sentence figure. Kokoro's held-out figures are
  unchanged; its tuning-sentence error rate moved by +0.01 point (1.99% to 2.00% weak, 4.88% to 4.89% full) because
  three sentences now reach the misaki engine as different spoken text (a URL, `10K`, `A1`), which the text analysis
  now reads as it should.
- The listening fixes (see "Listening Fixes" in `renderers.md`) left the Kokoro error rates at 2.00% weak and 4.88% full
  (4.89% before the `stronger` fix) and moved the Piper tuning-sentence figures by -0.05 point word match (89.44% to
  89.39%) and -0.08 point primary stress agreement (95.98% to 95.90%): the full form of a function word after
  `use`, `house` or `close` is a deliberate departure from espeak-ng's weak form in three of the 450 sentences.
- The second round of listening fixes (see "Listening Fixes 2" in `renderers.md`) gives the first word of every Piper
  utterance a secondary stress on a full-vowel weak form (the voice swallows an unmarked one there: the recognizer proxy
  heard the first word in 73.1% of 1,080 renderings before and 86.1% after) and flaps the `t` before a final `-y` that
  the dictionary stresses secondarily (`ability`). Piper tuning-sentence word match moved from 89.39% to 86.12% (held
  out 91.82% to 89.31%), the stress signature agreement from 96.75% to 93.48% and the secondary stress agreement from
  76.29% to 54.49%, all on purpose (espeak-ng writes no mark on that first word); the error rate (3.01%), the match
  without stress (91.14%) and the primary stress agreement (95.90%) did not change, the isolated-word match rose from
  78.63% to 78.70% (75.30% to 75.37% held out; the baseline re-measured at the start of the round was 75.33%) and
  Kokoro's word passes by 0.1 point (the same flap). The Kokoro sentence figures and goldens did not change.

## Supporting Measurements

| Measurement | Value |
| --- | --- |
| Plural and third-person `s` | 98.81% of 13,397 |
| Past `ed` | 99.58% of 4,317 |
| Present participle `ing` | 99.59% of 4,109 |
| Possessive `s` | 99.39% of 6,023 |
| Comparative `er` | 99.18% of 2,925 |
| Superlative `est` | 97.38% of 229 |
| Adverb `ly` | 97.46% of 1,143 |
| Classifier hit rate, readings golden | 385 of 395 (97.5%) |

Notes:

- The morphology rows are the agreement of the suffix sound class with the lexicon for words whose stem is listed; a
  test enforces at least 98% for the main endings. For `est` the exact figure is 79.48% because the lexicon mixes two
  reduced vowels (it was 96.07% and 78.17% before the velar stop of `longest`, `strongest` and `youngest` was added to
  the rule). The measurement tool prints the full table.
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

## Refinement Pass and Speech-Recognition Round Trip

After the first end-to-end audio the pass looked for causes that degrade the voices, measuring what can be measured
without a listener. Nothing below replaces the listening verdict; the recognizer is weak and its errors include
homophones, so the figures show only that the new path is not less intelligible than the old one.

- **Piper phoneme strings against espeak-ng.** For a pack of 133 sentences the whole phoneme string (words, separators,
  stress marks, punctuation) was compared with the engine's. Punctuation passes through at the same positions in all
  133, the word separators agree in 130 and no problem with framing, pauses or the model inputs was found: the engine
  inputs, the symbol framing and the scale triple (noise 0.333, length 1.0, noise width 0.333, the values the voice's
  installed configuration declares; an earlier version of this text wrongly said 0.667 and 0.8) are the voice's own. The
  heteronym sentence of the real-CLI check phonemizes identically to espeak-ng's string, so the garbled recognition
  (`rid the late`) is not a phoneme problem. The same phoneme string synthesized by seven speakers gave 6.1
  (speaker 0, the default), 6.2 (17), 6.3 (250), 6.9 (400), 7.1 (100), 7.6 (600) and 8.6 (speaker 5, the one used in
  that check) percent word errors on the pack, and on that sentence alone, in four draws each, speaker 17 was
  recognized exactly three times (the fourth missed only one word) and speaker 5 once: the voice's output differs from
  run to run (the model draws noise) and from speaker to speaker, and speaker 5 is one of the less intelligible ones.
  Different noise or length scales did not give a consistent gain (0.4/1.0/0.6: 5.8 percent; 0.3/1.0/0.5: 6.5; noise
  width 0.3: 8.5; length scale 1.1: 9.0 against 6.1), so the voice's own values stay.
- **What was fixed** (general causes, measured against the engine, see [renderers.md](renderers.md)): hyphenated
  compounds (every part keeps its own stress, closed dictionary spellings only when a part is not a word, small function
  words unstressed), a few function-word details of the espeak-style text (the flap of `it` and `at` before a vowel,
  `on the`, `for the`, the secondary stress of `made`, `put`, `which`, `what`, `between`, ...), and in the text analysis
  the number of a hyphenated compound (`12-year-old`), a number with a capital letter (`10K`, `3D`), identifiers of
  capital letters and digits (`A4`, `B12`, `H2O`, `MP3`), names joined to a number by a hyphen (`COVID-19`), URLs,
  e-mail addresses, bare domains, and `sq. ft.`.
- **Round trip through the repository's Zipformer recognizer** (133 sentences: the 30 of the listening packs and 103
  more own-written sentences without digits; word errors in percent, with trailing silence added so the streaming
  recognizer does not drop the last words; the references of the 7 sentences with digits are the spoken forms):

  | System | Word errors |
  | --- | --- |
  | Piper, old Sherpa and espeak-ng path (two runs) | 6.6 and 7.4 |
  | Piper, new pipeline before the pass | 6.2 |
  | Piper, new pipeline after the pass (two runs) | 6.1 and 5.6 |
  | Piper, listening-pack 30 sentences: old, new | 9.8 and 11.0, 6.4 and 5.2 |
  | Kokoro, old pipeline | 3.2 |
  | Kokoro, new pipeline | 2.9 |

  The new Piper path is at parity with the old one on sentences without numbers (5.9 to 6.4 against 5.9 to 6.6) and
  better on the numbers, dates and currency sentences, where the old path has no text analysis (the 12-year-old
  sentence, `1999`, `$1,250.50`). The sentences where the new path scored worse than the old in one run are
  homophones the recognizer picks freely (`ferry` and `fairy`, `wait` and `weight`, `wrote` and `rode`) and
  differences that change from run to run. Kokoro's recognition of the numbers and dates sentences is exact for both
  voices (March 14th as a date, a price, a percent, a time, a year). The speech-recognition figures are a cheap
  proxy only: they say nothing about prosody, naturalness or voice quality.
- **Comma pauses.** The core sentence chunker always splits at `,`, `;` and `:`, so each clause is its own utterance for
  every model (the same for the old path). VITS was trained with the comma inside the sentence. The owner's second
  listening round found the clause pieces of Piper swallowed (`coffee,`, `recommends` after a comma), and a controlled
  experiment (63 sentences, 5 speakers, 3 draws) confirmed it: the first word of a clause after a comma was recognized
  in 90.6 percent of the renderings as its own utterance and 97.5 percent inside its sentence, with no significant
  change of the total word error rate (6.78 and 6.63 percent). The Piper model therefore speaks a sentence as one
  utterance (its capability profile joins the clause pieces; no change of the core contract was needed), see "Listening
  Fixes 2" in [renderers.md](renderers.md). Kokoro's chunking is unchanged.

## Piper Sampling Noise

The owner's `live` listening finding (the phonemes are right, about a third of the renderings say `lɪv`) raised the
question whether the engine samples more noise than the old Sherpa path. It does not: the installed voice's
configuration (the Sherpa archive's `.onnx.json`) declares `noise_scale` 0.333, `length_scale` 1.0 and `noise_w`
0.333, the engine passes exactly these as the graph's `scales` input (speed divides the length scale), and the Sherpa
model class sets the same `NoiseScale` 0.333 and `NoiseScaleW` 0.333 (its length scale is 1 divided by the speed). The
values 0.667, 1.0 and 0.8 are only Piper's own defaults, used when a configuration has no `inference` block, and the
text of the refinement pass above wrongly called them the voice's values.

The grid below measured pronunciation stability for six settings plus a zero-noise anchor, through the real analyzer,
profile, pipeline and engine (only the noise values come from a modified copy of the voice's configuration) and the
repository's Zipformer recognizer. Sentences: 232 (the 133 sentences of the refinement pack, which contain the 12
heteronym-pair sentences, and 99 of the 395 heteronym reading sentences, every fourth); speakers 0, 17 and 400; 12 draws
of every sentence and speaker (50,112 renderings; the anchor 2 draws; the `live` sentence 36 draws). Word error is the
share of reference words missed; "exact" is the share of renderings recognized word for word; "agreement" is the share
of draws of one sentence and speaker whose recognized text equals the most frequent one (a stability measure, 100 where
the output is deterministic); "target" is the share of renderings in which the heteronym (or the word the reading
sentence is about) was recognized; these four columns are percent. The 95 percent bounds resample sentences. "Duration"
is the mean duration relative to the file's values, "spread" the standard deviation of the duration between draws
(percent of the mean), and "pitch" the standard deviation of the pitch in semitones.

| Noise / width | Word error | Exact | Agreement | Target | Duration | Spread | Pitch |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0.667 / 0.8 (Piper defaults) | 11.07 | 43.6 | 52.8 | 77.0 | 1.129 | 5.95 | 3.64 |
| 0.333 / 0.333 (file, Sherpa) | 7.74 | 57.3 | 71.8 | 81.2 | 1.000 | 2.98 | 3.32 |
| 0.5 / 0.5 | 8.64 | 52.5 | 64.1 | 80.1 | 1.035 | 3.93 | 3.48 |
| 0.333 / 0.8 | 8.88 | 51.0 | 61.9 | 80.5 | 1.129 | 6.00 | 3.39 |
| 0.667 / 0.333 | 9.93 | 48.7 | 60.0 | 78.2 | 1.000 | 2.99 | 3.54 |
| 0.2 / 0.2 | 7.43 | 58.8 | 76.5 | 81.7 | 0.981 | 2.22 | 3.28 |
| 0 / 0 (no noise) | 7.67 | 57.5 | 100 | 83.2 | 0.971 | 0.00 | 3.30 |

Differences against the file's values (paired by sentence, 95 percent bounds): Piper's defaults are clearly worse,
word error +3.32 points (+2.94 to +3.71), agreement -19.0 points (-20.4 to -17.6), target word -4.2 points (-5.8 to
-2.7), and 13 percent slower (a larger noise width lengthens the speech); every setting with more noise than the file
is worse in the same way. The setting 0.2 / 0.2 is a little better than the file (word error -0.32 points, -0.56 to
-0.08; agreement +4.6 points, +3.5 to +5.8; target word +0.5, -0.5 to +1.6, not significant), and no noise at all gives
the same word error as the file (-0.07, -0.50 to +0.38) with a deterministic output. The pitch spread of lower noise
is not flatter (the anchor differs from the file by -0.03 semitones, -0.10 to +0.04; only raised noise adds pitch
spread, +0.3 for noise 0.667), the harmonics-to-noise ratio varies by under 1 dB across the settings and the spectral
flatness does not move; the proxies cannot hear prosody, so they only fail to show a loss.

The `live` sentence (`I live near a theater that hosts live music every night.`, 36 draws for each of 3 speakers, 108
per setting): the second `live` (before `music`) was recognized as `live` in 63 percent of the renderings with the
file's values (53.6 to 71.5), 72 percent with Piper's defaults, 49 percent with 0.2 / 0.2 and 53 percent with
0.667 / 0.333; with no noise the same input gives the same output, and speakers 0 and 400 said it as something else
both times. So the sampling noise is not the cause: the voice's most likely rendering of this phoneme string is the
ambiguous one for some speakers, and a lower noise setting makes that worse, not better.

Decision: the values stay (0.333 / 1.0 / 0.333, read from the voice's file). The only setting measured as better,
0.2 / 0.2, gains 0.3 word error points and 4.6 agreement points, loses on the `live` sentence and shrinks the duration
spread, and no listener has judged it. The graph has no seed: the exported VITS graph takes `input`, `input_lengths`,
`scales` and `sid` only, its two `RandomNormalLike` nodes (duration predictor and latent) carry no `seed` attribute,
and ONNX Runtime has no session-wide seed, so the engine cannot make the output repeatable except by setting both noise
scales to 0, which gives the same output every time at the same word error rate. The experiment's scratch code and
outputs are outside the repository.

## Known Gaps

- Text analysis (`Text.En`) reads URLs, e-mail addresses and bare domains (the scheme is spelled, `www` is spelled, the
  separators are spoken). Markdown marks are dropped silently by the phonemizer and need no handling, but a Markdown
  link `[text](url)` still speaks the address after the text, file names (`report.pdf`, `Node.js`) are not read as
  addresses and Roman numerals are not read.
- The classifier sometimes labels both occurrences of a same-part-of-speech heteronym alike (`read ... read`,
  `lead ... lead`, `content ... content`); the 10 forced rows of the readings golden record the other misses.
- Piper isolated-word agreement with espeak-ng (78.70%) is below the 85% target. The difference is the
  spelling-driven choice of the unstressed vowel, which cannot be derived from the phones alone; the sentence gate is
  met.
- Kokoro British voices speak US phonemes, so they have an American pronunciation.
- The Piper voice could not be verified at its original host (huggingface.co is unreachable from the build machine,
  TLS interception), so the download descriptor pins the existing SHA-256 verified Sherpa archive of the same voice and
  extracts only the model and its configuration file.
- The renderers are rule based on ARPAbet; they are measured against espeak-ng and misaki, not against listeners
  (see the pending verdict below).

## Owner Listening Verdict

PENDING. The blinded listening packs were regenerated after the refinement pass (same sentences and structure as
before; the earlier packs are kept beside them as a backup). They are outside the repository:

- Kokoro: a pack kept in a local working folder of the maintainer
  (30 sentences; the old pipeline, the new pipeline with weak forms and the new pipeline with full forms, shuffled per
  sentence; the answer key is a separate file).
- Piper: a pack kept beside it in the same local working folder
  (the same 30 sentences; the new pipeline against the old Sherpa and espeak-ng path, shuffled; separate answer key).

The verdict, with the weak-form decision for Kokoro, will be recorded here.
