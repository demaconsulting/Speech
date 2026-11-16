### SentenceChunker

**Purpose**: Split synthesis-ready plain text into sentence/clause-sized chunks so chunked
synthesis has natural-sounding boundaries and no chunk exceeds a length a synthesis call can
reasonably handle.

**Data Model**: A stateless, **public** static class, so a model package can chunk text itself
instead of copying the algorithm. `DefaultMaxChunkLength` (200) is the public default budget.
`Chunk(text, maxLength)`/`ChunkWithMetadata(text, maxLength)`/`ChunkAligned(text, annotations,
sentenceBreaks, maxLength)` take no configuration beyond their arguments. `ChunkWithMetadata`
returns a public `SentenceChunk` record struct per chunk (`Text`, `EndsWithEllipsis`, and
`Annotations`, declared in the same file); `Chunk` is a pure projection of `ChunkWithMetadata`'s
chunk text, and `ChunkWithMetadata` is `ChunkAligned` with no annotations and no breaks.
`SentenceChunk.Annotations` is backed by a field that is `null` in a default-constructed struct,
and its getter returns the shared empty list in that case, so a default struct is safe to read.
Internally the chunker works on two private value shapes: a `SourceRun(SourceStart, Length,
TextStart)` records that `text[TextStart..TextStart+Length)` of a piece equals
`source[SourceStart..SourceStart+Length)` of the original text, and a `Piece(Text, Runs)` pairs a
piece's text with the runs that produced it. All annotation and break offsets are UTF-16 code
unit indices.

**Key Methods**:

- **Chunk(text, maxLength)** / **ChunkWithMetadata(text, maxLength)**: Splits on primary
  sentence-ending punctuation (`.`, `!`, `?`) first, then **unconditionally** splits every
  resulting piece further on secondary clause punctuation (`,`, `;`, `:`) - not only when the
  piece is still over `maxLength` - so every clause becomes its own chunk. A punctuation
  character embedded in a numeral is never treated as a boundary at either pass: a digit both
  immediately before and after (e.g. the `:` in `"12:30"`, the `,` in `"1,000"`, or a decimal
  point such as `"0.5"`) is always kept attached, and a decimal point specifically is also kept
  attached when a digit follows immediately but none precedes, as long as the character before it
  (if any) is not a letter - e.g. a leading fraction such as `".5"`, `"$.99"`, or `".5 units"` at
  start-of-text/after whitespace/a sign/a currency symbol. A genuine sentence-ending period
  directly followed by a digit after a letter (e.g. `"Wait.5 more."`) still splits normally, since
  the numeral exception only applies when no letter precedes the period. A piece still longer
  than `maxLength` after both punctuation passes is split on a whitespace budget; a single word
  that alone exceeds `maxLength` is returned whole, never split mid-word. Any resulting piece
  whose trimmed text contains no letter or digit at all (just punctuation and/or whitespace, e.g.
  a lone comma or a whitespace-spaced ellipsis like `". . ."`) is then merged onto the end of the
  immediately preceding non-empty chunk (joined by a single space), or dropped if there is no
  preceding chunk. `ChunkWithMetadata` additionally flags a chunk whose final text ends in three
  or more consecutive `.` characters (with or without interspersed whitespace) as
  `EndsWithEllipsis`. These numeral exceptions assume English/US-style numeral punctuation (`,`
  as thousands separator, `.` as decimal point); locales that swap the two roles (e.g.
  `"1.000,5"`) are not specially handled - every model in this library's current catalog is
  English-only, so this is not currently a defect, but a future non-English model would need
  these rules adjusted rather than assuming they generalize as-is.
- **ChunkAligned(text, annotations, sentenceBreaks, maxLength)**: The single, position-aware
  implementation that `Chunk` and `ChunkWithMetadata` also run (there is no second, older code
  path). It returns the same chunks as `ChunkWithMetadata` when `annotations` and `sentenceBreaks`
  are `null`, and additionally attaches to each chunk the annotations that lie wholly inside it,
  re-based to the chunk's text. When `sentenceBreaks` is not `null` the analyzer's breaks replace
  the primary punctuation pass (see *Authoritative breaks* below). Exactly the same exceptions as
  the other two methods apply. The algorithm is the **run-map algorithm**:
  1. Validate the arguments. Let `lead` be the count of leading characters for which
     `char.IsWhiteSpace` is true (the same characters `string.Trim` removes) and `trimmed` the
     trimmed text; an empty `trimmed` returns no chunks. Every source offset below is an absolute
     offset into the *original* `text`.
  2. **Primary pass** over the range `[lead, lead + trimmed.Length)`. With no breaks it is the
     existing heuristic (split after a run of `.`, `!`, `?` with consecutive terminators merged).
     With breaks it is the authoritative pass: drop every break `k <= lead` or `k >= lead +
     trimmed.Length`, sort and de-duplicate, split the range at each remaining `k`, trim each
     sub-range, and drop empty ones. No punctuation scan happens in this pass.
  3. **Secondary pass**: every primary range is always split on `,`, `;`, `:` exactly as before. The
     boundary scan takes `(source, start, end)` and returns trimmed `(Start, End)` ranges, and
     every neighbor test is **bounded by the range**, not by the whole source - in particular the
     numeral-flanking test never looks outside `[start, end)`, so a digit just outside a range
     cannot change how the range splits. This reproduces the old behavior, which tested against a
     substring, byte for byte.
  4. **Budget pass**: a range no longer than `maxLength` becomes one piece with a single run
     `(Start, Length, 0)`. A longer range is scanned as words (maximal non-whitespace runs) and
     grouped greedily exactly as before: the next word adds `word.Length` plus one separator when
     the group is non-empty, and the group is flushed when adding the word would exceed
     `maxLength`. The piece text is the words joined by a single space, with one run per word whose
     `TextStart` is that word's running position in the joined text. Whitespace runs inside such a
     piece therefore collapse, and the runs record exactly where each surviving word came from.
  5. **Degenerate merge** (rule unchanged): a piece with no letter or digit is appended to the
     previous piece as `previous + " " + piece`, with its runs appended after the previous runs,
     each shifted by `previous.Text.Length + 1`. With no previous piece it is dropped, and its runs
     vanish with it. "Has a letter or digit" is **scalar-aware only on the analyzed path**: when
     `sentenceBreaks` is not `null` or `annotations` is non-empty the test enumerates Unicode
     scalars (`Rune.IsLetterOrDigit`), so a chunk whose only letters are supplementary-plane
     characters (a surrogate pair, such as U+20000) keeps its text and annotation instead of being
     merged or dropped; a lone surrogate enumerates as U+FFFD, which is not a letter, as before.
     With neither (the no-analyzer path: `Chunk`, `ChunkWithMetadata` and a profile that supplies
     no analysis), the legacy per-UTF-16-code-unit `char.IsLetterOrDigit` test runs unchanged, so
     existing output is byte-for-byte identical. Offsets stay UTF-16 throughout.
  6. Each surviving piece becomes `SentenceChunk(piece.Text, EndsWithEllipsis(piece.Text))` with
     its re-based annotations.
  7. **Annotation re-basing**: for each annotation `a` (skipped when `a.Start < 0` or `a.End >
     text.Length`) and each piece, look for a run `r1` with `r1.SourceStart <= a.Start <
     r1.SourceStart + r1.Length` and a run `r2` with `r2.SourceStart <= a.End - 1 <
     r2.SourceStart + r2.Length`, both in the **same piece**. The new start is `r1.TextStart +
     (a.Start - r1.SourceStart)` and the new exclusive end is `r2.TextStart + (a.End - 1 -
     r2.SourceStart) + 1`; the chunk gets `SegmentAnnotation(newStart, newEnd - newStart, Kind,
     Value)`. When no piece satisfies both lookups - the first or last character is not covered by
     any run, or they fall in different pieces - the annotation is **dropped**, never misplaced.
     Whitespace collapsed by a budget-split join shrinks the length consistently, and chunk
     annotations keep the input order. Because chunk text is trimmed, an annotation that starts or
     ends on a whitespace character is dropped, so analyzers should begin and end annotations on
     non-whitespace. The cost is O((chunks + annotations) log runs).

  Worked example of the run map: for `"alpha   beta gamma"` (three spaces after `alpha`) with a
  budget small enough to force a word-grouping split, the first piece joins `alpha` and `beta` as
  `"alpha beta"` with runs `(0, 5, 0)` and `(8, 4, 6)`. An annotation over `beta` (source start 8,
  length 4) maps to `6 + (8 - 8) = 6`, length 4, so `piece[6..10]` is `beta` even though the source
  held three spaces.

  **Authoritative breaks.** A non-`null` `sentenceBreaks` list is the analyzer speaking for the
  request, so the primary punctuation heuristic is not consulted at all: `"Dr. Smith arrived. He
  left."` with a break at offset 18 yields `"Dr. Smith arrived."` and `"He left."` (the
  heuristic would split at `Dr.`), a break can split text that has no punctuation, and an *empty*
  list means "no sentence splits" so `"A. B."` stays one chunk. The secondary clause pass and the
  budget pass still apply to each sub-range, and out-of-range breaks are ignored. A `null` list
  means no opinion and the heuristic applies.

**Error Handling**: Rejects a non-positive `maxLength` with `ArgumentOutOfRangeException` and a
null `text` with `ArgumentNullException`, since neither describes a meaningful chunking request.
Empty and whitespace-only text return no chunks rather than throwing. `ChunkAligned` never throws
for annotation or break content: an annotation outside `[0, text.Length]` or one that cannot be
represented in a single chunk is dropped, and an out-of-range break is ignored, because both come
from a best-effort analysis that must not fault synthesis.

**Dependencies**: `SegmentAnnotation` from this subsystem's TextAnalysis unit, and the Base Class
Library.

**Callers**: `DefaultModelCapabilityProfile.Render(...)` and
`DefaultModelCapabilityProfile.RenderText(...)`, and external model profiles, which may call the
public `Chunk`, `ChunkWithMetadata`, and `ChunkAligned` directly to build their own tag-rendering
strategy without copying the chunker.
