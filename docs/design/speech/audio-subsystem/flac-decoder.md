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
  unknown, until no further frame sync follows (`FlacBitReader.PeekFrameSync`).
  Precondition: `data` is a non-null image and `maxSamples` is not negative. Postcondition: the
  returned `AudioClip` holds exactly the declared total samples when it is known (a frame that would
  exceed the remaining count is rejected rather than truncated), the clip's rate, channel count and
  bit depth come from STREAMINFO, and the effective cap is `maxSamples` clamped to
  `Array.MaxLength`. An internal overload `Decode(data, verifyChecksums, maxSamples, maxArrayLength)`
  takes the array-length ceiling explicitly (the public form passes `Array.MaxLength`) so tests can
  exercise the clamp with a small value: parse the frame header (block size, sample rate and bit depth codes,
  UTF-8 frame number, CRC-8), decode each subframe (constant, verbatim, fixed order 0-4, LPC order
  1-32 with Rice residual methods 0 and 1 and escape partitions, wasted bits), call
  `FlacBitReader.AlignToByte` to reach the frame footer, check the CRC-16, and undo left/side,
  side/right and mid/side stereo with an extra bit for the side channel. The frame must match
  STREAMINFO strictly: its channel count and sample rate must equal the declared values (an
  explicit bit-depth code is used as written and a code of zero means "as in STREAMINFO"), and its
  block size may not exceed the declared maximum
- **FlacBitReader**: bounds-checked bit, unary, signed and UTF-8 number reads, `PeekFrameSync`
  (are the next two byte-aligned bytes the 14-bit frame sync code) and `AlignToByte` (skip to the
  next byte boundary after the last subframe)

All arithmetic is in 64-bit integers so 32-bit audio and 33-bit side channels cannot overflow. The
STREAMINFO MD5 is not verified and damaged frames are not skipped.

**Error Handling**: Every malformed condition (bad sync, reserved codes, CRC mismatch when
`VerifyChecksums` is set, truncation, a block larger than the STREAMINFO maximum, invalid
predictor parameters, more samples than `maxSamples` or than `Array.MaxLength`, a frame larger than
the samples remaining of a known STREAMINFO total, a damaged ID3v2 prefix (including a size field
that is not syncsafe), or a frame that
disagrees with STREAMINFO) throws `InvalidDataException`, with the byte offset of the problem
where applicable.

**Dependencies**: `AudioClip`.

**Callers**: `AudioFileReader`.
