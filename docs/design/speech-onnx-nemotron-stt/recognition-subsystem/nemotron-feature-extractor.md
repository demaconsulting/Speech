### NemotronFeatureExtractor

**Purpose**: Convert 16 kHz mono audio into the 128-band log-mel feature frames the Nemotron
encoder was trained on, incrementally, so that a stream fed in arbitrary pieces produces exactly
the frames a one-shot computation of the whole signal would.

**Data Model**: A streaming sample buffer with the preemphasis carry, a queue of completed
feature frames (128 floats each), a count of frames produced, and a flag recording whether any
audio has been accepted. Constants: `SampleRate` 16000, `MelBands` 128, `FftSize` 512,
`WindowLength` 400, `HopLength` 160, `PreEmphasis` 0.97, and `LogEpsilon` 1e-10. The 400-sample
periodic Hann window is centered inside the 512-point FFT frame (offset 56). The 128 Slaney-scale
mel filters over the 257 FFT bins are built once.

**Key Methods**:

- **Accept(samples)**: applies preemphasis (0.97), tracks the center reflect pad of 256 samples
  on the left, and emits every frame that is complete; each frame is the 512-point FFT of the
  windowed samples, mapped through the mel filters, as `log(mel + 1e-10)`.
- **FrameCount**: the number of frames currently queued.
- **Dequeue(count, destination)**: copies `count` queued frames out in row-major order.
- **Flush()**: applies the right reflect pad of 256 samples so the final frames are emitted;
  input shorter than the pad is zero-extended, and no frames are produced when no audio was
  accepted.
- **Reset()**: clears all state, returning to the start-of-stream state.

**Error Handling**: no exception is expected from normal operation; invalid destination sizes in
`Dequeue` are programming errors surfaced by the underlying span operations.

**Dependencies**: `System.Numerics` and `System.Math` only; no ONNX Runtime dependency, so the
unit is verified against a golden signal with no model files.

**Callers**: `OnnxNemotronRecognitionEngine` accepts dithered samples into the extractor,
dequeues 56-frame chunks, and flushes it when finishing an utterance.
