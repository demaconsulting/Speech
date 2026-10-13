### OnnxChatterboxTurboNetworks

**Purpose**: Own the four ONNX Runtime `InferenceSession` objects (speech encoder, token embedder,
language model, conditional decoder) and expose them through `IChatterboxTurboNetworks`, so the
engine never touches tensors or session options directly.

**Data Model**: Four sessions, one shared `RunOptions` instance used for every run, the resolved encoder
output names (`_encoderOutputs`, by name or by position), the language model's past-input names
(`_pastNames`, `past_key_values.*`) and output names (`_languageModelOutputs`, logits then
`present.*`), the language-model key/value cache layout read from the graph's metadata
(layer count from the `past_key_values.*.key` inputs, cache element type, head count and head width,
defaulting to 16 heads of dimension 64 when the graph leaves them dynamic), the language model's
`inputs_embeds` element type, the intra-op thread count (exposed as `IntraOpThreads`), the layer count
(`LayerCount`) and the cache element type (`KeyValueElementType`), and a static `OnSessionCreated` hook
used only by tests to observe sessions. `CreateFloat16Tensor` is the shared helper that builds the half-precision
tensors for embeddings and empty cache entries. A `LanguageModelState` (the private
`IChatterboxTurboLanguageModelState` implementation) owns one generation's cache; it is not owned or
disposed by the networks, so the caller disposes each state, and a state disposed after its networks
is safe (it only releases its own cache tensors).

`IChatterboxTurboNetworks` is the internal seam the engine uses (`Encode`, `Embed`, `BeginGeneration`,
`Decode`, `Dispose`); `IChatterboxTurboLanguageModelState` is one generation's cache holder with
`Step` and `Dispose`. Both exist so the generation loop can be tested against a fake.

**Key Methods**:

- **Constructor(speechEncoderPath, embedTokensPath, languageModelPath, conditionalDecoderPath,
  intraOpThreads)**: validates that `intraOpThreads` is positive (there is no default; the caller
  chooses it), then creates the four sessions with the CPU provider, the given intra-op thread count,
  one inter-op thread and sequential execution; if any step fails, already-created sessions and the
  shared run options are disposed before rethrowing
- **Encode(referenceAudio)**: runs the speech encoder, binding outputs by name or, when names are absent
  but exactly four outputs exist, by position, and returns a `ChatterboxTurboConditioning`
- **Embed(tokenIds)**: runs the token embedder and returns the embeddings as floats; zero token ids
  return an empty array without running the graph
- **BeginGeneration()**: returns an `IChatterboxTurboLanguageModelState` that owns one generation's cache
- **IChatterboxTurboLanguageModelState.Step(inputEmbeddings, sequenceLength, attentionLength,
  firstPosition)**: runs one language-model step with zero-length cache tensors on the first step and the
  previous step's `present.*` tensors afterwards (the previous presents are disposed only after the run that
  consumed them), converts the embeddings to float16 when the graph declares them so, and returns the last
  position's logits as floats. `inputEmbeddings` must contain `sequenceLength` times
  `OnnxChatterboxTurboSynthesisEngine.EmbeddingSize` values, `sequenceLength` must be positive (zero is
  rejected), `firstPosition` must not be negative and `attentionLength` must be at least `sequenceLength`
  (an `ArgumentOutOfRangeException` before any native call otherwise). A step on a state that was itself
  disposed, or whose owning networks were disposed, throws `ObjectDisposedException` before any native call
- **Decode(speechTokens, conditioning)**: runs the conditional decoder with the conditioning's speaker
  embeddings and speaker features and returns the waveform; an empty token array throws
  `ArgumentException` before any native call
- **ReadFloats(value)**: copies a float32 or float16 tensor into a float array
- **Dispose()**: disposes all sessions and the run options through `DisposeAll`, which disposes every
  item even when an earlier dispose throws, then rethrows the single failure or an `AggregateException`
  for several; it is idempotent. A state disposed after its networks is safe, and states
  must be disposed by the caller. `Encode`, `Embed`, `Decode` and
  `BeginGeneration` throw `ObjectDisposedException` after dispose, because a call on a disposed ONNX Runtime
  session faults the process instead of throwing. Float16 outputs of any graph are converted to floats by
  one shared half-to-float helper.

**Error Handling**: `FileNotFoundException` for a missing graph (checked before a session is created);
`ArgumentOutOfRangeException` for a non-positive thread count, a non-positive sequence length, a negative
first position or an attention length shorter than the sequence length; `ObjectDisposedException` for use
after dispose of the networks or of a language-model state; `ArgumentException` for an embeddings array of
the wrong length or an empty decode token array; `ArgumentNullException` for null arguments;
`InvalidOperationException` when a graph lacks the expected structure (no
`past_key_values` inputs, or encoder outputs that match neither the named nor the positional contract);
other ONNX Runtime exceptions propagate after cleanup. Not thread-safe; the engine serializes calls.

**Dependencies**: `Microsoft.ML.OnnxRuntime`; `OnnxChatterboxTurboSynthesisEngine.EmbeddingSize` (embedding
width); `ChatterboxTurboConditioning` (the encoder result and decoder input).

**Callers**: `OnnxChatterboxTurboEnglishSynthesisModel.CreateBackend`.
