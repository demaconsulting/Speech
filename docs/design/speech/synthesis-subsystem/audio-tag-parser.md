### AudioTagParser

**Purpose**: Recognize the closed, fixed Natural Language Audio Tag vocabulary inline in text
intended for text-to-speech, producing an ordered, model-independent sequence of literal-text and
recognized-tag spans for a later Layer 2 rendering stage to consume.

**Data Model**:

- `NaturalLanguageAudioTag` — one canonical value per tag (31 values), collapsing every
  documented synonym onto a single value (for example `[excited]`/`[excitedly]` both resolve to
  `Excited`). The 22 base tags cover emotion (`Excited`, `Serious`, `Sarcastic`, `Panicked`,
  `Bored`, `Sad`), pace (`Slow`, `VerySlow`, `Fast`, `VeryFast`), pause (`ShortPause`,
  `LongPause`), emphasis, delivery volume (`Whispers`, `Soft`, `Loud`, `Breathy`), and non-verbal
  sounds (`Laughs`, `Sighs`, `Gasp`, `ClearsThroat`, `Snorts`). Nine mood tags extend the `Emotion`
  kind: `Angry` (`[angry]`, `[anger]`), `Calm`, `Happy`, `Enthused` (`[enthused]`,
  `[enthusiastic]`), `Surprised`, `Worried` (`[worried]`, `[anxious]`), `Confused`, `Frustrated`,
  and `Neutral`. Other notable aliases include `[shout]`/`[shouting]`/`[screams]` for `Loud`,
  `[shocked]` for `Panicked`, `[tired]` for `Bored`, `[crying]` for `Sad`, `[giggles]` for
  `Laughs`, `[inhale]` for `Gasp`, and `[cough]` for `ClearsThroat`; `AudioTagCatalog.Tags` is the
  authoritative alias list.
- `NaturalLanguageAudioTagKind` — the six kind categories: `Emotion`,
  `Pace`, `DeliveryVolume`, `NonVerbal`, `Pause`, `Emphasis`.
- `AudioTagDescriptor` — a read-only report of one canonical tag's kind and every alias that
  resolves to it, as exposed by `AudioTagCatalog.Tags`.
- `AudioTagCatalog` — the static, immutable alias-to-(tag, kind) lookup table, built once from
  the same descriptor list it exposes for enumeration. `Normalize` trims, collapses internal
  whitespace runs to a single space, and lower-cases bracket-interior text before lookup, so
  resolution is both case-insensitive and tolerant of extra spacing. `TryResolve` combines
  normalization and lookup into a single call.
- `TaggedTextSpanKind` — discriminates a span as `PlainText` or `Tag`.
- `TaggedTextSpan` — one ordered element of the parser's output: `Kind`, `Text` (populated for
  `PlainText`, including any unrecognized or malformed bracket content passed through literally),
  and `Tag` (populated only for `Tag` spans). Two non-positional `init` properties, defaulting to
  "none", let a `PlainText` span carry the output of text analysis (see *TextAnalysis*):
  `Annotations` (an `IReadOnlyList<SegmentAnnotation>`, default the shared empty list, stored as a
  private read-only copy) and `SentenceBreaks` (an `IReadOnlyList<int>?`, default `null` meaning
  "no analyzer opinion"; when set, a private copy of the non-negative offsets in the order given).
  Both are offsets relative to the span's own `Text`; on a `PlainText` span `SentenceBreaks`
  offsets are span-local. On a `Tag` span `Annotations` keep the default, while `SentenceBreaks`
  may carry an analyzer break adjacent to the tag, as offsets relative to the tag's single
  position: `0` is immediately before the tag, `1` immediately after it, other values are ignored
  (it carries a break between consecutive tags, where no plain text exists). The positional
  constructor `(Kind, Text, Tag)` and the existing
  factories are unchanged, so existing code is source and binary compatible, and a default tag
  span still equals one built before the property existed. `Annotations` takes
  part in record equality by reference, which keeps a default span equal to a span from before the
  property existed because both share the empty list. The span does not bound offsets against its
  `Text`: consumers (`SentenceChunker.ChunkAligned` and the default profile) defensively ignore
  out-of-range entries.

**Key Methods**:

- **AudioTagCatalog.TryResolve(rawBracketContent, out tag, out kind)**: normalizes and looks up
  one piece of bracket-interior text. Returns `false` for unknown words and for empty or
  whitespace-only content.
- **AudioTagParser.Parse(text)**: a single-pass, linear-time scanner. Ordinary characters
  accumulate into a running plain-text buffer. On encountering `[`, it searches for the nearest
  following bracket of either kind. If none exists, the remainder of the input (starting at the
  unmatched `[`) is appended to the plain-text buffer and scanning stops - an unbalanced bracket
  can never be a tag. If the nearest bracket is another `[`, the candidate is unclosed, so only
  the opening `[` is appended literally and scanning resumes right after it (no alias contains a
  bracket, so this keeps every scan bounded by the distance to the next bracket, even for
  adversarial input such as thousands of consecutive `[`). When the nearest bracket is `]`, the
  interior text is resolved against `AudioTagCatalog`: a match flushes the current plain-text
  buffer as its own span (so adjacent tags and tags at either edge of the input never merge with
  neighboring text) and appends a `Tag` span; anything else - an unknown word or empty brackets -
  appends only the opening `[` to the running plain-text buffer and resumes scanning immediately
  after it, so the text is passed through unchanged. The final buffer is flushed at the end
  of the scan. An empty input produces an empty span list, not a spurious empty `PlainText` span.
- **TaggedTextSpan.TagSpan(tag)** / **TaggedTextSpan.PlainText(text)** /
  **TaggedTextSpan.PlainText(text, annotations, sentenceBreaks)**: factory methods building a
  `Tag` span carrying a canonical tag, a `PlainText` span carrying literal text (`PlainText`
  rejects `null`), or a `PlainText` span that additionally carries annotations and sentence
  breaks. `AudioTagParser.Parse` itself never sets annotations or breaks; text analysis attaches
  them afterward (see *TextAnalysis*).

**Error Handling**: `Parse`, `TaggedTextSpan.PlainText`, `AudioTagCatalog.Normalize`, and
`AudioTagCatalog.TryResolve` throw `ArgumentNullException` for a `null` input. Setting
`TaggedTextSpan.Annotations` to a null list throws `ArgumentNullException` and a null element
throws `ArgumentException`; setting `SentenceBreaks` to a list containing a negative offset throws
`ArgumentOutOfRangeException` (`null` is accepted and means no opinion). The only other
failure mode is a programming error caught at type initialization: the catalog is built with a
fail-fast `Dictionary.Add`, so a duplicate alias would surface as a `TypeInitializationException`
(prevented by the no-ambiguity test). Per this library's "never worse than plain narration"
guarantee, no bracket content - however malformed - ever throws or is discarded; it always
becomes literal `PlainText`.

**Dependencies**: `SegmentAnnotation` from this subsystem's TextAnalysis unit (the type of a
span's annotation list); otherwise none outside this unit's own types. The parser and catalog
have no reference to any model, engine, or audio device.

**Callers**: `SynthesisSession` calls `AudioTagParser.Parse` as its Layer 1 tag-parsing
step before text analysis and Layer 2 rendering build a `SpeechPlan` from the resulting spans, per
this library's Layer 1 (model-independent parsing) / Layer 2 (model-specific rendering)
split. `TextAnalysisMapper` reads and rewrites spans, and `DefaultModelCapabilityProfile` reads
their annotations and breaks. The `speak` command of the SpeechCli system also calls the parser,
to strip tags for `--no-tags`.
