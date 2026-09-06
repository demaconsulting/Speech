### AudioTagParser

**Purpose**: Recognize the closed, fixed Natural Language Audio Tag vocabulary inline in text
intended for text-to-speech, producing an ordered, model-independent sequence of literal-text and
recognized-tag spans for a later Layer 2 rendering stage to consume.

**Data Model**:

- `NaturalLanguageAudioTag` — one canonical value per tag (22 values), collapsing every
  documented synonym onto a single value (for example `[excited]`/`[excitedly]` both resolve to
  `Excited`).
- `NaturalLanguageAudioTagKind` — the six kind categories architecture.md defines: `Emotion`,
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
  and `Tag` (populated only for `Tag` spans).

**Key Methods**:

- **AudioTagCatalog.TryResolve(rawBracketContent, out tag, out kind)**: normalizes and looks up
  one piece of bracket-interior text. Returns `false` for unknown words and for empty or
  whitespace-only content.
- **AudioTagParser.Parse(text)**: a single-pass scanner. Ordinary characters accumulate into a
  running plain-text buffer. On encountering `[`, it searches for the next `]`; if none exists,
  the remainder of the input (starting at the unmatched `[`) is appended to the plain-text buffer
  and scanning stops - an unbalanced bracket can never be a tag, so nothing later in the input
  could close it either. When a closing bracket is found, the interior text is resolved against
  `AudioTagCatalog`: a match flushes the current plain-text buffer as its own span (so adjacent
  tags and tags at either edge of the input never merge with neighboring text) and appends a
  `Tag` span; anything else - an unknown word or empty brackets - folds the whole `[...]` text,
  brackets included, into the running plain-text buffer. The final buffer is flushed at the end
  of the scan. An empty input produces an empty span list, not a spurious empty `PlainText` span.

**Error Handling**: `Parse` throws `ArgumentNullException` for a `null` input; there is no other
failure mode. Per architecture.md's "never worse than plain narration" guarantee, no bracket
content - however malformed - ever throws or is discarded; it always becomes literal
`PlainText`.

**Dependencies**: None outside this unit's own types. The parser and catalog have no reference to
any model, engine, or audio device.

**Callers**: `SherpaOnnxSpeechSynthesizer` calls `AudioTagParser.Parse` as its Layer 1 tag-parsing
step before Layer 2 rendering builds a `SpeechPlan` from the resulting spans, per
architecture.md's Layer 1 (model-independent parsing) / Layer 2 (model-specific rendering)
split.
