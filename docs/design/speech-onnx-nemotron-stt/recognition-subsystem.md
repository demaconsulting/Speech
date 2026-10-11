## RecognitionSubsystem Design

![RecognitionSubsystem Structure](NemotronRecognitionSubsystemView.svg)

### Overview

The SpeechOnnxNemotronStt RecognitionSubsystem supplies the real, raw-ONNX-backed implementation of
the core `Speech` library's public `IRecognitionBackend` seam (see _Speech RecognitionSubsystem
Design_) for Nemotron 3.5 ASR streaming. Unlike the sibling `DemaConsulting.Speech.Sherpa`
package, which drives a bundled native inference engine, this subsystem runs the model's three
graphs directly through ONNX Runtime and owns the whole signal path itself in managed code. The
core library's session, engine, resampling, and result-buffering logic remain in the core library
and drive this backend only through `IRecognitionBackend`. It contains the following units:

- **OnnxNemotronRecognitionEngine**: the real `IRecognitionBackend`; also documents the
  ONNX-backed `NemotronEncoder` and its `INemotronEncoder` seam
- **NemotronFeatureExtractor**: the streaming log-mel front end
- **NemotronRnntGreedyDecoder**: RNN-T greedy search over the `IRnntNetwork` seam; also documents
  the ONNX-backed `NemotronRnntNetwork`
- **SilenceRunLimiter**: the stateful streaming leading-silence limiter; also documents its
  `DitherNoise` companion

### Interfaces

The subsystem exposes no public API: every type is `internal`. `OnnxNemotronRecognitionEngine`
implements the core library's public `IRecognitionBackend` (`AcceptSamples`, `TryDecode`,
`TryFlush`, `Reset`, `Dispose`) and produces the core library's `SpeechRecognitionResult` values.
It consumes the `Microsoft.ML.OnnxRuntime` managed API (`InferenceSession`, `OrtValue`) and the
sibling `DemaConsulting.Speech.Onnx` system's `OnnxExecutionProviderSelector` (see _SpeechOnnx
Design_) indirectly, through the model that constructs it. It is constructed only by
`OnnxNemotronMultilingualRecognitionModel.CreateBackend` (see _SpeechOnnxNemotronStt
ModelManagementSubsystem Design_).

### Design

The core library's default backend factory holds no engine-specific knowledge: it forwards to the
selected model's `IRecognitionModel.CreateBackend`. The recognition model implements that member
by building the three sessions and constructing the engine, which the core library's
recognition engine owns and passes to each recognition session; the backend is called from one
dedicated worker at a time and is not thread-safe by contract.

The signal path inside the engine is:

1. `SilenceRunLimiter` truncates long quiet runs (see `silence-run-limiter.md`)
2. `DitherNoise` adds seeded Gaussian noise (amplitude 1e-5) so digital silence never yields an
   exactly-zero log-mel input
3. `NemotronFeatureExtractor` produces 128-band log-mel frames
4. The engine groups frames into 56-frame chunks, prepends a 9-frame pre-encode cache (the
   previous chunk's last nine frames) to make a 65-frame encoder input, zero-padding a partial
   chunk
5. `NemotronEncoder` runs the cache-aware encoder, feeding its cache outputs back as the next
   call's inputs
6. `NemotronRnntGreedyDecoder` converts the encoder output into token ids
7. `NemotronVocabulary` detokenizes the tokens (see the ModelManagementSubsystem)

All tuning constants live in the internal `NemotronEngineOptions` record (`ChunkFrames` 56,
`PreEncodeFrames` 9, `DitherAmplitude` 1e-5, `EndpointQuietMs` 2500, `EndpointEmptyChunks` 3,
`FlushTailChunks` 1) together with the limiter options.

Constructing the sessions loads the model into ONNX Runtime, so unusable model files or an
unavailable execution provider surface as an exception from `CreateBackend`. The core library's
`SpeechRecognizerFactory` catches that exception and degrades it to the honest unavailable
recognizer.
