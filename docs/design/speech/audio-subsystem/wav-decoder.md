### WavDecoder

**Purpose**: Decode RIFF/WAVE images for `AudioFileReader` (internal).

**Data Model**: A static class. A private record holds the validated `fmt` chunk.

**Key Methods**:

- **IsWav(data)**: checks for the `RIFF`...`WAVE` header
- **Decode(data, maxSamples)**: requires the `RIFF`...`WAVE` header, then walks the chunks from
  offset 12 (unknown chunks are skipped, odd sizes padded to a word boundary). A chunk whose
  padded body would run past the end of the image that is not the `data` chunk ends the walk, so
  the image is reported as having no `data` chunk. Every `fmt` chunk is handed to `ParseFormat`, and
  when several appear the last one before `data` wins; on reaching the `data` chunk (which must
  follow `fmt`) the size is clamped to the bytes present (a size of 0xFFFFFFFF written by streaming
  encoders is therefore accepted) and handed to `DecodeSamples`. Chunk sizes are 32-bit values
  clamped to `int.MaxValue` before use, so a huge non-data chunk simply ends the walk.
- **ParseFormat(data, body, size)** (private): validates the `fmt` chunk - at least 16 bytes, at
  least 40 for `WAVE_FORMAT_EXTENSIBLE` (whose sub-format GUID supplies the real tag; all 40 bytes must be
  present in the image, and the GUID must be the complete canonical `KSDATAFORMAT_SUBTYPE_PCM` or
  `KSDATAFORMAT_SUBTYPE_IEEE_FLOAT` value: the format tag as a 4-byte little-endian value followed by
  `00 00 10 00 80 00 00 AA 00 38 9B 71`) - and rejects zero
  channels or sample rate, a rate above `int.MaxValue` (with its own message naming the rate), and any
  format tag or bit depth other than 8/16/24/32-bit PCM and 32-bit float. Returns a private `WavFormat`
  record. Only the format tag,
  channel count, sample rate and bit depth are read; the byte rate, block align and the extensible
  channel mask and valid-bits fields are ignored.
- **DecodeSamples(data, start, available, format, maxSamples)** (private): drops a trailing partial
  frame, enforces `maxSamples`, and converts samples to `float`: 8-bit unsigned, 16/24/32-bit
  signed PCM and 32-bit float.

**Error Handling**: `InvalidDataException` for data without a RIFF/WAVE header (`Decode` checks
this itself even though `AudioFileReader` dispatches on the header first), a missing `fmt` or
`data` chunk, a truncated chunk, a `data` chunk that precedes `fmt`, zero channels or rate, a
sample rate above `int.MaxValue`, a `WAVE_FORMAT_EXTENSIBLE` `fmt` chunk shorter than 40 bytes,
or more samples than `maxSamples`; `NotSupportedException` for any other format tag or bit depth,
including an extensible sub-format other than PCM or float, or whose GUID is not canonical.

Float samples are passed through as stored, so a NaN in the file reaches the clip unchanged.

**Dependencies**: `AudioClip`.

**Callers**: `AudioFileReader`.
