### SherpaOnnxSynthesisEngine

**Purpose**: Implement the core library's `ISynthesisBackend` seam against the real sherpa-onnx
offline TTS API. This is the only type in this repository that calls speech-synthesis inference
APIs. It moved out of the core `Speech` library into this system, together with the concrete
models that construct it, so that the core library carries no sherpa-onnx dependency at all; its
namespace (`DemaConsulting.Speech.SynthesisSubsystem`) is unchanged by the move. The former
`SherpaOnnxSynthesisEngineFactory` was removed rather than moved: the core library's model-driven
`DefaultSynthesisBackendFactory` now asks each model to construct its own backend through
`ISynthesisModel.CreateBackend`, and each synthesis model in this system implements that member
by constructing this engine directly.

**Data Model**: Holds one loaded `OfflineTts`, its declared `SampleRate`, and a disposed flag.

**Key Methods**:

- **SherpaOnnxSynthesisEngine(config)**: Loads the `OfflineTts` from the supplied
  `OfflineTtsConfig` and records its declared output sample rate. Called only from the synthesis
  models' own `CreateBackend` implementations, which pass their own
  `BuildEngineConfig(installedModelDirectory)` result.
- **SampleRate**: The fixed rate the loaded model produces audio at; authoritative over the
  model's best-effort `PreferredAudioFormat` hint.
- **Generate(text, speed, speakerId)**: Calls the underlying `OfflineTts.Generate(text, speed,
  speakerId)` and wraps its output samples and rate in an `EngineAudio`.
- **Dispose()**: Releases the loaded `OfflineTts`. Safe to call more than once.

**Error Handling**: Construction loads the model into native memory and therefore throws when the
native runtime for the current platform is absent or the model files are unusable; that exception
propagates out of the model's `CreateBackend` and the core library's
`DefaultSynthesisBackendFactory` to `SpeechSynthesizerFactory`, which converts it into the honest
unavailable fallback. `Generate` throws `ArgumentNullException` for null text, and operational
members throw `ObjectDisposedException` after disposal.

**Dependencies**: The sherpa-onnx managed API (see _SherpaOnnx Design_); the core library's
`ISynthesisBackend` and `EngineAudio` from the _Speech SynthesisSubsystem Design_.

**Callers**: `SherpaOnnxVitsLibriTtsEnglishSynthesisModel` and
`SherpaOnnxKokoroEnglishSynthesisModel` construct the engine from their `CreateBackend`
implementations; the core library's `SpeechSynthesizerEngine` owns the engine and passes it to
each `SynthesisSession` it creates.
