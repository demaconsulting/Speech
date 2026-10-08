## RecognitionSubsystem Design

![RecognitionSubsystem Structure](SherpaRecognitionSubsystemView.svg)

### Overview

The SpeechSherpa RecognitionSubsystem supplies the real, sherpa-onnx-backed implementation of the
core `Speech` library's internal `IRecognitionBackend` seam (see _Speech RecognitionSubsystem
Design_). It previously lived inside the core library's own RecognitionSubsystem; it moved here,
unchanged in behavior, together with the models that construct it, so the core library carries no
sherpa-onnx dependency at all. The core library's session, engine, resampling, and composition
logic remain in the core library and drive this backend only through `IRecognitionBackend`. It
contains the following unit:

- **SherpaOnnxRecognitionEngine**: the real `IRecognitionBackend` over sherpa-onnx's streaming
  `OnlineRecognizer`/`OnlineStream` API, including the opt-in, per-model post-endpoint warm-up
  replay mitigation and the session-end flush and stream-recreating reset

### Interfaces

The subsystem exposes no public API: `SherpaOnnxRecognitionEngine` is `internal`. It implements
the core library's internal `IRecognitionBackend` (`AcceptSamples`, `TryDecode`, `TryFlush`,
`Reset`, `Dispose`) and produces the core library's `SpeechRecognitionResult` values, which is
possible because the core library grants this assembly `InternalsVisibleTo`. It consumes the
sherpa-onnx managed API (`OnlineRecognizer`, `OnlineStream`, `OnlineRecognizerConfig`; see
_SherpaOnnx Design_). It is constructed only by this system's recognition models'
`IRecognitionModel.CreateBackend` implementations (see _SpeechSherpa ModelManagementSubsystem
Design_).

### Design

The core library's `DefaultRecognitionBackendFactory` holds no engine-specific knowledge: it
forwards to the selected model's internal `IRecognitionModel.CreateBackend`. Each recognition
model in this system implements that member by constructing a `SherpaOnnxRecognitionEngine` from
its own sherpa-onnx configuration, its declared input sample rate, and its declared post-endpoint
warm-up window. The resulting engine is owned by the core library's `SpeechRecognizerEngine` and
driven by its `RecognitionSession` on a dedicated worker thread; the engine itself is not
thread-safe by contract.

Constructing the engine loads the model into native memory, so a missing platform native runtime
(`org.k2fsa.sherpa.onnx.runtime.{RID}`, which this package deliberately does not bundle) or
unusable model files surface as an exception from `CreateBackend`. The core library's
`SpeechRecognizerFactory` catches that exception and degrades it to the honest unavailable
recognizer, so a consumer that forgets a runtime package still gets a working, honestly-reporting
application rather than a crash.

The warm-up replay mitigation is scoped per model and is opt-in: only the Nemotron model, whose
endpoint detector was confirmed to cause post-reset word loss, enables it, while the Zipformer
model keeps it disabled because enabling it was confirmed to duplicate text. See
_SherpaOnnxRecognitionEngine_ for the full mechanism and its regression history.
