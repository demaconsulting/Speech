### AudioClip

**Purpose**: Carry decoded audio as plain library-owned data.

**Data Model**: An immutable sealed record of interleaved `float` `Samples` in [-1, 1], the
`SampleRate`, the `ChannelCount` and the source `BitsPerSample`. `FrameCount` and `Duration` are
derived.

**Key Methods**:

- **ToMono()**: down-mix by averaging channels through `AudioResampler.DownMix`
- **ToPcm16()**: convert to signed 16-bit samples, clamping out-of-range values and mapping NaN
  to zero

**Error Handling**: None beyond what the underlying helpers throw.

**Dependencies**: `AudioResampler`.

**Callers**: `AudioFileReader`, `WavDecoder`, `FlacDecoder`, `AudioResampler`.
