## RecognitionSubsystem Design

![RecognitionSubsystem Structure](RecognitionSubsystemView.svg)

### Overview

The RecognitionSubsystem turns audio captured by the AudioSubsystem into text using a model
installed by the ModelManagementSubsystem. It owns the library's public streaming
speech-to-text contract, the composition root that decides whether recognition is possible on
the current machine, the running pipeline that converts and streams audio into an inference
engine, and the honest fallback used when recognition is not possible. It contains the
following direct units:

- **ISpeechRecognizer**: the public streaming recognition contract, together with the
  **SpeechRecognitionResult** and **SpeechRecognitionEvent** value types it delivers
- **SpeechRecognizerFactory**: composition root that returns either a real recognizer or the
  honest unavailable fallback, and never throws for an ordinary machine state
- **SherpaOnnxSpeechRecognizer**: the real streaming implementation, together with the internal
  **IRecognitionEngine**/**IRecognitionEngineFactory** seam, its real
  **SherpaOnnxRecognitionEngine**/**SherpaOnnxRecognitionEngineFactory** implementations, and the
  **AudioFrameResampler** that converts captured audio into the format a model requires
- **UnavailableSpeechRecognizer** and **SpeechRecognizerUnavailableException**: honest fallback
  behavior when no model, no engine, or no capture device is available

### Interfaces

The subsystem exposes `ISpeechRecognizer`, `SpeechRecognitionResult`, `SpeechRecognitionEvent`,
`SpeechRecognizerFactory`, `UnavailableSpeechRecognizer`, and
`SpeechRecognizerUnavailableException` as its public API. It consumes `IAudioCaptureDevice` from
the AudioSubsystem for input audio, `IRecognitionModel` from the ModelManagementSubsystem for the
engine configuration and required input format, and `ISpeechDiagnostics` from the Diagnostics
subsystem to report structural composition, lifecycle, and fault facts without ever exposing
recognized text.

Both cross-subsystem dependencies were extended in this phase, additively:
`IAudioCaptureDevice` gained `ChannelCount`/`SampleRate` so the resolved capture format can be
discovered (see _IAudioCaptureDevice Design_), and `IRecognitionModel` now exposes a public
`AudioFormat` plus internal `CreateEngineConfig` so each model owns both its input-format
declaration and its engine configuration (see _SpeechModelContract Design_).

No member of the subsystem's public API names a sherpa-onnx type, per this library's
"engine backend stays swappable at the public API surface" decision. The sherpa-onnx
configuration type appears only on `IRecognitionModel`'s internal members and inside the
subsystem's internal engine seam.

### Design

`SpeechRecognizerFactory` is the subsystem's composition root. It checks, in order, whether the
requested model's files exist on disk, whether the model declares the recognition role, and
whether the supplied capture device is available; any failure returns
`UnavailableSpeechRecognizer.Instance` with a structural diagnostic explaining which condition
failed. Only then does it ask an `IRecognitionEngineFactory` to load the model, and a failure
there - the missing-native-runtime case - degrades exactly the
same honest way rather than throwing. Nothing about "is recognition possible?" is left for the
host to work out from separate signals. When a caller already knows the chosen model, the
recommended composition pattern is to construct the capture device first with
`AudioDeviceFactory.CreateCaptureDevice(selection, model.AudioFormat)` and then pass that device
into `SpeechRecognizerFactory.Create(...)`; when the backend honors the hint,
`AudioFrameResampler` stays on its existing equal-rate no-op fast path.

The running pipeline in `SherpaOnnxSpeechRecognizer` spans two threads by design. The capture
device raises frames on a high-priority audio callback thread, so the recognizer's frame handler
does nothing but copy the block into a bounded queue and return; all conversion, inference, and
event raising happens on a single background consumer task. Stopping completes the queue and
joins that task, so every result derived from audio captured before the stop request has been
delivered by the time the call returns.

`AudioFrameResampler` performs the format conversion, downmixing to mono and using simple linear
interpolation for rate conversion except on the downsampling path, where a small windowed-sinc
FIR lowpass filter now runs immediately before decimation to attenuate above-target-Nyquist
energy. The internal `IRecognitionEngine`/`IRecognitionEngineFactory` seam confines every
sherpa-onnx call to `SherpaOnnxRecognitionEngine`/`SherpaOnnxRecognitionEngineFactory`. That seam
is the reason the whole pipeline is verifiable in CI: the recognizer's threading, conversion,
fault containment, and result ordering are all exercised through pure managed fakes with no model
file and no native inference binary present. It mirrors the `IPortAudioApi` seam used for audio
interop and the `IModelDownloadClient` seam used for downloads.

#### UnavailableSpeechRecognizer

**Purpose**: Provide a safe, always-obtainable `ISpeechRecognizer` fallback for use when
recognition is not possible on the current machine.

**Data Model**: No instance fields other than the never-invoked backing field for
`ResultReceived`. Exposes a single static `Instance` singleton; the constructor is private.
`IsAvailable` always returns `false`.

**Key Methods**:

- **Start()** / **Stop()**: Always throw `SpeechRecognizerUnavailableException`.
- **Dispose()**: A no-op. Disposal must never throw or invalidate the shared instance, because a
  host that wraps its recognizer in a disposal scope receives this instance and may dispose it
  repeatedly.
- **ResultReceived**: Never raised; subscribing and unsubscribing are safe no-ops.

**Error Handling**: Signals misuse of a known-unavailable recognizer with
`SpeechRecognizerUnavailableException`.

**Dependencies**: `SpeechRecognizerUnavailableException`; implements `ISpeechRecognizer`.

**Callers**: `SpeechRecognizerFactory.Create(...)` for every unavailable state.

#### SpeechRecognizerUnavailableException

**Purpose**: Signal that an operational member of an unavailable recognizer was invoked, or that
a recognizer that claimed to be available failed on first use.

**Data Model**: No additional fields beyond the standard `Exception` base members.

**Key Methods**: Standard three-constructor exception pattern.

**Error Handling**: This type is itself the error-handling mechanism.

**Dependencies**: `Exception`.

**Callers**: `UnavailableSpeechRecognizer` for both operational members, and
`SherpaOnnxSpeechRecognizer.Start()` when its capture device fails to start.
