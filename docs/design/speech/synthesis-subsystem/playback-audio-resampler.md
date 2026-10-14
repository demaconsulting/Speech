### PlaybackAudioResampler

**Purpose**: Convert one synthesized segment's mono audio, produced at a synthesis backend's
fixed rate, into the format a playback device requires - its resolved rate and channel count.
Keeping this conversion in a pure, dependency-free type mirrors `AudioFrameResampler`'s
recognition direction role and makes it exhaustively testable with plain float arrays.

**Data Model**: Immutable: the backend sample rate, the target (device) sample rate, and the
target channel count, all validated as positive at construction. No per-call state, so one
instance serves a whole operation.

**Key Methods**:

- **Resample(samples, sourceSampleRate, targetSampleRate)**: Produces
  `floor(length * target / source)` samples via linear interpolation, clamped to the last input
  sample at the boundary. Equal rates copy the input unchanged, so the identity case introduces no
  error at all. When downsampling, a small Hamming-windowed sinc lowpass filter runs first to
  attenuate above-target-Nyquist energy before decimation.
- **UpmixToChannels(samples, channelCount)**: Replicates each mono sample across every output
  channel, interleaved. A single required channel copies the input unchanged.
- **Convert(samples)**: Composes `Resample` then `UpmixToChannels` into the single operation the
  session needs for every segment it plays. When the target channel count is 1,
  `UpmixToChannels` is skipped entirely and the resampled result is returned directly, since
  `UpmixToChannels`'s own single-channel case would only make a redundant copy of an array
  `Convert` already owns exclusively.

**Error Handling**: A non-positive sample rate or channel count throws
`ArgumentOutOfRangeException`. Empty input returns an empty result rather than throwing, since a
pure-silence segment legitimately has no samples to convert.

**Dependencies**: `AudioSubsystem.WindowedSincLowpassFilter` for the downsampling anti-aliasing
lowpass stage, shared with `AudioFrameResampler`'s identical need in the recognition subsystem;
otherwise none beyond the Base Class Library.

**Callers**: `SynthesisSession.PlaySegment(...)` (via `GenerateAndOptionallyPlayAsync`).
