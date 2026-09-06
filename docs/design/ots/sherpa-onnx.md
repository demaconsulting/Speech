<!-- cspell:ignore Kaldi -->
## SherpaOnnx

### Purpose

`org.k2fsa.sherpa.onnx` is used as the Speech library's local, offline speech-inference engine.
It was chosen so this repository could add real streaming speech-to-text without authoring an
ONNX inference pipeline, acoustic-model runtime, and endpoint detector from scratch. It is
developed by the Next-gen Kaldi team, runs entirely on the local machine with no network access
during recognition, and is published under a permissive license compatible with this library's
own.

### Features Used

- Managed streaming-recognition API: the online recognizer, its per-utterance audio stream, and
  the ready/decode/result/endpoint/reset polling protocol
- Managed configuration types describing a model's architecture, file locations, feature
  configuration (including its required input sample rate), and decoding options
- Per-platform native runtime packages carried transitively by the managed package, covering
  Windows, Linux, macOS, and Android targets

### Integration Pattern

The library references only the managed `org.k2fsa.sherpa.onnx` package from
`DemaConsulting.Speech.csproj`. It never references an `org.k2fsa.sherpa.onnx.runtime.{RID}`
package directly; those native runtime packages are declared as dependencies of the managed
package itself, so a consuming application restores the ones it needs transitively rather than
this library selecting or bundling any of them. A consumer that trims or excludes the native
runtime for its target platform still composes successfully - recognition simply reports itself
unavailable, per architecture.md's requirement that a missing native runtime "must degrade the
same honest way as a missing model file, never crash."

Sherpa-onnx types are confined to two places. Each model's backing class produces its own engine
configuration through an internal member of `IRecognitionModel`, matching architecture.md's
"sherpa-onnx configuration for its own model architecture" responsibility. The
RecognitionSubsystem then consumes that configuration behind its internal
`IRecognitionEngine`/`IRecognitionEngineFactory` seam, implemented for real by
`SherpaOnnxRecognitionEngine`/`SherpaOnnxRecognitionEngineFactory`. No sherpa-onnx type appears
anywhere in the library's public API: public callers interact only through `ISpeechRecognizer`,
`SpeechRecognitionResult`, and `SpeechRecognizerFactory`, keeping architecture.md's "engine
backend stays swappable at the public API surface" promise intact. See
_RecognitionSubsystem Design_ for the seam's structure.

Engine construction is the only step that loads native code, and it happens inside
`SpeechRecognizerFactory`'s guarded composition path, so a missing native binary, an unsupported
platform, or corrupt model files all degrade to an honest unavailable recognizer rather than
failing application start-up.
