### ChatterboxTurboTokenSelector

**Purpose**: Choose the next speech token from the language model's logits.

**Data Model**: Stateless static class; `RepetitionPenalty` is 1.2.

**Key Methods**:

- **Select(`ReadOnlySpan<float> logits`, `IReadOnlySet<long> generated`, `int blockedToken = -1`)**: returns the
  chosen token id. `logits` are the vocabulary-sized logits of the last position and `generated` holds the
  distinct ids generated so far, including the start token (ids outside the vocabulary are ignored, because the
  single scan runs over the logits length and only reads the history set by membership). A single
  pass over the unblocked ids divides a generated id's positive logit by the penalty and multiplies its
  negative logit by it, then keeps the highest score; ties resolve to the lowest id. A NaN logit is treated as
  negative infinity, positive infinity stays positive infinity (even for a generated id), and the first
  unblocked id seeds the result, so all-NaN or all-negative-infinity input
  returns the lowest unblocked id. The optional `blockedToken` (-1 for none) is skipped for this call; a
  blocked stop token (6562) is ignored so speech can always end. If the only id of the vocabulary is blocked,
  that id is returned. The method is deterministic, stateless and thread-safe.

**Error Handling**: empty `logits` throw `ArgumentException` and a null `generated` throws
`ArgumentNullException`. Non-finite logits never throw and never recurse.

**Dependencies**: `OnnxChatterboxTurboSynthesisEngine.StopSpeechToken` (the id that is never blocked).

**Callers**: `OnnxChatterboxTurboSynthesisEngine.Generate`.
