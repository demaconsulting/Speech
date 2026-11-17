### TextEnAnnotations

**Purpose**: Own the annotation vocabulary of the English text analysis and give a synthesis backend a typed
way to read it, so no backend hand-parses `Kind` and `Value` strings. The Speech library carries
annotations opaquely and never interprets them, so the vocabulary has to live with the package that
produces it.

**Data Model**: a static class with six public string constants, one per annotation kind of the package
family: `Normalized` (`en.norm`), `PartOfSpeech` (`en.upos`), `Reading` (`en.reading`),
`ReadingConfidence` (`en.reading-confidence`), `Pronunciation` (`en.pron`) and `SpellOut` (`en.spell`). All six
kinds are emitted by the analyzer: the reading pair only where a heteronym model exists or a `Reading` override names
the word. The `Value` of an `en.norm`
annotation is the exact spoken replacement for the annotated span (empty means speak nothing), and its
documentation states the consumer algorithm and the guarantees of the *Alignment Contract* (see *SpeechTextEn
System Design*). The `Value` of an `en.spell` annotation is the same kind of replacement, restricted to single
capital ASCII letters separated by single spaces with at most one padding space on either side (A11), with
the promise that the letters are letter names. The kind is the only spell-out signal: an `en.norm` value is
always spoken as words, whatever letters it holds (a `Say` override of `I` is the word), so no consumer infers
spelling from an `en.norm` value, and a meridiem glued to its number is split into an `en.norm` span and an
`en.spell` span. The `en.norm` and `en.spell` annotations form the
*replacement family*: one ascending, non-overlapping sequence. The `Value` of an `en.pron` annotation is
`scheme:phonemes` with a lower-case scheme from the closed list `ipa` and `arpabet`; it replaces nothing and
is disjoint from every replacement (A12). The `Value` of an `en.reading` annotation is a reading id (two or three
dot-separated segments of lower-case letters, digits and inner hyphens, at most 64 characters, the grammar of the
HeteronymSubsystem's `ReadingIdSyntax`, which this unit calls rather than repeats), and the `Value` of an
`en.reading-confidence` annotation is the confidence with three decimals and a period (`0.000` to `1.000`,
invariant culture). The two annotate exactly one whole token with the same span, coexist with `en.upos` there, and
never overlap an `en.norm`, `en.spell` or `en.pron` annotation (the coexistence rules of *Heteronym Readings* in
*SpeechTextEn System Design*); this unit's documentation states them. A private set holds the 17 Universal Dependencies
tag names that a
`PartOfSpeech` value may take. The reader grammars of `en.spell` and `en.pron` are not duplicated here: the
spelled-letter grammar is the alignment contract's `SpelledGrammar` and the pronunciation grammar is
`OverrideSyntax.ParsePronunciation`, the same functions that the alignment builder and the dictionary loader
use, so what is produced, validated and read cannot drift apart.

**Key Methods**:

- **TryReadPartOfSpeech(annotation, out partOfSpeech)**: returns true and the tag name when the annotation's
  kind is exactly `en.upos` (ordinal comparison) and its value is in the closed tag set; otherwise returns
  false with an empty string. A wrong kind or an unknown value is an ordinary "no" and never an exception,
  because a backend calls the reader on every annotation of a segment
- **TryReadNormalized(annotation, out spokenText)**: returns true and the annotation's value when its kind is
  exactly `en.norm` (ordinal comparison); the value may be empty, which means the span is spoken as nothing,
  so unlike the part-of-speech reader no value check is made. Any other kind, including a differently cased
  one, returns false with an empty string and is not an error
- **TryReadSpellOut(annotation, out spokenLetters)**: returns true and the value exactly as stored (padding
  included) when the kind is exactly `en.spell` and the value is a well-formed letter sequence
  (`[ ]?[A-Z]( [A-Z])*[ ]?`); otherwise false with an empty string
- **TryReadPronunciation(annotation, out scheme, out phonemes)**: returns true, the typed
  `PronunciationScheme` and the phonemes after the first colon when the kind is exactly `en.pron` and the
  value is a known scheme followed by phonemes that satisfy that scheme's syntax; an unknown scheme or
  malformed phonemes are an ordinary false (the backend then speaks the word as usual), never an exception
- **TryReadReplacement(annotation, out spokenText, out isSpelled)**: the single entry point for the
  replacement family: true for an `en.norm` annotation (`isSpelled` false, always, whatever the value holds)
  and for a well-formed `en.spell`
  annotation (`isSpelled` true); false for any other kind or a malformed spelled value
- **TryReadReading(annotation, out readingId)**: returns true and the id when the kind is exactly `en.reading` (ordinal
  comparison) and the value satisfies the reading id grammar; otherwise false with an empty string. The id is **not**
  looked up in the inventory (`ReadingInventory.Contains` does that), so a backend built against a newer inventory still
  reads an id this build does not know. A wrong kind or a malformed value (upper case, a space, a pipe or a colon, a
  missing or an extra segment, an empty value) is an ordinary "no", never an exception
- **TryReadReadingConfidence(annotation, out confidence)**: returns true and the number when the kind is exactly
  `en.reading-confidence` and the value is `0` or `1`, optionally followed by a period and one to six ASCII digits, and
  not above 1; otherwise false with 0. The value is parsed with the invariant culture whatever the current culture; a
  sign, an exponent, whitespace, a comma separator, a missing digit, non-ASCII digits and `1.001` are rejected, and
  nothing throws for a malformed value

**Error Handling**: a null annotation throws `ArgumentNullException` (from every reader); nothing else is
thrown.

**Dependencies**: the Speech library's `SegmentAnnotation`, the NormalizationSubsystem's `SpelledGrammar`, the
`PronunciationScheme` enumeration and `OverrideSyntax` (both of the PronunciationOverrides unit) and the
HeteronymSubsystem's `ReadingIdSyntax`.

**Callers**: `EnglishTextAnalyzer` (the kind constants), synthesis backends that implement
`IAnnotatedSynthesisBackend`, and the test projects.
