### OnnxKokoroSynthesisEngine

**Purpose**: Implement the core library's `ISynthesisBackend` and `IAnnotatedSynthesisBackend` seams against
Kokoro v1.0's bare ONNX graph, run directly through ONNX Runtime with no bundled native inference engine - this
package's first raw-ONNX synthesis engine, unlike the sibling `DemaConsulting.Speech.Sherpa` package's
sherpa-onnx-backed engines. This is the only type in this package that runs ONNX Runtime inference. It does
not phonemize itself: unlike sherpa-onnx, Kokoro's bare ONNX graph performs only the waveform-generation
forward pass, so the engine gets the token ids of every segment from its constructor-supplied
`KokoroPhonemePipeline` (shared English phonemizer, Kokoro symbols, Kokoro vocabulary) and runs the graph once
for every part the pipeline returns.

**Data Model**: Holds one loaded `InferenceSession`, a `KokoroPhonemePipeline`, every installed voice's raw
style-vector bytes keyed by speaker id (`IReadOnlyDictionary<int, float[]>`), and the disposal state (a disposed flag,
a count of the syntheses in progress and a lock that guards them). `SampleRate`
(`24000`) and `StyleVectorWidth` (`256`) are fixed constants confirmed from the model's own README and this
package's validation spike. `InputIdsTensorName` (`"input_ids"`), `StyleTensorName` (`"style"`), and
`SpeedTensorName` (`"speed"`) are shared `const string` fields used by both the generation and the static
`RunProbeInference` probe, so the two call sites can never name the graph's inputs differently from each other.

**Key Methods**:

- **OnnxKokoroSynthesisEngine(session, pipeline, voiceStylesBySpeakerId)**: takes ownership of the supplied
  `InferenceSession` (disposed together with this engine). Throws `ArgumentNullException` for any null parameter
  and `ArgumentException` when `voiceStylesBySpeakerId` is empty. Called only from
  `OnnxKokoroEnglishSynthesisModel.CreateBackend`.
- **SampleRate**: the model's own fixed output rate (`24000` Hz); authoritative over the model's best-effort
  `PreferredAudioFormat` hint.
- **Generate(text, speed, speakerId)**: speaks `text` as written - the pipeline is called without annotations, so
  every word has its default reading and nothing is normalized (the plain path, with no analysis). Throws
  `ArgumentNullException` for a null text.
- **GenerateAnnotated(text, annotations, speed, speakerId)**: speaks `text` with the annotations the session
  produced for the segment (offsets relative to `text`): the `en.norm`, `en.spell`, `en.reading` and `en.pron`
  kinds of Text.En are used by the phonemizer and every other kind is ignored. Throws `ArgumentNullException`
  for a null text or null annotations.
- **Synthesize(text, annotations, speed, speakerId)** (private, shared by both): asks the pipeline for the token
  id parts of the text, and for each part pads the ids with `KokoroPhonemeVocabulary.PadTokenId` at both ends,
  selects the requested voice's style-vector row for that part's own token count via `SelectStyleVector`, runs
  one ONNX Runtime forward pass over the shared `InputIdsTensorName`/`StyleTensorName`/`SpeedTensorName`
  tensors, and joins the waveforms of the parts in order into one `BackendAudio`. Returns an empty
  `BackendAudio` without running the graph when the pipeline returns no part (empty text, or text with no sound
  in this voice). The model is the int8 (`model_quantized`) graph because the fp16 graph overflows to NaN (heard
  as silence) for roughly one in ten inputs on the CPU execution provider, whereas the int8 graph produced none
  in a 150-sentence probe.
- **SelectStyleVector(speakerId, tokenCount)** (private): selects the 256-element style-vector row for
  `speakerId` at the row index matching `tokenCount`, clamped to the voice's own row count - exactly mirroring
  the proven Python reference pipeline's own `voices[len(ids)]` lookup. Falls back to the first available voice
  for an unrecognized `speakerId` rather than throwing, as a defensive measure beyond
  `ISynthesisModel.ResolveSpeakerId`'s own guarantee.
- **RunProbeInference(session)** *(internal static)*: runs one representative, roughly
  sentence-length inference (40 phoneme tokens, padded) directly against a caller-supplied
  session that has not yet been wrapped by this class, over the same shared tensor names
  `Generate` uses, letting `OnnxExecutionProviderSelector.Create` detect a candidate execution
  provider that constructs successfully but fails on the actual graph at `Run()` time (observed
  with DirectML and the `ConvTranspose` operator) before accepting that candidate. The probe token
  id is obtained at call time from a fresh `KokoroPhonemeVocabulary.ToTokenIds("a")` rather than a
  hard-coded numeric literal, so the probe is guaranteed to use a genuinely in-vocabulary token
  (verified against the real vocabulary, not merely asserted in a comment); it throws
  `InvalidOperationException` if `"a"` is somehow not in the embedded vocabulary, which would
  indicate a corrupt or incompatible embedded `tokenizer.json` resource, a condition this probe is
  not designed to recover from. The probe's own content is otherwise irrelevant - only whether the
  execution provider can run the graph at all matters. Propagates `OnnxRuntimeException` for a
  provider that cannot run this model, matching `OnnxExecutionProviderSelector.Create`'s expected
  "try the next candidate" signal.
- **Dispose()**: stops new runs (they throw `ObjectDisposedException`), waits for the runs in progress to finish, then
  releases the loaded `InferenceSession` exactly once. Safe to call more than once and from several threads at once;
  each call returns after the session is released. Every synthesis holds a lease (a counter under a lock) on the
  session for its whole duration, which is what the disposal waits for. It must not be called from inside a run on the
  same thread, which would wait for itself.

**Error Handling**: Construction loads the model into ONNX Runtime and therefore throws when the
requested execution provider is unavailable or the model file is unusable; that exception
propagates out of the model's `CreateBackend` and the core library's
`DefaultSynthesisBackendFactory` to `SpeechSynthesizerFactory`, which converts it into the honest
unavailable fallback. `Generate` and `GenerateAnnotated` throw `ArgumentNullException` for null arguments, and
operational members throw `ObjectDisposedException` after disposal. A word that the pronunciation dictionary
does not have is never an error and never dropped (the pipeline pronounces it from its spelling). `RunProbeInference`
propagates
`OnnxRuntimeException` for an unusable candidate session (the signal
`OnnxExecutionProviderSelector.Create` expects to try the next candidate) and
`InvalidOperationException` for the (not expected to occur) case where the embedded vocabulary
does not recognize its own probe character.

**Accepted Residual Risk (NaN output)**: the generation does not detect or retry a NaN waveform; the
engine runs the model exactly once per part. The int8 graph produced no NaN output in a 150-sentence
probe, so the earlier detect-and-retry logic (needed for the fp16 graph) was removed. A rare NaN
result, if it ever occurs, would be heard as silence for that utterance. This risk is accepted
for the benefit of simpler, deterministic synthesis.

**Dependencies**: `Microsoft.ML.OnnxRuntime` (`InferenceSession`, `DenseTensor<T>`,
`NamedOnnxValue`); `KokoroPhonemePipeline` (same subsystem) and `KokoroPhonemeVocabulary`
(ModelManagementSubsystem, used by the probe and for the pad token); the core library's `ISynthesisBackend`,
`IAnnotatedSynthesisBackend`, `SegmentAnnotation` and `BackendAudio` from the *Speech SynthesisSubsystem
Design*.

**Callers**: `OnnxKokoroEnglishSynthesisModel` constructs the engine from its `CreateBackend`
implementation; the core library's `SpeechSynthesizerEngine` owns the engine and passes it to
each `SynthesisSession` it creates, which calls `GenerateAnnotated` for a segment with annotations and
`Generate` for one without. `RunProbeInference` is additionally called directly (as a
method group, with no engine instance) by `OnnxKokoroEnglishSynthesisModel.CreateBackend` and by
the `tools/KokoroOnnxBenchmark` development tool, both passing it to
`OnnxExecutionProviderSelector.Create` as the `validateSession` probe.
