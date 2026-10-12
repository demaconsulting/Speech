### RecognizerSessionFactory

**Purpose**:

`IRecognizerSessionFactory` / `RecognizerSessionFactory` is the demo-owned recognizer-engine
composition seam over the library's `SpeechModelStore` and `SpeechRecognizerFactory`. The two are
treated as one unit because the interface has no independently observable behavior of its own.

The library composes a recognizer engine through the static
`SpeechRecognizerFactory.LoadAsync(IRecognitionModel, SpeechModelStore, ISpeechDiagnostics?,
IReadOnlyDictionary<string,object>?, CancellationToken)` method, which requires an
`IRecognitionModel`. `IRecognitionModel` is a fully public interface (any assembly, including a
third-party one, may implement it), but a demo-owned seam still accepts the common `ISpeechModel`
contract instead and performs the narrowing itself, so a panel's unit tests can depend on the
simpler, role-agnostic contract rather than committing every fake model double to the
recognition-specific shape. This also works around the static factory method itself not being
substitutable in a ViewModel unit test.

**Data Model**:

| Member | Returns | Behavior |
| --- | --- | --- |
| `LoadAsync(model, cancellationToken)` | `Task<ISpeechRecognizerEngine>` | Never throws for a valid argument |

The unit holds the shared `SpeechModelStore` it was constructed with.

**Key Methods**:

- **`LoadAsync`**: narrows the model to `IRecognitionModel` and forwards it with the store to
  `SpeechRecognizerFactory.LoadAsync`, which resolves the model's installed-files directory itself
  and so inherits that factory's "nothing throws at composition" contract. A model that declares a
  role other than recognition - and is therefore not an `IRecognitionModel` - returns the library's
  own `UnavailableSpeechRecognizerEngine.Instance`, exactly like a model that is not installed,
  rather than throwing: a host that lets a user choose an installed model with the wrong role must
  still get a working, if unavailable, engine back.

**Error Handling**:

- A null model store at construction or a null model at `LoadAsync` throws
  `ArgumentNullException`, because both are composition mistakes that must surface at the call
  site.
- Every other outcome — a wrong-role model or an uninstalled model — is reported as an unavailable
  engine, not an exception.

**Dependencies**:

- The library's `SpeechModelStore`, `SpeechRecognizerFactory`, `IRecognitionModel` and
  `UnavailableSpeechRecognizerEngine`

**Callers**:

- `RecognitionPanelViewModel`, through `IRecognizerSessionFactory`
