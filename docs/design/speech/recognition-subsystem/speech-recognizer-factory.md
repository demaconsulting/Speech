### SpeechRecognizerFactory

**Purpose**: Provide the single composition entry point for obtaining an `ISpeechRecognizer`, so
all "can this machine recognize speech right now?" logic lives in one reviewable place.

**Data Model**: A static class with no state. The public `Create(...)` overload composes against
the real sherpa-onnx engine factory; an internal overload accepts an injected
`IRecognitionEngineFactory` so composition can be verified without model files or a native
runtime.

**Key Methods**:

- **Create(IRecognitionModel model, string installedModelDirectory, IAudioCaptureDevice
  captureDevice, ISpeechDiagnostics? diagnostics)**: Returns a real `SherpaOnnxSpeechRecognizer`
  when the model's installed directory exists, the model declares `SpeechModelRole.Recognition`,
  the capture device reports `IsAvailable`, and the engine loads. Otherwise returns
  `UnavailableSpeechRecognizer.Instance`. Preconditions: `model` and `captureDevice` are
  non-null. Postcondition: the returned recognizer is never null, and either owns a loaded engine
  or is the shared unavailable instance. Loading the engine allocates native resources, so the
  returned recognizer must be disposed. The recommended caller pattern is to compose
  `captureDevice` first via `AudioDeviceFactory.CreateCaptureDevice(selection, model.AudioFormat)`
  so the device opens already matching the model when the backend honors the hint.

The checks run in a deliberate order - installed, then role, then device, then engine load - so
the cheapest and most common cause of unavailability (a model not downloaded yet) is reported
first and no native memory is allocated for a recognizer that could never run.

**Error Handling**: Every ordinary machine state is represented as the honest unavailable
recognizer plus a structural diagnostic, never as an exception, per this library's "nothing
throws at composition" decision. An engine load failure - the missing-native-runtime case for a
missing `org.k2fsa.sherpa.onnx.runtime.{RID}` binary or unusable model files - is caught and
degraded identically to a missing model. Only a null `model`, `captureDevice`, or engine factory
throws `ArgumentNullException`, since a null argument is a programming error rather than a
machine state.

**Dependencies**: `IRecognitionModel` and `SpeechModelRole` from the ModelManagementSubsystem,
`IAudioCaptureDevice` from the AudioSubsystem, `ISpeechDiagnostics`/`NullSpeechDiagnostics` from
the Diagnostics subsystem, and the subsystem's own `IRecognitionEngineFactory`,
`SherpaOnnxRecognitionEngineFactory`, `SherpaOnnxSpeechRecognizer`, and
`UnavailableSpeechRecognizer`.

**Callers**: Host applications composing speech recognition at start-up, and the system-level
integration tests.
