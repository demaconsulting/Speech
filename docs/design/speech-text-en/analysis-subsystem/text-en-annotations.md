### TextEnAnnotations

**Purpose**: Own the annotation vocabulary of the English text analysis and give a synthesis backend a typed
way to read it, so no backend hand-parses `Kind` and `Value` strings. The Speech library carries
annotations opaquely and never interprets them, so the vocabulary has to live with the package that
produces it.

**Data Model**: a static class with six public string constants, one per annotation kind of the package
family: `Normalized` (`en.norm`), `PartOfSpeech` (`en.upos`), `Reading` (`en.reading`),
`ReadingConfidence` (`en.reading-confidence`), `Pronunciation` (`en.pron`) and `SpellOut` (`en.spell`).
`PartOfSpeech`, `Normalized`, `SpellOut` and `Pronunciation` are produced by this release; `Reading` and
`ReadingConfidence` are declared so a backend can reference stable names from the first release and are
documented as reserved. The `Value` of an `en.norm`
annotation is the exact spoken replacement for the annotated span (empty means speak nothing), and its
documentation states the consumer algorithm and the guarantees of the _Alignment Contract_ (see _SpeechTextEn
System Design_). The `Value` of an `en.spell` annotation is the same kind of replacement, restricted to single
capital ASCII letters separated by single spaces with at most one padding space on either side (A11), with
the promise that the letters are letter names. The `en.norm` and `en.spell` annotations form the
_replacement family_: one ascending, non-overlapping sequence. The `Value` of an `en.pron` annotation is
`scheme:phonemes` with a lower-case scheme from the closed list `ipa` and `arpabet`; it replaces nothing and
is disjoint from every replacement (A12). A private set holds the 17 Universal Dependencies tag names that a
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
  replacement family: true for an `en.norm` annotation (`isSpelled` false) and for a well-formed `en.spell`
  annotation (`isSpelled` true); false for any other kind or a malformed spelled value

**Error Handling**: a null annotation throws `ArgumentNullException` (from every reader); nothing else is
thrown.

**Dependencies**: the Speech library's `SegmentAnnotation`, the NormalizationSubsystem's `SpelledGrammar`, the
`PronunciationScheme` enumeration and `OverrideSyntax` (both of the PronunciationOverrides unit).

**Callers**: `EnglishTextAnalyzer` (the kind constants), synthesis backends that implement
`IAnnotatedSynthesisBackend`, and the test projects.
