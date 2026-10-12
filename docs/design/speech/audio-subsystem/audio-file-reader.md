### AudioFileReader

**Purpose**: Decode WAV and FLAC audio files or streams into `AudioClip` values with managed code
only, so hosts and models can accept either format without a native decoder dependency.

**Data Model**: A static class with no state. `SupportedExtensions` lists `.wav` and `.flac`.
`AudioFileReadOptions` carries `VerifyChecksums` (default `true`) and `MaxSamples` (default
`DefaultMaxSamples`, 2^28 samples across channels) as init-only settings.

**Key Methods**:

- **Read(path | stream, options)**: first bounds the input by a byte cap derived from `MaxSamples`
  (five bytes per sample plus 1 MiB of slack, never above the maximum array length): a file is
  rejected from its `FileInfo.Length`, a seekable stream from its remaining length, and a
  non-seekable stream is copied in chunks that stop as soon as the cap is exceeded, so an oversized
  input is never fully read. Then reads all bytes, detects the container from its magic
  (`RIFF`/`WAVE`, or `fLaC` optionally behind an ID3v2 tag), and delegates to `WavDecoder` or
  `FlacDecoder`
- **ReadMono(path | stream, options)**: `Read` followed by `AudioResampler.DownMix`, returning the
  mono samples with the file's native sample rate

**Error Handling**: `FileNotFoundException` for a missing file; `InvalidDataException` for data
that is neither WAV nor FLAC or that is malformed; `NotSupportedException` for a WAV encoding that
is not supported; `ArgumentNullException`/`ArgumentException` for a null stream or an empty path;
`ArgumentOutOfRangeException` for a negative `MaxSamples`. Path-based reads prefix the path to the
message of decoder failures.

**Dependencies**: `WavDecoder`, `FlacDecoder`, `AudioClip`, `AudioResampler`.

**Callers**: `ChatterboxTurboReferenceVoice.FromFile` and any host application.
