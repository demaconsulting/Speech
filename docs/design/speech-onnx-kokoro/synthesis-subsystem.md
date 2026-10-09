## SynthesisSubsystem Design

![SynthesisSubsystem Structure](KokoroSynthesisSubsystemView.svg)

### Overview

The SpeechOnnxKokoro SynthesisSubsystem supplies the real, raw-ONNX-backed implementation of the
core `Speech` library's public `ISynthesisBackend` seam (see _Speech SynthesisSubsystem Design_)
for Kokoro v1.0. Unlike the sibling `DemaConsulting.Speech.Sherpa` package, which drives a
bundled native inference engine (sherpa-onnx) that performs its own phonemization internally, this
subsystem runs Kokoro's bare ONNX graph directly through ONNX Runtime and owns phonemization
itself, entirely in managed code, via the ModelManagementSubsystem's `KokoroLexiconPhonemizer` and
`KokoroPhonemeVocabulary` units. The core library's session, engine, Natural Language Audio Tag
rendering, chunking, resampling, and composition logic remain in the core library and drive this
backend only through `ISynthesisBackend`. It contains the following unit:

- **OnnxKokoroSynthesisEngine**: the real `ISynthesisBackend` over Kokoro v1.0's bare ONNX graph

### Interfaces

The subsystem exposes no public API: `OnnxKokoroSynthesisEngine` is an `internal` class that
implements the core library's public `ISynthesisBackend` (`SampleRate`, `Generate`, `Dispose`) and
produces the core library's equally public `EngineAudio` values - `ISynthesisBackend` is public
specifically so any package, not only this one, can supply its own implementation. It consumes
the `Microsoft.ML.OnnxRuntime` managed API (`InferenceSession`, `DenseTensor<T>`,
`NamedOnnxValue`) and the sibling `DemaConsulting.Speech.Onnx` system's
`OnnxExecutionProviderSelector` (see _SpeechOnnx Design_) indirectly, through the model that
constructs it. It is constructed only by `OnnxKokoroEnglishSynthesisModel.CreateBackend` (see
_SpeechOnnxKokoro ModelManagementSubsystem Design_).

### Design

The core library's `DefaultSynthesisBackendFactory` holds no engine-specific knowledge: it
forwards to the selected model's internal `ISynthesisModel.CreateBackend`. The one synthesis
model in this system implements that member by loading an `InferenceSession` through
`OnnxExecutionProviderSelector`, loading every installed voice's raw style-vector file bytes, and
constructing an `OnnxKokoroSynthesisEngine` from that session together with a fresh
`KokoroPhonemeVocabulary` and `KokoroLexiconPhonemizer`. The resulting engine is owned by the
core library's `SpeechSynthesizerEngine` and passed to each `SynthesisSession` it creates, which
calls `Generate` from exactly one dedicated-worker call at a time; the engine itself is not
thread-safe by contract (an `InferenceSession` instance is not guaranteed thread-safe for
concurrent `Run` calls).

`Generate` phonemizes the given text via `KokoroLexiconPhonemizer`, converts the resulting
phoneme string into token ids via `KokoroPhonemeVocabulary`, pads the id sequence with
`KokoroPhonemeVocabulary.PadTokenId` at both ends, selects the requested voice's style-vector row
indexed by token count (mirroring the proven Python reference pipeline's own
`voices[len(ids)]` lookup), and runs a single ONNX Runtime forward pass over `input_ids`,
`style`, and `speed` tensors, returning the model's raw waveform output at its own fixed
`SampleRate` (24000 Hz). Text that phonemizes to zero tokens (empty text, or text whose every
word is absent from the embedded lexicon) short-circuits to an empty `EngineAudio` without
running the ONNX graph. The engine's `SampleRate` is authoritative over the model's best-effort
`PreferredAudioFormat` hint, and the core library resamples to the playback device's format when
they differ.

Constructing the engine loads the model into ONNX Runtime, so unusable model files or an
unavailable execution provider surface as an exception from `CreateBackend`. The core library's
`SpeechSynthesizerFactory` catches that exception and degrades it to the honest unavailable
synthesizer.
