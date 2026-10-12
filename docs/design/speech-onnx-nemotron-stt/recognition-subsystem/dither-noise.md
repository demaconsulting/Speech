### DitherNoise

**Purpose**: Add a tiny deterministic noise so digital silence does not produce an exactly-zero
log-mel input to the Nemotron model. It is the companion of `SilenceRunLimiter` in the Nemotron
recognition pipeline.

**Data Model**: `DitherNoise` holds a seeded Gaussian noise source: `DefaultAmplitude` 1e-5
(standard deviation) and `DefaultSeed` 20260101.

**Key Methods**:

- **Apply(samples)**: adds Gaussian noise of the configured amplitude in place; an amplitude of
  zero leaves samples unchanged; the same seed yields the same sequence.
- **Reset()**: restarts the noise sequence from the seed.

**Error Handling**: no exception is expected from normal operation.

**Dependencies**: `System.Math` only; no ONNX Runtime dependency.

**Callers**: `OnnxNemotronRecognitionEngine` applies the dither to the `SilenceRunLimiter` output
and resets it with the engine.
