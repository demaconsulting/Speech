## SpeechDemo ModelSettingsSubsystem Verification

### Verification Approach

The ModelSettingsSubsystem is verified through deterministic tests that exercise
`ModelSettingsViewModel` together with the parameter presenters it builds (the subsystem boundary
is the view model's public surface: `Model`, `Parameters`, `BuildValueBag`, `RestoreValues` and the
empty-state members), against library parameter descriptors constructed in-test (`NumericParameter`,
`ChoiceParameter`, `BooleanParameter`, plus an unrecognized-kind descriptor) and a
fake `ISpeechModel`. Subsystem-level scenarios, below, cover behavior that spans both units:
generic rendering of every parameter kind, the value bag assembled from the presenters, restoring
captured values, the empty states, and tolerance of an unrecognized parameter kind. Unit
behavior is verified in each unit's own verification document
(`model-settings-subsystem/model-settings-view-model.md` and
`model-settings-subsystem/parameter-view-models.md`). No audio hardware, native runtime, or
downloaded model is required, because this subsystem's whole behavior is generic rendering over the
library's public parameter contract.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own presenters and view model
- **Test doubles**: `FakeSpeechModel` declaring a controlled set of parameters

### Acceptance Criteria

A ModelSettingsSubsystem test run passes when: one presenter is rendered per declared parameter,
chosen by concrete type alone, in declaration order; and the value bag assembled from the
presenters round-trips every presented value keyed by its declared identifier; captured values are
restored while keeping defaults for keys or options that no longer apply; an empty panel explains
whether no model is selected or the model has no parameters; and a parameter of an unrecognized
kind is skipped without hiding the others. The acceptance
criteria of each unit are stated in its own verification document.

### Test Scenarios

#### ModelSettingsViewModel_Model_ModelWithAllThreeParameterKinds_RendersMatchingPresenters

**Scenario**: A model declaring one numeric, one choice, and one boolean parameter is selected.

**Expected**: Exactly three presenters are built, each of the concrete type matching its
parameter, in declaration order.

#### ModelSettingsViewModel_BuildValueBag_AllThreeParameterKinds_RoundTripsCurrentValues

**Scenario**: All three presenter values are changed from their defaults, and the value bag is
assembled.

**Expected**: The bag contains each parameter's changed value keyed by its declared id.

#### ModelSettingsViewModel_RestoreValues_AppliesCapturedValues

**Scenario**: A bag captured from a model's presenters is restored into the presenters of the model.

**Expected**: Every presenter takes its captured value.

#### ModelSettingsViewModel_RestoreValues_UnavailableOptionOrKey_KeepsDefault

**Scenario**: The restored bag holds a key the model does not declare and a choice option the
parameter no longer offers.

**Expected**: The affected presenters keep their defaults and the other values still apply.

#### ModelSettingsViewModel_Constructor_NoModel_ReportsNoModelEmptyState

**Scenario**: The panel is created with no model.

**Expected**: The panel is empty and its message says no model is selected.

#### ModelSettingsViewModel_Model_ModelWithNoParameters_ReportsNoParametersEmptyState

**Scenario**: A model that declares no parameters is selected.

**Expected**: The panel is empty and its message says the model has no parameters.

#### ModelSettingsViewModel_Model_UnrecognizedParameterKind_SkipsItAndPresentsTheRest

**Scenario**: A model declares a recognized parameter, a parameter of an unrecognized kind, then another
recognized parameter.

**Expected**: The unrecognized parameter is skipped and the two recognized ones are presented in order.

### Accepted Gaps

#### XAML Template Selection

`ModelSettingsView.axaml`'s `DataTemplate` selection between `Slider` (continuous) and
`NumericUpDown` (`IsInteger`) is not itself exercised by an automated UI test. This subsystem has
no precedent for XAML-level template-selection testing - every other `DataTemplate` choice in this
view (choice/boolean presenters) is likewise verified only indirectly, through the presenter type
checks in `ModelSettingsViewModel_Model_ModelWithAllThreeParameterKinds_RendersMatchingPresenters`
rather than a rendered-control assertion. Consistent with that precedent, the `IsInteger` binding
path is confirmed structurally (the compiled XAML's `IsVisible="{Binding IsInteger}"`/
`IsVisible="{Binding !IsInteger}"` bindings and the `NumericUpDown`'s
`Value`/`Minimum`/`Maximum`/`Increment`/`FormatString` bindings were read and checked by hand
against `NumericParameterViewModel`'s members) rather than through a UI testing mechanism that has
no precedent in this project.
