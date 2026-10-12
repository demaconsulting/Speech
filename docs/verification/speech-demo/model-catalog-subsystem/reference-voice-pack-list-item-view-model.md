### ReferenceVoicePackListItemViewModel Verification

#### Verification Approach

`ReferenceVoicePackListItemViewModel` is verified through deterministic unit tests that construct
rows directly from library `ReferenceVoicePack` values and assert the projected details, derived
flags, captions and change announcements. The row touches neither the catalog seam nor Avalonia.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test builds its own row; no shared state exists between tests

#### Acceptance Criteria

The unit passes when: a missing pack is rejected; the pack's identity, size, license and consent
note are projected readably; install, remove and status caption follow the install state; the
installed-size caption follows the emotion option and the installed variant; "Add emotions" is
offered only for a voices-only install; and a failure message update re-announces its flag.

#### Test Scenarios

##### ReferenceVoicePackListItemViewModel_Constructor_NullPack_ThrowsArgumentNullException

**Scenario**: A row is built with no pack.

**Expected**: `ArgumentNullException`.

##### ReferenceVoicePackListItemViewModel_Constructor_ProjectsPackDetails

**Scenario**: A row is built from a pack.

**Expected**: Identity, size text, license text and consent note are projected from the pack.

##### ReferenceVoicePackListItemViewModel_State_DrivesFlagsAndStatusText

**Scenario**: The row's install state is changed through each state.

**Expected**: The install and remove enablement and the status caption follow the state.

##### ReferenceVoicePackListItemViewModel_InstalledSizeText_FollowsOptionAndInstall

**Scenario**: The include-emotions option and the installed variant are varied.

**Expected**: The installed-size caption shows the voices-only or with-emotions size and follows
the installed variant.

##### ReferenceVoicePackListItemViewModel_AddEmotions_OnlyForVoicesOnlyInstall

**Scenario**: Installed rows with and without emotion clips are compared.

**Expected**: "Add emotions" and the upgrade flag are offered only for a voices-only install of a
pack that has emotion variants.

##### ReferenceVoicePackListItemViewModel_FailureMessage_UpdatesHasFailureMessage

**Scenario**: A failure message is set and cleared.

**Expected**: The failure flag follows the message and is re-announced.

##### ReferenceVoicePackListItemViewModel_State_Changed_RaisesDependentPropertyChanged

**Scenario**: The row's state is changed while a property-change listener is attached.

**Expected**: Change notifications are raised for the state and every derived value (status caption,
install and remove enablement, installing flag, installed-size caption, upgrade flag, emotion option
visibility and button text).

##### ReferenceVoicePackListItemViewModel_HasEmotions_Changed_RaisesDependentPropertyChanged

**Scenario**: The installed-with-emotions flag is changed on an installed row.

**Expected**: Change notifications are raised for the flag, the status caption, the installed-size
caption, install enablement, the upgrade flag and the button text.

##### ReferenceVoicePackListItemViewModel_IncludeEmotions_Changed_RaisesInstalledSizeTextChanged

**Scenario**: The include-emotions option is cleared on a not-yet-installed row.

**Expected**: The installed-size caption change is announced and now reads "voices only".

##### ReferenceVoicePackListItemViewModel_FailureMessage_Changed_RaisesHasFailureMessageChanged

**Scenario**: A failure message is set while a property-change listener is attached.

**Expected**: A change notification is raised for the failure flag.

##### ReferenceVoicePackListItemViewModel_PackWithoutEmotionsOrSize_HidesEmotionOptionAndSize

**Scenario**: A row is built from a pack with no emotion clips and no known installed size.

**Expected**: The emotion option is hidden, emotions are never requested, no upgrade is offered, the
installed-size caption is empty, and the installed status reads "Installed".

##### ReferenceVoicePackListItemViewModel_UnknownState_StatusTextFallsBackToStateName

**Scenario**: A row is built with an undefined state value.

**Expected**: The status caption falls back to the state's name.
