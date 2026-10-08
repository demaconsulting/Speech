<!-- cspell:ignore Kaldi -->
## SherpaOnnx

### Purpose

`org.k2fsa.sherpa.onnx` is used as the SpeechSherpa library's local, offline speech-inference
engine, supplying the concrete recognition and synthesis backends this repository ships for the
Speech library. It was chosen so this repository could add real streaming speech-to-text and
local text-to-speech without authoring an
ONNX inference pipeline, acoustic-model runtime, and endpoint detector from scratch. It is
developed by the Next-gen Kaldi team, runs entirely on the local machine with no network access
during recognition, and is published under a permissive license compatible with this library's
own.

### Features Used

- Managed streaming-recognition API: the online recognizer, its per-utterance audio stream, and
  the ready/decode/result/endpoint/reset polling protocol
- Managed offline text-to-speech API: the offline synthesizer, its declared output sample rate,
  and single-call generation for a given text, speed, and speaker id
- Managed configuration types describing a model's architecture, file locations, feature
  configuration (including its required input sample rate), and decoding options
- Per-platform native runtime packages carried transitively by the managed package, covering
  Windows, Linux, macOS, and Android targets

### Integration Pattern

The SpeechSherpa library references only the managed `org.k2fsa.sherpa.onnx` package from
`DemaConsulting.Speech.Sherpa.csproj`; the Speech library (`DemaConsulting.Speech.csproj`) does
not reference it at all. It never references an `org.k2fsa.sherpa.onnx.runtime.{RID}`
package directly; those native runtime packages are declared as dependencies of the managed
package itself, so a consuming application restores the ones it needs transitively rather than
this library selecting or bundling any of them. A consumer that trims or excludes the native
runtime for its target platform still composes successfully - recognition or synthesis simply
reports itself unavailable, per this library's requirement that a missing native runtime "must degrade the
same honest way as a missing model file, never crash."

Sherpa-onnx types are confined to the SpeechSherpa library, in two places. Each model's backing
class builds its own sherpa-onnx configuration privately, through its internal static
`BuildEngineConfig` member, and uses it inside its `IRecognitionModel.CreateBackend`/
`ISynthesisModel.CreateBackend` implementation, matching the "engine configuration for its own
model architecture" responsibility. `SherpaOnnxRecognitionEngine` and `SherpaOnnxSynthesisEngine`
then wrap that configuration as the real implementations of the Speech library's public
`IRecognitionBackend`/`ISynthesisBackend` seams. The Speech library itself names no sherpa-onnx
type anywhere, not even internally: its `IRecognitionModel`/`ISynthesisModel` contract returns only
its own engine-neutral backend interfaces, and its model-driven `DefaultRecognitionBackendFactory`/
`DefaultSynthesisBackendFactory` simply forward to the model. Public callers interact only through
`ISpeechRecognizerEngine`, `IRecognitionSession`, `SpeechRecognitionResult`,
`SpeechRecognizerFactory`, and their synthesis counterparts, keeping the "engine backend stays
swappable at the public API surface" promise intact. See _SpeechSherpa RecognitionSubsystem
Design_ and _SpeechSherpa SynthesisSubsystem Design_ for the backends' structure.

Engine construction is the only step that loads native code, and it happens inside
`SpeechRecognizerFactory`'s and `SpeechSynthesizerFactory`'s guarded composition paths, so a
missing native binary, an unsupported platform, or corrupt model files all degrade to an honest
unavailable recognizer or synthesizer rather than failing application start-up.
