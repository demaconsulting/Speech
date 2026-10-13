### AudioResampler

**Purpose**: Down-mix and resample floating-point audio to the form models require.

**Data Model**: A static class with no state. The resampler is a Hann-windowed sinc interpolator.
For an output sample at input position `c`, the cutoff is `min(1, outputRate / inputRate)` (so
downsampling limits the band to the output Nyquist frequency), the kernel half-width is
`16 / cutoff` input samples, and each input sample at distance `d` within the half-width has weight
`cutoff * sinc(pi * d * cutoff) * 0.5 * (1 + cos(pi * d / halfWidth))`. The kernel is normalized by
its weight sum so DC keeps unity gain at the clip edges; when a truncated edge kernel has almost no
weight (sum of 0.05 or less, which cannot be reached by the supported rate ratios and is
therefore untested) the nearest input sample is used rather than dividing by a tiny sum.

**Key Methods**:

- **DownMix(interleaved, channelCount)**: average channels per frame. Precondition:
  `channelCount` is positive. Postcondition: one output sample per whole frame; a trailing partial
  frame is dropped, and mono input is returned as a copy.
- **Resample(input, inputRate, outputRate)**: both rates are validated first (positive), then equal
  rates or empty input return a copy (empty for empty input); otherwise the output length is
  `floor(input.Length * outputRate / inputRate)` and each sample is the band-limited interpolation
  described above
- **ToMonoResampled(clip, outputRate)**: `clip.ToMono()` (which uses `DownMix`) then `Resample` at the
  clip's rate

**Error Handling**: `ArgumentOutOfRangeException` for a non-positive channel count or sample
rate; `ArgumentNullException` for a null clip.

**Dependencies**: `AudioClip` (for `ToMonoResampled`; `AudioClip.ToMono` in turn calls `DownMix`,
a deliberate mutual reference within the subsystem) and the Base Class Library.

**Callers**: `AudioClip`, `AudioFileReader`, `ChatterboxTurboReferenceVoice`.
