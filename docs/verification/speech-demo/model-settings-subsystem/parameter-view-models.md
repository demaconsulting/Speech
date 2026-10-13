### ParameterViewModels Verification

#### Verification Approach

The presenters are tested directly against library parameter descriptors constructed in-test. The
abstract base is verified through the concrete presenters, since it has no behavior observable on
its own. No model, audio hardware or native runtime is required.

The XAML `DataTemplate` selection between `Slider` and `NumericUpDown` is not covered by an
automated UI test; see the _Accepted Gaps_ section of the ModelSettingsSubsystem verification
document.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own presenters
- **Test doubles**: None; the presenters are driven by real library descriptors

#### Acceptance Criteria

The unit passes when: each presenter kind starts at its declared default (the choice presenter
resolving the default by value) and enforces its own constraint (numeric clamping, additionally
rounding to the nearest whole number when the descriptor declares `IsInteger: true`, and keeping
fractions otherwise, and ignoring non-finite writes); a changed choice selection or toggled boolean
updates the boxed value and notifies observers, while a null or non-member choice selection is
ignored and the previous selection kept; and every presenter kind rejects a missing descriptor.

#### Test Scenarios

##### NumericParameterViewModel_Constructor_Parameter_StartsAtDeclaredDefault

**Scenario**: A numeric presenter is built from a library descriptor.

**Expected**: `Value` starts at the declared default, and `Minimum`/`Maximum`/`Step`/`Unit` all
come from the descriptor unchanged.

##### NumericParameterViewModel_Value_SetOutsideRange_ClampsToBounds

**Scenario**: `Value` is set above the declared maximum and below the declared minimum.

**Expected**: Both writes are clamped to the declared bounds.

##### NumericParameterViewModel_Value_SetFractionalOnContinuousParameter_KeepsFraction

**Scenario**: `Value` is set to a fractional value inside the range on a continuous parameter.

**Expected**: The fraction is kept, not rounded.

##### NumericParameterViewModel_Value_SetFractionalOnIntegerParameter_RoundsToNearestWholeNumber

**Scenario**: `Value` is set to a fractional value (`153.51`) on a presenter whose descriptor
declares `IsInteger: true`.

**Expected**: `Value` is rounded to the nearest whole number (`154`).

##### NumericParameterViewModel_Value_SetFractionalOutsideRangeOnIntegerParameter_ClampsAndRounds

**Scenario**: `Value` is set to a fractional value above the declared maximum on an integer
presenter.

**Expected**: The write is both clamped to the declared maximum and rounded, composing correctly
regardless of write order.

##### NumericParameterViewModel_Constructor_IntegerParameter_ExposesIsIntegerTrue

**Scenario**: A numeric presenter is built from a descriptor declaring `IsInteger: true` (for
example the LibriTTS-R `speaker` parameter).

**Expected**: `IsInteger` is exposed as `true`. This verifies both the presenter's reporting
requirement and, as far as the unit can, the whole-number-control requirement: the `DataTemplate`
binding that renders `NumericUpDown` instead of `Slider` is an accepted gap (see above).

##### NumericParameterViewModel_Value_SetNonFinite_KeepsPreviousValue

**Scenario**: (Theory) `Value` is set to `NaN`, `+Infinity` and `-Infinity` on a presenter that
already holds a valid value.

**Expected**: Each write is ignored and `Value` keeps its previous value.

##### ChoiceParameterViewModel_SelectedOption_SetNull_KeepsPreviousSelection

**Scenario**: `SelectedOption` is set to `null`.

**Expected**: The previous selection is kept and `BoxedValue` is not `null`.

##### ChoiceParameterViewModel_SelectedOption_SetNonMember_KeepsPreviousSelection

**Scenario**: `SelectedOption` is set to an option that is not among the declared options.

**Expected**: The previous selection is kept and `BoxedValue` still holds the previous option's value.

##### ChoiceParameterViewModel_Constructor_Parameter_StartsAtDeclaredDefaultOption

**Scenario**: A choice presenter is built from a library descriptor.

**Expected**: `SelectedOption` starts at the option whose value matches the declared default, and
`Options` carries the full declared option list.

##### ChoiceParameterViewModel_Constructor_Parameter_PresentsDeclaredOptionsInOrder

**Scenario**: A choice presenter is built from a descriptor declaring three options.

**Expected**: `Options` carries exactly the declared options, in declaration order.

##### ChoiceParameterViewModel_Constructor_NonFirstDefault_SelectsOptionMatchingDefaultByValue

**Scenario**: A choice presenter is built from a descriptor whose default is not the first option.

**Expected**: `SelectedOption` is the option matching the default by value, not the first option.

##### ChoiceParameterViewModel_SelectedOption_ChangedToDifferentOption_UpdatesBoxedValue

**Scenario**: A different declared option is selected.

**Expected**: `BoxedValue` reflects the newly selected option's value.

##### ChoiceParameterViewModel_SelectedOption_Changed_RaisesPropertyChanged

**Scenario**: `SelectedOption` is changed while a listener records property-changed notifications.

**Expected**: A notification is raised for the selection.

##### BooleanParameterViewModel_Constructor_Parameter_StartsAtDeclaredDefault

**Scenario**: A boolean presenter is built from a library descriptor.

**Expected**: `Value` starts at the declared default.

##### BooleanParameterViewModel_Constructor_FalseDefault_StartsFalseAndCarriesIdentity

**Scenario**: A boolean presenter is built from a descriptor whose default is `false`.

**Expected**: `Value` starts `false`, and `Id`, `DisplayName` and `Description` carry the
descriptor's identity.

##### BooleanParameterViewModel_Value_Toggled_UpdatesBoxedValue

**Scenario**: `Value` is toggled.

**Expected**: `BoxedValue` reflects the toggle.

##### ParameterViewModels_Constructor_NullParameter_ThrowArgumentNullException

**Scenario**: Each of the three presenter kinds is constructed with no library descriptor.

**Expected**: `ArgumentNullException` from every kind.
