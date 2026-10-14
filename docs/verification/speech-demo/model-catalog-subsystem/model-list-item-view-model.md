### ModelListItemViewModel Verification

#### Verification Approach

`ModelListItemViewModel` is verified through deterministic unit tests that construct rows directly
from library `SpeechModelDescriptor` values and assert the derived captions and flags. The row
touches neither the library catalog nor Avalonia, so no hardware, network or installed model is
needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test builds its own row; no shared state exists between tests

#### Acceptance Criteria

The unit passes when: a missing descriptor is rejected; identity and state come from the
descriptor unchanged; each state maps to its documented caption; only not-downloaded and failed
rows offer a download; the downloading and failure flags reflect the row; and a state change
re-announces every derived value.

#### Test Scenarios

##### ModelListItemViewModel_Constructor_NullDescriptor_ThrowsArgumentNullException

**Scenario**: A row is built with no descriptor.

**Expected**: `ArgumentNullException`, rather than an unidentified model in the list.

##### ModelListItemViewModel_Constructor_Descriptor_CarriesDescriptorIdentityAndState

**Scenario**: A row is built from a descriptor.

**Expected**: Identity, display name, role and state all come from the descriptor unchanged.

##### ModelListItemViewModel_StatusText_EachState_ReturnsReadableCaption

**Scenario**: Each install state is presented.

**Expected**: Each maps to its documented human-readable caption.

##### ModelListItemViewModel_CanDownload_EachState_ReflectsRetryPolicy

**Scenario**: Each install state is presented.

**Expected**: Only not-downloaded and failed offer a download, so installed content is protected
and a retry after failure stays possible.

##### ModelListItemViewModel_IsDownloading_DownloadingState_ReturnsTrue

**Scenario**: A downloading row and an idle row are compared.

**Expected**: Only the downloading row reports an in-flight download.

##### ModelListItemViewModel_HasFailureMessage_ReflectsPresenceOfMessage

**Scenario**: A failure explanation is recorded on a row.

**Expected**: The row reports a failure message present only when there is one to show.

##### ModelListItemViewModel_State_Changed_NotifiesDerivedProperties

**Scenario**: A row's install state changes.

**Expected**: The caption, download enablement and progress visibility are all re-announced, so a
bound row cannot go stale mid-download.
