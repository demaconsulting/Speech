## RenderingSubsystem Design

![RenderingSubsystem Structure](PhonemesEnRenderingSubsystemView.svg)

### Overview

The SpeechPhonemesEn RenderingSubsystem turns a `PhonemizedText` into the phoneme string that a voice model reads. The
two supported voices were trained on text from different phonemizers, so the same phones are written differently: the
Piper voice reads the espeak-style IPA of the espeak-ng `en-us` voice, and Kokoro reads the symbol set of the misaki
phonemizer. The subsystem knows the sound conventions of each and nothing of text, annotations or inference. It contains
the following units:

- **EspeakIpaRenderer**: the public renderer for the Piper voice, with its profile enumeration `EspeakIpaProfile`, the
  ARPAbet to IPA table and the word-sound rules (`EspeakWordRules`)
- **MisakiRenderer**: the public renderer for Kokoro, with `MisakiProfile`, the ARPAbet to misaki table and its
  word-sound rules (`MisakiWordRules`)
- **WeakForms**: the table of the small function words that are unstressed in running speech, with their reduced forms
- **PhonemeStream**: the layout both renderers share, the splitter that cuts a long text into parts of a bounded size,
  the word-shape helpers (`WordShape`) that the sound rules of both renderers use, and the translation and validation of
  host IPA overrides against the two voices' symbol tables (`IpaSymbols`)

### Interfaces

The subsystem exposes four public types in the `DemaConsulting.Speech.Phonemes.En.RenderingSubsystem` namespace:
`EspeakIpaRenderer(EspeakIpaProfile profile = EnUs, bool useWeakForms = true)` and
`MisakiRenderer(MisakiProfile profile = Us, bool useWeakForms = false)`, each with `Render(PhonemizedText)` and
`RenderParts(PhonemizedText, maxSymbols)`, and the two profile enumerations, which each have the one value `EnUs` or
`Us` and exist as a seam for a later British profile. The renderers are immutable and thread-safe. Everything else is
internal; the item-level rendering (`RenderItems`), the per-pronunciation rendering used by the coverage tests and the
tables are visible only to the tools and tests through `InternalsVisibleTo`.

The subsystem consumes the result types of the PhonemizerSubsystem and the phone types of the LexiconSubsystem.

### Design

**Symbol sets.** The Piper voice's `phoneme_id_map` has 159 symbols and Kokoro's vocabulary 115; a symbol outside them
is dropped or fails at inference. Both tables are authored from phonetics and from the symbol inventories, and the
coverage tests prove that no pronunciation of the dictionary, no phone in any neighborhood of two phones, no conversion
of the letter-to-sound model and no weak form renders to a symbol outside the voice's table. The two inventories used by
the tests are the real ones: a trimmed copy of the voice's configuration (CC BY 4.0, attributed in the file) and
Kokoro's own tokenizer file.

**Piper (espeak-style) rules.** The textbook ARPAbet to IPA mapping, with the stress mark before the vowel it belongs
to, one symbol per sound (`tʃ`, `oʊ`, `ɜː`), the flap `ɾ` for a `t` between a vowel or `r` and an unstressed vowel, `ŋ`
before `k` or `g`, a linking `ɹ` after a word-final `ɚ` or `ɜː` before a vowel-initial word, the unstressed `AH0`, `IH0`
and `IY0` by environment (`ᵻ` for `-ed` after `t` or `d` and `-es` after a sibilant, `ɐ` word-initially, `ɛ` for `ex-`
and `en-`, `ɪ` for `-ing`, `-ic`, `-age`), `ɔ` before `ŋ f θ s`, the secondary stress dropped next to the primary and on
a final `-y`, and the numbers thirteen to nineteen (and only those) stressed on the first syllable. Quotation marks,
brackets and dashes become a comma (a pause) because the voice has no symbol for them, an ellipsis stays three dots, and
the clause marks `, ; : . ! ?` pass through. Every part of a hyphenated compound keeps its stress.

**Kokoro (misaki) rules.** The same textbook table in the misaki symbols (`A I O W Y ʧ ʤ ɜɹ əɹ`), the flap, `ᵻ`, `ᵊ` for
a final syllabic `l` after a consonant or `m` and `n` after `t d s z p f`, an unstressed `IH0` as a schwa except in
`-ing`, `-ic`, `-ive`, `-age` and `-ish`, an unstressed prefix `IY0` as a schwa, no secondary stress right before the
primary and none on a final `-y`. Kokoro keeps its own quotation marks, brackets and ellipsis (`“ ” ( ) …`), writes a
dash between spaces, and keeps the stress only on the first part of a hyphenated compound, demoting the later primary
stresses (the item's compound break says where the second part starts).

**Weak forms.** In running speech the small function words are unstressed and often reduced, and the Piper voice was
trained on text where they are. About sixty words have a weak form in one of three classes: a reduced vowel (`the` is
`ðə`, before a vowel `ðɪ`), the full vowel without the stress mark (`and` is `ænd`) or the full form with a secondary
stress (`but`, `not`). A function word and the article after it are one group written without a space (`of the` is
`ʌvðə`). The Piper renderer applies the table by default and a caller can switch it off; the espeak-style details (the
flap of `it` and `at` before a vowel, `on the` with an unmarked `on`, `for the` as `fɚðə`, the secondary stress of
`made`, `make`, `which`, `what` and a few more) apply to Piper only. The Kokoro renderer renders full forms by default
(the renderer's own default), and a caller can ask for the weak forms, which agree better with the reference phonemizer
for function words; the Kokoro model package asks for them (see *SpeechOnnxKokoro KokoroPhonemePipeline Design*, where
the pipeline's `UseWeakForms` constant is `true`, so the weak-forms-off default described here is the renderer's, not
the one Kokoro speech uses).

**Layout and parts.** Both renderers join the rendered pieces of the items with one space between words, a closing mark
attached to the word before and an opening mark attached to the word after (`PhonemeStream.Join`). `RenderParts` renders
each item on its own, splits the sequence between items so that every part fits `maxSymbols` Unicode scalar values (a
reserve of ten symbols is kept free for the edge effects of rendering a run, so a part never exceeds the limit), ends a
part after the last clause mark in its second half when there is one, otherwise after the last word that fits, and
renders every part exactly as `Render` would render that stretch. The minimum limit is 16.

**Measured fidelity.** The agreement with the espeak-ng `en-us` voice and with misaki is measured on a developer machine
only, by the tools command `measure-oracle`, and recorded as aggregate numbers in
`docs/evaluation/phonemes-en/renderers.md`; no engine output is committed. The committed regression baselines are the
two golden corpora, the renderings of the 150 reviewed sentences with the model token ids.

**Error handling.** A null text throws `ArgumentNullException`, a part limit below 16 `ArgumentOutOfRangeException`, and
a profile that is not defined `ArgumentOutOfRangeException` at construction. A word that the host pronounced with an IPA
override is translated into the symbols of the voice (`IpaSymbols`) and, when the voice's table lacks one of its
symbols, replaced by the word's normal pronunciation rather than approximated, and an unsupported token renders to
nothing without an extra space.
