### OnnxChatterboxTurboSynthesisEngine

**Purpose**: The `ISynthesisBackend` implementation for Chatterbox Turbo. It assembles the language
model prompt, runs the bounded autoregressive loop and decodes the speech tokens into audio.

**Data Model**: The injected `IChatterboxTurboNetworks`, tokenizer, voices and options, plus a
per-voice cache of `ChatterboxTurboConditioning` computed on first use. Constants: output rate
24000 Hz, start token 6561 (never embedded), stop token 6562, silence token 4299 (appended three
times to the decoder input) and embedding size 1024.

**Key Methods**:

- **Generate(text, speed, speakerId)**: returns empty audio without network calls for empty text;
  otherwise tokenizes and embeds the text ids, prepends the voice conditioning, steps the language
  model, selects tokens with `ChatterboxTurboTokenSelector`, stops at the stop token or at
  `min(maxNewTokens, ceil(textIds * maxAudioTokensPerTextToken) + 32)`, then decodes the prompt
  tokens, the speech tokens and trailing silence. Hitting the bound sets `HitTokenLimit`; it never
  throws. An out-of-range speaker id falls back to the first voice. The `speed` argument is not
  used by the model.
- **Dispose()**: disposes the networks; later `Generate` throws `ObjectDisposedException`

**Error Handling**: null and invalid constructor arguments throw argument exceptions; runtime
failures from the networks propagate.

**Dependencies**: `IChatterboxTurboNetworks`, `ByteLevelBpeTokenizer`, `ChatterboxTurboTokenSelector`.

**Callers**: `OnnxChatterboxTurboEnglishSynthesisModel.CreateBackend` and Speech's synthesis session.
