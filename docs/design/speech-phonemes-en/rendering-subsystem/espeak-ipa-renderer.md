### EspeakIpaRenderer

**Purpose**: Render a `PhonemizedText` as the espeak-style IPA phoneme string that the Piper voice reads: the public
renderer of the package for that voice.

**Data Model**:

- `EspeakIpaRenderer(profile, useWeakForms)`: immutable; `useWeakForms` (default true) says whether the function words
  are rendered by the weak-form table. `EspeakIpaProfile` is a public enumeration with the one value `EnUs`
- The ARPAbet to IPA table (consonants, and each vowel with its unstressed, primary and secondary form), the stress
  marks `ˈ` and `ˌ`, and the word-sound rules (`EspeakWordRules`, which use the helpers of `WordShape` in the
  PhonemeStream unit: the position of the tonic vowel, the vowel count, flap and inflection-ending tests)
- Marks: `, ; : . ! ?` pass through; `— – - ( ) [ ] { } …` become a comma (a pause); other punctuation is dropped; a run
  of one mark is at most three symbols (an ellipsis is `...`)

**Key Methods**:

- **Render(text)**: the phoneme string of a whole text: each word item is rendered (weak form first when enabled and the
  word is in the table, then the sound rules for the phones; the first word of the text, which is the first word of an
  utterance of the voice, gets its full-vowel weak form with a secondary stress, see *WeakForms*), a punctuation item
  as its mark, an unsupported item as nothing and a host IPA override translated into Piper symbols (`ɝ` and the
  ligatures `ʧ ʤ` become their espeak form) or, when a symbol is not in the Piper table, replaced by the word's normal
  pronunciation; the pieces are joined by the shared layout with the weak-form groups written without a space
- **RenderParts(text, maxSymbols)**: the parts of a long text (see *PhonemeStream*); each part is rendered exactly as
  `Render` would render that stretch, so the first word of each part is the first word of an utterance too
- **Render(items)** and **RenderItems(items)** (internal): the same for a slice of items and one string per item, which
  the coverage tests and the splitter use; **RenderPronunciation(pronunciation)** (internal) renders one pronunciation

**Rules.** The mapping and rules are listed in *RenderingSubsystem Design*. They are phone-class statements (a flap is a
`t` between a vowel or `r` and an unstressed vowel, the linking `ɹ` follows a final `ɚ` or `ɜː` before a vowel-initial
word) and not lists of words, and their choices were decided by measuring against the training engine of the voice (see
the evaluation record) and then frozen into the golden corpus. The vowels before `r` follow that engine: `IY` before `r`
is the lax `ɪ` (`hˈɪɹ`, never `hˈiːɹ`, which the voice hears as *he*), `AY` and `AW` before `r` are followed by a schwa
that carries the `r` (`aɪɚ`, `aʊɚ`, and `aɪɚɹɪŋ` before a high front vowel), `UW` before `r` is the vowel of *cure*
(`pʊɹ`) and `EY` before `r` and a consonant or the end is the vowel of *square*. The tense `i` of an unstressed `IY` is
the voice's word-final vowel (*happy*): inside a word, after a consonant and before a consonant and the primary stress
(the end of a prefix or of the first element of a compound), the voice lengthens it and pauses after it as if the word
ended there, so it is written lax, as the training engine writes it (`sˌɛmɪsˈɜːkəl`, `mˌʌltɪkˈʌlɚd`, `ˌæntɪwˈɔːɹ`;
`ᵻ` after `r`: `ɹᵻbˈɪld`); after the primary stress, at the start and at the end of a word it stays `i`
(`hˈæpinəs`, `ˈɛvɹibˌɑːdi`). A compound element boundary is never written as a word boundary: the elements of a word
are joined without a space or a mark, and the `n` before a velar stop is the velar nasal `ŋ` only inside one element
(`sˈʌŋkən`), never across the joint (`vine-clad` is `vˈaɪnklˌæd`, not *vying-clad*; espeak-ng assimilates there too,
but the first element then no longer sounds like its word). A word of the weak-form table that the text analysis tags as
a noun is rendered from its phones with its stress (see *WeakForms*, `IsContentUse`), and the forms of a grouped pair
are those of the group (`InGroup`: `of a` is `əvə`). The stress of a compound written with a
hyphen is read from the pronunciation's element starts: a secondary stress of a compound is always written, even next to
the primary stress (`wˈɛlnˌoʊn`, `ˌɛkswˈaɪf`), where the stress-clash rule of a single word would drop it, and the
vowel-before-`r` rules look only at the phones of the element the vowel is in (`free-range` is `fɹˈiːɹˌeɪndʒ`,
`tea-room` is `tˈiːɹˌuːm`, not r-colored across the joint).

**Error Handling**: a null text throws `ArgumentNullException`; `RenderParts` with a limit below 16 throws
`ArgumentOutOfRangeException`; an undefined profile throws `ArgumentOutOfRangeException` at construction. Rendering
never throws for any content.

**Dependencies**: `WeakForms`, `PhonemeStream`, `Phone`/`Pronunciation` and the result types of the PhonemizerSubsystem.

**Callers**: the Piper model package (through its phoneme pipeline), the tools and the tests.
