## SpeechDemo ModelSettingsSubsystem Verification

### Verification Approach

The ModelSettingsSubsystem is verified through deterministic unit tests in
`ModelSettingsViewModelTests.cs`, exercising `ModelSettingsViewModel` and all three concrete
parameter presenters directly against library parameter descriptors constructed in-test
(`NumericParameter`, `ChoiceParameter`, `BooleanParameter`) and a fake `ISpeechModel`. No audio
hardware, native runtime, or downloaded model is required, because this subsystem's whole
behavior is generic rendering over the library's public parameter contract.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own presenters and view model
- **Test doubles**: `FakeSpeechModel` declaring a controlled set of parameters

### Test Scenarios

#### ModelSettingsViewModel_Constructor_NoModel_ReportsNoModelEmptyState

**Scenario**: The panel is constructed with no model.

**Expected**: `HasModel` is false, `Parameters` is empty, and `EmptyMessage` is `NoModelMessage`.

**Requirement coverage**: `SpeechDemo-Settings-HonestEmptyState`.

#### ModelSettingsViewModel_Model_ModelWithNoParameters_ReportsNoParametersEmptyState

**Scenario**: A model declaring no parameters is selected.

**Expected**: `HasModel` is true, `HasParameters` is false, and `EmptyMessage` is
`NoParametersMessage`, distinguishing this case from "no model selected".

**Requirement coverage**: `SpeechDemo-Settings-HonestEmptyState`.

#### ModelSettingsViewModel_Model_ModelWithAllThreeParameterKinds_RendersMatchingPresenters

**Scenario**: A model declaring one numeric, one choice, and one boolean parameter is selected.

**Expected**: Exactly three presenters are built, each of the concrete type matching its
parameter, in declaration order.

**Requirement coverage**: `SpeechDemo-Settings-GenericParameterRendering`.

#### ModelSettingsViewModel_Model_ChangedToDifferentModel_ReplacesPresenters

**Scenario**: The panel is switched from a model with one parameter to a different model
declaring a different parameter.

**Expected**: Only the new model's parameter is presented; the previous presenter is discarded
rather than merged.

**Requirement coverage**: `SpeechDemo-Settings-ModelSwitch`.

#### ModelSettingsViewModel_BuildValueBag_AllThreeParameterKinds_RoundTripsCurrentValues

**Scenario**: All three presenter values are changed from their defaults, and the value bag is
assembled.

**Expected**: The bag contains each parameter's changed value keyed by its declared id.

**Requirement coverage**: `SpeechDemo-Settings-ValueBag`.

#### ModelSettingsViewModel_BuildValueBag_NoModel_ReturnsEmptyBag

**Scenario**: The value bag is assembled with no model selected.

**Expected**: An empty bag, not a fault.

**Requirement coverage**: `SpeechDemo-Settings-ValueBag`.

#### NumericParameterViewModel_Constructor_Parameter_StartsAtDeclaredDefault

**Scenario**: A numeric presenter is built from a library descriptor.

**Expected**: `Value` starts at the declared default, and `Minimum`/`Maximum`/`Step`/`Unit` all
come from the descriptor unchanged.

**Requirement coverage**: `SpeechDemo-Settings-NumericPresenter-DefaultInitialization`.

#### NumericParameterViewModel_Value_SetOutsideRange_ClampsToBounds

**Scenario**: `Value` is set above the declared maximum and below the declared minimum.

**Expected**: Both writes are clamped to the declared bounds.

**Requirement coverage**: `SpeechDemo-Settings-NumericPresenter-Clamping`.

#### NumericParameterViewModel_Constructor_IntegerParameter_ExposesIsIntegerTrue

**Scenario**: A numeric presenter is built from a library descriptor declaring `IsInteger: true`
(for example the LibriTTS-R `speaker` parameter).

**Expected**: `IsInteger` is exposed as `true`, so `ModelSettingsView.axaml`'s `DataTemplate`
renders `NumericUpDown` instead of `Slider` for this presenter.

**Requirement coverage**: `SpeechDemo-Settings-NumericPresenter-ControlSelection`.

#### NumericParameterViewModel_Value_SetFractionalOnIntegerParameter_RoundsToNearestWholeNumber

**Scenario**: `Value` is set to a fractional value (`153.51`) on a presenter whose descriptor
declares `IsInteger: true`.

**Expected**: `Value` is rounded to the nearest whole number (`154`), proving no code path - test,
XAML binding, or otherwise - can leave the presenter holding a fractional value for a parameter
with no fractional meaning.

**Requirement coverage**: `SpeechDemo-Settings-NumericPresenter-IntegerRounding`.

#### NumericParameterViewModel_Value_SetFractionalOutsideRangeOnIntegerParameter_ClampsAndRounds

**Scenario**: `Value` is set to a fractional value above the declared maximum on an integer
presenter.

**Expected**: The write is both clamped to the declared maximum and rounded, composing correctly
regardless of write order.

**Requirement coverage**: `SpeechDemo-Settings-NumericPresenter-IntegerRounding`.

#### ChoiceParameterViewModel_Constructor_Parameter_StartsAtDeclaredDefaultOption

**Scenario**: A choice presenter is built from a library descriptor.

**Expected**: `SelectedOption` starts at the option whose value matches the declared default, and
`Options` carries the full declared option list.

**Requirement coverage**: `SpeechDemo-Settings-ChoicePresenter`.

#### ChoiceParameterViewModel_SelectedOption_ChangedToDifferentOption_UpdatesBoxedValue

**Scenario**: A different declared option is selected.

**Expected**: `BoxedValue` reflects the newly selected option's value.

**Requirement coverage**: `SpeechDemo-Settings-ChoicePresenter`.

#### BooleanParameterViewModel_Constructor_Parameter_StartsAtDeclaredDefault

**Scenario**: A boolean presenter is built from a library descriptor.

**Expected**: `Value` starts at the declared default.

**Requirement coverage**: `SpeechDemo-Settings-BooleanPresenter`.

#### BooleanParameterViewModel_Value_Toggled_UpdatesBoxedValue

**Scenario**: `Value` is toggled.

**Expected**: `BoxedValue` reflects the toggle.

**Requirement coverage**: `SpeechDemo-Settings-BooleanPresenter`.

#### ParameterViewModels_Constructor_NullParameter_ThrowArgumentNullException

**Scenario**: Each of the three presenter kinds is constructed with no library descriptor.

**Expected**: `ArgumentNullException` from every kind.

**Requirement coverage**: `SpeechDemo-Settings-ParameterConstructionGuards`.

### Requirements Coverage

- **`SpeechDemo-Settings-GenericParameterRendering`**:
  `ModelSettingsViewModel_Model_ModelWithAllThreeParameterKinds_RendersMatchingPresenters`
- **`SpeechDemo-Settings-NumericPresenter-DefaultInitialization`**:
  `NumericParameterViewModel_Constructor_Parameter_StartsAtDeclaredDefault`
- **`SpeechDemo-Settings-NumericPresenter-Clamping`**:
  `NumericParameterViewModel_Value_SetOutsideRange_ClampsToBounds`
- **`SpeechDemo-Settings-NumericPresenter-IntegerRounding`**:
  `NumericParameterViewModel_Value_SetFractionalOnIntegerParameter_RoundsToNearestWholeNumber`,
  `NumericParameterViewModel_Value_SetFractionalOutsideRangeOnIntegerParameter_ClampsAndRounds`
- **`SpeechDemo-Settings-NumericPresenter-ControlSelection`**:
  `NumericParameterViewModel_Constructor_IntegerParameter_ExposesIsIntegerTrue`
- **`SpeechDemo-Settings-ChoicePresenter`**:
  `ChoiceParameterViewModel_Constructor_Parameter_StartsAtDeclaredDefaultOption`,
  `ChoiceParameterViewModel_SelectedOption_ChangedToDifferentOption_UpdatesBoxedValue`
- **`SpeechDemo-Settings-BooleanPresenter`**:
  `BooleanParameterViewModel_Constructor_Parameter_StartsAtDeclaredDefault`,
  `BooleanParameterViewModel_Value_Toggled_UpdatesBoxedValue`
- **`SpeechDemo-Settings-ModelSwitch`**:
  `ModelSettingsViewModel_Model_ChangedToDifferentModel_ReplacesPresenters`
- **`SpeechDemo-Settings-ParameterConstructionGuards`**:
  `ParameterViewModels_Constructor_NullParameter_ThrowArgumentNullException`
- **`SpeechDemo-Settings-ValueBag`**:
  `ModelSettingsViewModel_BuildValueBag_AllThreeParameterKinds_RoundTripsCurrentValues`,
  `ModelSettingsViewModel_BuildValueBag_NoModel_ReturnsEmptyBag`
- **`SpeechDemo-Settings-HonestEmptyState`**:
  `ModelSettingsViewModel_Constructor_NoModel_ReportsNoModelEmptyState`,
  `ModelSettingsViewModel_Model_ModelWithNoParameters_ReportsNoParametersEmptyState`

### Acceptance Criteria

A ModelSettingsSubsystem test run passes when: one presenter is rendered per declared parameter,
chosen by concrete type alone; each presenter kind starts at its declared default and enforces
its own constraint (numeric clamping - additionally rounding to the nearest whole number when the
descriptor declares `IsInteger: true` - choice restricted to declared options); switching models
replaces rather than merges presenters; the value bag round-trips every presented value and is
empty rather than faulted with no model; every presenter kind rejects a missing descriptor; and
both "no model" and "model with no parameters" are explained rather than shown blank.

### Accepted Gap: XAML Template Selection

`ModelSettingsView.axaml`'s `DataTemplate` selection between `Slider` (continuous) and
`NumericUpDown` (`IsInteger`) is not itself exercised by an automated UI test. This subsystem has
no existing precedent for XAML-level template-selection testing - every other `DataTemplate`
choice in this view (choice/boolean presenters) is likewise verified only indirectly, through the
`NumericParameterViewModel`/`ChoiceParameterViewModel`/`BooleanParameterViewModel` type checks in
`ModelSettingsViewModel_Model_ModelWithAllThreeParameterKinds_RendersMatchingPresenters` rather
than a rendered-control assertion. Consistent with that precedent, the `IsInteger` binding path is
confirmed structurally (the compiled XAML's `IsVisible="{Binding IsInteger}"`/
`IsVisible="{Binding !IsInteger}"` bindings and the `NumericUpDown`'s
`Value`/`Minimum`/`Maximum`/`Increment`/`FormatString` bindings were read and checked by hand
against `NumericParameterViewModel`'s members) rather than through a new UI testing mechanism
invented for this pass.
