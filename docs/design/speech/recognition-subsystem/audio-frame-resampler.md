### AudioFrameResampler

**Purpose**: Convert one block of interleaved, multi-channel capture audio into the mono audio at
the model-declared `AudioFormat.SampleRate` that a recognition engine requires. Keeping this
conversion in a pure,
dependency-free type makes it exhaustively testable with plain float arrays.

**Data Model**: Immutable: the source sample rate, source channel count, and target sample rate,
all validated as positive at construction. No per-block state, so one instance serves a whole
session and is safe to share across threads.

**Key Methods**:

- **Convert(ReadOnlySpan&lt;float&gt;)**: Downmixes then resamples. Downmixing first means the
  interpolation runs over one signal rather than once per channel, which is both cheaper and
  removes any possibility of channels drifting out of alignment. Returns an empty array when the
  input contains no complete frame. A single-channel source skips the downmix step entirely and
  resamples the input directly, since `Resample` already copies or allocates as needed and a
  downmix pass would only duplicate the input. For a multi-channel source, the downmixed
  intermediate is written into an `ArrayPool<float>`-rented buffer rather than a newly allocated
  array, since it is read only by `Resample` immediately afterward and then discarded. This only
  removes that redundant downmix-buffer allocation: the final resampled result is still allocated,
  and (when downsampling) so is the FIR lowpass kernel that `Resample` builds fresh on every call.
- **DownmixToMono(ReadOnlySpan&lt;float&gt;, int channelCount)**: Averages each frame's channels,
  accumulating in double precision so a high channel count cannot lose low-level detail to
  repeated single-precision rounding. A trailing partial frame is discarded, since it has no
  defined average. A single-channel input is copied unchanged. Averaging values in `[-1.0, 1.0]`
  stays in range, so no clipping step is needed. Delegates its per-frame averaging loop to the
  destination-buffer overload below.
- **DownmixToMono(ReadOnlySpan&lt;float&gt;, int channelCount, Span&lt;float&gt; destination)**:
  Writes the same averaged frames as the array-returning overload into a caller-supplied buffer,
  letting a caller (such as `Convert`) supply an `ArrayPool<float>`-rented buffer for a purely
  transient result instead of allocating a new array. Throws `ArgumentException` when
  `destination` is shorter than the number of complete input frames.
- **Resample(ReadOnlySpan&lt;float&gt;, int sourceSampleRate, int targetSampleRate)**: Produces
  `floor(length * target / source)` samples, each the linear blend of the two input samples
  straddling its position, with the final positions clamped to the last input sample. Equal rates
  copy the input unchanged, so the identity case introduces no error at all. When downsampling, a
  small Hamming-windowed sinc lowpass filter runs first to attenuate above-target-Nyquist energy
  before decimation. The output length is computed in 64-bit arithmetic so a long block at a high
  rate cannot overflow the intermediate product.

**Error Handling**: A non-positive sample rate or channel count throws
`ArgumentOutOfRangeException`, because no meaningful conversion exists for it. Empty,
single-sample, and rounds-to-empty conversions all return an empty or clamped result rather than
throwing, since a capture device may legitimately deliver a very short block.

**Dependencies**: `AudioSubsystem.WindowedSincLowpassFilter` for the downsampling anti-aliasing
lowpass stage, shared with `PlaybackAudioResampler`'s identical need in the synthesis subsystem;
otherwise none beyond the Base Class Library.

**Callers**: `RecognitionSession`.

**Resampler quality is a deliberate, bounded trade-off.** Downmixing remains a straight
arithmetic mean and the upsampling path remains linear interpolation, keeping the implementation
small, reviewable, and dependency-free. The downsampling path now adds a small Hamming-windowed
sinc FIR lowpass filter immediately before decimation, closing the most harmful aliasing case
without turning this unit into a full polyphase DSP subsystem. Each captured block is still
converted independently, so interpolation restarts at every block boundary, but the new filter
removes much of the above-target-Nyquist energy that would otherwise fold into the speech band.
The design should be revisited only if a real model's measured recognition accuracy on
non-native-rate hardware is shown to require something stronger.
