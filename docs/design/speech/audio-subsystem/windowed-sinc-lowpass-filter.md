### WindowedSincLowpassFilter

**Purpose**: Provide the shared, direction-agnostic windowed-sinc Hamming lowpass FIR primitive
that both `RecognitionSubsystem.AudioFrameResampler` (capture direction) and
`SynthesisSubsystem.PlaybackAudioResampler` (playback direction) need immediately before their
own decimation step, so the anti-aliasing DSP math is written and tested once instead of
duplicated byte-for-byte in two direction-specific resamplers.

**Data Model**: Stateless static class; no instance fields. Operates purely on `float` sample
arrays and kernel coefficients, with no knowledge of channels, capture, or playback.

**Key Methods**:

- **BuildLowpassKernel(double cutoffRatio, int tapCount)**: Builds a normalized Hamming-windowed
  sinc kernel whose coefficients sum to `1.0`, for a given cutoff ratio (a fraction of the source
  signal's Nyquist frequency) and an odd tap count.
- **ApplyLowpassFilter(ReadOnlySpan&lt;float&gt;, float[])**: Applies a symmetric FIR kernel to a
  mono signal, replicating the nearest endpoint sample at the edges so the output length always
  matches the input length exactly.
- **DownsamplingFilterTapCount**: The odd-numbered FIR tap count (`33`) both resamplers use for
  their anti-aliasing filter.

**Error Handling**: `BuildLowpassKernel` throws `ArgumentOutOfRangeException` when the cutoff
ratio lies outside `(0, 1]` or the tap count is non-positive, and `ArgumentException` when the
tap count is even, because the kernel is only well-defined when symmetric around one exact center
sample. `ApplyLowpassFilter` throws `ArgumentNullException` for a null kernel.

**Dependencies**: None beyond the Base Class Library.

**Callers**: `RecognitionSubsystem.AudioFrameResampler`, `SynthesisSubsystem.PlaybackAudioResampler`.
