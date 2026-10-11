### ChatterboxTurboTokenSelector

**Purpose**: Choose the next speech token from the language model's logits.

**Data Model**: Stateless static class; `RepetitionPenalty` is 1.2.

**Key Methods**:

- **Select(logits, history)**: copies the logits, applies the repetition penalty to every token in
  the history (positive logits are divided, negative logits multiplied), and returns the argmax with
  ties resolved to the lowest id

**Error Handling**: empty or null input is handled without throwing (see the tests for the exact
result); no other exceptions are expected.

**Dependencies**: none.

**Callers**: `OnnxChatterboxTurboSynthesisEngine.Generate`.
