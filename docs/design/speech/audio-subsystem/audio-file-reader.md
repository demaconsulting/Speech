### AudioFileReader

**Purpose**: Decode WAV and FLAC audio files or streams into `AudioClip` values with managed code
only, so hosts and models can accept either format without a native decoder dependency.

**Data Model**: A static class with no state. `SupportedExtensions` lists `.wav` and `.flac`.
`AudioFileReadOptions` carries `VerifyChecksums` (default `true`) and `MaxSamples` (default
`DefaultMaxSamples`, 2^28 samples across channels) as init-only settings.

**Key Methods**:

- **Read(path | stream, options)**: reads all bytes, detects the container from its magic
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
