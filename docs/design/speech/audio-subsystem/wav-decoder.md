### WavDecoder

**Purpose**: Decode RIFF/WAVE images for `AudioFileReader` (internal).

**Data Model**: A static class. A private record holds the validated `fmt` chunk.

**Key Methods**:

- **IsWav(data)**: checks for the `RIFF`...`WAVE` header
- **Decode(data, maxSamples)**: requires the `RIFF`...`WAVE` header, then walks the chunks from
  offset 12 (unknown chunks are skipped, odd sizes padded to a word boundary). A chunk whose
  padded body would run past the end of the image that is not the `data` chunk ends the walk, so
  the image is reported as having no `data` chunk. Every `fmt` chunk is handed to `ParseFormat`;
  on reaching the `data` chunk (which must follow `fmt`) the size is clamped to the bytes present
  and handed to `DecodeSamples`.
- **ParseFormat(data, body, size)** (private): validates the `fmt` chunk - at least 16 bytes, at
  least 40 for `WAVE_FORMAT_EXTENSIBLE` (whose sub-format supplies the real tag) - and rejects zero
  channels or sample rate, a rate above `int.MaxValue`, and any format tag or bit depth other than
  8/16/24/32-bit PCM and 32-bit float. Returns a private `WavFormat` record.
- **DecodeSamples(data, start, available, format, maxSamples)** (private): drops a trailing partial
  frame, enforces `maxSamples`, and converts samples to `float`: 8-bit unsigned, 16/24/32-bit
  signed PCM and 32-bit float.

**Error Handling**: `InvalidDataException` for data without a RIFF/WAVE header (`Decode` checks
this itself even though `AudioFileReader` dispatches on the header first), a missing `fmt` or
`data` chunk, a truncated chunk, a `data` chunk that precedes `fmt`, zero channels or rate, a
sample rate above `int.MaxValue`, a `WAVE_FORMAT_EXTENSIBLE` `fmt` chunk shorter than 40 bytes,
or more samples than `maxSamples`; `NotSupportedException` for any other format tag or bit depth,
including an extensible sub-format other than PCM or float.

Float samples are passed through as stored, so a NaN in the file reaches the clip unchanged.

**Dependencies**: `AudioClip`.

**Callers**: `AudioFileReader`.
