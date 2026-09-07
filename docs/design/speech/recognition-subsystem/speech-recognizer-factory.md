### SpeechRecognizerFactory

**Purpose**: Provide the single composition entry point for obtaining an `ISpeechRecognizer`, so
all "can this machine recognize speech right now?" logic lives in one reviewable place.

**Data Model**: A static class with no state. Three public `Create(...)` overloads exist: one
resolving an installed-model directory from a caller-supplied `string`, one resolving it from
a `SpeechModelStore` directly via `store.GetCurrentDirectory(model.Id)`, and one resolving it
from a `SpeechModelCatalog` directly via `catalog.Store.GetCurrentDirectory(model.Id)`. All call
through to the same internal composition logic, each with an internal counterpart that accepts
an injected `IRecognitionEngineFactory` so composition can be verified without model files or a
native runtime.

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
- **Create(IRecognitionModel model, SpeechModelStore store, IAudioCaptureDevice captureDevice,
  ISpeechDiagnostics? diagnostics)**: A convenience overload with byte-for-byte identical behavior
  to the `string`-based overload above; it resolves `store.GetCurrentDirectory(model.Id)` for the
  caller and delegates to the same overload, so a host never needs to know
  `SpeechModelStore`'s on-disk directory-naming scheme just to compose a recognizer.
  Preconditions: `model`, `store`, and `captureDevice` are non-null.
- **Create(IRecognitionModel model, SpeechModelCatalog catalog, IAudioCaptureDevice
  captureDevice, ISpeechDiagnostics? diagnostics)**: A convenience overload delegating through the
  `SpeechModelStore`-based overload via the catalog's own `Store` property, so a host that already
  owns a `SpeechModelCatalog` for enumeration and download can compose a recognizer through that
  same catalog instance, without constructing a second, potentially divergent `SpeechModelStore`.
  Preconditions: `model`, `catalog`, and `captureDevice` are non-null.

The checks run in a deliberate order - installed, then role, then device, then engine load - so
the cheapest and most common cause of unavailability (a model not downloaded yet) is reported
first and no native memory is allocated for a recognizer that could never run.

**Error Handling**: Every ordinary machine state is represented as the honest unavailable
recognizer plus a structural diagnostic, never as an exception, per this library's "nothing
throws at composition" decision. An engine load failure - the missing-native-runtime case for a
missing `org.k2fsa.sherpa.onnx.runtime.{RID}` binary or unusable model files - is caught and
degraded identically to a missing model. Only a null `model`, `store`, `catalog`, `captureDevice`,
or engine factory throws `ArgumentNullException`, since a null argument is a programming error
rather than a machine state.

**Dependencies**: `IRecognitionModel`, `SpeechModelRole`, `SpeechModelStore`, and
`SpeechModelCatalog` from the ModelManagementSubsystem, `IAudioCaptureDevice` from the
AudioSubsystem, `ISpeechDiagnostics`/`NullSpeechDiagnostics` from the Diagnostics subsystem, and
the subsystem's own `IRecognitionEngineFactory`, `SherpaOnnxRecognitionEngineFactory`,
`SherpaOnnxSpeechRecognizer`, and `UnavailableSpeechRecognizer`.

**Callers**: Host applications composing speech recognition at start-up, and the system-level
integration tests.
