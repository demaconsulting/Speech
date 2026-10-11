### OnnxChatterboxTurboNetworks

**Purpose**: Own the four ONNX Runtime `InferenceSession` objects (speech encoder, token embedder,
language model, conditional decoder) and expose them through `IChatterboxTurboNetworks`, so the
engine never touches tensors or session options directly.

**Data Model**: Four sessions, the language-model key/value cache shape (default 16 heads of
dimension 64, overridden by the graph's metadata when present), and a static
`OnSessionCreated` hook used only by tests to observe sessions.

**Key Methods**:

- **Constructor**: creates the sessions with the CPU provider and the configured intra-op thread
  count (default derived from the processor count with a ceiling); if any step fails, already-created
  sessions are disposed before rethrowing
- **Encode(samples)**: runs the speech encoder, binding outputs by name or, when names are absent,
  by position
- **Embed(ids)**: runs the token embedder
- **Step(...)**: runs one language-model step with the cached `past_key_values.*`/`present.*`
  tensors and returns the last logits
- **Decode(tokens, conditioning)**: runs the conditional decoder and returns the waveform
- **Dispose()**: disposes all sessions

**Error Handling**: `FileNotFoundException` for a missing graph; invalid thread counts throw
`ArgumentOutOfRangeException`; ONNX Runtime exceptions propagate after cleanup.

**Dependencies**: `Microsoft.ML.OnnxRuntime`.

**Callers**: `OnnxChatterboxTurboEnglishSynthesisModel.CreateBackend`.
