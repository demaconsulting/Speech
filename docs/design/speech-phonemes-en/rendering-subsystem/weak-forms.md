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

The table holds only words whose unstressed use is by far the most common; it does not know the sense of a word.

**Key Methods**:

- **TryGet(word, nextStartsWithVowel, nextWord, espeakStyle, out form, out demoted)**: finds a word (any case); the form
  is chosen by the sound that follows, the espeak-style details apply only when `espeakStyle` is true, and a word that
  is only demoted has an empty form and `demoted` true; a word that is not in the table returns `false`
- **GroupedWithPrevious(items)**: marks the word items that form one group with the word before them (`in the`,
  `of the`, `on the`, `with the`, `from the`, `for the`, `that the`, `was the`, `was a`, `of a`), written without a
  space
- **Words**, **AllForms**, **Count**: the contents of the table, so a test can prove every form is in the voice's symbol
  table

**Error Handling**: nothing can fail; an unknown word is simply not in the table.

**Dependencies**: the result types of the PhonemizerSubsystem (for the grouping).

**Callers**: `EspeakIpaRenderer` (by default) and `MisakiRenderer` (when weak forms are switched on, without the
espeak-style details).
