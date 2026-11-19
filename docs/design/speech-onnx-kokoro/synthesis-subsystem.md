## SynthesisSubsystem Design

![SynthesisSubsystem Structure](KokoroSynthesisSubsystemView.svg)

### Overview

The SpeechOnnxKokoro SynthesisSubsystem supplies the real, raw-ONNX-backed implementation of the
core `Speech` library's public `ISynthesisBackend` seam (see *Speech SynthesisSubsystem Design*) and of its
annotated derivative `IAnnotatedSynthesisBackend` for Kokoro v1.0. Unlike the sibling
`DemaConsulting.Speech.Sherpa` package, which drives a bundled native inference engine (sherpa-onnx) that
performs its own phonemization internally, this subsystem runs Kokoro's bare ONNX graph directly through
ONNX Runtime and owns the conversion of text to phoneme token ids itself, entirely in managed code. The core
library's session, engine, Natural Language Audio Tag rendering, chunking, resampling, and composition logic
remain in the core library and drive this backend only through the two seams. It contains the following
units:

- **KokoroPhonemePipeline**: turns the text of one segment, with the annotations of the English text
  analysis when there are any, into Kokoro's phoneme token ids, in parts of at most 510 tokens
- **OnnxKokoroSynthesisEngine**: the real `ISynthesisBackend` and `IAnnotatedSynthesisBackend` over Kokoro
  v1.0's bare ONNX graph, running the graph once for every part

### Interfaces

The subsystem exposes no public API: `OnnxKokoroSynthesisEngine` is an `internal` class that implements the
core library's public `ISynthesisBackend` (`SampleRate`, `Generate`, `Dispose`) and `IAnnotatedSynthesisBackend`
(`GenerateAnnotated`) and produces the core library's equally public `BackendAudio` values - both interfaces
are public specifically so any package, not only this one, can supply its own implementation.
`KokoroPhonemePipeline` is an `internal` class with one method, `ToTokenIds(text, annotations)`, that returns
one token id sequence per part. The subsystem consumes the `Microsoft.ML.OnnxRuntime` managed API
(`InferenceSession`, `DenseTensor<T>`, `NamedOnnxValue`) and the sibling `DemaConsulting.Speech.Onnx` system's
`OnnxExecutionProviderSelector` (see *SpeechOnnx Design*) indirectly, through the model that constructs it;
the Phonemes.En `EnglishPhonemizer` and `MisakiRenderer` and the Speech library's `SegmentAnnotation`; and
`KokoroPhonemeVocabulary` from the ModelManagementSubsystem. It is constructed only by
`OnnxKokoroEnglishSynthesisModel.CreateBackend` (see *SpeechOnnxKokoro ModelManagementSubsystem Design*).

### Design

The core library's `DefaultSynthesisBackendFactory` holds no engine-specific knowledge: it forwards to the
selected model's internal `ISynthesisModel.CreateBackend`. The one synthesis model in this system implements
that member by loading an `InferenceSession` through `OnnxExecutionProviderSelector`, loading every
installed voice's raw style-vector file bytes, and constructing an `OnnxKokoroSynthesisEngine` from that
session together with a `KokoroPhonemePipeline` (over a fresh `KokoroPhonemeVocabulary`) and the voice
styles. The resulting engine is owned by the core library's `SpeechSynthesizerEngine` and passed to each
`SynthesisSession` it creates, which calls the generation from exactly one dedicated-worker call at a time;
the engine itself is not thread-safe by contract (an `InferenceSession` instance is not guaranteed
thread-safe for concurrent `Run` calls).

**Analyzed and plain generation.** The model supplies the shared English text analysis, so the session
analyzes the whole request, splits it into sentence-sized segments with the annotations aligned to them, and
calls `GenerateAnnotated(text, annotations, speed, speakerId)` for a segment that carries annotations and
`Generate(text, speed, speakerId)` for one that does not. Both end in the same path: the pipeline turns the
text into token id parts, and the engine runs the graph once for every part. The plain call has no analysis,
so it speaks every word with its default reading and does not normalize numbers (decision D12 of the
adoption: the session path always analyzes, the direct call is the plain path).

**Text to token ids.** `KokoroPhonemePipeline` phonemizes the text with the Phonemes.En `EnglishPhonemizer`
(which uses the annotations: the spoken replacement of a number or abbreviation, the chosen reading of a
heteronym), renders it as Kokoro's phoneme symbols with the `MisakiRenderer` (US profile, function words in
their weak forms) and converts the symbols through `KokoroPhonemeVocabulary`. A word that is not in the
pronunciation dictionary is pronounced from its spelling by the letter-to-sound model of the phonemizer, so no
word is dropped; only a character with no sound at all (an emoji) leaves nothing, and text with no sound
anywhere gives no part, which the engine answers with empty audio without running the graph.

**Long text.** The graph accepts at most 510 phoneme tokens per run. A segment that phonemizes to more is
split by the renderer between words, preferring a clause mark, into parts that each fit, and the engine runs
the graph once per part. Kokoro's style rows are indexed by the token count of the utterance, so the row is
chosen for every part from that part's own count, and the audio of the parts is joined in order. Nothing is
truncated.

**Output.** The engine returns the model's raw waveform at its own fixed `SampleRate` (24000 Hz), which is
authoritative over the model's best-effort `PreferredAudioFormat` hint; the core library resamples to the
playback device's format when they differ.

Constructing the engine loads the model into ONNX Runtime, so unusable model files or an unavailable
execution provider surface as an exception from `CreateBackend`. The core library's `SpeechSynthesizerFactory`
catches that exception and degrades it to the honest unavailable synthesizer.
