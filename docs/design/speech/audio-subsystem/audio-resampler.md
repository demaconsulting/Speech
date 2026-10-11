### AudioResampler

**Purpose**: Down-mix and resample floating-point audio to the form models require.

**Data Model**: A static class with no state. The resampler is a Hann-windowed sinc interpolator
with 16 lobes each side. When downsampling the kernel cutoff is scaled to the output Nyquist
frequency to prevent aliasing.

**Key Methods**:

- **DownMix(interleaved, channelCount)**: average channels per frame
- **Resample(input, inputRate, outputRate)**: return a copy at equal rates, otherwise a
  band-limited interpolation whose length is the input length scaled by the rate ratio
- **ToMonoResampled(clip, outputRate)**: `DownMix` then `Resample`

**Error Handling**: `ArgumentOutOfRangeException` for a non-positive channel count or sample
rate; `ArgumentNullException` for a null clip.

**Dependencies**: None beyond the Base Class Library.

**Callers**: `AudioClip`, `AudioFileReader`, `ChatterboxTurboReferenceVoice`.
