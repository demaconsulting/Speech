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
  captureDevice, ISpeechDiagnostics? diagnostics, IReadOnlyDictionary&lt;string, object&gt;?
  parameterValues = null)**: Returns a real `SherpaOnnxSpeechRecognizer`
  when the model's installed directory exists, the model declares `SpeechModelRole.Recognition`,
  the capture device reports `IsAvailable`, and the engine loads. Otherwise returns
  `UnavailableSpeechRecognizer.Instance`. Preconditions: `model` and `captureDevice` are
  non-null. Postcondition: the returned recognizer is never null, and either owns a loaded engine
  or is the shared unavailable instance. Loading the engine allocates native resources, so the
  returned recognizer must be disposed. The recommended caller pattern is to compose
  `captureDevice` first via `AudioDeviceFactory.CreateCaptureDevice(selection, model.AudioFormat)`
  so the device opens already matching the model when the backend honors the hint.
  `parameterValues` is an optional session-level parameter value bag forwarded to the model's own
  `CreateEngineConfig(installedModelDirectory, parameterValues)` overload when the engine is
  constructed; `null` (or any bag, for either of today's two shipped models) resolves to today's
  exact parameter-less behavior via that member's default hook.
- **Create(IRecognitionModel model, SpeechModelStore store, IAudioCaptureDevice captureDevice,
  ISpeechDiagnostics? diagnostics, IReadOnlyDictionary&lt;string, object&gt;? parameterValues =
  null)**: A convenience overload with byte-for-byte identical behavior
  to the `string`-based overload above; it resolves `store.GetCurrentDirectory(model.Id)` for the
  caller and delegates to the same overload, so a host never needs to know
  `SpeechModelStore`'s on-disk directory-naming scheme just to compose a recognizer.
  Preconditions: `model`, `store`, and `captureDevice` are non-null. Preconditions unchanged.
- **Create(IRecognitionModel model, SpeechModelCatalog catalog, IAudioCaptureDevice
  captureDevice, ISpeechDiagnostics? diagnostics, IReadOnlyDictionary&lt;string, object&gt;?
  parameterValues = null)**: A convenience overload delegating through the
  `SpeechModelStore`-based overload via the catalog's own `Store` property, so a host that already
  owns a `SpeechModelCatalog` for enumeration and download can compose a recognizer through that
  same catalog instance, without constructing a second, potentially divergent `SpeechModelStore`.
  Preconditions: `model`, `catalog`, and `captureDevice` are non-null.

The checks run in a deliberate order - parameter validation, then installed, then role, then
device, then engine load - so a caller-supplied parameter value invalid for a recognized
parameter is rejected synchronously and loudly before any of the ordinary, never-throw machine
state checks run, and so the cheapest and most common cause of unavailability (a model not
downloaded yet) is reported first among those and no native memory is allocated for a recognizer
that could never run.

**Reuse and concurrent pre-warming**: Since a call to `Create` is the expensive step (it loads
the model into native memory) while `ISpeechRecognizer.Start`/`Stop` are cheap, a host doing
repeated, low-latency recognition should construct one recognizer once via `Create` and reuse it
across many `Start`/`Stop` cycles rather than calling `Create` again per turn - see
`ISpeechRecognizer`'s own design doc for that reuse contract. Because this factory itself holds
no state, a host may also call `Create` concurrently from a background task to overlap the
model-load step with other work (for example, pre-warming the next turn's recognizer while the
current turn's prompt is still speaking); this is safe with respect to the factory, but only
safe with respect to a caller-supplied `diagnostics` sink when that sink is itself safe for
concurrent use from multiple threads.

**Error Handling**: Every ordinary machine state is represented as the honest unavailable
recognizer plus a structural diagnostic, never as an exception, per this library's "nothing
throws at composition" decision. An engine load failure - the missing-native-runtime case for a
missing `org.k2fsa.sherpa.onnx.runtime.{RID}` binary or unusable model files - is caught and
degraded identically to a missing model. Only a null `model`, `store`, `catalog`, `captureDevice`,
or engine factory throws `ArgumentNullException`, since a null argument is a programming error
rather than a machine state. **Breaking change**: `parameterValues` is now validated against
`model.Parameters` before any other work runs. A supplied key that names a parameter *not*
declared by `model` is still silently ignored exactly as before (this deliberately preserves the
documented cross-model-compatibility contract - a host reusing one settings bag across different
models must not break just because model B doesn't declare a parameter model A had) but now also
reports an `Info` diagnostic. A supplied value for a parameter *that is declared* by `model` but
fails that parameter's own validation (wrong CLR type, a `NumericParameter` value outside
`[Minimum, Maximum]` or - when `IsInteger` is `true` - a non-integral value, an unrecognized
`ChoiceParameter` option, or a non-`bool` for a `BooleanParameter`) now throws `ArgumentException`
synchronously from `Create()` naming the parameter id, model id, and the reason the value is
invalid, via the shared `SpeechModelParameterDiagnostics.ValidateAndReport` helper. Previously
such a value was silently substituted with a default deeper in the composed recognizer; a caller
targeting a specific, declared parameter on this model with an invalid value is a caller bug that
should surface immediately rather than silently misbehave later.

**Dependencies**: `IRecognitionModel`, `SpeechModelRole`, `SpeechModelStore`,
`SpeechModelCatalog`, and `SpeechModelParameterDiagnostics` from the ModelManagementSubsystem,
`IAudioCaptureDevice` from the AudioSubsystem, `ISpeechDiagnostics`/`NullSpeechDiagnostics` from
the Diagnostics subsystem, and the subsystem's own `IRecognitionEngineFactory`,
`SherpaOnnxRecognitionEngineFactory`, `SherpaOnnxSpeechRecognizer`, and
`UnavailableSpeechRecognizer`.

**Callers**: Host applications composing speech recognition at start-up, and the system-level
integration tests.
