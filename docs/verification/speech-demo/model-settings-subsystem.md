## SpeechDemo ModelSettingsSubsystem Verification

### Verification Approach

The ModelSettingsSubsystem is verified through deterministic unit tests that exercise
`ModelSettingsViewModel` and the parameter presenters directly against library parameter
descriptors constructed in-test (`NumericParameter`, `ChoiceParameter`, `BooleanParameter`) and a
fake `ISpeechModel`. Subsystem-level scenarios, below, cover behavior that spans both units:
generic rendering of every parameter kind and the value bag assembled from the presenters. Unit
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
presenters round-trips every presented value keyed by its declared identifier. The acceptance
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
