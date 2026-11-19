# Phonemes.En Renderers: Piper IPA and Kokoro Symbols

This is the committed record of how `DemaConsulting.Speech.Phonemes.En` turns the ARPAbet pronunciations of its
`PhonemizedText` into the phoneme strings the two voice models expect, and how close those strings are to the reference
engines the models were trained on. The numbers are in `renderers.tsv`; this document explains them. Only aggregate
numbers and phonetic class descriptions are recorded here: the output of the reference engines (espeak-ng and misaki)
is used on a developer machine to measure and is never committed, and neither engine's data or code was used to write
the mapping tables.

## Summary

- **Two renderers, both authored from phonetics.** `EspeakIpaRenderer` (profile `EspeakIpaProfile.EnUs`) writes the
  espeak-style IPA the Piper `en_US` voice reads: stress marks before the vowel, `ɜː`, `ɚ`, `ɑːɹ`, diphthongs as symbol
  pairs, the flap `ɾ`, the lax `ᵻ`, words separated by a space, clause punctuation passed through. `MisakiRenderer`
  (profile `MisakiProfile.Us`) writes Kokoro's misaki-style symbols: `A I O W Y` for the diphthongs, `ʧ ʤ`, `ɜɹ`/`əɹ`,
  `ᵊ`, `ɾ`, `ᵻ`, `ˈ ˌ` before the vowel. Out-of-the-box Piper uses the weak forms of about sixty function words
  (decision D11); Kokoro uses the full dictionary forms (an opt-in `useWeakForms` exists, see below).
- **100 percent vocabulary coverage.** Every one of the 133,971 dictionary pronunciations, every conversion of the
  out-of-vocabulary model for a sample of dictionary spellings and the golden words, every phone in every neighborhood
  of two other phones (69 x 69 x 69 sequences), and every weak form render to symbols inside the Piper
  `phoneme_id_map` (159 symbols) and Kokoro's vocabulary (115 symbols): `RenderingSubsystemTests` proves it on every
  target framework.
- **D5 gates (provisional, report then ratchet).** Piper against espeak-ng en-us on the 450-sentence tuning corpus:
  **word match 86.1 percent (gate 85), phoneme error rate 2.9 percent excluding stress (gate 8), primary stress
  agreement 95.4 percent (gate 90)**. Kokoro against misaki US: **phoneme error rate 4.9 percent with full forms (gate
  6)**, 2.0 percent with the opt-in weak forms. On text nobody tuned the rules on (60 fresh sentences, 3,000 further
  frequent words) the figures hold: Piper 89.5 percent word match, 2.4 percent PER, 98.1 percent primary stress
  agreement on the sentences, and 4.4 percent PER on isolated words; Kokoro 4.5 percent PER on the sentences and 2.8
  percent on the words. The one figure below its provisional gate is the **word match of isolated words** (78.9 percent
  tuned, 75.5 percent held out): it is not the gate's corpus (sentences) and its residual is the engine's
  spelling-driven vowel choice (see "Residual classes").
- **The gates are ratcheted, never loosened:** the figures above are the new floor (the first authored mapping scored
  61.6 percent word match and 9.7 percent PER for Piper, 6.6 percent PER for Kokoro; see "What the measurement
  changed"). The refinement pass after the first end-to-end audio (hyphenated compounds, a few function-word details)
  raised Piper from 88.1 to 89.4 percent word match on the tuned sentences (90.1 to 91.8 held out), lowered its PER from
  3.14 to 3.01 (2.58 to 2.47) and raised its primary stress agreement from 94.3 to 96.0 (97.1 to 98.1); Kokoro's
  held-out figures did not move, and its tuned-sentence PER moved by +0.01 point (4.88 to 4.89 with full forms, 1.99 to
  2.00 with weak forms) because three sentences of the corpus now reach the misaki engine as different spoken text (a
  URL, `10K`, `A1`). The later listening fixes (see "Listening Fixes") brought the Piper figures to 89.4 word match and
  95.9 primary stress, and the Kokoro full-form PER back to 4.88. The second round of listening fixes (see "Listening
  Fixes 2") lowered the Piper sentence word match to 86.1 (89.3 held out) by design, because the first word of every
  utterance now carries a secondary stress that espeak-ng does not write; the error rate, the primary stress agreement
  and every other figure are unchanged or slightly better. The third round (see "Listening Fixes 3") lowered the Piper
  error rate to 2.9 (2.4 held out) and, by design, the primary stress agreement on the tuning sentences to 95.4, because
  a hyphenated compound now has one primary stress where espeak-ng writes two.

## What Is Rendered

Both renderers work on the ARPAbet phones of each item. A word with an IPA override is translated into the voice's
symbols (the diphthongs and affricates of the espeak-style IPA become Kokoro's single letters, `ɝ` becomes `ɜː` for
Piper and `ɜɹ` for Kokoro) and checked against the voice's symbol table; an override with a symbol the voice does not
have is not approximated, the word's normal pronunciation is rendered instead. An ARPAbet override renders exactly like
the dictionary word with the same phones. An unsupported token renders to nothing, and a punctuation item is rendered as
the model's own symbol or dropped.

**Piper (`EspeakIpaRenderer`).** The basic table is the textbook correspondence between ARPAbet and General American IPA
(`tʃ dʒ θ ð ʃ ʒ ŋ ɹ`, `oʊ aɪ aʊ eɪ ɔɪ`, `ɑː ɔː ɜː iː uː`, `ɛ ɪ ʊ ʌ æ`, `ɚ` for unstressed `ER`). The rules on top, each
a statement about phone classes and positions:

- The stress mark (`ˈ`, `ˌ`) stands directly before the vowel.
- A secondary stress next to the primary one is not realized (stress clash); a secondary stress on a final `-y` is not
  realized; only the seven numbers thirteen to nineteen are stressed on the first syllable (words that merely end in the
  sounds of `-teen`, such as `routine` and `canteen`, keep the stress of their dictionary pronunciation).
- A `t` between a vowel (or the `r` after it) and an unstressed vowel is a flap `ɾ`; `n` before `k` or `g` is `ŋ`.
- An `r`-colored vowel before another vowel gets a linking `ɹ` (also across a word boundary: `ɚ` or `ɜː` before a
  vowel-initial word); `ʊɹ` before a vowel is doubled.
- The unstressed short vowels: the lax `ᵻ` for the endings `-ed` and `-es`, the open `ɐ` for a word-initial `AH0`, the
  `ɛ` of `ex-`/`en-`, `ɪ` for `-ing`, `-ic`, `-age` and after the stress before some consonants, `ə` otherwise.
- `ɔ` (short) before `ŋ f θ s`.
- Weak forms (see below).

**Kokoro (`MisakiRenderer`).** The same textbook table with Kokoro's symbols, and the rules the misaki output shows: the
flap, the lax `ᵻ` of `-ed`/`-es`, the superscript schwa `ᵊ` for a syllabic `l` at the end of a word after a consonant
and for a syllabic `n` after `t d s z p f`, a plain schwa for a reduced `i` except before `ŋ` and in
`-ic`/`-ive`/`-age`/`-ish`, a schwa for the prefix `IY0` (re-, de-), and no secondary stress right before the primary
stress.

**Layout.** One space between words; a closing mark (`, ; : . ! ?`, `)`) attaches to the word before it; Kokoro's
opening marks (`“`, `(`) attach to the word after them and its dash stands between spaces. Piper has no symbol for
quotation marks, brackets and dashes and pauses with a comma instead (never two pause marks in a row). An ellipsis is
three dots for Piper and `…` for Kokoro.

**Weak forms (D11).** About sixty function words are rendered as running speech says them. The authored table has three
classes, which the measurement below decided: (a) the articles and the particle `to` (reduced vowel: `ðə`, `ɐ`, `tə`,
and `ðɪ`/`tʊ` before a vowel); (b) most conjunctions, prepositions, auxiliaries and pronouns, which keep their full
vowel but lose the stress mark (`ænd`, `ʌv`, `wʌz`, `hiː`); (c) a short list of words that keep their dictionary form
with a secondary instead of a primary stress (`but`, `not`, `how`, `so`, `up`, `us`, `them`, `over`, ...). A few short
function words followed by the article form one group without a space (`of the`, `in the`, `with the`, `for the`,
`was a`). Piper's profile adds a few details of its own training text (see "Refinement After the First End-to-End
Audio"): the flap of `it` and `at` before a vowel, `on the` without a mark, and a secondary stress for `made`, `put`,
`which`, `what`, `between` and a few more.

Kokoro's default is the full dictionary form (owner decision). The measurement shows that the reference text of Kokoro
treats function words exactly like the Piper text does (unstressed or demoted), so
`MisakiRenderer(profile, useWeakForms: true)` applies the same table converted to misaki symbols; it is off by default
and is the recommended setting if the listening test prefers it (see the figures).

## Method

**Clean room.** The mapping tables were authored from phonetics, from the Piper voice's own `phoneme_id_map` and from
the character vocabulary of Kokoro's `tokenizer.json`. The two reference engines were installed on the developer machine
outside the repository only after the first tables existed, purely as measurement oracles. A rule was changed only when
the measurement showed a class of disagreement, and each rule is stated as a phonetic generality (see "What the
measurement changed"); no rule or table entry maps one word to the engine's string for that word, except the
function-word table, which is per word by nature and whose three classes were decided by the class statistics.

**Corpora.** Three corpora, all own-written or drawn from the repository's own data, deterministic:

- *Sentences, tuned:* the 150 reviewed sentences of `sentences-golden.tsv` (numbers, dates, abbreviations, heteronyms,
  unknown words) and 300 more own-written sentences (`data/phonemes-en/renderer-corpus.tsv`, CC0), 450 sentences and
  4,300 words, each run through the real text analysis and the phonemizer. The reference engines were given the spoken
  text of each sentence (the words after the normalization), so number and abbreviation handling does not count against
  a renderer.
- *Words, tuned:* the 3,000 most frequent dictionary words of the text-analysis training sentences (`measure-oracle`).
- *Held out:* 60 further own-written sentences (`data/phonemes-en/renderer-validation.tsv`) and the next 3,000 words of
  the frequency list. They were written and run once after the rules were frozen; no rule was changed after looking at
  them.

**Metrics** (`measure-oracle` of the tools project exports our renderings to the git-ignored `data/phonemes-en/work/`;
the comparison script lives outside the repository and is run with the local oracle virtual environment):

- *Word match:* the word strings are identical, stress marks included. Words are aligned by dynamic programming on their
  edit distance (the engines merge some word pairs, which then count as a mismatch of the second word).
- *Phoneme error rate excluding stress:* character-level edit distance (insertions, deletions, substitutions) between
  our word and the engine's word with the stress marks removed, summed over all aligned words and divided by the
  engine's length. The models read one symbol per character, so a character is the right unit.
- *Stress:* the stress marks are scored separately. A word's primary stress position is the index of the vowel group the
  primary mark stands before; *primary stress agreement* is the share of words with a primary stress in either string
  whose positions are equal; *signature agreement* compares all marks of all words; *secondary agreement* compares the
  secondary marks of the words that have one in either string.
- The reference engine for Piper was espeak-ng en-us (through its Python binding, language-switch flags removed,
  punctuation preserved) and for Kokoro misaki's US English G2P without its fallback.

## Results

All numbers are percent. The first pass of each pair is the corpus the rules were tuned on, the second the held-out one.
`Piper` words are rendered without the weak-form table (isolated words have no sentence stress).

| System and corpus | Words | Match | No stress | PER | Primary | Signature | Secondary |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Piper, 450 sentences | 4,279 | 86.07 | 91.35 | 2.93 | 95.39 | 93.18 | 53.08 |
| Piper, 60 held-out sentences | 636 | 89.47 | 92.77 | 2.40 | 98.10 | 95.60 | 55.74 |
| Piper, 3,000 words | 3,000 | 78.93 | 81.50 | 4.40 | 95.39 | 93.80 | 62.56 |
| Piper, 3,000 held-out words | 3,000 | 75.53 | 77.67 | 4.35 | 96.76 | 94.00 | 68.28 |
| Kokoro full, 450 sentences | 4,357 | 62.73 | 82.19 | 4.88 | 71.45 | 75.10 | 23.54 |
| Kokoro full, 60 held-out sentences | 659 | 64.80 | 83.16 | 4.50 | 72.17 | 76.33 | 21.62 |
| Kokoro full, 3,000 words | 2,990 | 81.34 | 86.09 | 2.91 | 94.81 | 91.27 | 48.47 |
| Kokoro full, 3,000 held-out words | 2,955 | 80.68 | 83.82 | 2.80 | 97.39 | 93.81 | 69.02 |
| Kokoro weak, 450 sentences | 4,357 | 86.21 | 93.09 | 2.00 | 94.14 | 91.53 | 45.34 |
| Kokoro weak, 60 held-out sentences | 659 | 89.23 | 94.84 | 1.43 | 96.59 | 93.47 | 48.19 |

Columns: words compared, word match (stress marks included), word match ignoring stress, phoneme error rate (PER)
excluding stress, primary stress agreement, stress signature agreement, secondary stress agreement.

**Gates.** Piper word match 86.1 (gate 85), PER 2.9 (gate 8), primary stress 95.4 (gate 90): met on the tuned corpus and
on the held-out sentences (89.5, 2.4, 98.1). Kokoro PER 4.9 with full forms and 2.0 with weak forms (gate 6): met. Not
met: Piper word match on isolated words (78.9 and 75.5 against 85); that metric is not the gate's corpus and is
dominated by the class "unstressed short vowel chosen by spelling" below. The stress agreement of Kokoro with full forms
(71 percent) is low only because of function words: with the weak forms it is 94 percent. The Piper word match on
sentences fell from 89.4 to 86.1 and its stress signature and secondary stress agreement fell with it, on purpose: see
"Listening Fixes 2". The third round (see "Listening Fixes 3") moved the Piper figures by less than a point: word match
86.12 to 86.07 on the tuned sentences (89.31 to 89.47 held out), PER 3.01 to 2.93 (2.47 to 2.40), primary stress 95.90
to 95.39 (98.10 held out, unchanged); Kokoro's figures did not move.

## What the Measurement Changed

The first authored tables (textbook mapping, stress mark before the vowel, flap after a stressed vowel, reduced vowels
`ə`/`ɪ`, a first sixty-word weak-form table with reduced vowels) scored: Piper 61.6 percent word match, 9.7 percent PER,
89.0 percent primary stress agreement on the sentences and 62.8 percent / 7.5 percent / 95.1 percent on the words;
Kokoro 6.6 percent PER and 71.4 percent stress agreement on the sentences, 5.7 percent PER on the words. The class
statistics then showed, in order of size, and each of these became a general rule:

1. **Unstressed short vowels** were the largest class for both engines. Piper's engine distinguishes `ɪ`, the lax `ᵻ`,
   `ə` and the open `ɐ` by environment: `ᵻ` in the endings `-ed` (after `t`/`d`) and `-es` (after a sibilant), `ɪ`
   before a final `ŋ` or `k`, `ɐ` at the start of a word, `ɛ` for `ex-`/`en-`. Kokoro's engine writes a schwa where the
   textbook has `ɪ` (prefixes, most unstressed positions) and keeps `ɪ` before `ŋ`, final `k`, `v`, `ʤ`, `ʃ`.
2. **The syllabic consonant mark `ᵊ`** (Kokoro): a quarter of the edits of the word pass. A rule for a final syllabic
   `l` after a consonant and for `n` after `t d s z p f` captures most of the class.
3. **Function words** (Piper, sentences): the engine's training text leaves most function words unstressed and keeps
   their full vowel; the first table reduced too many vowels. The three classes above came from this.
4. **Flapping** extends to a `t` after an unstressed vowel and after `r` (the first rule required a stressed vowel
   before).
5. **Secondary stress** is dropped next to the primary stress and on a final `-y` for Piper; Kokoro drops it only right
   before the primary stress.
6. **Linking `ɹ`** between an `r`-colored vowel and a following vowel-initial word (Piper).
7. **Smaller classes:** the shortened `ɔ` before `ŋ f θ s`; `ɚ` followed by a vowel; stress of the numbers thirteen to
   nineteen; `ʊɹ` doubling before a vowel; `un-` as `ʌ`; word groups such as `of the`.

## Refinement After the First End-to-End Audio

After real audio existed (the owner's listening packs and a speech-recognition round trip, see the summary), a second
measurement pass looked at the whole phoneme string of sentences against espeak-ng, not only word by word: separators,
punctuation, stress marks and compounds. On a pack of 133 own-written sentences the punctuation passes through at the
same positions in all 133 and the word separators agree in 130 (the three others are the groups below), so no
systematic problem of framing, pauses or word boundaries was found; 38 of the 133 whole strings were identical before
this pass and 42 are after it. The classes it did find were:

1. **Hyphenated compounds (Piper).** All 40 of 40 sentences of a forty-compound probe differed from espeak-ng, and the
   cause was general: the engine gives every content part of a compound its own primary stress, where the phonemizer
   demoted the later parts to secondary stress, and it used the dictionary's closed spelling (for example the entry for
   full-time, whose stress pattern is that of one word) when one was listed. A compound is now pronounced part by part,
   each part with its own stress; a closed dictionary spelling is used only when a part is not a word of its own
   (`wi-fi`, where `wi` and `fi` are not words); the small function words inside a compound are unstressed (`of` keeps
   its full vowel without a mark, `to` is reduced). 26 of the 40 probe sentences now agree exactly, the rest differ only
   in the spelling-driven vowels. The text-analysis item records where the second part starts, and the Kokoro renderer,
   whose reference stresses the first part of a compound, demotes the primary stresses after that point (as before).
2. **Function-word details (Piper only).** The espeak-style text flaps the final `t` of `it` and `at` before a vowel
   (3 of 3 each), leaves `on` unstressed in `on the` (14 of 14) and says `for the` as one group with
   the reduced vowel (7 of 7), and gives a secondary stress to `made`, `make`, `put`, `which`, `what`,
   `between`, `behind`, `beside`, `beneath`, `any` and `already` in all or nearly all of their occurrences in the
   corpus. These entries apply
   to the Piper renderer only; the misaki-based profile does not use them (misaki keeps the primary stress and the `t`).

Two candidate rules were measured and rejected because they made the figures worse: a reduced vowel for `for` before any
consonant (the engine keeps the full vowel of `for` there except in `for the`), and a medial `ɪ` for an unstressed `IY`
before a consonant (the engine writes `ɪ` in `everyone` but `i` in `twenty-one`).

The numbers above are after this pass: Piper word match 88.05 to 89.44, PER 3.14 to 3.01, primary stress agreement
94.34 to 95.98 on the 450 tuning sentences; 90.09 to 91.82, 2.58 to 2.47 and 97.05 to 98.10 on the 60 held-out ones.

### Listening Fixes

The owner's blinded listening led to four rule changes. They are general rules, not per-word entries, and they moved the
tuning-sentence figures only slightly (the held-out and word passes did not move):

1. **Full form after a voicing-alternating word.** A function word with a reduced form keeps its full dictionary form
   right after a word whose readings or dictionary variants differ only by the voicing of the final consonant (`close
   to`, `a house to`, `the use of`). The set of such words (about 50 spellings, among them `close`, `use`, `house`,
   `excuse`, `abuse`, `diffuse`) is derived at first use from the reading table and the lexicon, with the function
   words themselves left out. The rule fires in 9 of the 450 tuning sentences and changes the rendering in three of them
   (`use it`, `house on`, `house is`), where espeak-ng keeps the weak form, so Piper moved from 89.44 to 89.39 percent
   word match and from 95.98 to 95.90 percent primary stress agreement (three words of 4,279). This is the intended cost
   of the rule.
2. **A bare full vowel in a Kokoro weak form gets a secondary stress mark.** `I` is one symbol in the Kokoro
   vocabulary, and the model swallows the lone unmarked symbol after a pause (`Honestly, I couldn't`). Misaki writes
   it with a secondary stress mark, and so does the renderer now: Kokoro weak-form secondary stress agreement rose from
   41.73 to 45.34 percent and word match from 85.72 to 86.21 percent.
3. **Dictionary preference for the comparatives of `long`, `strong` and `young`.** The dictionary lists `stronger`
   twice, first without the `g` of the velar stop. The variant with it now comes first when the dictionary's own `-est`
   form of the same stem has it; derived superlatives after a stressed `ng` get the `g` too. Kokoro full-form error rate
   4.89 to 4.88 percent against misaki.
4. **A hyphen between two replaced digit groups is a pause.** The text analysis reads the groups of `555-0147` one by
   one and the phonemizer keeps the hyphen between them as a pause (a comma for Piper, a dash for Kokoro) instead of a
   compound joint. Espeak-ng has no pause there, so one tuning sentence of 450 has a different word count (same word
   count 94.89 to 94.67 percent of the sentences) with no change in the error rate.

### Listening Fixes 2

The second round of owner listening (Piper pack, sentences 14 to 30) found that the new path had fixed the old failures
and reported four more: `He` almost silent at the start of `He whispered, "We should leave ...`, `coffee` with a clipped
last vowel in `tea, coffee, or something stronger`, `recommends` with its first sound missing in `The committee's
report, released on Tuesday, recommends sweeping changes`, and in general the words right after a comma. A speech
recognizer (the library's
Zipformer, a weak streaming model, so every number below counts as errors of a proxy, not as listening) was run over
many renderings of each variant, because the voice draws noise and one rendering proves nothing. All sets use several
speakers and three draws per speaker; the words and counts are of the proxy's output.

**What the strings are.** For all three sentences the phoneme strings equal espeak-ng's for the same words
(`hiː wˈɪspɚd,`, `kˈɑːfi,` against `kˈɔfi,`, `ɹˌɛkəmˈɛndz` in both), so the cause is not a wrong phoneme but how the
voice speaks the start of an utterance. The session speaks every segment as its own text and the core chunker makes one
segment per clause, so the first word of each clause piece is the first word of an utterance; the phonemizer sees each
segment as a text start.

1. **The first word of an utterance (Piper).** Over 90 sentences that start with 50 different words (1,080 renderings, 4
   speakers) the proxy recognized the first word in 73.1 percent of the renderings with the weak forms as they were. An
   unmarked full-vowel weak form is often swallowed or heard as another word: `should` and `could` were recognized in
   none of 24 renderings each and `my` in none, `these` in 4, `can` in 9, `if` in 12, `it` in 15, `was` in 15, `will`
   in 17. The rule now: the first word of the rendered text (and of each part of a long text) keeps its full-vowel weak
   form but with a secondary stress mark before the vowel (`hˌiː`, `ʃˌʊd`, `ˌaɪ`; `AtTextStart`), the reduced forms
   (`ðə`, `ɐ`, `tə`), forms that have a mark already and `the` are unchanged, and a word after a comma inside the text
   is not first. With it the first word was recognized in 86.1 percent (`should` 21 of 24, `my` 17, `could` 14, `these`
   16, `if` 21, `was` 24, `will` 24) and the first word was lost (the transcript starts at the second word) in 39
   renderings instead of 56. One exception was measured and kept: `for` with any stress mark is heard as `war` or `or`
   (21 of 24 unmarked, 7 of 24 with `ˌ`; in a second set 31 of 48 unmarked against 6, 11 and 14 of 48 for `fˌɔːɹ`,
   `fˈɔːɹ` and `fɚ`), so it keeps `fɔːɹ`. The Kokoro model does not have the problem: over the same 90 sentences with
   three voices the first word was recognized in 97.8, 87.8 and 98.9 percent without the mark and in 98.9, 87.8 and 100
   percent with a secondary stress on it, so Kokoro's rendering is unchanged (its earlier `ˌI` rule stays; for the
   pronoun `I` it gives the same result as the new rule gives Piper). The words that start a text and have no stress
   mark at all in the Piper rendering are exactly the weak-form table's, so no other class is left unmarked. Cost
   against espeak-ng (which writes no mark there): 59 of the 150 golden sentences and about 140 of the 4,279 word pairs
   of the tuning sentences have a differing first word, so the word match fell from 89.39 to 86.12 percent (91.82 to
   89.31 held out), the stress signature agreement from 96.75 to 93.48 and the secondary stress agreement from 76.29 to
   54.49; the error rate excluding stress (3.01), the match excluding stress (91.14) and the primary stress agreement
   (95.90) did not move. The gate of 85 percent word match is still met, and the loss is the intended price of a first
   word that is heard.
2. **Final vowels and `coffee`.** The final unstressed vowel of the words in the two 3,000-word passes whose final vowel
   is `i` or `iː` agreed with espeak-ng in 93.1 percent of 175 words (95.4 percent of 194 held out) before this round
   and 95.3 percent of 171 (95.9 percent of 193) after it. The convention is espeak's own: `i` after the spellings `-y`,
   `-ey` and `-ie` (1,205 of 1,380, 405 of 439 and 143 of 157 words of a 2,932-word sample of the dictionary's `IY0`
   endings) and `iː` after most `-ee` (46 of the 49 whose final vowel is unstressed) but `kəmˈɪɾi` for `committee` and
   `kˈɔfi` for `coffee`; for Kokoro the misaki final `i` agrees in 1,099 of 1,120 comparable words. The differences that
   remained in the frequent words were the flap of `-ty` after an unstressed vowel (`ability`, `identity`, `reality`:
   the dictionary stresses the final `-y` secondarily, which the renderer already drops, but the flap test did not know
   it; now `ɐbˈɪlᵻɾi`, 12 of 175 to 8 of 171 and 9 of 194 to 8 of 193 differences, Kokoro likewise) and the weekday
   names (espeak-ng and misaki end them in `eɪ`, the dictionary lists that variant second: not changed, listed as a
   residual). `kˈɑːfi` against espeak's `kˈɔfi`: in a rendering of the clause piece `coffee,` on its own the proxy
   recognized our `kˈɑːfi` 23 of 24 times and espeak's string 0 of 24 (it lost the first sound), and `kˈɑːfiː`
   6 of 24; inside the sentence all variants were recognized 24 of 24. So the final vowel was not the cause and its
   convention was not changed; the cause was a one-word utterance (item 4).
3. **`recommends` and `released`.** The strings are espeak's. As clause pieces the proxy heard `recommends` as
   `a commends`, `I commence` or `at the men` in 11 of 15 renderings (and `released` as `the least`), inside the
   sentence in 2 of 15: the voice loses the first consonant of an utterance when the first syllable is unstressed or
   carries a secondary stress (34 words that start with a consonant and a secondary stress, 6 renderings each: 77.9
   percent recognized as written, 61.3 percent without the mark and 87.3 percent with a primary mark in its place). The
   primary mark is not espeak's convention and was not adopted; the cure is not to make such a word start an utterance
   (item 4).
4. **Clause pieces as utterances (the pattern).** Controlled experiment: 63 sentences with commas (60 of the renderer
   corpus plus the three listening sentences), 5 speakers, 3 draws, so 945 renderings and 11,475 words per variant; (a)
   the current chunking, each clause piece its own utterance, and (b) one utterance per sentence, the clause pieces
   joined, the comma passed through as a symbol (how the voice was trained). With the final renderer: word error rate of
   the proxy 6.78 percent for (a) and 6.63 for (b) (no significant difference: the paired difference over sentences is
   18 errors with a 95 percent bootstrap interval of -91 to +129); the first word of a clause after a comma was
   recognized in 90.6 percent of the renderings for (a) and 97.5 percent for (b) (87 fewer misses in (b), interval 39 to
   146), in every one of the five speakers (88.5 to 92.9 against 94.4 to 99.6); the first word of the sentence in 89.6
   and 86.9 percent (26 more misses in (b), interval -80 to +25, not significant: it is the cost of a longer utterance,
   mostly `the` and `a`). The clause pieces that variant (a) lost are the short ones and those that start with a
   consonant or a weak word: `clocks` (0 of 15 recognized against 15), `strawberries` (3 against 15), `small` (5 against
   15), `recommends` (4 against 13), `grilled` (4 against 12), `a` (54 of 75 against 74). Before the first-word rule the
   figures were 7.15 and 6.75 percent errors and 88.9 and 98.1 percent for the first word of a clause.

   **Decision: implemented for Piper.** The owner's three complaints are all in the class (b) fixes, the effect on the
   clause-initial word is large and significant in every speaker, and the effect on the total error rate is neutral to
   slightly positive, with a small possible cost for the first word of long sentences. The seam needed no change of the
   core: the model's `CapabilityProfile` (a public member of `ISynthesisModel`) wraps the library's default profile and
   joins the clause pieces (`PiperCapabilityProfile`, see *SpeechOnnxPiper Design*), within the core chunker's own
   200-character budget and with the engine's 256-symbol limit still cutting what is longer. The change is one line (the
   profile property) to revert. Kokoro's chunking is unchanged; it was not part of this experiment.

Evidence is in the experiment sets kept outside the repository; none of the recognizer output or audio is committed. The
Phonemes.En goldens changed in 59 lines of the Piper golden (the first word of the sentence, in every case); the Piper
pipeline golden changed in 23 rows (the same first-word marks, and the sentences with commas now one utterance each).
The Kokoro goldens did not change.

### Listening Fixes 3

The third round of owner listening (the Gettysburg Address, Piper LibriTTS-R, speaker 0) flagged four sentences:
*battle-field* heard as *battle ... field*, *for those who here gave* heard as *who he gave*, *so nobly advanced* heard
as *so knobbly*, and *the last full measure of ... devotion* with a pause. The causes were general, and each was found
in the code and not in the voice:

1. **A compound with two primary stresses.** Every part of a hyphenated compound was given its own primary stress
   (`bˈæɾəlfˈiːld`; espeak-ng writes the same two marks), and the voice reads two primary stresses in one written word
   as two stressed words with a break between them. A compound now has one primary stress, on its first part, and a
   secondary stress on each later part where it had a primary one (`bˈæɾəlfˌiːld`, `wˈɛlnˌoʊn`, `sˈɛlfˌɛvədənt`,
   `twˈɛntiwˌʌn`, `fˈʊltˌaɪm`), also when the later part stands right next to the primary stress (the stress-clash rule
   of a single word would drop it). The dictionary's own stress pattern is taken when it lists the closed spelling with
   the same sounds (`battlefield` is `B AE1 T AH0 L F IY2 L D`) and keeps the primary on the vowel the parts give it and
   a stress on every later stressed part (`email` is not followed: it has no stress on the first part, and `e-mail` is
   spoken `ee-mail`). Numbers are not compounds of this kind: the text analysis writes them as separate words
   (`twenty one`), each with its own stress, as espeak-ng speaks the digits (`twˈɛnti wˈʌn`). Kokoro already demoted the
   later parts and did not change. Cost against espeak-ng, which writes two primary stresses: the primary stress
   agreement of the 450 tuning sentences fell from 95.90 to 95.39 percent (the held-out sentences did not move), by
   design.
2. **`ear`, `here` and every other NEAR word.** The dictionary writes the vowel of *here* as `IY1 R`, and the renderer
   wrote the long `iː` before the `r` (`hˈiːɹ`); espeak-ng writes the lax `ɪ` (`hˈɪɹ`), and the voice heard `hˈiːɹ` as
   *he*. The whole dictionary (124,926 words) was compared with espeak-ng en-us class by class (the vowel before `r`, by
   stress and by what follows the `r`) and the systematic differences were fixed: `IY` before `r` is `ɪ` (words ending
   in `IY1 R` agreed in 0 of 25 before and 56 percent now; before a consonant 0 of 53 and 62 percent; before a vowel 3
   and 30 percent); `AY` and `AW` before `r` are followed by a schwa that carries the `r` (`aɪɚ`: `inspire`, `retire`,
   `admire`; `aʊɚ`), with the `r` kept before a high front vowel (`aɪɚɹɪŋ`: `firing`) and a plain `ɹ` before any other
   vowel (`kˈaɪɹoʊ`: `cairo`, `cyrus`, `environment`): 0 of 42 agreed before, 71 percent now (50 words ending in `AY2 R`
   0 and 56 percent; before a vowel 31 and 42 percent); `UW` before `r` is the vowel of *cure* (`pˈʊɹ`, `pˈʊɹɹɚ`: 0 of
   24 and 29 percent; 9 and 28 percent before a vowel); `EY` before `r` and a consonant or the end of the word is the
   vowel of *square* (`ʃˈɛɹ`: 0 of 9 and 44 percent). The other conventions agree already: SQUARE `ɛɹ`, CURE `ʊɹ`, NORTH
   and FORCE `ɔːɹ`, START `ɑːɹ`, NURSE `ɜː` and lettER `ɚ` (the remaining differences in those classes are the engine's
   spelling-driven choices, e.g. `ɔɹ` of *orange* against `ɔːɹ` of *horrible*, and the proper names of the dictionary).
   Over the whole dictionary the stress-free word match rose from 54.46 to 54.70 percent (68,035 to 68,331 words; most
   of the rest are names the engine guesses from the spelling).
3. **`nobly`.** The word was pronounced exactly as the dictionary lists it: CMUdict (the pinned file) has
   `nobly N AA1 B L IY0`, the vowel of *knobbly*, not that of *noble* (`N OW1 B AH0 L`). It is a transcription error of
   the dictionary, not a precedence problem. Nothing was overriding a direct entry: every one of the 124,926 words of
   the dictionary, phonemized alone, was compared with its dictionary entry (the first variant, the velar comparative
   first for the adjectives in `ng`), and 15 words were wrong before the fix, all of them tokenizer artifacts: the
   dictionary's 41 dropped-g headwords (the ones that end in an apostrophe after `n`) lost their trailing apostrophe to
   the quote rule, so each was looked up as the headword without it (a different word, or none) or guessed by the
   letter-to-sound model, and 15 of them came out different from the dictionary's entry; a trailing apostrophe that
   makes a headword of the lexicon is now part of the word. Zero are wrong now except
   the two reviewed corrections (`nobly` and `novelly`, found by comparing the 137 words that are a stem plus `-ly`,
   `-ness`, `-ful`, `-less`, `-ment` or `-ish` with the entry of their stem), which live in
   `data/phonemes-en/lexicon-corrections.tsv` and are consulted before the dictionary; the phonemizer's test over every
   word of the dictionary keeps it that way. The dictionary also lacks the `n` of `government` (`G AH1 V ER0 M AH0 N T`
   against espeak-ng's `ɡˈʌvɚnmənt`); it was not flagged by listening and is not changed here.
4. **A pause inside `the last full measure of devotion`.** The cause is not Piper's symbol limit but the core chunker's
   whitespace fallback: a clause longer than 200 characters that has no comma (the clause before the first comma of the
   sentence is 301 characters long and holds three dashes) was cut at the last space that fits,
   `...the last full measure of | devotion --`. The chunker now cuts such a clause where a speaker pauses (after a dash
   or before an opening bracket, when the piece holds a quarter of the budget), else before a conjunction or relative
   word (two fifths), else after a word that is not a function word (half), and never between a function word and the
   word after it when another boundary fits; the engine's own splitter (`RenderParts`) uses the same order with the
   words of the weak-form table. The recognizer proxy (the repository's Nemotron recognizer, a proxy and not listening)
   punctuated *measure of. devotion* in 5 of 8 renderings of the final sentence (four speakers, two draws each) before
   the fix and in none after it.

**The cap.** The Piper utterance budget was 200 characters (the core chunker's budget, up to which the profile joined
clause pieces) and 256 phoneme symbols (the engine's limit). The same eight renderings of the final sentence (656 words
per configuration) were recognized with these results:

| Configuration | Word errors | Rate | Silences of 0.3 s or more | Break at *of devotion* |
| --- | --- | --- | --- | --- |
| Before: 200 characters, 256 symbols, whitespace cut | 44 | 6.71 percent | 9 | 5 of 8 |
| Phrase-boundary cut, 200 and 256 | 45 | 6.86 percent | 3 | 0 of 8 |
| Phrase-boundary cut, 300 and 384 (core budget too) | 31 | 4.73 percent | 5 | 0 of 8 |
| Phrase-boundary cut, 450 and 600 (core budget too) | 24 | 3.66 percent | 16 | 0 of 8 |
| **Implemented**: pieces of 200, joined to 300, 384 symbols | 32 | 4.88 percent | 4 | 0 of 8 |

The voice is not hurt by longer utterances; it makes fewer word errors the longer they are (the pieces of the 200-character
budget end at dashes, so they are short: 81, 126 and 92 characters, and the voice does worse with short utterances, see
*Listening Fixes 2*). The 450-character setting is the most accurate but speaks the whole sentence as one utterance with
16 silences of 0.3 s or more (against 3 to 5), and the evidence is one sentence and a proxy, so the budget was raised to
300 characters and 384 symbols (a 300-character segment is about 390 symbols, so the splitter rarely has to cut it), not
further. The profile now also joins the pieces the core chunker cut at a dash or inside a long clause (they end in a
dash or a letter), so the final sentence is two utterances of 208 and 239 characters. The core chunker's own budget
(200) is unchanged.

**Measured effect on the gates** (Piper against espeak-ng en-us; the numbers of "Results" are the new ones):

| Corpus | Word match | PER | Primary stress |
| --- | --- | --- | --- |
| 450 tuning sentences | 86.12 to 86.07 | 3.01 to 2.93 | 95.90 to 95.39 |
| 60 held-out sentences | 89.31 to 89.47 | 2.47 to 2.40 | 98.10 to 98.10 |
| 3,000 words | 78.70 to 78.93 | 4.47 to 4.40 | 95.39 to 95.39 |
| 3,000 held-out words | 75.37 to 75.53 | 4.38 to 4.35 | 96.76 to 96.76 |

Kokoro's figures did not change. The Phonemes.En goldens changed in 10 lines of the sentence golden (the secondary
stress of the later part of a compound) and 14 of the Piper golden (compounds, `here`, `euros`, `tireless`); one row of
the Piper pipeline golden changed (`12-year-old`); the Kokoro goldens did not. The Gettysburg golden pins five
sentences, among them the four of this round.

**Other anomalies in the flagged phoneme strings** (compared with espeak-ng, word by word): none that the listening
could have heard. Differences are the unstressed `ɪ` of *dedicate* where espeak-ng writes the lax `ᵻ`, the missing `n`
of *government* (dictionary), the merged *to be* and *not have* of the engine's running speech, and stress marks on
function words at the start of an utterance (the first-word rule of *Listening Fixes 2*).

## Residual Classes

The largest disagreements that remain are idiosyncrasies of the two engines, not renderer faults, and no phone-only rule
reaches them (shares are of all character edits of the pass):

- **Unstressed lax vowel chosen by spelling (Piper, 20 to 30 percent of the edits).** The engine writes `ɪ`, `ᵻ` or `ə`
  for an unstressed dictionary `AH0`/`IH0` according to the vowel letter (e, i against a, o, u), which a phone sequence
  does not carry: `ɪ` where the table has `ə` (9 to 10 percent), `ᵻ` where it has `ɪ` (6 percent), `ə` where it has `ɪ`
  (4 to 5 percent), and the reverse pairs. The environments that are determined by phones are already rules.
- **`-ate` endings (Piper, 3 to 4 percent):** the engine realizes a verb-like `-ate` as `eɪ` with a secondary stress;
  the dictionary lists the noun or adjective form (`AH0 T`) first.
- **Lexical vowel sets (Piper, 2 to 4 percent):** `ɑ` against `ɔ` (cost, want), a stressed against a reduced
  `con-`/`com-` prefix, `ɜː` against `ɚ` before a consonant in `per-`/`sur-` prefixes.
- **Hiatus vowels (Piper, 2 to 4 percent):** `i` against `ɪ` before a vowel (`-ia`, `-io`), and the `iː`/`ɪ` of
  `re-`/`de-`.
- **Secondary stress placement (both):** the engines assign a secondary stress on rhythm and word-class grounds that the
  dictionary's stress digits do not give (letter-name sequences, compounds, closed final syllables). It is the main part
  of the stress disagreement of the word pass.
- **Joined word pairs (Piper, sentences):** the engine joins some short word pairs (`there are`, `did not`) into one
  unit beyond the groups the renderer knows.
- **Final `-y` details (both):** the weekday names end in `eɪ` in espeak-ng and misaki (`tˈuːzdeɪ`; the dictionary lists
  that variant second, so the renderers say `tˈuːzdi`), a final `-ty` after `l` is flapped in some words (`loyalty`,
  `penalty`) and not in others, and espeak-ng says `ɔ` for 26 of the 187 dictionary words with `AA` before `f` (among
  them `coffee` and `cough`) where the dictionary vowel gives `ɑː`.
- **Syllabic mark `ᵊ` (Kokoro, 17 to 23 percent of the edits of the word pass):** the reference dictionary writes `ᵊ`
  after some consonants and `ə` after others with no phone-determined pattern; the rule covers roughly three quarters of
  the syllabic `l`/`n` cases, the rest swap in both directions.
- **Function words with full forms (Kokoro, sentences):** the reference weakens articles, prepositions and auxiliaries;
  this is the whole gap between the full-form and weak-form rows above.
- **Vowel sets of `ɑ`/`ɔ` and `ɐ`/`ə`/`æ` in a few unstressed positions (Kokoro, 6 to 12 percent).**

These classes are ratcheted: the floor is the table above; a later change must not make any gated figure worse.

## Coverage Evidence

`RenderingSubsystemTests` (all target frameworks) renders every dictionary pronunciation (133,971), 328,509 three-phone
sequences, about 18,000 conversions of the out-of-vocabulary model and every weak form, and asserts that no character is
outside the Piper `phoneme_id_map` or Kokoro's vocabulary; the golden corpora `piper-ipa-golden.tsv` and
`kokoro-misaki-golden.tsv` pin the renderings of the 150 reviewed sentences with the model token id of every character.
These golden files are regression baselines regenerated by `regenerate-goldens --renderers` and reviewed line by line,
not oracles.

## Reproducing the Measurement

1. `dotnet run --project tools/DemaConsulting.Speech.Phonemes.En.Tools -- measure-oracle` writes `ours.jsonl` (the
   tuning corpora; with `--word-offset 3000` the held-out passes to `ours-validation.jsonl`) to a folder in the
   temporary directory, outside the repository. It refuses any output folder inside the repository other than the
   git-ignored `work` folder and never runs an engine.
2. A comparison script kept outside the repository runs the reference engines on the same spoken text, aligns the words,
   computes the metrics above and writes the aggregates and the class lists next to the export. It needs a local Python
   environment with espeak-ng and misaki installed outside the repository; neither is a dependency of any project.
3. Only the aggregates (`renderers.tsv`) and the phonetic class descriptions of this document are committed.

## Licenses and Provenance

The Piper `phoneme_id_map` used by the tests is a trimmed copy of the symbol table of the `en_US` `libritts_r` medium
voice (CC BY 4.0, trained on LibriTTS-R, OpenSLR resource 141, voice from the Piper project); it is attributed in the
file. Kokoro's vocabulary is the package's own `tokenizer.json`. No espeak-ng or misaki data, code or output is in the
repository; the own-written sentences are released under the repository's license (CC0 for the data).
