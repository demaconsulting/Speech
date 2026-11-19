## PhonemizerSubsystem Verification

### Verification Approach

The SpeechPhonemesEn PhonemizerSubsystem is verified by deterministic tests in
`test/DemaConsulting.Speech.Phonemes.En.Tests/PhonemizerSubsystemTests.cs`. The subsystem-level tests
(`PhonemizerSubsystem_*`) drive the public `EnglishPhonemizer` with the real lexicon, reading tables and letter-to-sound
model and with synthetic annotations of the kinds the English text analysis defines (`en.norm`, `en.spell`,
`en.reading`, `en.pron`), built by small helpers, so that every behavior is exercised with a controlled annotation and
does not depend on what the classifier of SpeechTextEn happens to decide. The end-to-end behavior with the real analyzer
is the system test (see *SpeechPhonemesEn System Verification*). The units are verified by their own
`EnglishPhonemizer_*`, `WordTokenizer_*` and `WordPronouncer_*` tests in the same file, which drive the internal classes
directly (see the unit verification documents).

The pronunciations asserted are well-known dictionary entries or the documented sound rules of the morphology.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Phonemes.En.Tests`
- **Isolation**: no network, no files, no model; the phonemizer is immutable and shared by the tests of the class

### Acceptance Criteria

The subsystem tests pass when: a text without annotations gives every word its first dictionary variant, a bare digit
its digit word and nothing is normalized; a reading annotation selects the reading's pronunciation for the lemma, an
inflected form and a possessive (with a straight or a typographic apostrophe, also when the apostrophe is a
normalization replacement), an id that belongs to another word falls back visibly, and a host override beats the reading
and the dictionary in ARPAbet and in IPA and applies to the headword of a possessive or contraction; replacements are
spoken, spelled letters are letter names (an `A` and an `I` are letters), clause punctuation passes through and the
audio-tag placeholder produces nothing; contractions, possessives, hyphenated compounds and derived forms are one word
with the expected sounds; an unknown word is pronounced, flagged and listed once; an unsupported token is kept and
listed; a diacritic is folded for the lookup; and an annotation outside the text is ignored.

### Test Scenarios

#### Source priority and defaults

**Tests**: `PhonemizerSubsystem_Phonemize_NoAnnotations_UsesDefaultReadingsWithoutNormalization`,
`PhonemizerSubsystem_Phonemize_EveryDictionaryWord_IsPronouncedAsTheDictionaryListsItExceptTheCorrections`,
`PhonemizerSubsystem_Phonemize_PronunciationOverride_BeatsReadingAndLexicon`,
`PhonemizerSubsystem_Phonemize_HeadwordAnnotationsWithClitic_ApplyForBothApostrophes`

`record 5 cats` is spoken as written; each of the 124,926 words of the dictionary, phonemized alone, is its first
variant from the lexicon (the velar comparative first for the adjectives in `ng`), with only the two reviewed
corrections as exceptions; an ARPAbet override (with and without stress digits) and an IPA override win over a reading
on the same word; the override of `record` also applies to `record's` and `record'll` (and to `record’s` with the
analysis' normalization annotation of the apostrophe), the IPA one with the ending added in IPA, and an override of the
whole token wins.

#### Reading selection

**Tests**: `PhonemizerSubsystem_Phonemize_ReadingAnnotation_SelectsThatReading`,
`PhonemizerSubsystem_Phonemize_ReadingThatDoesNotApply_FallsBackVisibly`,
`PhonemizerSubsystem_Phonemize_HeadwordAnnotationsWithClitic_ApplyForBothApostrophes`

`record.noun` and `record.verb` give different stresses for the same spelling, `records` and `record's` follow the
reading with their ending, and `record.noun` on the word `cat` leaves `cat` with its default, the requested id recorded
and the applied flag false; `bass.fish` on the headword of `bass's` and of `bass’s` (the latter with the normalization
annotation of the apostrophe, as the text analysis emits it) gives the fish reading with the possessive ending.

#### Annotation-aware speech

**Tests**: `PhonemizerSubsystem_Phonemize_Replacements_AreSpoken`,
`PhonemizerSubsystem_Phonemize_SpelledLetterAOrI_IsTheLetterName`,
`PhonemizerSubsystem_Phonemize_AnnotationOutsideTheText_IsIgnored`

`Dr. Smith` with a replacement for `Dr.` is `Doctor Smith`; `FBI` spelled is three letter names and `FBI's` attaches the
possessive to the last letter; an empty replacement removes the span; a spelled `A I` is the names `EY1 AY1`; a
replacement running past the end of the text and a reading id of the wrong shape change nothing.

#### Tokens

**Tests**: `PhonemizerSubsystem_Phonemize_Punctuation_PassesThrough`,
`PhonemizerSubsystem_Phonemize_ContractionsPossessivesAndCompounds_AreOneWord`,
`PhonemizerSubsystem_Phonemize_HyphenatedCompounds_StressTheFirstPartAndReduceFunctionWords`,
`PhonemizerSubsystem_Phonemize_TrailingApostropheOfALexiconWord_StaysInTheWord`,
`PhonemizerSubsystem_Phonemize_BareDigits_AreSpokenOneByOne`,
`PhonemizerSubsystem_Phonemize_Diacritics_AreFoldedForLookup`

`Wait... what, then? <placeholder> Go!` gives eight items with the marks as punctuation;
`don't dog's dogs' well-known unfollowed 'tis` gives six words; the compounds `full-time`, `state-of-the-art`,
`day-to-day`, `wi-fi`, `battle-field`, `self-evident`, `twenty-one` and `e-mail` have the stresses of the design (one
primary stress, on the first part); the dictionary's dropped-g headwords (all that end in `in'`) are words of their own;
`42` is `F AO1 R` and `T UW1`; `café` sounds like `cafe`.

#### Reporting

**Tests**: `PhonemizerSubsystem_Phonemize_UnknownWord_IsFlaggedAndReported`,
`PhonemizerSubsystem_Phonemize_UnsupportedTokens_AreKeptAndReported`

`Zorblax met Zorblax` flags `Zorblax` but not `met` as out of vocabulary and lists the word once;
`Hi 😀 there 日本 and #` keeps six items in order and lists the three unsupported tokens, which have no phones and the
source `None`.
