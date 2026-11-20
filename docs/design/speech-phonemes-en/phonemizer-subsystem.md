## PhonemizerSubsystem Design

![PhonemizerSubsystem Structure](PhonemesEnPhonemizerSubsystemView.svg)

### Overview

The SpeechPhonemesEn PhonemizerSubsystem turns the text of one speech segment, with or without the annotations of the
English text analysis, into a stream of items: words with ARPAbet pronunciations and their provenance, punctuation, and
tokens it cannot pronounce. It is the public face of the package. It knows nothing of voices; the RenderingSubsystem
turns its result into a phoneme string. It contains the following units:

- **EnglishPhonemizer**: the public entry point, with the public result types `PhonemizedText`, `PhonemizedItem`,
  `PhonemizedItemKind` and `PronunciationSource`; it drives the other units
- **WordTokenizer**: the spoken text (the text with its replacements applied, `SpokenText`), the index of the reading
  and pronunciation annotations (`AnnotationIndex`) and the tokenizer that cuts the spoken text into tokens
- **WordPronouncer**: the pronunciation of one word by the priority list, with the rules for compounds, diacritics and
  prefixes

### Interfaces

The subsystem exposes five public types in the `DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem` namespace:
`EnglishPhonemizer` with a public parameterless constructor, `Phonemize(text)` and `Phonemize(text, annotations)`;
`PhonemizedText` (`Items`, `OovWords`, `UnsupportedTokens`); `PhonemizedItem` (`Kind`, `Text`, `Phones`, `Arpabet`,
`Source`, `FromReading`, `Oov`, `ReadingId`, `IpaOverride`); and the two enumerations `PhonemizedItemKind` (`Word`,
`Punctuation`, `Unsupported`) and `PronunciationSource` (`None`, `Override`, `Reading`, `Lexicon`, `Morphology`,
`Spelled`, `Digits`, `LetterToSound`; the order of the first five and the last is the priority). Everything else (the
tokenizer, the spoken text, the annotation index, the pronouncer and the internal phone-based members of the items) is
internal.

It consumes `SegmentAnnotation` of the Speech library and `TextEnAnnotations` of SpeechTextEn (the typed readers of the
`en.reading` and `en.pron` values), the `CmuLexicon`, `ReadingPronunciations` and `Morphology` of the LexiconSubsystem
and `LetterToSound` of the LetterToSoundSubsystem.

### Design

**Flow.** `Phonemize` builds the `SpokenText` and the `AnnotationIndex`, tokenizes the spoken text, and turns each token
into items in order: punctuation and unsupported runs become items without sound, a digit run becomes one item per digit
(spoken as the dictionary's digit word), a spelled letter becomes its letter name (`Spelled`), a clitic is joined to the
word before it when that word ends where the clitic starts (`I` + `'s` of a spelled `FBI's`), and a word token is
pronounced as described below. The result lists each out-of-vocabulary word once and each unsupported token once.

**Annotations are anchored to original text.** The text analysis never rewrites text: `en.norm` and `en.spell` give the
spoken replacement of a span, and `en.reading` and `en.pron` annotate a word of the original. `SpokenText` therefore
builds the spoken string itself and remembers, for every spoken character, where it came from in the original (or that a
replacement introduced it) and whether it belongs to a spelled replacement. It also remembers the end of the original
span each character stands for. A reading or pronunciation annotation applies to a word token that starts with unchanged
original text, and covers either the whole original span of the token or only its unchanged headword (the `record` of
`record's`, also when the text analysis normalized the typographic apostrophe of `record’s`, which makes the apostrophe
replacement text); the ending that follows the headword must be a bare plural-possessive apostrophe or a clitic, which
`Morphology` attaches (for an IPA override the same sound rules in IPA), otherwise the annotation does not describe the
token. A token that starts with replacement text is pronounced as plain text, and an override beats a reading of the
same headword. Overlapping replacements cannot both apply (the earlier wins), and an annotation that is out of range,
empty of value or malformed is not indexed and the text is spoken as usual.

**Tokens.** At each position the tokenizer tries, in order: skip whitespace and the audio-tag placeholder (U+FFFC); a
letter of a spelled replacement is a token of its own; a run of ASCII digits; a run of Latin letters, which may contain
inner apostrophes and hyphens and may take a trailing apostrophe after `s` (the plural possessive `dogs'`) or when the
lexicon lists the word with one (the dropped-g headwords, which end in `in'`), except that a hyphen whose neighbors on
both sides are replacement text (the hyphen between the digit groups of `555-0147`, each read by the text analysis on
its own) ends the word and is a punctuation token, a pause between the groups; a leading apostrophe belongs to a word
only when the lexicon lists it so (`'tis`, `'cause`) and otherwise is a quote mark; a contraction ending directly after
a word that a replacement boundary cut off is a clitic; the marks `. , ; : ! ? … — – - ( ) [ ] { } " “ ” ‘ ’ ' « »` are
punctuation, a run of one mark being one token; anything else (an emoji, a character of another script, `#`) is an
unsupported run.

**Pronouncing a word.** `WordPronouncer` folds the word (lower case, straight apostrophes, diacritics removed, `ß` as
`ss`; a character that cannot be folded to the Latin alphabet makes the word unpronounceable) and tries the sources in
priority order: the reading, when the reading table resolves the spelling under the requested id; the reviewed
correction of a wrong dictionary entry; the first variant of
the dictionary entry (for the comparative of an adjective in `ng` the variant with the velar stop first, since the
dictionary lists `stronger` also without it); the morphology derivation from a known stem; a productive prefix
(`counter under super inter multi semi anti over non out sub dis mis pre un re`) with a known or derivable remainder of
at least four letters, so that a real word that merely begins like a prefix is never split; and the letter-to-sound
model, which marks the item out of vocabulary. A host override is handled by the phonemizer before the pronouncer is
asked, because it replaces the word entirely: an ARPAbet override is parsed (a text without a stress digit gives its
first vowel primary stress, so the word is not spoken flat) and an IPA override is carried on the item together with the
word's normal pronunciation, which a renderer speaks when its voice cannot represent the IPA. An override that cannot be
parsed falls back to the plain word.

**Hyphenated compounds.** The closed spelling of the dictionary is used only when a part is not a word of its own
(`wi-fi` is `wifi`); otherwise the sounds are those of the parts and the stress is that of one compound word: the first
primary stress is kept and a later primary stress becomes a secondary one (`well-known` is `W EH1 L N OW2 N`,
`battle-field` is `B AE1 T AH0 L F IY2 L D`; a voice that reads two primary stresses in one written word pauses between
them, which is how *battle-field* was heard as *battle ... field*). When the dictionary lists the closed spelling with
exactly the same sounds, its stress pattern is the word's, wherever its primary stress falls (`cooperate`, `nonfiction`;
not when the first element is a single letter: `e-mail`). A productive prefix
(`re pre un dis mis non anti semi inter ex de`, those whose root keeps the primary stress in the dictionary) has the
sound and the weak stress of a prefix and the root keeps its primary stress (`re-enter`, `ex-wife`). A small function
word in a later part (`of the in to for and or on at by as a an`) is unstressed, `to` as `T AH0` and `of` as `AH2 V`.
The item records where each element after the first starts, so that the Piper renderer writes a secondary stress of a
compound even next to the primary one, which a single word's stress-clash rule would drop, and applies its
vowel-before-`r` rules inside one element only (`free-range`, `tea-room` and `blue-ribbon` keep their long vowels).

**Reviewed corrections.** The dictionary is the only source of pronunciations, so a wrong entry is spoken wrong for
good: it lists *nobly* as `N AA1 B L IY0` (the vowel of *knobbly*). `data/phonemes-en/lexicon-corrections.tsv` holds the
few reviewed corrections (a lower-case word, the pronunciation, the reason), embedded by link and consulted by the word
pronouncer before the dictionary; the dictionary image and its pins stay untouched. The phonemizer's test over every
word of the dictionary proves that these rows are the only words that are not pronounced as the dictionary lists them.

**Error handling.** A null text throws `ArgumentNullException`; no other input throws. A reading that cannot be applied
to its word is not an error: the word keeps its default pronunciation and the item records the requested id with
`FromReading` false.
