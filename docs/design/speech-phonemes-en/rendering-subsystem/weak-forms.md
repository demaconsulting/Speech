### WeakForms

**Purpose**: Say how the small English function words are spoken when they do not carry the sentence stress, so that the
Piper voice, which was trained on text where they are unstressed, does not read a sentence as a list of stressed words.

**Data Model**: three classes of words, authored from the standard description of English weak forms and fixed by the
measurement against the voice's training engine (the table records those classes, not engine output):

- *Forms*: about sixty words with an explicit form before a consonant and one before a vowel (the same for most). A few
  have a reduced vowel (`the` `ðə`/`ðɪ`, `a` `ɐ`, `to` `tə`/`tʊ`, `into`); the rest keep the full vowel without a stress
  mark: conjunctions, prepositions, auxiliaries and pronouns (`and` `ænd`, `of` `ʌv`, `in`, `with`, `can`, `was`, `he`,
  `they`, `it` ...)
- *Demoted*: words that keep their dictionary vowels with a secondary stress instead of the primary (`but`, `not`, `so`,
  `how`, `up`, `us`, `him`, `me`, `our`, `over`, `under`, `down`, `been`, `about`, `who`, `why`, `where`, `while`,
  `near`, `them` ...)
- *Espeak-only details*: more demoted words (`made`, `make`, `put`, `which`, `what`, `between`, `behind`, `beside`,
  `beneath`, `any`, `already`), the flapped `ɪɾ` and `æɾ` of `it` and `at` before a vowel, `ɔn` for `on` before `the`
  and `fɚ` for `for` before `the`

The table holds only words whose unstressed use is by far the most common; it does not know the sense of a word, but the
part of speech the text analysis gave the word tells a content use of the same spelling apart (see `IsContentUse`).

**Key Methods**:

- **TryGet(word, nextStartsWithVowel, nextWord, previousWord, espeakStyle, out form, out demoted)**: finds a word (any
  case); the form is chosen by the sound that follows, the espeak-style details apply only when `espeakStyle` is true,
  and a word that is only demoted has an empty form and `demoted` true; a word that is not in the table returns `false`.
  A word with an explicit form returns `false` as well when `previousWord` is a voicing-alternating word (see below), so
  the caller uses its dictionary form
- **VoicingAlternatingWords**: the spellings whose pronunciations differ only by the voicing of the final consonant
  (`close` `kloʊz`/`kloʊs`, `use`, `house`, `excuse`, `abuse`, `diffuse` ...), derived at first use, not listed by hand:
  the reading table's lemmas whose readings differ so, and the dictionary spellings with two variants that differ so (a
  fricative `S`/`Z`, `F`/`V` or `TH`/`DH` after the last vowel, and a `T`/`D` right after such a pair), without the
  words of this table. A reduced function word right after one of them keeps its full form (`close to`, `a house to`),
  because the reduced vowel would blur the `s` or `z` that tells the readings apart
- **GroupedWithPrevious(items)**: marks the word items that form one group with the word before them (`in the`,
  `of the`, `on the`, `with the`, `from the`, `for the`, `that the`, `was the`, `was a`, `of a`), written without a
  space, except where the first word of the group keeps its full form after a voicing-alternating word
- **IsContentUse(item)**: whether a word of the table is used here as the content word of the same spelling, which is
  never weak: tagged `NOUN` or `PROPN` (`my will`, `the can`, `a must`, `Will` as a name), or `VERB` for the modal
  spellings that are also main verbs (`will`, `can`). Any other tag, and no tag (text that was not analyzed), keeps the
  table: `VERB` is not enough for the other words because the tagger gives the auxiliaries `be`, `have` and `do` that
  tag too. Both renderers skip the table for such a word and render its dictionary form with its stress (`maɪ wˈɪl`, not
  the auxiliary's `maɪ wɪl`)
- **InGroup(first, firstForm, second, secondForm)**: the forms of a group as one unit: `of a` is `əv` and `ə` (`əvə`, as
  the training engine writes it) when both have their plain weak forms (`ʌv`, `ɐ`); any other group keeps its words'
  forms (`was a` stays `wʌzɐ`, as the engine writes it). The word-initial `ɐ` joined after a full vowel reads as the
  start of a new word: measured with the noise off, *the wall of a gallery* is 127 ms longer with `ʌvɐ` than with `əvə`,
  and over five phrases `ʌvɐ` was the longest of five spellings every time, while `wʌzɐ` was not lengthened (see the
  evaluation record). The espeak-style renderer applies it to every grouped pair
- **AtTextStart(word, form)**: the form of a word that is the first word of a text, which is the first word of an
  utterance of the voice: an unmarked weak form with a full vowel gets a secondary stress mark before its first full
  vowel (`hiː` becomes `hˌiː`, `ʃʊd` becomes `ʃˌʊd`, `aɪ` becomes `ˌaɪ`), because at the start of an utterance the voice
  swallows an unmarked weak form (a recognizer hears `should`, `could` and `my` in none of 24 renderings each and in
  24, 19 and 16 with the mark); a reduced form (`ðə`, `ɐ`, `ɐn`, `tə`), a form that has a mark already (`ˌɔn`,
  `bɪkˈʌz`), `the` and `for` (whose vowel any stress mark makes the voice say as `war` or `or`) are returned unchanged.
  The renderer calls it for the first word of the rendered text only, never for a word after a comma inside the text
- **Words**, **AllForms**, **Count**: the contents of the table, so a test can prove every form is in the voice's symbol
  table

**Error Handling**: nothing can fail; an unknown word is simply not in the table.

**Dependencies**: the result types of the PhonemizerSubsystem (for the grouping), and `CmuLexicon` and
`ReadingPronunciations` (for the voicing-alternating words).

**Callers**: `EspeakIpaRenderer` (by default, with `AtTextStart` for the first word of a text) and `MisakiRenderer`
(when weak forms are switched on, without the espeak-style details and without `AtTextStart`: the Kokoro model does not
swallow the first word, measured by the same recognizer proxy).
