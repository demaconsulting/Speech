### ChatterboxTurboConditioning

**Purpose**: The immutable bundle of speech-encoder outputs for one reference clip. The engine computes it
once per (voice, mood) and reuses it for every utterance spoken with that clip, so the expensive encoder
graph never runs twice for the same clip.

**Data Model**: An internal sealed record with six fields:

- `ConditioningEmbeddings` (`float[]`): the flattened conditioning embeddings, `ConditioningLength` x
  embedding size, prepended to the text embeddings of the language-model prompt
- `ConditioningLength` (`int`): the number of conditioning positions
- `PromptTokens` (`long[]`): the speech prompt tokens that open the decoder input
- `SpeakerEmbeddings` (`float[]`): the flattened speaker embedding vector given to the decoder
- `SpeakerFeatures` (`float[]`): the flattened speaker feature frames given to the decoder
- `SpeakerFeatureFrames` (`int`): the number of speaker feature frames

**Key Methods**: None; the unit is a pure data record with value-style construction and `with`
copying. It holds no behavior and validates nothing, because the only producer is
`OnnxChatterboxTurboNetworks.Encode`, which builds it from the encoder graph's outputs.

**Error Handling**: None; construction never throws. The arrays are shared by reference and are never
modified after construction by any consumer.

**Dependencies**: None.

**Callers**: `OnnxChatterboxTurboNetworks` (`Encode` produces it and `Decode` consumes it through
`IChatterboxTurboNetworks`) and `OnnxChatterboxTurboSynthesisEngine` (caches it per voice and mood, and reads
the embeddings and prompt tokens when it assembles a generation).
