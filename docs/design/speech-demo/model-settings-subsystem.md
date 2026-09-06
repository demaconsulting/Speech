## SpeechDemo ModelSettingsSubsystem Design

![ModelSettingsSubsystem Structure](ModelSettingsSubsystemView.svg)

### Overview

The ModelSettingsSubsystem provides the demo's generic per-model tunable-parameter settings
panel, embedded within the synthesis and recognition panels for whichever model is currently
selected there. It contains the following units:

- **ParameterViewModelBase**: the presentation state shared by every parameter kind — the
  parameter's id, display name, description, and its boxed current value
- **NumericParameterViewModel** / **ChoiceParameterViewModel** / **BooleanParameterViewModel**:
  the three concrete presenters, one per library parameter type
- **ModelSettingsViewModel**: the panel's presentation state — the rendered presenters, the
  selected model, and the honest empty-state message

### Why a Generic Presenter Set

this library's typed, self-describing parameter design lets a host render an appropriate
control for a parameter using only a type check against the three concrete
`ISpeechModelParameter` implementations. `ModelSettingsViewModel` performs exactly that type
check and nothing more, which is what proves a new model with its own declared parameter set
works with this panel unmodified — the panel never needs prior knowledge of a specific model's
parameter set.

### ParameterViewModelBase

| Member | Type | Purpose |
| --- | --- | --- |
| `Id` | `string` | The stable key used in the untyped key-value bag |
| `DisplayName` | `string` | The short label a host UI shows |
| `Description` | `string` | The longer explanation shown as a tooltip or help text |
| `BoxedValue` | `abstract object` | The parameter's current value, boxed for the value bag |

Each concrete presenter's constructor rejects a null library descriptor, because a presenter
built with no descriptor has nothing to display.

### Concrete Presenters

| Presenter | Rendered as | Starting value | Constraint enforced |
| --- | --- | --- | --- |
| `NumericParameterViewModel` | Slider / `NumericUpDown` (`IsInteger`) | Declared default | Clamped, rounded if int |
| `ChoiceParameterViewModel` | Dropdown | Option matching default | `SelectedOption` restricted to declared options |
| `BooleanParameterViewModel` | Checkbox / toggle | Declared default | None beyond the two boolean states |

`NumericParameterViewModel.Value` clamps on every write specifically so a bound control that
allows a wider range than declared — for example, free-text entry — or a test setting the value
directly can never push the parameter outside the range the model declared it could honor. When
the underlying `NumericParameter.IsInteger` is `true` (for example the LibriTTS-R
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`'s `speaker` parameter, a discrete 0-903 index with
no fractional meaning), `Value` additionally rounds every write to the nearest whole number, and
`ModelSettingsView.axaml`'s `DataTemplate` renders Avalonia's built-in `NumericUpDown` — bound to
`Value`/`Minimum`/`Maximum`/`Step` with `FormatString="0"` — instead of the `Slider` used for a
continuous parameter, so a fractional value (for example a slider landing on `153.51`) can never
be selected in the first place. `NumericParameterViewModel.IsInteger` mirrors the library
descriptor's flag and selects which control the `DataTemplate` shows via `IsInteger`/`!IsInteger`
visibility bindings on the two controls.

### ModelSettingsViewModel

| Member | Type | Purpose |
| --- | --- | --- |
| `NoModelMessage` | `const string` | The explanation shown when no model is selected |
| `NoParametersMessage` | `const string` | The explanation shown for a model with no declared parameters |
| `Parameters` | `ObservableCollection<ParameterViewModelBase>` | The rendered presenters, in declaration order |
| `Model` | `ISpeechModel?` | The model whose parameters are presented |
| `HasModel` / `HasParameters` / `IsEmpty` | `bool` | The current empty-state classification |
| `EmptyMessage` | `string` | The message matching the current empty state |

**Rebuild algorithm.** Assigning `Model` — including assigning `null` — clears `Parameters` and
rebuilds it by pattern-matching each of the model's declared parameters to its concrete presenter
type, skipping any parameter kind the presenter does not recognize. Presenters are rebuilt rather
than merged because a newly selected model's parameter set is unrelated to the previous one's;
keeping a stale presenter alive across a model change would let the panel offer a control for a
parameter the new model does not declare.

**Empty states.** `IsEmpty` is true whenever there is no model selected or the selected model
declares no parameters; `EmptyMessage` distinguishes the two cases. This mirrors the honest
empty-state precedent the model catalog panel established for `SpeechModelCatalog.KnownModels`
being empty: an unexplained blank panel would read to a user as a broken application rather than
an expected state.

**Value bag.** `BuildValueBag()` assembles every presented parameter's current `BoxedValue` into
a dictionary keyed by `Id`. The design describes this untyped key-value bag as the interface
between a host's settings UI and per-model synthesis/recognition parameter handling. As of this
pass, `SynthesisPanelViewModel.PlayAsync` forwards this bag as the `parameterValues` argument to
`ISynthesizerSessionFactory.Create`, which threads it through to the library's
`SpeechSynthesizerFactory.Create` and, from there, to `ISynthesisModel.ResolveSpeakerId` — so
selecting a voice in the TTS panel now genuinely changes synthesized output. The recognition
panel does not yet consume this bag: `ISpeechRecognizer`'s contract accepts no per-call parameter
bag on any member, so for recognition it remains built and fully exercised by unit tests, ready
for a future library revision exposing a per-call recognition parameter surface. This is stated
plainly here rather than silently implied.

### Interactions with Other Units

`ModelSettingsViewModel` is embedded directly by `SynthesisPanelViewModel` and
`RecognitionPanelViewModel`, which assign their own `SelectedModel` to its `Model` property
whenever the selection changes. It depends only on the library's `ISpeechModel` and
`ISpeechModelParameter` value types, never on `ISynthesisModel`, `IRecognitionModel`, or any
audio/session seam, which is what allows every presenter kind — including the model-switch and
empty-state behavior — to be verified with no installed model and no native runtime.
