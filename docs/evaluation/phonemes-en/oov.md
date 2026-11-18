# Phonemes.En Out-Of-Vocabulary Pronunciation: Benchmark and Decision

This is the committed record of how `DemaConsulting.Speech.Phonemes.En` pronounces a word that neither the CMU
Pronouncing Dictionary (CMUdict) nor the morphology rules know (the "letter-to-sound" fallback), why the shipped
approach was chosen, and how good it is. The decisive numbers are in `oov.tsv` (written by
`benchmark-lts --write` of the tools project `tools/DemaConsulting.Speech.Phonemes.En.Tools`); this document is their
interpretation. The decision rule was fixed by the owner before any number was measured: adopt a trained model only if
its phoneme error rate (PER) is at most 60 percent of the rules baseline's, its license is verified, it is at most 1 MB,
and it is deterministic (and fast enough); prefer a model trained here over a third-party one unless the third-party
model is at least 1.5 PER points better and its license is verified in writing; otherwise ship the rules.

## Summary

- **Decision: ship our own trained model** (three small integer neural networks, 665,711 bytes compressed), derived only
  from CMUdict. On held-out words it makes **11.59 percent PER with stress against 25.53 percent for the rules baseline
  (45 percent of it)**, gets 57.7 percent of words exactly right with stress (63.6 percent ignoring stress) against 26.7
  (30.8) percent, and puts the primary stress on the right syllable for 88.7 percent of words (87.2 percent of words of
  two or more syllables) against 76.9 (73.6) percent. It pronounces a word in about 0.09 ms.
- The g2p_en checkpoint measures better on this test set (9.80 percent PER) but its weights have **no license or
  provenance statement** and the test words are very probably in its training data, so it cannot be adopted and its
  number is an upper bound (see "g2p_en").
- The rules baseline stays only in the tools project as the benchmark's reference.

## The Benchmark

**Data.** The pinned CMUdict (`data/phonemes-en/MANIFEST.tsv`), after the filter of the embedded lexicon: 124,926
headwords of the letters a to z and apostrophes. CMUdict's words are not out-of-vocabulary words in the strict sense
(they include common words, and names are a large share), so the numbers are a measure of the model's ability to
generalize to unseen spellings, not an exact forecast for the unknown words a text will have.

**Split by stem family.** A random split would put `cats` in the test set and `cat` in the training set and reward
memorization. Words are therefore grouped by stem family: a word is joined to every headword it derives from by one
inflection or common derivational suffix
(`s es ies ed ied ing er est ly ily y ness ment ful less able ish ist ism ize ise`, with the undoubled and silent-e
variants and the possessive `'s`) or by one of the 16 productive prefixes the phonemizer itself recognizes
(`un re dis mis pre non over under out anti sub inter semi super counter multi`, on a remainder of at least four
letters), a stem has at least three letters, and the connected groups are the families. The side of a family is a
function of its alphabetically first word only: SHA-256 of `lts-split-v1:` and that word, read as a little-endian number
`n`; `n mod 10 = 0` is the test set, of the others `(n / 10) mod 20 = 3` is a validation set that is only used to watch
training. The split has no seed and no stored list (`LtsCorpus` in the tools project recomputes it from the pinned
file): 80,625 families, **107,202 training words, 5,466 validation words and 12,258 test words** (9.8 percent).

**Metrics** (`LtsBenchmark`). A word may have several dictionary variants; a candidate is scored against the best
matching variant.

- **PER with stress**: the Levenshtein distance between the hypothesis and the variant at the smallest distance, summed
  over the test words and divided by the summed lengths of those variants, in percent; a vowel with another stress digit
  is an error. **PER without stress**: the same with vowels compared by their base symbol (the best variant is chosen
  separately).
- **Word accuracy**: the percentage of words whose hypothesis equals some variant exactly, with stress and without.
- **Primary-stress position**: the percentage of words whose primary-stressed vowel (by vowel index, or "no primary
  stress") is the same as in some variant, over all words and over the words with two or more vowels.
- **Time per word** is measured on the developer machine (13th-generation Intel Core i7, .NET 10), single threaded, and
  is not committed in `oov.tsv`, which has to be identical on every machine.

## The Candidates

**A. Rules baseline** (`NrlRules`, tools project only). Context-sensitive letter-to-phone rules in the published format
of the Naval Research Laboratory report "Letter-to-Sound Rules for Automatic Translation of English Text to Phonetics"
(NRL Report 7948, 1976): `left(letters)right=phones` with the context symbols `# : ^ . + %` and space. The report is a
US government publication; the **296 rules here were written for this project** from the published rule format and
English orthography, none copied from a table, program or data set, and not tuned on CMUdict. They produce phones;
stress is added by orthographic heuristics (one vowel: primary; endings such as `-tion -ic -ity` stress the syllable
before them; otherwise by the stem's syllable count, with a prefix rule for two-syllable words; secondary stress two
syllables before the primary; unstressed full vowels reduce to schwa).

**B. g2p_en weights** (`g2p_en` 2.1.0 from PyPI, checkpoint `checkpoint20.npz`). A GRU sequence-to-sequence model; it
was measured by a local Python script kept outside the repository (the inference of the package's `G2p.predict`,
re-implemented without the package's text-processing dependencies) on the same test words. Only aggregate numbers are
recorded here.

**C. Our own model** (`LtsTrainer`, `FloatNetwork`, library `LtsModel` and `LtsNetwork`). Three small networks trained
on CMUdict only:

1. **Alignment.** Every letter of a training word is aligned with zero, one or two phones (`x` gets `K S`, a silent `e`
   gets none) by expectation-maximization over the training words with a weak vowel prior and a final Viterbi pass
   (`LtsAligner`). Only the first pronunciation variant of a word is trained on.
2. **Phone network.** For each letter it chooses the letter's phone chunk (93 or 94 classes seen at least three times)
   from a window of six letters on each side, the letter's distance from both word ends, the chunks chosen for the
   previous three letters, and the first and last three letters of the word. Left to right, greedy.
3. **Stress networks.** A *primary* network scores every vowel of the resulting phones as the carrier of primary stress
   (phone context, syllable position, the word's first and last letters, syllable weight); the best vowel gets it (a
   one-vowel word always does). A *secondary* network then decides for each other vowel, knowing its distance from the
   primary vowel. All other vowels are unstressed.

Each network is a rectifier network (sparse categorical inputs summed into a hidden layer of 256, 96 or 64 units, then a
second hidden layer and an output layer), trained with Adam and dropout. It is **quantized to integers** (16-bit
embeddings, 8-bit dense weights, 11-bit activations) and runs in exact integer arithmetic, so it gives the same answer
on every machine.

Variants measured (all with the same recipe, trained on the training split only): `small` (phone network 128 and 64
hidden units), `shipped` (256 and 128) and `large` (384 and 192, more dropout). The shipped model uses the `shipped`
settings and is trained on **all** entries; the numbers below are for the `shipped` settings trained on the training
split (the "held-out" model), because nothing about a model is measured on the words it was trained on.

## Results

Held-out test set of 12,258 words (`oov.tsv`). All figures are percent except sizes (gzip bytes of the model image) and
times:

| Candidate | PER with stress | PER without stress | Word accuracy | Word accuracy, no stress |
| --- | --- | --- | --- | --- |
| A. rules (296 rules) | 25.53 | 22.01 | 26.69 | 30.76 |
| C. own `small` | 12.23 | 9.38 | 56.13 | 61.78 |
| **C. own `shipped`** | **11.59** | **8.81** | **57.71** | **63.57** |
| C. own `large` | 11.27 | 8.44 | 59.13 | 65.12 |
| B. g2p_en (contaminated, see below) | 9.80 | 7.85 | 68.90 | 73.40 |

| Candidate | Primary stress position | Same, 2+ syllables | Size | Milliseconds per word |
| --- | --- | --- | --- | --- |
| A. rules (296 rules) | 76.87 | 73.62 | none (code) | 0.01 |
| C. own `small` | 88.08 | 86.39 | 344,566 | 0.05 |
| **C. own `shipped`** | **88.73** | **87.16** | **664,484** | **0.09** |
| C. own `large` | 88.80 | 87.26 | 960,792 | 0.13 |
| B. g2p_en | 92.32 | 91.23 | 3,342,298 | not comparable (Python) |

The `shipped` model has 45 percent of the rules baseline's PER with stress (and 40 percent without); the `small` and
`large` variants have 48 and 44 percent.

How the variants were chosen: the first version of the phone network (no chunk history, no dropout) had 13.8 percent PER
at 128 hidden units and gained nothing from 256 or 384 units (13.5 and 13.7), so it was not capacity that limited it.
The history of the previous three chunks, dropout (0.1 to 0.3), fewer epochs for the stress networks (they stopped
improving after four to six) and the first and last three letters of the word then brought it to the numbers above.
`large` is 0.3 PER points better than `shipped` but 300 KB bigger, slower, and close to the 1 MB budget (960,792 of
1,048,576 bytes), so `shipped` was chosen; `large` can be selected by changing the settings if more accuracy is wanted.
No setting was tuned on the test words (the validation set was used for the epoch counts and dropout).

A sample of the mistakes of the `shipped` held-out model (word, closest dictionary variant, model):

```text
fragrant   F R EY1 G R AH0 N T            F R AA1 G R AH0 N T
medusa     M AH0 D UW1 S AH0              M EH0 D UW1 S AH0
chagrined  SH AH0 G R IH1 N D             CH AE1 G R AH0 N D
globulin   G L AA1 B Y AH0 L IH0 N        G L OW0 B UW1 L IH0 N
quotient   K W OW1 SH AH0 N T             K W OW1 T IY0 AH0 N T

Most are the vowel quality of an unstressed syllable, a foreign name or a genuinely irregular word, where several
answers are plausible; they are the kind of error a listener hardly notices.

## g2p_en: License, Provenance And Result

What the package says, read from the PyPI release `g2p_en` 2.1.0 (wheel `g2p_en-2.1.0-py3-none-any.whl`) and the
repository <https://github.com/Kyubyong/g2p>:

- **Code license**: Apache License 2.0. The repository's `LICENSE.txt` and the wheel's `dist-info/LICENSE.txt` are the
  unmodified Apache-2.0 text, and the PyPI metadata says `License: Apache Software License`. There is no `NOTICE` file.
- **The weights** are the file `g2p_en/checkpoint20.npz`: 3,342,298 bytes (an array archive), shipped inside the same
  wheel. **Nothing in the repository, the README or the PyPI description states a license, a data source or a training
  procedure for the checkpoint specifically.** The README says that the package uses CMUdict for words it knows ("Looks
  up The CMU Pronouncing Dictionary for non-homographs") and "a deep learning seq2seq framework" for the others, which
  is as close as the text comes to the origin of the weights. The Apache-2.0 license of the repository would normally
  cover its artifacts, but the training data of a checkpoint is a separate fact that is not documented, and the training
  script is not in the repository or the wheel (the repository's only other document, `g2p.pdf`, is a browser print of a
  web page made in October 2019 and states no license or data source). The wheel's own `g2p.py` loads the file and
  implements the inference; the checkpoint's exact training and development words are unknown.
- **Verdict: the license and provenance of the weights are not verified in writing.** They would also have to be
  quantized to meet the 1 MB budget (3.3 MB as shipped), which would change the artifact being licensed.
- **Result** (`G2p.predict` re-implemented outside the package; the stress-less symbol `UW` of its phoneme list read as
  `UW0`): 9.80 percent PER with stress, 7.85 without, 68.9 percent word accuracy, 92.3 percent primary-stress position.
  This is 1.79 PER points better than our model. **The measurement is an upper bound**: the checkpoint was almost
  certainly trained on CMUdict including the words of our test set (the split is ours, the checkpoint's training words
  are not published), so its test performance is memorization plus generalization, while ours is generalization only.

Applying the rule: g2p_en is at least 1.5 points better, but its license is not verified in writing (and the comparison
is contaminated in its favor), so the preference for our own model stands.

## The Decision

Each criterion of the owner's rule, with the result:

- **PER at most 60 percent of the rules baseline**: 11.59 of 25.53 is 45.4 percent. Met.
- **License verified**: met. The weights are derived only from CMUdict (its BSD-style notice is carried in
  `PACKAGE-LICENSE.md` and `THIRD-PARTY-NOTICES.md`); no third-party model or code is used.
- **At most 1 MB**: 665,711 bytes compressed, against 1,048,576 allowed. Met.
- **Deterministic**: met. Integer inference is bit-identical on every machine; training is deterministic, and
  `train-lts --check` retrained the shipped model on a loaded machine and reproduced the identical image (SHA-256
  `9ac8ad6e...`).
- **At most 0.5 ms per word** (measured, not unit tested): about 0.09 ms. Met.
- **Prefer our own model over g2p_en unless g2p_en is 1.5 points better with a verified license**: g2p_en has no
  verified license, so our own model is preferred.

The rules baseline is therefore not shipped; it lives only in the tools project as the reference of this benchmark. The
shipped fallback is `LetterToSound` in the library, behind the unchanged `ILetterToSound` seam.

## What Ships

- `src/DemaConsulting.Speech.Phonemes.En/Resources/lts-en.bin.gz`: **665,711 bytes** (the uncompressed image is 723,851
  bytes, SHA-256 `9ac8ad6ebad7778076bbefe0a0c0737d6c06bb339337d145d50ec3bfafde3b52`, pinned in
  `data/phonemes-en/MANIFEST.tsv`).
- **Which model ships.** The shipped model is trained on **all 124,926 entries** with exactly the `shipped` settings of
  the benchmark (`LtsTrainingSettings.Shipped`). The held-out numbers above come from the same settings trained on the
  107,202 training words, as that is the only honest way to measure generalization; the shipped model has seen the test
  words, so nothing is measured on it. The accuracy on genuinely unseen words should be a little better than the
  held-out figure, because it has about 17 percent more training data.
- A word of any spelling gets a pronunciation: characters other than letters and apostrophes are skipped, apostrophes at
  either end are dropped, very long words and repeated letters work (the cost is linear and the window is local), and a
  word for which the model yields no vowel (`www`, `sq`) is spoken letter by letter by letter name, so that it is never
  silent or impossible to say. Results are cached per word in a bounded, thread-safe map; the answer depends on the word
  only.

## The Golden Of Realistic Words

`test/DemaConsulting.Speech.Phonemes.En.Tests/TestData/oov-golden.tsv` holds 115 words that CMUdict lacks (names,
brands, neologisms, compounds, loanwords, chosen by hand), each with the shipped model's pronunciation. It exists only
to detect drift of the weights: a behavior test fails when any pronunciation changes, and the tools project's golden
regeneration command (`regenerate-goldens --oov`)
rewrites it for review. The golden files were reviewed after generation and **no line was corrected by hand**.
Pronunciations that are clearly wrong or doubtful are recorded here and not fixed in the file (word, model output,
what a listener would expect):

```text
airbnb    EH1 R B N B          air bee en bee: letter names expected, the consonants run together
chatgpt   CH AE1 T P T         chat jee pee tee: a spelled-out ending
postgres  P OW1 S T G R Z      P OW1 S T G R EH2 S: the second syllable lost its vowel
nginx     N JH IH1 NG K S      engine-x: an initialism-like brand
djembe    JH EH1 M B           jem-bay: the final e is not voiced
saoirse   S AW1 R S           seer-sha: an Irish name
huawei    HH W AO1 EY0         wah-way: first syllable
ramen     R AE1 M AH0 N        rah-men: loanword vowel
```

The other 107 are plausible to a native listener or differ in one detail, for example:

```text
bokeh           B OW0 K EY1
kombucha        K AA2 M B UW1 CH AH0
kubernetes      K Y UW1 B ER0 N IY2 T S
fluffernutter   F L AH1 F ER0 N AH2 T ER0
xiomara         Z IY2 OW0 M AA1 R AH0
cryptocurrency  K R IH0 P T OW1 K ER0 AH0 N S IY0     (the stress could be on the second syllable)
```

In the sentence golden file `sentences-golden.tsv` seven of the 150 lines changed, all of them only in a word marked
`!oov` (eight words moved from the old letter-name placeholder to the model's output: the made-up name, the capital of
Nepal, the keyboard layout, the cat's name, the currency word, the dessert, and the abbreviations for square and foot;
`www` is still spelled by letter name, because the model gives it no vowel); the other 143 lines are unchanged.

## Limits

- The benchmark words are dictionary words, which have a different mix from the unknown words of a real text (more
  names and loanwords in the unknown words, more common words in the dictionary). The stem-family split is harder than
  the random splits of most published G2P results, so these numbers are not comparable with them.
- Names with foreign origin are inherently ambiguous; no letter-to-sound model can be right for them without knowing the
  language of the name. There was no listening test in this chunk; the owner's blinded listening check covers it later.
- The model pronounces a word in isolation; heteronyms and sentence stress are the text analysis's business.
- The measurement of g2p_en is contaminated in its favor (see above), and its speed is a Python figure not comparable
  with ours.

## Reproducing

```text
dotnet run -c Release --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- benchmark-lts --variants small,shipped,large --write
dotnet run -c Release --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- benchmark-lts --export-test-words test-words.txt
dotnet run -c Release --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- benchmark-lts --variants small,shipped,large --g2p-en predictions.tsv --g2p-en-bytes 3342298 --write
dotnet run -c Release --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- train-lts --check
dotnet run -c Release --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- regenerate-goldens --oov
```

The first command trains the three variants on the training split (about 25 minutes) and rewrites `oov.tsv` without the
g2p_en row. The third also scores g2p_en from `predictions.tsv`, a two-column file (word, ARPAbet phones) that a local
Python script (not in the repository) writes for the words of `--export-test-words`; it is the command that produced the
committed `oov.tsv`. `train-lts --check` retrains the shipped model (about 8 to 10 minutes) and compares it with the
committed resource and `MANIFEST.tsv`. The CMUdict file must be fetched first (see `data/phonemes-en/README.md`).
