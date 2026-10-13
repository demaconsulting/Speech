### AudioClip

**Purpose**: Carry decoded audio as plain library-owned data.

**Data Model**: An immutable sealed record of interleaved `float` `Samples` in [-1, 1], the
`SampleRate`, the `ChannelCount` and the source `BitsPerSample`. `FrameCount` and `Duration` are
derived. `BitsPerSample` is informational only: it is neither validated nor used by any member.
`FrameCount` is `Samples.Length / ChannelCount` with integer division, so a sample count that is not a
multiple of the channel count truncates: the stray trailing samples are not counted in `FrameCount`
or `Duration` and are dropped by `ToMono` (but still converted by `ToPcm16`). Because `BitsPerSample`
is not validated, a `with { BitsPerSample = ... }` expression accepts any value; only `Samples`,
`SampleRate` and `ChannelCount` are guarded.
The `Samples` array is shared, not copied, so callers must not modify it and thread safety depends
on callers (the clip is safe to share while nobody writes to the array).

The record declares explicit properties backed by private fields, initialized from the primary
constructor parameters through validating helpers, with validating `init` accessors. A
compiler-generated positional property would accept any value in a `with` expression; the explicit
`init` accessors make a `with` expression run the same validation as the constructor.

**Key Methods**:

- **ToMono()**: down-mix by averaging channels through `AudioResampler.DownMix`; a mono clip
  yields a copy, and a NaN sample makes the mono frame that contains it NaN (incidental, not a
  guaranteed contract)
- **ToPcm16()**: convert to signed 16-bit samples by scaling by 32768 and rounding to nearest
  (ties to even), clamping to [-32768, 32767] so +1.0 becomes 32767, and mapping NaN to zero

**Error Handling**: The constructor and `with` expressions validate: `Samples` null throws
`ArgumentNullException`; `SampleRate` or `ChannelCount` of zero or less throws
`ArgumentOutOfRangeException` (so `Duration` and `ToMono` cannot divide by zero). An empty sample
array is valid and has zero duration. The record keeps reference equality for `Samples`.

**Dependencies**: `AudioResampler`.

**Callers**: `AudioFileReader`, `WavDecoder`, `FlacDecoder`, `AudioResampler`.
