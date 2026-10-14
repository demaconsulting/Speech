### ModelSettingsViewModel Verification

#### Verification Approach

`ModelSettingsViewModel` is tested directly against a fake `ISpeechModel` declaring a controlled
set of library parameters. The tests cover the empty states, model switching, value-bag capture
and restore, and the handling of stale or unrecognized input. The abstract `ISpeechModelParameter`
is publicly implementable, so a nested test parameter of an unrecognized kind exercises the
skip-unrecognized-kind path without any source change.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own view model
- **Test doubles**: `FakeSpeechModel`, and a nested `UnrecognizedParameter` implementing
  `ISpeechModelParameter`

#### Acceptance Criteria

The unit passes when: both "no model" and "model with no parameters" are explained rather than
shown blank and the empty-state members are notified on change; switching models replaces rather
than merges presenters and an unrecognized parameter kind is skipped; the value bag is empty rather
than faulted with no model and a duplicate identifier does not throw; and a restore applies
captured values while ignoring stale, unknown or wrongly typed entries, and offers options that only
the replacement model instance declares while keeping a still-offered captured selection.

#### Test Scenarios

##### ModelSettingsViewModel_Constructor_NoModel_ReportsNoModelEmptyState

**Scenario**: The panel is constructed with no model.

**Expected**: `HasModel` is false, `Parameters` is empty, and `EmptyMessage` is `NoModelMessage`.

##### ModelSettingsViewModel_Model_ChangedToNull_ClearsPresentersAndReportsNoModelState

**Scenario**: A model with parameters is selected, then `Model` is assigned `null`.

**Expected**: `Parameters` is cleared and the panel reports the no-model empty state.

##### ModelSettingsViewModel_Model_ModelWithNoParameters_ReportsNoParametersEmptyState

**Scenario**: A model declaring no parameters is selected.

**Expected**: `HasModel` is true, `HasParameters` is false, and `EmptyMessage` is
`NoParametersMessage`, distinguishing this case from "no model selected".

##### ModelSettingsViewModel_Model_Changed_RaisesEmptyStateNotifications

**Scenario**: `Model` is assigned while a listener records property-changed notifications.

**Expected**: Notifications are raised for the empty-state members (`HasModel`, `HasParameters`,
`IsEmpty`, `EmptyMessage`).

##### ModelSettingsViewModel_Model_ChangedToDifferentModel_ReplacesPresenters

**Scenario**: The panel is switched from a model with one parameter to a different model
declaring a different parameter.

**Expected**: Only the new model's parameter is presented; the previous presenter is discarded
rather than merged.

##### ModelSettingsViewModel_Model_UnrecognizedParameterKind_SkipsItAndPresentsTheRest

**Scenario**: A model declares a parameter of a kind the view model does not recognize alongside
recognized parameters.

**Expected**: The unrecognized parameter is skipped and every recognized parameter is presented.

##### ModelSettingsViewModel_BuildValueBag_NoModel_ReturnsEmptyBag

**Scenario**: The value bag is assembled with no model selected.

**Expected**: An empty bag, not a fault.

##### ModelSettingsViewModel_BuildValueBag_DuplicateParameterIds_LaterPresenterWins

**Scenario**: A model declares two parameters with the same identifier and different values.

**Expected**: The bag is assembled without throwing and holds the later presenter's value under
the shared identifier.

##### ModelSettingsViewModel_RestoreValues_AppliesCapturedValues

**Scenario**: A model with a 150-voice choice, a numeric and a boolean parameter is changed, the
bag captured, the model replaced by an equivalent instance (defaults restored) and the bag
restored.

**Expected**: Every control is back at its captured value.

##### ModelSettingsViewModel_VoiceChoices_ReferenceVoicesChanged_NewVoicesAvailableAndSelectionRetained

**Scenario**: A choice selection is captured, a replacement model instance declaring additional
options is assigned, and the bag is restored.

**Expected**: The additional options are presented and the captured selection remains selected.

##### ModelSettingsViewModel_RestoreValues_UnavailableOptionOrKey_KeepsDefault

**Scenario**: A bag with an option the model no longer offers, an unknown key, a wrongly typed
value and a null is restored.

**Expected**: Nothing throws and the affected controls keep their defaults.

##### ModelSettingsViewModel_RestoreValues_WrongTypedValues_KeepsDefaults

**Scenario**: A bag whose entries all carry the wrong value type for their parameters is
restored.

**Expected**: Nothing throws and every control keeps its default.
