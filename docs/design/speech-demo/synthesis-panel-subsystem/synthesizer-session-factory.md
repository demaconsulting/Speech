### SynthesizerSessionFactory

**Purpose**: Provide a demo-owned synthesizer-engine composition seam over the library's
`SpeechModelStore` and `SpeechSynthesizerFactory`, accepting the library's public `ISpeechModel`
contract so the panel can be tested with a plain fake model without depending on the
role-specific `ISynthesisModel` interface at all.

**Why a Demo-Owned Seam**: The library composes a synthesizer engine through the static
`SpeechSynthesizerFactory.LoadAsync(ISynthesisModel, SpeechModelStore, ISpeechDiagnostics?,
IReadOnlyDictionary<string, object>?, CancellationToken)` method, which requires an
`ISynthesisModel`. `ISynthesisModel` is a public interface that hosts may implement to register
their own models, so tests can supply a deterministic implementation. A demo-owned
seam nevertheless accepts the common `ISpeechModel` contract instead and performs the narrowing
itself, so a panel's unit tests can substitute a plain fake model for every scenario (including
"wrong role") rather than committing every fake model double to the
synthesis-specific shape. This is also what works around the static factory method itself not
being substitutable in a ViewModel unit test. `ISynthesizerSessionFactory` and
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
| `LoadAsync(model, parameterValues, cancellationToken)` | `Task<ISpeechSynthesizerEngine>` | See note |

`LoadAsync` throws for a null model; otherwise it returns an engine whose `IsAvailable` reports the outcome.

The unit holds one field, `_store`, the `SpeechModelStore` supplied at construction. The
constructor rejects a null store, so `_store` is non-null for the life of the instance and every
`LoadAsync` call can pass it to the library without a further check. The unit is stateless
otherwise, so it is safe to call from any thread and from concurrent callers, subject to the
thread-safety of the supplied store and of the library's `SpeechSynthesizerFactory`.

**Key Methods**:

- **LoadAsync(model, parameterValues?, cancellationToken)**: Narrows the model to
  `ISynthesisModel` and forwards it, the shared `SpeechModelStore`, and the `parameterValues` bag
  unchanged to `SpeechSynthesizerFactory.LoadAsync(ISynthesisModel, SpeechModelStore,
  ISpeechDiagnostics?, IReadOnlyDictionary<string, object>?, CancellationToken)`, which resolves
  the model's installed-files directory internally, inheriting that factory's "nothing throws for
  an ordinary unavailable state" contract. A model that declares a role other than synthesis — and is therefore not
  an `ISynthesisModel` — returns the library's own `UnavailableSpeechSynthesizerEngine.Instance`,
  exactly like a model that is not installed, rather than throwing: a host that lets a user
  choose an installed model with the wrong role must still get a working, if unavailable, engine
  back - including when a non-null `parameterValues` bag is supplied, since the role check
  happens before any parameter is consulted. The `parameterValues` argument is the untyped
  key-value bag `Settings.BuildValueBag()` assembles from the embedded settings panel's current
  parameter values (for example a selected voice); forwarding it unchanged is what lets picking a
  voice in the demo's TTS panel genuinely change what is synthesized.

**Error Handling**: Throws `ArgumentNullException` for a null model (an explicit misuse of the
seam's own contract). Never throws for an ordinary unavailable state (not installed, wrong role,
missing runtime): each is reported as an unavailable engine. The one other exception is the
`ArgumentException` the library's engine composition throws when `parameterValues` holds an
invalid value (wrong type, out of range, or not one of the offered choices) for a parameter the
model declares; unrecognized keys are ignored. The adapter does not catch or translate it.
The wrong-role path returns before the library is consulted, so it can never throw that
exception.

**Dependencies**: The library's `SpeechModelStore`, `SpeechSynthesizerFactory`, `ISpeechModel`,
`ISynthesisModel`, `ISpeechSynthesizerEngine`, `UnavailableSpeechSynthesizerEngine`.

**Callers**: `SynthesisPanelViewModel` (loads a synthesizer engine through this seam, at most
once per selected model/parameter combination, reused across many Play calls).
