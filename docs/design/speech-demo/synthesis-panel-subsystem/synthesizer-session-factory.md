### SynthesizerSessionFactory

**Purpose**: Provide a demo-owned synthesizer composition seam over the library's
`SpeechModelStore` and `SpeechSynthesizerFactory`, accepting the library's public `ISpeechModel`
contract so the panel can be tested with a plain fake model and no `InternalsVisibleTo` grant
from the library.

**Why a Demo-Owned Seam**: The library composes a synthesizer through the static
`SpeechSynthesizerFactory.Create(ISynthesisModel, string, IAudioPlaybackDevice,
ISpeechDiagnostics?)` method, which requires an `ISynthesisModel` — an interface whose members
are partly `internal` to the library, so only the library's own assemblies can implement it. A
demo-owned seam therefore accepts the common `ISpeechModel` contract instead and performs the
narrowing itself. This is also what works around the static factory method itself not being
substitutable in a ViewModel unit test. `ISynthesizerSessionFactory` and
`SynthesizerSessionFactory` are documented as one unit because the interface has no
independently observable behavior of its own — every test exercises it through
`SynthesizerSessionFactory`, its sole implementation.

**Data Model**:

| Member | Returns | Behavior |
| --- | --- | --- |
| `Create(model, playbackDevice, parameterValues)` | `ISpeechSynthesizer` | Never throws |

**Key Methods**:

- **Create(model, playbackDevice, parameterValues?)**: Resolves the model's installed-files
  directory from the shared `SpeechModelStore`, narrows the model to `ISynthesisModel`, and
  forwards to `SpeechSynthesizerFactory.Create` (including the `parameterValues` bag unchanged),
  inheriting that factory's "nothing throws at composition" contract. A model that declares a
  role other than synthesis — and is therefore not an `ISynthesisModel` — returns the library's
  own `UnavailableSpeechSynthesizer.Instance`, exactly like a model that is not installed, rather
  than throwing: a host that lets a user choose an installed model with the wrong role must
  still get a working, if unavailable, synthesizer back. The `parameterValues` argument is the
  untyped key-value bag `Settings.BuildValueBag()` assembles from the embedded settings panel's
  current parameter values (for example a selected voice); forwarding it unchanged is what lets
  picking a voice in the demo's TTS panel genuinely change what is synthesized.

**Error Handling**: Throws `ArgumentNullException` for a null model or playback device (an
explicit misuse of the seam's own contract); otherwise never throws, returning an honest
unavailable synthesizer for every unavailable composition state.

**Dependencies**: The library's `SpeechModelStore`, `SpeechSynthesizerFactory`, `ISpeechModel`,
`ISynthesisModel`, `IAudioPlaybackDevice`, `UnavailableSpeechSynthesizer`.

**Callers**: `SynthesisPanelViewModel` (composes a synthesizer through this seam on `Play`).
