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
of the kind matching the parameter, in declaration order; and the value bag assembled from the
presenters round-trips every presented value keyed by its declared identifier; captured values are
restored while keeping defaults for keys or options that no longer apply; after a model
installation notification newly available voices are offered immediately and the prior voice
selection is retained; an empty panel explains
whether no model is selected or the model has no parameters; and a parameter of an unrecognized
kind is skipped without hiding the others; a numeric parameter declared as whole-number gets a
whole-number-only control and no continuous slider. The acceptance
criteria of each unit are stated in its own verification document.

### Test Scenarios

#### ModelSettingsViewModel_Model_ModelWithAllThreeParameterKinds_RendersMatchingPresenters

**Scenario**: A model declaring one numeric, one choice, and one boolean parameter is selected.

**Expected**: Exactly three presenters are built (one item per declared parameter), each of the
kind matching its parameter (numeric, choice, boolean), in declaration order. This single test
verifies the count, order and kind requirements as separate assertions.

#### ModelSettingsViewModel_VoiceChoices_ReferenceVoicesChanged_NewVoicesAvailableAndSelectionRetained

**Scenario**: A model with a voice choice is presented and the user selects one voice; the bag is
captured. A model installation notification then yields a replacement model instance whose voice
choice declares additional voices; it is assigned and the captured bag restored.

**Expected**: The newly available voices are offered immediately among the choice's options, and
the voice the user chose is still selected.

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

#### ModelSettingsViewModel_RestoreValues_WrongTypedValues_KeepsDefaults

**Scenario**: The restored bag holds values of the wrong type for their parameters.

**Expected**: The affected presenters keep their defaults and nothing throws.

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

#### ModelSettingsView_NumericTemplate_IntegerParameter_ShowsWholeNumberControlInsteadOfSlider

**Scenario**: The view markup's numeric parameter template is read (the demo has no headless UI host,
so the markup is checked structurally rather than rendered).

**Expected**: The template holds one slider visible only when the parameter is not declared
whole-number and one up-down control visible only when it is, with the up-down formatted in whole
numbers and bound to the presenter's value and bounds. Together with the presenter rounding every
write to a whole number, no fractional value can be selected for such a parameter.

### Accepted Gaps

#### Rendered Controls

The structural test reads the view markup; it does not render the view, so Avalonia's own control
behavior (for example how `NumericUpDown` handles typed input) is not exercised and is covered
by the Avalonia OTS evidence instead. The choice and boolean templates are likewise verified only
indirectly, through the presenter type checks in
`ModelSettingsViewModel_Model_ModelWithAllThreeParameterKinds_RendersMatchingPresenters`.
