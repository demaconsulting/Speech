### NemotronEncoder

**Purpose**: Run the Nemotron cache-aware streaming encoder through ONNX Runtime, over the
`INemotronEncoder` seam (`HiddenSize` 1024, `Encode`, `Reset`) that lets the engine's chunking,
endpointing, and flush logic be unit tested with a managed fake. `NemotronEncoder` is the
ONNX-backed implementation and one of the two types in the package that run ONNX Runtime
inference.

**Data Model**: Holds the encoder `InferenceSession` (owned unless the caller says otherwise),
the language id chosen at construction, and the three cache tensors (`cache_last_channel`,
`cache_last_time`, `cache_last_channel_len`) as native `OrtValue`s. The caches stay native and
are fed back as the next call's inputs without copying; the previous call's outputs are released
only after the next call has consumed them.

**Key Methods**:

- **NemotronEncoder(session, languageId, ownsSession)**: takes the session and language id;
  throws `ArgumentNullException` for a null session.
- **Encode(features, frameCount, out outputFrames)**: feeds six inputs - `audio_signal`
  [1, 65, 128], `length`, `cache_last_channel` [1, 24, 56, 1024], `cache_last_time`
  [1, 24, 1024, 8], `cache_last_channel_len`, and `lang_id` - and reads five outputs: the
  encoder frames, their length, and the three `*_next` caches. Returns the encoded frames and
  their count.
- **Reset()**: returns to zero-initialized caches.
- **RunProbeInference(session)** (static): runs one representative inference on a session
  without taking ownership of it, so `OnnxExecutionProviderSelector.Create` can reject an
  accelerated candidate that loads but cannot run the graph.
- **Dispose()**: releases the caches and, when owned, the session.

**Error Handling**: the constructor throws `ArgumentNullException` for a null session; `Encode`
and `Reset` throw `ObjectDisposedException` after disposal; ONNX Runtime exceptions propagate to
the engine and from there to Speech's dedicated worker, which converts them into a faulted
recognition session.

**Dependencies**: `Microsoft.ML.OnnxRuntime` (`InferenceSession`, `OrtValue`);
`NemotronFeatureExtractor.MelBands` and the `NemotronEngineOptions` frame constants.

**Callers**: `OnnxNemotronRecognitionEngine` (through `INemotronEncoder`) encodes every chunk
through it and resets and disposes it with the engine; `OnnxNemotronMultilingualRecognitionModel
.CreateBackend` constructs it.
