## SpeechDemo ModelSettingsSubsystem Design

![ModelSettingsSubsystem Structure](ModelSettingsSubsystemView.svg)

### Overview

The ModelSettingsSubsystem provides the demo's generic per-model tunable-parameter settings
panel, embedded within the synthesis panel for whichever model is currently selected there. It
contains the following units, each documented in its own file:

- **ModelSettingsViewModel**: the panel's presentation state — the rendered presenters, the
  selected model, the honest empty-state message, and the value-bag capture and restore — see
  _ModelSettingsViewModel Design_ (`model-settings-subsystem/model-settings-view-model.md`)
- **ParameterViewModels** (`ParameterViewModelBase`, `NumericParameterViewModel`,
  `ChoiceParameterViewModel`, `BooleanParameterViewModel`): the presenters, one per library
  parameter type, over a shared base — see _ParameterViewModels Design_
  (`model-settings-subsystem/parameter-view-models.md`)

The abstract base class and the three concrete presenters are documented and verified as one unit.
The base class has no independently observable behavior: its identity members and value-bag hook
are only observable through the concrete presenters, and all three presenters share one
construction contract (a library descriptor in, a presenter starting at the declared default
out). Each presenter is still exercised directly by its own tests.

### Interfaces

The subsystem exposes `ModelSettingsViewModel` and the presenter types to the
SynthesisPanelSubsystem, which embeds the panel and consumes `BuildValueBag()` and
`RestoreValues()`. It consumes only the library's `ISpeechModel` and `ISpeechModelParameter`
descriptors (`NumericParameter`, `ChoiceParameter`, `BooleanParameter`).

### Design

The subsystem is a generic presenter set: `ModelSettingsViewModel` type-checks each declared
parameter and builds one presenter per recognized kind, and each presenter exposes the value-bag
hook the view model assembles from. This library's typed, self-describing parameter design lets a
host render an appropriate control for a parameter using only a type check against the three
concrete `ISpeechModelParameter` implementations. `ModelSettingsViewModel` performs exactly that
type check and nothing more, which is what proves a new model with its own declared parameter set
works with this panel unmodified — the panel never needs prior knowledge of a specific model's
parameter set.

The untyped key-value bag that `BuildValueBag()` assembles is the interface between a host's
settings UI and per-model synthesis parameter handling. `SynthesisPanelViewModel.PlayAsync`
forwards this bag as the `parameterValues` argument to `ISynthesizerSessionFactory.LoadAsync`,
which threads it through to the library's `SpeechSynthesizerFactory.LoadAsync` and, from there, to
`ISynthesisModel.ResolveSpeakerId`, so selecting a voice in the TTS panel changes synthesized
output. The recognition panel does not consume this bag and does not embed the settings panel:
`IRecognitionSession`'s contract accepts no per-call parameter bag on any member. The bag is
justified by its synthesis consumer; a library revision exposing a per-call recognition parameter
surface could reuse it.

### Interactions with Other Units

`ModelSettingsViewModel` is embedded directly by `SynthesisPanelViewModel`, which assigns its own
`SelectedModel` to the `Model` property whenever the selection changes, and which captures the bag
before a catalog rebuild (a voice pack install or removal) and restores it afterwards so the user
keeps their chosen voice and other settings. The subsystem depends only on the library's
`ISpeechModel` and `ISpeechModelParameter` value types, never on `ISynthesisModel`,
`IRecognitionModel`, or any audio/session seam, which is what allows every presenter kind —
including the model-switch and empty-state behavior — to be verified with no installed model and
no native runtime.

Within the subsystem, `ModelSettingsViewModel` constructs one `ParameterViewModels` presenter per
recognized parameter and reads each presenter's `Id` and `BoxedValue` (and writes its typed value
on restore); the presenters never call back into the view model.
