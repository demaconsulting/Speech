### SpeechSynthesizerFactory

**Purpose**: Provide the single composition entry point for obtaining an
`ISpeechSynthesizerEngine`, so all "can this machine speak right now?" logic lives in one
reviewable place, mirroring `SpeechRecognizerFactory` exactly. Unlike the former synchronous
factory, this type no longer takes a playback device at all: an engine loaded here can create
many sessions over its life, each bound to its own device, via
`ISpeechSynthesizerEngine.CreateSessionAsync`.

**Data Model**: A static class with no state. Three public `LoadAsync(...)` overloads exist: one
resolving an installed-model directory from a caller-supplied `string`, one resolving it from a
`SpeechModelStore` directly via `store.GetCurrentDirectory(model.Id)`, and one resolving it from
a `SpeechModelCatalog` directly via `catalog.Store.GetCurrentDirectory(model.Id)`. All call
through to the same internal composition logic, each with an internal counterpart that accepts
an injected `ISynthesisBackendFactory` so composition can be verified without model files or a
native runtime. The blocking native model load itself runs on a `DedicatedWorker` rather than the
calling thread, so awaiting any `LoadAsync` overload never blocks a caller's synchronization
context.

**Key Methods**:

- **LoadAsync(ISynthesisModel model, string installedModelDirectory, ISpeechDiagnostics?
  diagnostics, IReadOnlyDictionary&lt;string, object&gt;? parameterValues = null,
  CancellationToken cancellationToken = default)**: Returns a real
  `SpeechSynthesizerEngine` when the model's installed directory exists, the model
  declares `SpeechModelRole.Synthesis`, and the backend loads. Otherwise returns
  `UnavailableSpeechSynthesizerEngine.Instance`. Precondition: `model` is non-null. Postcondition:
  the returned engine is never null, and either owns a loaded backend or is the shared unavailable
  instance. Loading the backend allocates native resources, so the returned engine must be
  disposed. `parameterValues` is an optional engine-level parameter value bag (for example a
  selected voice, built from the model's declared `ISpeechModel.Parameters`), forwarded unchanged
  to the returned engine and onward to every session it creates, which re-resolves it via
  `ISynthesisModel.ResolveSpeakerId` once per synthesized segment; `null` means every model's own
  default voice/speaker.
- **LoadAsync(ISynthesisModel model, SpeechModelStore store, ISpeechDiagnostics? diagnostics,
  IReadOnlyDictionary&lt;string, object&gt;? parameterValues = null, CancellationToken
  cancellationToken = default)**: A convenience overload with byte-for-byte identical behavior to
  the `string`-based overload above; it resolves `store.GetCurrentDirectory(model.Id)` for the
  caller and delegates to the same overload, so a host never needs to know `SpeechModelStore`'s
  on-disk directory-naming scheme just to compose an engine. Preconditions: `model` and `store`
  are non-null.
- **LoadAsync(ISynthesisModel model, SpeechModelCatalog catalog, ISpeechDiagnostics? diagnostics,
  IReadOnlyDictionary&lt;string, object&gt;? parameterValues = null, CancellationToken
  cancellationToken = default)**: A convenience overload delegating through the
  `SpeechModelStore`-based overload via the catalog's own `Store` property, so a host that
  already owns a `SpeechModelCatalog` for enumeration and download can compose an engine through
  that same catalog instance, without constructing a second, potentially divergent
  `SpeechModelStore`. Preconditions: `model` and `catalog` are non-null.

The checks run in the same deliberate order as the recognition-direction factory - parameter
validation, then installed, then role, then backend load - so a caller-supplied parameter value
invalid for a recognized parameter is rejected synchronously and loudly before any of the
ordinary, never-throw machine state checks run, and so the cheapest and most common cause of
unavailability (a model not downloaded yet) is reported first among those and no native memory is
allocated for an engine that could never run.

**Error Handling**: Every ordinary machine state is represented as the honest unavailable engine
plus a structural diagnostic, never as an exception, per this library's "nothing throws at
composition" decision. A backend load failure is caught and degraded identically to a missing
model. Only a null `model`/`store`/`catalog`/backend factory throws `ArgumentNullException`, an
invalid `parameterValues` entry throws `ArgumentException`, and a cancelled `cancellationToken`
throws `OperationCanceledException`, since those are programming errors or an explicit caller
request rather than a machine state. `parameterValues` is validated against `model.Parameters`
before any other work runs, using the same shared `SpeechModelParameterDiagnostics.ValidateAndReport`
helper as `SpeechRecognizerFactory`. A supplied key naming a parameter not declared by `model` is
silently ignored (preserving the documented cross-model-compatibility contract) but reports an
`Info` diagnostic. A supplied value for a parameter that *is* declared by `model` but fails that
parameter's own validation (wrong CLR type, out-of-range or non-integral for a `NumericParameter`,
unrecognized `ChoiceParameter` option, non-`bool` for a `BooleanParameter`) throws
`ArgumentException` synchronously from `LoadAsync()` naming the parameter id, model id, and the
reason the value is invalid. This validation happens once, up front, at `LoadAsync()`; it does
not change `ResolveSpeakerId`'s or `ResolveOverrideRatios`'s own existing never-throw, per-segment
runtime contract.

**Dependencies**: `ISynthesisModel`, `SpeechModelRole`, `SpeechModelStore`, `SpeechModelCatalog`,
and `SpeechModelParameterDiagnostics` from the ModelManagementSubsystem,
`ISpeechDiagnostics`/`NullSpeechDiagnostics` from the Diagnostics subsystem, and the subsystem's
own `ISynthesisBackendFactory`, `DefaultSynthesisBackendFactory`,
`SpeechSynthesizerEngine`, `UnavailableSpeechSynthesizerEngine`, and `DedicatedWorker`.

**Callers**: Host applications composing speech synthesis at start-up, and the system-level
integration tests.
