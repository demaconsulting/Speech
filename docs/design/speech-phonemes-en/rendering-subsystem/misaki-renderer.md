### MisakiRenderer

**Purpose**: Render a `PhonemizedText` as the misaki symbol string that Kokoro reads: the public renderer of the package
for that voice.

**Data Model**:

- `MisakiRenderer(profile, useWeakForms)`: immutable; `useWeakForms` (default false) says whether the function words are
  rendered by the weak-form table. `MisakiProfile` is a public enumeration with the one value `Us`
- The ARPAbet to misaki table (the single-letter diphthongs `A I O W Y`, `ʧ ʤ`, `ɜɹ`, `əɹ`, `ᵻ`, `ᵊ`) and the word-sound
  rules (`MisakiWordRules`, which use the helpers of `WordShape` in the PhonemeStream unit)
- Marks: Kokoro keeps its own `“ ” ( ) …`, a dash is written between spaces, opening marks attach to the next word and
  closing marks to the previous one

**Key Methods**:

- **Render(text)**: the phoneme string of a whole text, as for `EspeakIpaRenderer`, with the misaki rules; the later
  primary stresses of a hyphenated compound are demoted to secondary (only the first part keeps its stress) A host IPA
  override is translated into Kokoro's symbols (`aɪ aʊ eɪ oʊ ɔɪ tʃ dʒ` to `I W A O Y ʧ ʤ`, `ɝ ɜː` to `ɜɹ`, `ɚ` to `əɹ`,
  the length mark dropped) and checked against the vocabulary; an override with a symbol the vocabulary does not have is
  replaced by the word's normal pronunciation
- **RenderParts(text, maxSymbols)**: the parts of a long text for Kokoro's 510-token limit (see *PhonemeStream*)
- **RenderItems(items)** and **RenderPronunciation(pronunciation)** (internal): the item-level and pronunciation-level
  renderings used by the splitter and the coverage tests

**Rules.** The sound rules are listed in *RenderingSubsystem Design*: the flap, `ᵻ`, `ᵊ` for a final syllabic `l`, `m`
or `n`, an unstressed `IH0` as a schwa except in `-ing`, `-ic`, `-ive`, `-age` and `-ish`, an unstressed prefix `IY0` as
a schwa, no secondary stress right before the primary and none on a final `-y`. The agreement with misaki is measured
locally (the phone error rate is a few percent on the tuning sentences and lower with weak forms) and recorded as
aggregate numbers only.

**Error Handling**: as for `EspeakIpaRenderer`: `ArgumentNullException` for a null text, `ArgumentOutOfRangeException`
for a limit below 16 and for an undefined profile.

**Dependencies**: `WeakForms`, `PhonemeStream`, `Phone`/`Pronunciation` and the result types of the PhonemizerSubsystem.

**Callers**: the Kokoro model package (through its phoneme pipeline), the tools and the tests.
