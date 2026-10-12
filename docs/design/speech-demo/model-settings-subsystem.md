## SpeechDemo ModelSettingsSubsystem Design

![ModelSettingsSubsystem Structure](ModelSettingsSubsystemView.svg)

### Overview

The ModelSettingsSubsystem provides the demo's generic per-model tunable-parameter settings
panel, embedded within the synthesis panel for whichever model is currently
selected there. It contains the following units:

- **ParameterViewModelBase**: the presentation state shared by every parameter kind — the
  parameter's id, display name, description, and its boxed current value
- **NumericParameterViewModel** / **ChoiceParameterViewModel** / **BooleanParameterViewModel**:
  the three concrete presenters, one per library parameter type
- **ModelSettingsViewModel**: the panel's presentation state — the rendered presenters, the
  selected model, and the honest empty-state message

### Interfaces

The subsystem exposes `ModelSettingsViewModel` and the three presenter types to the
SynthesisPanelSubsystem, which embeds the panel and consumes `BuildValueBag()` and
`RestoreValues()`. It consumes only the library's `ISpeechModel` and `ISpeechModelParameter`
descriptors (`NumericParameter`, `ChoiceParameter`, `BooleanParameter`).

### Design

The subsystem is a generic presenter set: `ModelSettingsViewModel` type-checks each declared
parameter and builds one presenter per recognized kind. The sections below describe each unit.

### Why a Generic Presenter Set

This library's typed, self-describing parameter design lets a host render an appropriate
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

`NumericParameterViewModel` adds `Value`, `Minimum`, `Maximum`, `Step`, `Unit` and `IsInteger`;
`ChoiceParameterViewModel` adds `Options` and `SelectedOption`; `BooleanParameterViewModel` adds
`Value`. Each concrete presenter's constructor rejects a null library descriptor
(`ArgumentNullException`), because a presenter built with no descriptor has nothing to display.
`ChoiceParameterViewModel` assumes the descriptor declares at least one option and selects the
option matching the default (falling back to the first option).

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
empty-state precedent the model catalog panel established for an empty `SpeechModelCatalog`: an
unexplained blank panel would read to a user as a broken application rather than an expected state.

**Value bag.** `BuildValueBag()` assembles every presented parameter's current `BoxedValue` into
a dictionary keyed by `Id`. The design describes this untyped key-value bag as the interface
between a host's settings UI and per-model synthesis parameter handling.
`SynthesisPanelViewModel.PlayAsync` forwards this bag as the `parameterValues` argument to
`ISynthesizerSessionFactory.LoadAsync`, which threads it through to the library's
`SpeechSynthesizerFactory.LoadAsync` and, from there, to `ISynthesisModel.ResolveSpeakerId`, so
selecting a voice in the TTS panel changes synthesized output. The recognition
panel does not consume this bag and does not embed the settings panel: `IRecognitionSession`'s
contract accepts no per-call parameter bag on any member. The bag is justified by its synthesis
consumer; a future library revision exposing a per-call recognition parameter surface could
reuse it.

**Restoring values.** `RestoreValues(values)` applies a previously captured bag onto the current
presenters: choices by option value, numerics as `double`, booleans as `bool`. Unknown keys,
wrongly typed values and options the model no longer offers are ignored, leaving the default.
`SynthesisPanelViewModel` captures the bag before a catalog rebuild (a voice pack install or
removal) and restores it afterwards so the user keeps their chosen voice and other settings.

### Interactions with Other Units

`ModelSettingsViewModel` is embedded directly by `SynthesisPanelViewModel`, which assigns its
own `SelectedModel` to the `Model` property
whenever the selection changes. It depends only on the library's `ISpeechModel` and
`ISpeechModelParameter` value types, never on `ISynthesisModel`, `IRecognitionModel`, or any
audio/session seam, which is what allows every presenter kind — including the model-switch and
empty-state behavior — to be verified with no installed model and no native runtime.
