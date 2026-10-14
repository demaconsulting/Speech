### AudioFileReader

**Purpose**: Decode WAV and FLAC audio files or streams into `AudioClip` values with managed code
only, so hosts and models can accept either format without a native decoder dependency.

**Data Model**: A static class with no state. `SupportedExtensions` lists `.wav` and `.flac`.
`AudioFileReadOptions` carries `VerifyChecksums` (default `true`), `MaxSamples` (default
`DefaultMaxSamples`, 2^28 samples across channels) and `MaxEncodedBytes` (default
`DefaultMaxEncodedBytes`, 1.25 GiB) as init-only settings.

**Key Methods**:

- **Read(path | stream, options)**: first bounds the input by `MaxEncodedBytes` (never above the maximum
  array length, the most that can be held in memory): a file is rejected from the remaining length of the `FileStream` it
  opens (read-only, `FileShare.Read`), a seekable stream from its remaining length (measured from
  its current position, never negative), and a non-seekable stream is copied in chunks that stop as
  soon as the limit is exceeded. `MaxEncodedBytes` is independent of `MaxSamples` (a default of 1.25 GiB
  covers an uncompressed 32-bit file of `DefaultMaxSamples` samples), and no tighter bound is derived from
  `MaxSamples`, because a
  valid FLAC stream (Rice-coded residuals) has no upper bound on encoded bytes per decoded sample.
  The decoders instead enforce the exact `MaxSamples` limit while decoding: `WavDecoder` before
  allocating its output, and `FlacDecoder` on the declared total and then as each frame's samples
  are appended, growing its output buffer in steps never beyond the cap. A seekable stream positioned at
  or past its end yields no bytes and is rejected by the container sniffing as neither WAV nor FLAC.
  Then reads all bytes, detects the container from its magic
  (`RIFF`/`WAVE`, or `fLaC` optionally behind an ID3v2 tag), and delegates to `WavDecoder` or
  `FlacDecoder`. The decoders enforce `MaxSamples` on the decoded sample count, and
  `VerifyChecksums` is passed to the FLAC decoder (it has no effect on WAV)
- **ReadMono(path | stream, options)**: `Read` followed by `AudioClip.ToMono` (which uses
  `AudioResampler.DownMix`), returning the mono samples with the file's native sample rate. It
  repeats every error of `Read`, unchanged.

**Thread safety**: the class is static and stateless, so concurrent reads of different inputs are
safe; a single stream must not be shared between concurrent reads. A stream read starts at the
stream's current position, not at its start.

**Error Handling**: `FileNotFoundException` for a missing file or a directory path; `InvalidDataException` for data
that is neither WAV nor FLAC or that is malformed; `NotSupportedException` for a WAV encoding that
is not supported; `ArgumentNullException` for a null stream or a null path, `ArgumentException` for an empty or
whitespace path; `IOException` (for example a file locked by another process) and
`UnauthorizedAccessException` propagate unwrapped from the file open or read;
`ArgumentOutOfRangeException` for a negative `MaxSamples` or `MaxEncodedBytes`. The negative-option exception
reports `options.MaxSamples` or `options.MaxEncodedBytes` as its parameter name (the faulty value), and a
path-based read checks that the file exists first, so a negative `MaxSamples` with a missing path
reports `FileNotFoundException` (a stream read has no existence check, so it reports the negative
value directly). Path-based reads prefix the path to the message of decoder
failures and of byte-cap failures; stream reads have no path and so add no prefix.

**Dependencies**: `WavDecoder`, `FlacDecoder`, `AudioClip`, `AudioResampler`.

**Callers**: `ChatterboxTurboReferenceVoice.FromFile` and any host application.
