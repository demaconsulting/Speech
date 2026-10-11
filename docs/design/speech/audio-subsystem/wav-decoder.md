### WavDecoder

**Purpose**: Decode RIFF/WAVE images for `AudioFileReader` (internal).

**Data Model**: A static class. A private record holds the validated `fmt` chunk.

**Key Methods**:

- **IsWav(data)**: checks for the `RIFF`...`WAVE` header
- **Decode(data, maxSamples)**: walks the chunks (unknown chunks are skipped, odd sizes padded),
  validates `fmt`, clamps the `data` size to the bytes present, drops a trailing partial frame
  and converts samples to `float`: 8-bit unsigned, 16/24/32-bit signed PCM and 32-bit float,
  including `WAVE_FORMAT_EXTENSIBLE`

**Error Handling**: `InvalidDataException` for a missing header, `fmt` or `data` chunk, a
truncated chunk, zero channels or rate, or more samples than `maxSamples`; `NotSupportedException`
for any other format tag or bit depth.

**Dependencies**: `AudioClip`.

**Callers**: `AudioFileReader`.
