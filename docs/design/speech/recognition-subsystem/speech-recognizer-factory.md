### SpeechRecognizerFactory

**Purpose**: Provide the single composition entry point for obtaining an
`ISpeechRecognizerEngine`, so all "can this machine recognize speech right now?" logic lives in
one reviewable place.

**Data Model**: A static class with no state. Three public `LoadAsync(...)` overloads exist: one
resolving an installed-model directory from a caller-supplied `string`, one resolving it from a
`SpeechModelStore` directly via `store.GetCurrentDirectory(model.Id)`, and one resolving it from a
`SpeechModelCatalog` directly via `catalog.Store.GetCurrentDirectory(model.Id)`. All call through
to the same internal composition logic, which has its own internal overload that accepts an
injected `IRecognitionBackendFactory` so composition can be verified without model files or a
native runtime.

**Key Methods**:

- **LoadAsync(IRecognitionModel model, string installedModelDirectory, ISpeechDiagnostics?
  diagnostics = null, IReadOnlyDictionary&lt;string, object&gt;? parameterValues = null,
  CancellationToken cancellationToken = default)**: Returns a task that completes with a real
  `SpeechRecognizerEngine` when the model's installed directory exists, the model
  declares `SpeechModelRole.Recognition`, and the backend loads. Otherwise completes with
  `UnavailableSpeechRecognizerEngine.Instance`. Preconditions: `model` is non-null. Postcondition:
  the returned engine is never null, and either owns a loaded backend or is the shared unavailable
  instance. Loading runs on a `DedicatedWorker` thread rather than blocking the calling thread
  synchronously, since it allocates native resources and can take meaningful time.
  `parameterValues` is an optional session-level parameter value bag forwarded to the model's own
  `CreateBackend(installedModelDirectory, parameterValues)` overload (through the real
  `DefaultRecognitionBackendFactory`) when the backend is constructed; `null` (or any bag, for a
  model that declares no recognition parameter) resolves to the exact parameter-less behavior via
  that member's default hook. A capture device is *not* supplied
  here - it is bound later, per session, via `ISpeechRecognizerEngine.CreateSessionAsync`.
- **LoadAsync(IRecognitionModel model, SpeechModelStore store, ISpeechDiagnostics? diagnostics =
  null, IReadOnlyDictionary&lt;string, object&gt;? parameterValues = null, CancellationToken
  cancellationToken = default)**: A convenience overload with byte-for-byte identical behavior to
  the `string`-based overload above; it resolves `store.GetCurrentDirectory(model.Id)` for the
  caller and delegates to the same overload, so a host never needs to know
  `SpeechModelStore`'s on-disk directory-naming scheme just to compose an engine. Preconditions:
  `model` and `store` are non-null.
- **LoadAsync(IRecognitionModel model, SpeechModelCatalog catalog, ISpeechDiagnostics?
  diagnostics = null, IReadOnlyDictionary&lt;string, object&gt;? parameterValues = null,
  CancellationToken cancellationToken = default)**: A convenience overload delegating through the
  `SpeechModelStore`-based overload via the catalog's own `Store` property, so a host that already
  owns a `SpeechModelCatalog` for enumeration and download can compose an engine through that same
  catalog instance, without constructing a second, potentially divergent `SpeechModelStore`.
  Preconditions: `model` and `catalog` are non-null.

The checks run in a deliberate order - parameter validation, then installed, then role, then
backend load - so a caller-supplied parameter value invalid for a recognized parameter is
rejected synchronously and loudly before any of the ordinary, never-throw machine state checks
run, and so the cheapest and most common cause of unavailability (a model not downloaded yet) is
reported first among those and no native memory is allocated for an engine that could never run.

**Reuse and concurrent pre-warming**: Since a call to `LoadAsync` is the expensive step (it loads
the model into native memory) while `ISpeechRecognizerEngine.CreateSessionAsync` and the resulting
session's `StartAsync`/`StopAsync` cycle are cheap, a host doing repeated, low-latency recognition
should load one engine once via `LoadAsync` and reuse it across many sequential sessions rather
than calling `LoadAsync` again per turn - see `ISpeechRecognizerEngine`'s own design doc for that
reuse contract. Because this factory itself holds no state, a host may also call `LoadAsync`
concurrently from a background task to overlap the model-load step with other work (for example,
pre-warming the next turn's engine while the current turn's prompt is still speaking); this is
safe with respect to the factory, but only safe with respect to a caller-supplied `diagnostics`
sink when that sink is itself safe for concurrent use from multiple threads.

**Error Handling**: Every ordinary machine state is represented as the honest unavailable engine
plus a structural diagnostic, never as an exception, per this library's "nothing throws at
composition" decision. A backend load failure - for example, for a sherpa-onnx-backed model from
the sibling `SpeechSherpa` system, the missing-native-runtime case for a missing
`org.k2fsa.sherpa.onnx.runtime.{RID}` binary or unusable model files - is caught and degraded
identically to a missing model. A null `model`, `store`, `catalog`, or backend factory results in
an `ArgumentNullException`: for the `string`-based overload this faults the returned task (the null
check lives inside its `async` implementation), while for the `SpeechModelStore`-/
`SpeechModelCatalog`-based overloads it is thrown synchronously, before any task is created, since
those overloads are ordinary synchronous methods that validate their own arguments and delegate to
the `string`-based overload. Either way the exception is a programming error, never a machine
state, and is indistinguishable to an `await`-based caller. A supplied key in `parameterValues`
that names a parameter *not* declared by `model` is silently ignored (this deliberately preserves
the documented cross-model-compatibility contract - a host reusing one settings bag across
different models must not break just because model B doesn't declare a parameter model A had) but
reports an `Info` diagnostic. A supplied value for a parameter *that is declared* by `model` but
fails that parameter's own validation (wrong CLR type, a `NumericParameter` value outside
`[Minimum, Maximum]` or - when `IsInteger` is `true` - a non-integral value, an unrecognized
`ChoiceParameter` option, or a non-`bool` for a `BooleanParameter`) faults the returned task with
`ArgumentException` naming the parameter id, model id, and the reason the value is invalid, via the
shared `SpeechModelParameterDiagnostics.ValidateAndReport` helper. A `cancellationToken` cancelled
before loading completes faults the returned task with `OperationCanceledException`.

**Dependencies**: `IRecognitionModel`, `SpeechModelRole`, `SpeechModelStore`,
`SpeechModelCatalog`, and `SpeechModelParameterDiagnostics` from the ModelManagementSubsystem,
`ISpeechDiagnostics`/`NullSpeechDiagnostics` from the Diagnostics subsystem, and the subsystem's
own `IRecognitionBackendFactory`, `DefaultRecognitionBackendFactory`,
`SpeechRecognizerEngine`, `UnavailableSpeechRecognizerEngine`, and `DedicatedWorker`.

**Callers**: Host applications composing speech recognition at start-up, and the system-level
integration tests.
