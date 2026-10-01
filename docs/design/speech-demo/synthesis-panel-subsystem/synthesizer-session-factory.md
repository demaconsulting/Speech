### SynthesizerSessionFactory

**Purpose**: Provide a demo-owned synthesizer-engine composition seam over the library's
`SpeechModelStore` and `SpeechSynthesizerFactory`, accepting the library's public `ISpeechModel`
contract so the panel can be tested with a plain fake model and no `InternalsVisibleTo` grant
from the library.

**Why a Demo-Owned Seam**: The library composes a synthesizer engine through the static
`SpeechSynthesizerFactory.LoadAsync(ISynthesisModel, SpeechModelStore, ISpeechDiagnostics?,
IReadOnlyDictionary<string, object>?, CancellationToken)` method, which requires an
`ISynthesisModel` — an interface whose members are partly `internal` to the library, so only
the library's own assemblies can implement it. A
demo-owned seam therefore accepts the common `ISpeechModel` contract instead and performs the
narrowing itself. This is also what works around the static factory method itself not being
substitutable in a ViewModel unit test. `ISynthesizerSessionFactory` and
`SynthesizerSessionFactory` are documented as one unit because the interface has no
independently observable behavior of its own — every test exercises it through
`SynthesizerSessionFactory`, its sole implementation. Only the engine is composed through this
seam; a playback device is bound later, per run, through
`ISpeechSynthesizerEngine.CreateSessionAsync` directly against the returned engine, so a host can
load one engine per model/parameter combination and reuse it across many sessions instead of
reloading the model on every Play.

**Data Model**:

| Member | Returns | Behavior |
| --- | --- | --- |
| `LoadAsync(model, parameterValues, cancellationToken)` | `Task<ISpeechSynthesizerEngine>` | Never throws |

**Key Methods**:

- **LoadAsync(model, parameterValues?, cancellationToken)**: Narrows the model to
  `ISynthesisModel` and forwards it, the shared `SpeechModelStore`, and the `parameterValues` bag
  unchanged to `SpeechSynthesizerFactory.LoadAsync(ISynthesisModel, SpeechModelStore,
  ISpeechDiagnostics?, IReadOnlyDictionary<string, object>?, CancellationToken)`, which resolves
  the model's installed-files directory internally, inheriting that factory's "nothing throws at
  composition" contract. A model that declares a role other than synthesis — and is therefore not
  an `ISynthesisModel` — returns the library's own `UnavailableSpeechSynthesizerEngine.Instance`,
  exactly like a model that is not installed, rather than throwing: a host that lets a user
  choose an installed model with the wrong role must still get a working, if unavailable, engine
  back - including when a non-null `parameterValues` bag is supplied, since the role check
  happens before any parameter is consulted. The `parameterValues` argument is the untyped
  key-value bag `Settings.BuildValueBag()` assembles from the embedded settings panel's current
  parameter values (for example a selected voice); forwarding it unchanged is what lets picking a
  voice in the demo's TTS panel genuinely change what is synthesized.

**Error Handling**: Throws `ArgumentNullException` for a null model (an explicit misuse of the
seam's own contract); otherwise never throws, returning an honest unavailable engine for every
unavailable composition state.

**Dependencies**: The library's `SpeechModelStore`, `SpeechSynthesizerFactory`, `ISpeechModel`,
`ISynthesisModel`, `ISpeechSynthesizerEngine`, `UnavailableSpeechSynthesizerEngine`.

**Callers**: `SynthesisPanelViewModel` (loads a synthesizer engine through this seam, at most
once per selected model/parameter combination, reused across many Play calls).
