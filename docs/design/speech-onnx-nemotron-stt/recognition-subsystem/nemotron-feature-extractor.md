### NemotronFeatureExtractor

**Purpose**: Convert 16 kHz mono audio into the 128-band log-mel feature frames the Nemotron
encoder was trained on, reproducing the inference path of NeMo's `FilterbankFeatures` with this
model's settings, incrementally, so that a stream fed in arbitrary pieces produces exactly the
frames a one-shot computation of the whole signal would.

**Data Model**: A streaming framing buffer of pre-emphasized samples (the padded signal from the
next frame start), the preemphasis carry (previous raw sample), the number of raw samples
accepted, a flag recording whether the left pad has been placed, and a queue of completed feature
frames (128 floats each). Constants: `SampleRate` 16000, `MelBands` 128, `FftSize` 512,
`WindowLength` 400, `HopLength` 160, `PreEmphasis` 0.97, and `LogEpsilon` `2^-24`
(5.9604644775390625E-08, NeMo's default log guard and the export's `log_eps`); the center pad is a
constant (zero) pad of `FftSize / 2` = 256 samples. The 400-sample **symmetric** Hann window
(`0.5 - 0.5 cos(2 pi n / 399)`, NeMo's `periodic_window=False`) is centered inside the 512-point
FFT frame (offset 56). The 128 Slaney-scale, Slaney-normalized mel filters over the 257 FFT bins
(0 to 8000 Hz) are built once.

**Key Methods**:

- **Accept(samples)**: on the first non-empty input places the left pad of 256 zeros, so frame 0
  is centered on the first sample; applies preemphasis (`y[n] = x[n] - 0.97 x[n-1]`,
  `y[0] = x[0]`); and emits every frame whose 512 samples are available. Frame `i` is centered on
  input sample `160 i` and is emitted as soon as the 256 samples after its center have arrived, so
  frames stream out during input with no further wait. Each frame is the 512-point FFT of the
  windowed samples, its power spectrum mapped through the mel filters, as `log(mel + 2^-24)`, with
  no further normalization.
- **FrameCount**: the number of frames currently queued.
- **Dequeue(count, destination)**: copies `count` queued frames out in row-major order.
- **Flush()**: appends the right pad of 256 zeros and emits the remaining frames, giving
  `len / 160 + 1` frames in total for `len` accepted samples, as NeMo does; does nothing when no
  audio was accepted. It is meant for one-shot extraction: the streaming recognition engine never
  calls it and completes its chunks with dithered silence audio instead, as a live stream would
  continue.
- **Reset()**: clears all state, returning to the start-of-stream state.

**Error Handling**: no exception is expected from normal operation; `Dequeue` throws
`ArgumentOutOfRangeException` when asked for more frames than are queued, and an undersized
destination is a programming error surfaced by the underlying span operations.

**Dependencies**: `System.Numerics` and `System.Math` only; no ONNX Runtime dependency, so the
unit is verified against golden values - all frames of four signals, derived independently from
the NeMo formulas - with no model files.

**Callers**: `OnnxNemotronRecognitionEngine` accepts dithered samples (real audio and padding
silence) into the extractor and dequeues 56-frame chunks; its `SampleRate`, `HopLength`, and
`MelBands` constants are used by the engine, and `SampleRate` is the endpoint detector's default
sample rate.
