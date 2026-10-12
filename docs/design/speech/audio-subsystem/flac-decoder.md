### FlacDecoder

**Purpose**: Decode FLAC streams for `AudioFileReader` (internal). The unit is implemented by
`FlacDecoder` (stream and metadata handling), `FlacFrameDecoder` (frame, subframe and residual
decoding), `FlacBitReader` (most-significant-bit-first reader), `FlacCrc` (CRC-8 and CRC-16) and the
`FlacStreamInfo` record.

**Data Model**: `FlacStreamInfo` holds the sample rate, channel count, bits per sample, total
samples (0 when unknown) and the maximum block size from STREAMINFO. Per-channel `long` buffers
are fixed at the maximum block size, and the interleaved `float` output grows on demand, so a
hostile declared length cannot force a large allocation; samples are scaled by `2^(bits-1)`.

**Key Methods**:

- **IsFlac(data)**: the `fLaC` magic, optionally behind an ID3v2 tag
- **Decode(data, verifyChecksums, maxSamples)**: skip an ID3v2 tag, require STREAMINFO first, skip
  other metadata blocks and decode frames until the declared total is reached or, when the total is
  unknown, until no further frame sync follows
- **FlacFrameDecoder.Decode**: parse the frame header (block size, sample rate and bit depth codes,
  UTF-8 frame number, CRC-8), decode each subframe (constant, verbatim, fixed order 0-4, LPC order
  1-32 with Rice residual methods 0 and 1 and escape partitions, wasted bits), undo left/side,
  side/right and mid/side stereo with an extra bit for the side channel, and check the CRC-16
- **FlacBitReader**: bounds-checked bit, unary, signed and UTF-8 number reads

All arithmetic is in 64-bit integers so 32-bit audio and 33-bit side channels cannot overflow. The
STREAMINFO MD5 is not verified and damaged frames are not skipped.

**Error Handling**: Every malformed condition (bad sync, reserved codes, CRC mismatch when
`VerifyChecksums` is set, truncation, a block larger than the STREAMINFO maximum, invalid
predictor parameters, more samples than `maxSamples`, a damaged ID3v2 prefix, or a frame that
disagrees with STREAMINFO) throws `InvalidDataException`, with the byte offset of the problem
where applicable.

**Dependencies**: `AudioClip`.

**Callers**: `AudioFileReader`.
