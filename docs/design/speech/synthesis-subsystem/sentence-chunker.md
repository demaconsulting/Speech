### SentenceChunker

**Purpose**: Split synthesis-ready plain text into sentence/clause-sized chunks so chunked
synthesis has natural-sounding boundaries and no chunk exceeds a length a synthesis call can
reasonably handle.

**Data Model**: A stateless static class; `Chunk(text, maxLength)`/`ChunkWithMetadata(text,
maxLength)` take no configuration beyond their two arguments. `ChunkWithMetadata` returns a
`SentenceChunk` record struct per chunk (`Text`, `EndsWithEllipsis`); `Chunk` is a pure projection
of `ChunkWithMetadata`'s chunk text.

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

**Error Handling**: Rejects a non-positive `maxLength` with `ArgumentOutOfRangeException` and a
null `text` with `ArgumentNullException`, since neither describes a meaningful chunking request.
Empty and whitespace-only text return no chunks rather than throwing.

**Dependencies**: None beyond the Base Class Library.

**Callers**: `DefaultModelCapabilityProfile.Render(...)`.
