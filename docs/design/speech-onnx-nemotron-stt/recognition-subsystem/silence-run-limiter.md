### SilenceRunLimiter

**Purpose**: Prevent the Nemotron model's failure mode after long leading digital silence or very
low-level noise, where it emits nothing or garbage. The limiter truncates long quiet runs to a
short fixed length: that keeps some context for the model while preventing the failure, and it
never removes speech. `DitherNoise`, its companion in this unit, then adds a tiny deterministic
noise so digital silence does not produce an exactly-zero log-mel input.

**Data Model**: `SilenceRunLimiter` is a stateful streaming limiter that works on 20 ms frames
(320 samples). A frame is *quiet* when its RMS is below the current threshold. Quiet runs longer
than 400 ms (20 frames) are truncated to 400 ms; loud frames pass through unchanged. The
threshold is adaptive: until calibrated it is the default `0.003`; after the first 20 frames
(400 ms) it becomes 3.5 times the 15th percentile of those frames' RMS values, clamped to the
range 0.0015 to 0.006. Audio keeps flowing during calibration (calibration frames are passed
through the same truncation rules with the default threshold, never delayed or withheld). A
partial trailing frame is held until the next call or `Flush`. Counters: `Threshold`,
`IsCalibrated`, `QuietRunSamples` (the current quiet run measured on the **input** clock,
including dropped samples), `DroppedSamples`, and `LoudSamples`. The constants come from the
internal `SilenceRunLimiterOptions` record; adaptation can be disabled, which keeps the default
threshold.

`DitherNoise` holds a seeded Gaussian noise source: `DefaultAmplitude` 1e-5 (standard deviation)
and `DefaultSeed` 20260101.

**Key Methods**:

- **Process(input, output)**: appends the limited audio for `input` to `output`; equivalent
  output regardless of how the audio is split across calls.
- **Flush(output)**: emits the held partial frame.
- **Reset()**: restores the initial, uncalibrated state.
- **DitherNoise.Apply(samples)**: adds Gaussian noise of the configured amplitude in place;
  an amplitude of zero leaves samples unchanged; the same seed yields the same sequence.
- **DitherNoise.Reset()**: restarts the noise sequence from the seed.

**Design Decision (D2)**: the limiter's calibration is deliberately kept internal to the
recognition engine. The Speech library's `RecognitionSessionState.Starting` means the audio
device is starting, whereas calibration is content-driven and occurs on the audio stream, so it
cannot map to a session state transition without a Speech contract change.

**Error Handling**: no exception is expected from normal operation.

**Dependencies**: `System.Math` only; no ONNX Runtime dependency.

**Callers**: `OnnxNemotronRecognitionEngine` processes every `AcceptSamples` input through the
limiter, applies the dither to the limiter output, reads `LoudSamples` and `QuietRunSamples` to
detect speech and input-clock quiet, and resets or flushes them with the engine.
