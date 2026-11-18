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
  **word match 89.4 percent (gate 85), phoneme error rate 3.0 percent excluding stress (gate 8), primary stress
  agreement 96.0 percent (gate 90)**. Kokoro against misaki US: **phoneme error rate 4.9 percent with full forms (gate
  6)**, 2.0 percent with the opt-in weak forms. On text nobody tuned the rules on (60 fresh sentences, 3,000 further
  frequent words) the figures hold: Piper 91.8 percent word match, 2.5 percent PER, 98.1 percent primary stress
  agreement on the sentences, and 4.4 percent PER on isolated words; Kokoro 4.5 percent PER on the sentences and 2.8
  percent on the words. The one figure below its provisional gate is the **word match of isolated words** (78.6 percent
  tuned, 75.3 percent held out): it is not the gate's corpus (sentences) and its residual is the engine's
  spelling-driven vowel choice (see "Residual classes").
- **The gates are ratcheted, never loosened:** the figures above are the new floor (the first authored mapping scored
  61.6 percent word match and 9.7 percent PER for Piper, 6.6 percent PER for Kokoro; see "What the measurement
  changed"). The refinement pass after the first end-to-end audio (hyphenated compounds, a few function-word details)
  raised Piper from 88.1 to 89.4 percent word match on the tuned sentences (90.1 to 91.8 held out), lowered its PER from
  3.14 to 3.01 (2.58 to 2.47) and raised its primary stress agreement from 94.3 to 96.0 (97.1 to 98.1); Kokoro's
  held-out figures did not move, and its tuned-sentence PER moved by +0.01 point (4.88 to 4.89 with full forms, 1.99 to
  2.00 with weak forms) because three sentences of the corpus now reach the misaki engine as different spoken text (a
  URL, `10K`, `A1`).

## What Is Rendered

Both renderers work on the ARPAbet phones of each item; a word with an IPA override passes through unchanged, an
unsupported token renders to nothing, and a punctuation item is rendered as the model's own symbol or dropped.

**Piper (`EspeakIpaRenderer`).** The basic table is the textbook correspondence between ARPAbet and General American IPA
(`tʃ dʒ θ ð ʃ ʒ ŋ ɹ`, `oʊ aɪ aʊ eɪ ɔɪ`, `ɑː ɔː ɜː iː uː`, `ɛ ɪ ʊ ʌ æ`, `ɚ` for unstressed `ER`). The rules on top, each
a statement about phone classes and positions:

- The stress mark (`ˈ`, `ˌ`) stands directly before the vowel.
- A secondary stress next to the primary one is not realized (stress clash); a secondary stress on a final `-y` is not
  realized; the numbers thirteen to nineteen are stressed on the first syllable.
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
| Piper, 450 sentences | 4,279 | 89.44 | 91.14 | 3.01 | 95.98 | 96.80 | 76.29 |
| Piper, 60 held-out sentences | 636 | 91.82 | 92.61 | 2.47 | 98.10 | 98.11 | 75.56 |
| Piper, 3,000 words | 3,000 | 78.63 | 81.20 | 4.49 | 95.39 | 93.80 | 62.56 |
| Piper, 3,000 held-out words | 3,000 | 75.27 | 77.40 | 4.39 | 96.73 | 93.97 | 68.28 |
| Kokoro full, 450 sentences | 4,357 | 62.70 | 82.17 | 4.89 | 71.45 | 75.10 | 23.54 |
| Kokoro full, 60 held-out sentences | 659 | 64.80 | 83.16 | 4.50 | 72.17 | 76.33 | 21.62 |
| Kokoro full, 3,000 words | 2,990 | 81.20 | 85.95 | 2.93 | 94.81 | 91.27 | 48.47 |
| Kokoro full, 3,000 held-out words | 2,955 | 80.61 | 83.76 | 2.81 | 97.39 | 93.81 | 69.02 |
| Kokoro weak, 450 sentences | 4,357 | 85.72 | 93.09 | 2.00 | 94.25 | 91.07 | 41.73 |
| Kokoro weak, 60 held-out sentences | 659 | 88.92 | 94.84 | 1.43 | 96.59 | 93.17 | 45.78 |

Columns: words compared, word match (stress marks included), word match ignoring stress, phoneme error rate (PER)
excluding stress, primary stress agreement, stress signature agreement, secondary stress agreement.

**Gates.** Piper word match 89.4 (gate 85), PER 3.0 (gate 8), primary stress 96.0 (gate 90): met on the tuned corpus and
on the held-out sentences. Kokoro PER 4.9 with full forms and 2.0 with weak forms (gate 6): met. Not met: Piper word
match on isolated words (78.6 and 75.3 against 85); that metric is not the gate's corpus and is dominated by the class
"unstressed short vowel chosen by spelling" below. The stress agreement of Kokoro with full forms (71 percent) is low
only because of function words: with the weak forms it is 94 percent.

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
