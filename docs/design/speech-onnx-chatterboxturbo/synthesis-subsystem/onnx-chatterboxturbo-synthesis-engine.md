### OnnxChatterboxTurboSynthesisEngine

**Purpose**: The `ISynthesisBackend` implementation for Chatterbox Turbo. It assembles the language
model prompt, runs the bounded autoregressive loop and decodes the speech tokens into audio.

**Data Model**: The injected `IChatterboxTurboNetworks`, tokenizer, a private snapshot of the voice list
(copied at construction, so later changes to the caller's list do not affect the engine) and bounds, plus a
cache of `ChatterboxTurboConditioning` per (voice, mood) computed on first use. The conditioning is an
immutable copying value, so cached instances are shared safely across utterances. Constants: output rate
24000 Hz, start token 6561 (never embedded), stop token 6562, silence token 4299 (appended three
times to the decoder input) and embedding size 1024.

`ChatterboxTurboGenerationResult` is the value returned by `GenerateDetailed`: `Audio` (the 24 kHz
samples, empty when there is no speech), `HitTokenLimit` (true when a generation bound ended the
segment instead of the stop token) and `SpeechTokenCount` (the number of speech tokens generated).
`IChatterboxTurboNetworks` and `IChatterboxTurboLanguageModelState` are the internal seams to the
four graphs and to one generation's cache (see _OnnxChatterboxTurboNetworks_ design); the engine
disposes each state it begins, and the networks it was given on `Dispose`.

**Key Methods**:

- **Generate(text, speed, speakerId)**: returns `GenerateDetailed(text, speakerId).Audio`.
- **GenerateDetailed(text, speakerId)**: returns a `ChatterboxTurboGenerationResult` (audio, `HitTokenLimit`,
  `SpeechTokenCount`). Returns empty audio without network calls for empty text;
  otherwise tokenizes and embeds the text ids, prepends the voice conditioning, steps the language
  model, selects tokens with `ChatterboxTurboTokenSelector`, stops at the stop token or at
  `min(maxNewTokens, ceil(textIds * maxAudioTokensPerTextToken) + 32)` (computed in floating point and
  clamped, so a huge finite ratio cannot overflow), then decodes the prompt
  tokens, the speech tokens and trailing silence. The loop also tracks the last token and its run
  length; once the run reaches `maxConsecutiveSpeechTokenRepeats` (a configured value; the model
  options default it to 12, and 0 disables) that
  token is passed to the selector as blocked for the next step only. The stop token is never blocked.
  The selector's repetition penalty is tracked over the start token and every generated token.
  Hitting the bound sets `HitTokenLimit`; it never throws. An out-of-range speaker id falls back to
  the first voice. The `speed` argument is ignored (the model has no speed control), so any speed gives
  identical audio. Empty text, and text that holds only marker characters, return empty audio without
  calling the networks. Segment text may begin with the internal mood marker written by
  `ChatterboxTurboCapabilityProfile`: the engine reads it, strips every marker character, returns
  empty audio if no speech remains and tokenizes only the stripped text, so the marker never
  reaches the tokenizer or the speech. Conditioning is cached per (voice, mood) and computed on
  first use; the mood clip is used when the voice has it, otherwise (no mood, a voice without
  moods, or an unknown mood) the voice's base clip is used and shares the base cache entry. Each
  segment is a separate `Generate` call, so the stop bound and repeat cap apply per segment and
  each segment pays its own prefill cost.
- **Dispose()**: sets the disposed flag atomically (`Interlocked.Exchange`), so only the first call
  disposes; a second concurrent call returns immediately without waiting for the first to finish. The
  first call then, under the generation lock, disposes the networks; an in-flight `Generate` stops at
  its next step and later calls throw `ObjectDisposedException`
- **SampleRate**: always 24000 Hz, the conditional decoder's output rate

**Bounds**: the maximum token count, the per-text-token ratio and the repeat cap are constructor
arguments (configured values); their defaults (1024, 25 and 12) belong to `ChatterboxTurboModelOptions`.

**Threading**: calls are serialized with an internal lock, so concurrent `Generate` calls are safe
(they run one at a time); `Dispose` may be called from another thread.

**Error Handling**: null and invalid constructor arguments throw argument exceptions (a non-finite
`maxAudioTokensPerTextToken` throws `ArgumentOutOfRangeException`; a null voice list throws
`ArgumentNullException`); null text throws
`ArgumentNullException`; runtime failures from the networks propagate.

**Dependencies**: `IChatterboxTurboNetworks`, `ByteLevelBpeTokenizer`, `ChatterboxTurboTokenSelector`,
`ChatterboxTurboCapabilityProfile` (`ReadMoodMarker`), `ChatterboxTurboReferenceVoice` (voice and mood
clips) and `ChatterboxTurboConditioning`.

**Callers**: `OnnxChatterboxTurboEnglishSynthesisModel.CreateBackend` and Speech's synthesis session.
