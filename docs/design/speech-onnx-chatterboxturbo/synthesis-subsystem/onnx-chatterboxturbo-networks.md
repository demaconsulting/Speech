### OnnxChatterboxTurboNetworks

**Purpose**: Own the four ONNX Runtime `InferenceSession` objects (speech encoder, token embedder,
language model, conditional decoder) and expose them through `IChatterboxTurboNetworks`, so the
engine never touches tensors or session options directly.

**Data Model**: Four sessions, the language-model key/value cache layout read from the graph's metadata
(layer count from the `past_key_values.*.key` inputs, cache element type, head count and head width,
defaulting to 16 heads of dimension 64 when the graph leaves them dynamic), the language model's
`inputs_embeds` element type, the intra-op thread count, and a static `OnSessionCreated` hook used only
by tests to observe sessions.

**Key Methods**:

- **Constructor(speechEncoderPath, embedTokensPath, languageModelPath, conditionalDecoderPath,
  intraOpThreads)**: validates that `intraOpThreads` is positive (there is no default; the caller
  chooses it), then creates the four sessions with the CPU provider, the given intra-op thread count,
  one inter-op thread and sequential execution; if any step fails, already-created sessions and the
  shared run options are disposed before rethrowing
- **Encode(referenceAudio)**: runs the speech encoder, binding outputs by name or, when names are absent
  but exactly four outputs exist, by position, and returns a `ChatterboxTurboConditioning`
- **Embed(tokenIds)**: runs the token embedder and returns the embeddings as floats
- **BeginGeneration()**: returns an `IChatterboxTurboLanguageModelState` that owns one generation's cache
- **IChatterboxTurboLanguageModelState.Step(inputEmbeddings, sequenceLength, attentionLength,
  firstPosition)**: runs one language-model step with zero-length cache tensors on the first step and the
  previous step's `present.*` tensors afterwards (the previous presents are disposed only after the run that
  consumed them), converts the embeddings to float16 when the graph declares them so, and returns the last
  position's logits as floats. `inputEmbeddings` must contain `sequenceLength` times
  `OnnxChatterboxTurboSynthesisEngine.EmbeddingSize` values and `attentionLength` must be at least
  `sequenceLength`
- **Decode(speechTokens, conditioning)**: runs the conditional decoder and returns the waveform
- **ReadFloats(value)**: copies a float32 or float16 tensor into a float array
- **Dispose()**: disposes all sessions and the run options

**Error Handling**: `FileNotFoundException` for a missing graph (checked before a session is created);
`ArgumentOutOfRangeException` for a non-positive thread count or an attention length shorter than the
sequence length; `ArgumentException` for an embeddings array of the wrong length; `ArgumentNullException`
for null arguments; `InvalidOperationException` when a graph lacks the expected structure (no
`past_key_values` inputs, or encoder outputs that match neither the named nor the positional contract);
other ONNX Runtime exceptions propagate after cleanup. Not thread-safe; the engine serializes calls.

**Dependencies**: `Microsoft.ML.OnnxRuntime`; `OnnxChatterboxTurboSynthesisEngine.EmbeddingSize` (embedding
width); `ChatterboxTurboConditioning` (the encoder result and decoder input).

**Callers**: `OnnxChatterboxTurboEnglishSynthesisModel.CreateBackend`.
