### TextEnAnnotations

**Purpose**: Own the annotation vocabulary of the English text analysis and give a synthesis backend a typed
way to read it, so no backend hand-parses `Kind` and `Value` strings. The Speech library carries
annotations opaquely and never interprets them, so the vocabulary has to live with the package that
produces it.

**Data Model**: a static class with six public string constants, one per annotation kind of the package
family: `Normalized` (`en.norm`), `PartOfSpeech` (`en.upos`), `Reading` (`en.reading`),
`ReadingConfidence` (`en.reading-confidence`), `Pronunciation` (`en.pron`) and `SpellOut` (`en.spell`).
`PartOfSpeech` and `Normalized` are produced by this release; the other four are declared so a backend can
reference stable names from the first release and are documented as reserved. The `Value` of an `en.norm`
annotation is the exact spoken replacement for the annotated span (empty means speak nothing), and its
documentation states the consumer algorithm and the guarantees of the _Alignment Contract_ (see _SpeechTextEn
System Design_). A private set holds the 17 Universal Dependencies tag names that a `PartOfSpeech` value may
take.

**Key Methods**:

- **TryReadPartOfSpeech(annotation, out partOfSpeech)**: returns true and the tag name when the annotation's
  kind is exactly `en.upos` (ordinal comparison) and its value is in the closed tag set; otherwise returns
  false with an empty string. A wrong kind or an unknown value is an ordinary "no" and never an exception,
  because a backend calls the reader on every annotation of a segment
- **TryReadNormalized(annotation, out spokenText)**: returns true and the annotation's value when its kind is
  exactly `en.norm` (ordinal comparison); the value may be empty, which means the span is spoken as nothing,
  so unlike the part-of-speech reader no value check is made. Any other kind, including a differently cased
  one, returns false with an empty string and is not an error

**Error Handling**: a null annotation throws `ArgumentNullException` (from either reader); nothing else is
thrown.

**Dependencies**: the Speech library's `SegmentAnnotation`.

**Callers**: `EnglishTextAnalyzer` (the kind constants), synthesis backends that implement
`IAnnotatedSynthesisBackend`, and the test projects.
