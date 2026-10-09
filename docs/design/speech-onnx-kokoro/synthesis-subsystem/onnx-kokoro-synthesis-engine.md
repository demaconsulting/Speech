### OnnxKokoroSynthesisEngine

**Purpose**: Implement the core library's `ISynthesisBackend` seam against Kokoro v1.0's bare ONNX
graph, run directly through ONNX Runtime with no bundled native inference engine - this package's
first raw-ONNX synthesis engine, unlike the sibling `DemaConsulting.Speech.Sherpa` package's
sherpa-onnx-backed engines. This is the only type in this package that runs ONNX Runtime
inference. It owns phonemization itself: unlike sherpa-onnx, which performs phonemization inside
its own native library, Kokoro's bare ONNX graph performs only the waveform-generation forward
pass, so this engine phonemizes text via its constructor-supplied `KokoroLexiconPhonemizer` and
converts the result to token ids via its constructor-supplied `KokoroPhonemeVocabulary` before
every inference call.

**Data Model**: Holds one loaded `InferenceSession`, a `KokoroPhonemeVocabulary`, a
`KokoroLexiconPhonemizer`, every installed voice's raw style-vector bytes keyed by speaker id
(`IReadOnlyDictionary<int, float[]>`), and a disposed flag. `SampleRate` (`24000`) and
`StyleVectorWidth` (`256`) are fixed constants confirmed from the model's own README and this
package's validation spike.

**Key Methods**:

- **OnnxKokoroSynthesisEngine(session, vocabulary, phonemizer, voiceStylesBySpeakerId)**: takes
  ownership of the supplied `InferenceSession` (disposed together with this engine). Throws
  `ArgumentNullException` for any null parameter and `ArgumentException` when
  `voiceStylesBySpeakerId` is empty. Called only from `OnnxKokoroEnglishSynthesisModel.CreateBackend`.
- **SampleRate**: the model's own fixed output rate (`24000` Hz); authoritative over the model's
  best-effort `PreferredAudioFormat` hint.
- **Generate(text, speed, speakerId)**: phonemizes `text`, converts the phoneme string to token
  ids, pads with `KokoroPhonemeVocabulary.PadTokenId` at both ends, selects the requested voice's
  style-vector row via `SelectStyleVector`, and runs a single ONNX Runtime forward pass over
  `input_ids`/`style`/`speed` tensors, returning the raw waveform output wrapped in an
  `EngineAudio`. Short-circuits to an empty `EngineAudio` without running the graph when
  phonemization produces zero tokens (empty text, or text whose every word is out of vocabulary).
- **SelectStyleVector(speakerId, tokenCount)** (private): selects the 256-element style-vector row
  for `speakerId` at the row index matching `tokenCount`, clamped to the voice's own row count -
  exactly mirroring the proven Python reference pipeline's own `voices[len(ids)]` lookup. Falls
  back to the first available voice for an unrecognized `speakerId` rather than throwing, as a
  defensive measure beyond `ISynthesisModel.ResolveSpeakerId`'s own guarantee.
- **Dispose()**: releases the loaded `InferenceSession`. Safe to call more than once.

**Error Handling**: Construction loads the model into ONNX Runtime and therefore throws when the
requested execution provider is unavailable or the model file is unusable; that exception
propagates out of the model's `CreateBackend` and the core library's
`DefaultSynthesisBackendFactory` to `SpeechSynthesizerFactory`, which converts it into the honest
unavailable fallback. `Generate` throws `ArgumentNullException` for null text, and operational
members throw `ObjectDisposedException` after disposal.

**Dependencies**: `Microsoft.ML.OnnxRuntime` (`InferenceSession`, `DenseTensor<T>`,
`NamedOnnxValue`); `KokoroPhonemeVocabulary` and `KokoroLexiconPhonemizer` from the
ModelManagementSubsystem; the core library's `ISynthesisBackend` and `EngineAudio` from the
_Speech SynthesisSubsystem Design_.

**Callers**: `OnnxKokoroEnglishSynthesisModel` constructs the engine from its `CreateBackend`
implementation; the core library's `SpeechSynthesizerEngine` owns the engine and passes it to
each `SynthesisSession` it creates.
