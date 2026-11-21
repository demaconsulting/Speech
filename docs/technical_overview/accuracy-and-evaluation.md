# Accuracy and Evaluation

This section collects the figures that the repository records for the English front end. Every figure is taken from a
committed evaluation record, which names the exact model and data it was measured on; when the model changes, the
record is regenerated and these figures must follow it. None of the figures is a listener's judgment: they are
agreement with labels or with reference engines, and their limits are stated with them.

## Heteronym Readings

The heteronym model is measured end to end through the real analyzer: a target word for which the analyzer emits no
reading counts as wrong. The baseline is the reading that the part-of-speech tag alone gives, which a reader gets from
the tagger for free (source: the Text.En evaluation record, [REF-3]).

| Set | Words | Model | Tag alone | Majority reading |
| --- | ---: | ---: | ---: | ---: |
| Natural holdout (the gate) | 1,167 | 96.23% | 90.66% | 82.01% |
| Independent holdout, written blind by another model family | 1,972 | 95.44% | 91.23% | 51.37% |
| Development set (used to choose settings; optimistic) | 3,235 | 95.30% | 88.56% | 65.87% |

- **The gate passed.** On the natural holdout the model is right for 1,123 of 1,167 words, above the threshold of 93.0
  percent, and its improvement over the tag alone is +5.57 points with a 95 percent interval of +3.16 to +8.15 points
  (266 document clusters).
- **Seeds.** Five training runs with seeds 1 to 5 give 95.46 to 97.00 percent on the natural holdout (mean 96.43). The
  shipped model is seed 1, chosen before any holdout was read, not the best seed.
- **Confidence.** On the natural holdout the reading was right for 99.33 percent of the words at a confidence of 0.90
  or more (1,035 of 1,042), for 75.00 percent between 0.60 and 0.90 (78 of 104) and for 47.62 percent below 0.60 (10 of
  21). A reading below 0.60 is right only about half the time.

### External Datasets

Two public datasets are used for evaluation only: they are fetched at evaluation time from pinned revisions, mapped to
the reading inventory by an id-only table and never committed (source: the external evaluation record, [REF-4]). They
cover only part of the inventory and follow their own labeling, so they are informational.

| Set | Words | Model | Tag alone |
| --- | ---: | ---: | ---: |
| WikipediaHomographData, evaluation split (headline) | 1,615 | 93.00% | 90.59% |
| WikipediaHomographData, training split | 14,487 | 91.65% | 89.74% |
| heteronym-bench | 299 | 93.31% | 80.27% |

### Evaluation Overlays

Two further sets measure what the gate does not (source: [REF-3]). Neither is part of the gate, and nothing is trained
on either:

- **The regression overlay** (519 sentences: listening flags, analyzer and golden test targets, and selected training
  and development rows) is read correctly in all 519 cases with the cue priors and in 515 without them. The four
  differences are three flagged listening sentences that the cue priors were written for and one data row of a flagged
  construction.
- **The generalization set** (242 sentences written blind by another model family around cue words and constructions
  that the training data does not contain) is read correctly in 177 cases, 73.14 percent, against 64.88 percent for the
  tag alone. New cue words and constructions are where the model is weakest, far below its 96.23 percent on the natural
  holdout.

### Limits of This Evidence

- **The labels are not human-verified.** The reading of each natural sentence was chosen by a rule from the part of
  speech or by a language model, and model-written rows were audited blind by a second model. The accuracy is agreement
  with these labels.
- **The tagger saw part of the natural holdout.** Rule-labeled rows come from the corpora that the part-of-speech
  tagger was trained on, which flatters the tag-based features; the independent holdout agrees within about one point,
  but it is synthetic text.
- **Small counts.** A reading has few holdout rows, so a per-reading figure is noisy; the pooled figures weigh the
  common readings most.
- **Coverage.** The model decides 395 readings of 207 words of US English, one sentence at a time.

## Pronunciation and Rendering

The phonemizer is measured against reference engines that a developer installs outside the repository: espeak-ng for
the Piper renderer and misaki for the Kokoro renderer. They are measurement oracles only; none of their output or data
is stored in the repository. Agreement with them is not the same as correctness, and the owner's listening verdict on
the blinded listening packs is still pending (source: the Phonemes.En gate summary, [REF-5]).

| Gate | Target | Measured | Result |
| --- | --- | --- | --- |
| Reading ids with a pronunciation | 100% | 853 of 853 | Pass |
| Piper and Kokoro symbols in the voice's table | 100% | 100% | Pass |
| Piper word match, sentences | 85% or more | 86.12% (held out 89.31%) | Pass |
| Piper word match, isolated words | 85% or more | 78.70% (held out 75.37%) | Below |
| Piper phoneme error rate | 8% or less | 3.01% (held out 2.47%) | Pass |
| Piper primary stress agreement | 90% or more | 95.90% (held out 98.10%) | Pass |
| Kokoro phoneme error rate | 6% or less | 2.00% weak forms, 4.88% full forms | Pass |

- **Isolated words.** The Piper isolated-word figure is below its target because the unstressed vowel that espeak-ng
  writes depends on the spelling, which the phones alone do not show; the sentence gate is met.
- **Deliberate departures.** Some listening fixes moved the Piper figures away from espeak-ng on purpose, for example a
  secondary stress on a weak function word that starts an utterance, which espeak-ng does not write, because the voice
  swallowed that word without it.
- **Morphology.** The suffix rules agree with the lexicon's own inflected entries for 97.38 to 99.59 percent of the
  words, depending on the ending.
- **Unknown words.** The trained letter-to-sound model has a phoneme error rate of 11.59 percent on held-out
  dictionary words, against 25.53 percent for a rules baseline.
- **Readings golden.** The real analyzer chooses the intended reading in 385 of the 395 sentences of the readings
  golden; the 10 misses are pinned as forced readings rather than hidden.
