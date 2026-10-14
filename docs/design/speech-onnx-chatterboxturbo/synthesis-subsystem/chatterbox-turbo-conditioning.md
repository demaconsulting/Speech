### ChatterboxTurboConditioning

**Purpose**: The shallowly immutable bundle of speech-encoder outputs for one reference clip. The engine computes it
once per (voice, mood) and reuses it for every utterance spoken with that clip, so the expensive encoder
graph never runs twice for the same clip.

**Data Model**: An internal sealed class with immutable state, constructed from six values. The
constructor copies every array it receives, so the caller's arrays can change afterwards without
affecting the instance, and each array property exposes the copy as `ReadOnlyMemory<T>` so consumers
cannot modify it:

- `ConditioningEmbeddings` (`ReadOnlyMemory<float>`): the flattened conditioning embeddings,
  `ConditioningLength` x embedding size, prepended to the text embeddings of the language-model prompt
- `ConditioningLength` (`int`): the number of conditioning positions
- `PromptTokens` (`ReadOnlyMemory<long>`): the speech prompt tokens that open the decoder input
- `SpeakerEmbeddings` (`ReadOnlyMemory<float>`): the flattened speaker embedding vector given to the decoder
- `SpeakerFeatures` (`ReadOnlyMemory<float>`): the flattened speaker feature frames given to the decoder
- `SpeakerFeatureFrames` (`int`): the number of speaker feature frames

**Key Methods**: The constructor `(conditioningEmbeddings, conditioningLength, promptTokens,
speakerEmbeddings, speakerFeatures, speakerFeatureFrames)` takes the six values above, rejects a null
array, and stores copies. There are no other methods. The only producer is
`OnnxChatterboxTurboNetworks.Encode`, which builds the instance from the encoder graph's outputs.

**Error Handling**: `ArgumentNullException` for a null array argument; nothing else is validated. Because
the state is copied on construction and exposed read-only, one instance is safely shareable across
utterances and threads.
**Dependencies**: N/A - the record uses no other unit, subsystem, OTS item or shared package.

**Callers**: `OnnxChatterboxTurboNetworks` (`Encode` produces it and `Decode` consumes it through
`IChatterboxTurboNetworks`) and `OnnxChatterboxTurboSynthesisEngine` (caches it per voice and mood, and reads
the embeddings and prompt tokens when it assembles a generation).
