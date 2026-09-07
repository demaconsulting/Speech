### SpeechSynthesizerFactory

**Purpose**: Provide the single composition entry point for obtaining an `ISpeechSynthesizer`, so
all "can this machine speak right now?" logic lives in one reviewable place, mirroring
`SpeechRecognizerFactory` exactly.

**Data Model**: A static class with no state. Three public `Create(...)` overloads exist: one
resolving an installed-model directory from a caller-supplied `string`, one resolving it from
a `SpeechModelStore` directly via `store.GetCurrentDirectory(model.Id)`, and one resolving it
from a `SpeechModelCatalog` directly via `catalog.Store.GetCurrentDirectory(model.Id)`. All call
through to the same internal composition logic, each with an internal counterpart that accepts
an injected `ISynthesisEngineFactory` so composition can be verified without model files or a
native runtime.

**Key Methods**:

- **Create(ISynthesisModel model, string installedModelDirectory, IAudioPlaybackDevice
  playbackDevice, ISpeechDiagnostics? diagnostics, IReadOnlyDictionary&lt;string, object&gt;?
  parameterValues = null)**: Returns a real `SherpaOnnxSpeechSynthesizer`
  when the model's installed directory exists, the model declares `SpeechModelRole.Synthesis`,
  the playback device reports `IsAvailable`, and the engine loads. Otherwise returns
  `UnavailableSpeechSynthesizer.Instance`. Preconditions: `model` and `playbackDevice` are
  non-null. Postcondition: the returned synthesizer is never null, and either owns a loaded
  engine or is the shared unavailable instance. Loading the engine allocates native resources, so
  the returned synthesizer must be disposed. `parameterValues` is an optional session-level
  parameter value bag (for example a selected voice, built from the model's declared
  `ISpeechModel.Parameters`), forwarded unchanged to the returned synthesizer, which re-resolves
  it via `ISynthesisModel.ResolveSpeakerId` once per synthesized segment; `null` means every
  model's own default voice/speaker.
- **Create(ISynthesisModel model, SpeechModelStore store, IAudioPlaybackDevice playbackDevice,
  ISpeechDiagnostics? diagnostics, IReadOnlyDictionary&lt;string, object&gt;? parameterValues =
  null)**: A convenience overload with byte-for-byte identical behavior to the `string`-based
  overload above; it resolves `store.GetCurrentDirectory(model.Id)` for the caller and delegates
  to the same overload, so a host never needs to know `SpeechModelStore`'s on-disk
  directory-naming scheme just to compose a synthesizer. Preconditions: `model`, `store`, and
  `playbackDevice` are non-null.
- **Create(ISynthesisModel model, SpeechModelCatalog catalog, IAudioPlaybackDevice
  playbackDevice, ISpeechDiagnostics? diagnostics, IReadOnlyDictionary&lt;string, object&gt;?
  parameterValues = null)**: A convenience overload delegating through the
  `SpeechModelStore`-based overload via the catalog's own `Store` property, so a host that already
  owns a `SpeechModelCatalog` for enumeration and download can compose a synthesizer through that
  same catalog instance, without constructing a second, potentially divergent `SpeechModelStore`.
  Preconditions: `model`, `catalog`, and `playbackDevice` are non-null.

The checks run in the same deliberate order as the recognition-direction factory - installed,
then role, then device, then engine load - so the cheapest and most common cause of
unavailability (a model not downloaded yet) is reported first and no native memory is allocated
for a synthesizer that could never run.

**Error Handling**: Every ordinary machine state is represented as the honest unavailable
synthesizer plus a structural diagnostic, never as an exception, per this library's "nothing
throws at composition" decision. An engine load failure is caught and degraded identically to a
missing model. Only a null `model`, `store`, `catalog`, `playbackDevice`, or engine factory
throws `ArgumentNullException`, since a null argument is a programming error rather than a
machine state.

**Dependencies**: `ISynthesisModel`, `SpeechModelRole`, `SpeechModelStore`, and
`SpeechModelCatalog` from the ModelManagementSubsystem, `IAudioPlaybackDevice` from the
AudioSubsystem, `ISpeechDiagnostics`/`NullSpeechDiagnostics` from the Diagnostics subsystem, and
the subsystem's own `ISynthesisEngineFactory`, `SherpaOnnxSynthesisEngineFactory`,
`SherpaOnnxSpeechSynthesizer`, and `UnavailableSpeechSynthesizer`.

**Callers**: Host applications composing speech synthesis at start-up, and the system-level
integration tests.
