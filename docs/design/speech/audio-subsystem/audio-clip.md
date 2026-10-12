### AudioClip

**Purpose**: Carry decoded audio as plain library-owned data.

**Data Model**: An immutable sealed record of interleaved `float` `Samples` in [-1, 1], the
`SampleRate`, the `ChannelCount` and the source `BitsPerSample`. `FrameCount` and `Duration` are
derived.

**Key Methods**:

- **ToMono()**: down-mix by averaging channels through `AudioResampler.DownMix`
- **ToPcm16()**: convert to signed 16-bit samples, clamping out-of-range values and mapping NaN
  to zero

**Error Handling**: The constructor and `with` expressions validate: `Samples` null throws
`ArgumentNullException`; `SampleRate` or `ChannelCount` of zero or less throws
`ArgumentOutOfRangeException` (so `Duration` and `ToMono` cannot divide by zero). An empty sample
array is valid and has zero duration. The record keeps reference equality for `Samples`.

**Dependencies**: `AudioResampler`.

**Callers**: `AudioFileReader`, `WavDecoder`, `FlacDecoder`, `AudioResampler`.
