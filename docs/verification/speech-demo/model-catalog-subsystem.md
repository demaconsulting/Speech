## SpeechDemo ModelCatalogSubsystem Verification

### Verification Approach

The ModelCatalogSubsystem is verified through deterministic unit tests in two layers. The
`ModelCatalogViewModel` and `ModelListItemViewModel` are tested against an NSubstitute fake of
`IModelCatalogService`, which is what makes the whole download lifecycle — progress, success,
checksum mismatch, transport failure, unexpected seam fault, and cancellation — verifiable with
no network access and no downloadable model in existence. The `ModelCatalogService` adapter is
tested against a real `SpeechModelCatalog` rooted in an isolated scratch directory, proving it
really does forward to the library.

Download progress callbacks are made deterministic by installing an inline synchronization
context for the duration of the test, because `Progress<T>` otherwise marshals to the thread
pool. This changes nothing about production behavior; it only removes a race from the assertions.

Automated tests do **not** claim proof of a real, multi-hundred-megabyte model download. As of
Phase 7b the library's real catalog contains three real models (two recognition, one synthesis),
so the real-catalog tests now prove those models pass through the adapter unchanged; the full
download lifecycle is still proven against controlled catalog data, never a real network fetch.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Adapter tests root the library's model store in a fresh directory under the test
  output folder, so no test touches a developer's real installed-model store
- **Test doubles**: An NSubstitute fake of `IModelCatalogService`, a fake `ISpeechModel`, a fake
  `IModelDownloadClient` (for the `ModelCatalogService`-level `ModelInstalled` tests, which inject
  a model and client directly into the library's `SpeechModelCatalog` so a genuine install can be
  proven with no real network access), and an inline synchronization context for deterministic
  progress delivery

### Test Scenarios

#### ModelCatalogService_Constructor_NullCatalog_ThrowsArgumentNullException

**Scenario**: The adapter is constructed with no library catalog.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Models-LibraryCatalogDelegation`.

#### ModelCatalogService_Enumerate_LibraryKnowsRealModels_ReturnsThem

**Scenario**: The adapter enumerates the real library catalog.

**Expected**: The same models the library itself knows about (as of Phase 7b, its three real
models - two recognition, one synthesis), reported as-is with each model's own declared role
preserved rather than replaced with invented placeholder models.

**Requirement coverage**: `SpeechDemo-Models-LibraryCatalogDelegation`.

#### ModelCatalogService_DownloadAsync_UnknownModelId_ThrowsArgumentException

**Scenario**: A download is requested for a model id the library does not know.

**Expected**: The library's own `ArgumentException` propagates, proving the adapter delegates
rather than reimplements.

**Requirement coverage**: `SpeechDemo-Models-LibraryCatalogDelegation`.

#### ModelCatalogService_Enumerate_AfterAnotherAdapterFallsOutOfUse_StillWorks

**Scenario**: A second adapter over the same catalog goes out of use and the first enumerates.

**Expected**: Enumeration still succeeds, proving an adapter never disposes a catalog it does not
own.

**Requirement coverage**: `SpeechDemo-Models-LibraryCatalogDelegation`.

#### ModelCatalogService_DownloadAsync_Installed_RaisesModelInstalledWithCorrectIdAndRole

**Scenario**: A download completes with the library genuinely installing an injected fake model
(a fake `IModelDownloadClient` serving a payload whose checksum matches the model's declared
descriptor, so no real network access is needed).

**Expected**: `ModelInstalled` fires exactly once, carrying the installed model's id and its
declared role.

**Requirement coverage**: `SpeechDemo-Models-InstallNotification`.

#### ModelCatalogService_DownloadAsync_Failed_DoesNotRaiseModelInstalled

**Scenario**: A download fails against an injected fake model and a fake `IModelDownloadClient`
that always throws.

**Expected**: `ModelInstalled` is never raised.

**Requirement coverage**: `SpeechDemo-Models-InstallNotification`.

#### ModelCatalogViewModel_Constructor_NullService_ThrowsArgumentNullException

**Scenario**: The panel is constructed with no catalog seam.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Models-LibraryCatalogDelegation`.

#### ModelCatalogViewModel_Constructor_PopulatedCatalog_BuildsOneRowPerModel

**Scenario**: The seam reports several models.

**Expected**: Exactly one row per reported model, with nothing added or hidden.

**Requirement coverage**: `SpeechDemo-Models-Enumeration`.

#### ModelCatalogViewModel_Constructor_PopulatedCatalog_SelectsFirstModel

**Scenario**: The seam reports several models.

**Expected**: The first row is preselected, so the panel is immediately usable.

**Requirement coverage**: `SpeechDemo-Models-Enumeration`.

#### ModelCatalogViewModel_Constructor_EmptyCatalog_ReportsHonestEmptyState

**Scenario**: The seam reports no models.

**Expected**: No rows, and the panel reports itself empty so the explanatory message is shown.

**Requirement coverage**: `SpeechDemo-Models-EmptyCatalogExplanation`.

#### ModelCatalogViewModel_EmptyCatalogMessage_Read_ExplainsNoModelsAreShippedYet

**Scenario**: The empty-catalog message is read.

**Expected**: It states that the library ships no downloadable models yet and that the list will
populate automatically once it knows about any.

**Requirement coverage**: `SpeechDemo-Models-EmptyCatalogExplanation`.

#### ModelCatalogViewModel_Refresh_StateChanged_RebuildsRowsAndKeepsSelection

**Scenario**: A model's install state changes and the panel is refreshed.

**Expected**: Rows are rebuilt from the library's current view and the previously selected model
stays selected.

**Requirement coverage**: `SpeechDemo-Models-Refresh`.

#### ModelCatalogViewModel_Refresh_ModelNoLongerKnown_DropsRowAndFallsBack

**Scenario**: The selected model is no longer reported and the panel is refreshed.

**Expected**: Its row is dropped and another row is selected, so the panel never offers a model
the library no longer knows.

**Requirement coverage**: `SpeechDemo-Models-Refresh`.

#### ModelCatalogViewModel_RefreshCommand_Executed_ReReadsCatalog

**Scenario**: The refresh command is executed the way a bound button does.

**Expected**: The catalog is re-read through the seam.

**Requirement coverage**: `SpeechDemo-Models-Refresh`.

#### ModelCatalogViewModel_DownloadAsync_Installed_MarksModelDownloaded

**Scenario**: A download completes with the library reporting the model installed.

**Expected**: The row becomes installed at full progress.

**Requirement coverage**: `SpeechDemo-Models-DownloadProgress`.

#### ModelCatalogViewModel_DownloadAsync_ProgressReported_UpdatesRowProgress

**Scenario**: The library reports transfer progress during a download.

**Expected**: The downloading row's progress fraction tracks the reported value.

**Requirement coverage**: `SpeechDemo-Models-DownloadProgress`.

#### ModelCatalogViewModel_DownloadAsync_ChecksumMismatch_ReportsExplainedFailure

**Scenario**: The library reports a checksum mismatch.

**Expected**: The row becomes failed with an explanation stating the files did not match their
expected checksums and were discarded.

**Requirement coverage**: `SpeechDemo-Models-DownloadOutcomes`.

#### ModelCatalogViewModel_DownloadAsync_Failed_ReportsLibraryErrorMessage

**Scenario**: The library reports a transport failure carrying an error.

**Expected**: The row becomes failed carrying the library's own error message.

**Requirement coverage**: `SpeechDemo-Models-DownloadOutcomes`.

#### ModelCatalogViewModel_DownloadAsync_SeamThrows_ContainsFaultInRow

**Scenario**: The seam throws an unexpected exception.

**Expected**: The row becomes failed carrying that message; the application does not crash.

**Requirement coverage**: `SpeechDemo-Models-DownloadOutcomes`.

#### ModelCatalogViewModel_DownloadAsync_Canceled_ReportsNotDownloaded

**Scenario**: A download is canceled.

**Expected**: The row returns to not-downloaded with a cancellation note, because the library
guarantees a canceled download installs nothing.

**Requirement coverage**: `SpeechDemo-Models-DownloadOutcomes`.

#### ModelCatalogViewModel_DownloadAsync_RetryAfterFailure_ClearsPreviousFailureMessage

**Scenario**: A failed download is retried.

**Expected**: The previous failure explanation is cleared when the retry starts.

**Requirement coverage**: `SpeechDemo-Models-DownloadRetry`.

#### ModelCatalogViewModel_DownloadAsync_AlreadyInstalledModel_DoesNotDownload

**Scenario**: A download is requested for an already-installed model.

**Expected**: No download is started, so working installed content is never disturbed.

**Requirement coverage**: `SpeechDemo-Models-DownloadGuard`.

#### ModelCatalogViewModel_DownloadAsync_NullModel_DoesNothing

**Scenario**: The download command is invoked with no selected row.

**Expected**: Nothing happens and nothing throws.

**Requirement coverage**: `SpeechDemo-Models-DownloadGuard`.

#### ModelListItemViewModel_Constructor_NullDescriptor_ThrowsArgumentNullException

**Scenario**: A row is built with no descriptor.

**Expected**: `ArgumentNullException`, rather than an unidentified model in the list.

**Requirement coverage**: `SpeechDemo-Models-StatePresentation`.

#### ModelListItemViewModel_Constructor_Descriptor_CarriesDescriptorIdentityAndState

**Scenario**: A row is built from a descriptor.

**Expected**: Identity, display name, role, and state all come from the descriptor unchanged.

**Requirement coverage**: `SpeechDemo-Models-StatePresentation`.

#### ModelListItemViewModel_StatusText_EachState_ReturnsReadableCaption

**Scenario**: Each install state is presented.

**Expected**: Each maps to its documented human-readable caption.

**Requirement coverage**: `SpeechDemo-Models-StatePresentation`.

#### ModelListItemViewModel_CanDownload_EachState_ReflectsRetryPolicy

**Scenario**: Each install state is presented.

**Expected**: Only not-downloaded and failed offer a download, so installed content is protected
and a retry after failure stays possible.

**Requirement coverage**: `SpeechDemo-Models-StatePresentation`.

#### ModelListItemViewModel_IsDownloading_DownloadingState_ReturnsTrue

**Scenario**: A downloading row and an idle row are compared.

**Expected**: Only the downloading row reports an in-flight download.

**Requirement coverage**: `SpeechDemo-Models-StatePresentation`.

#### ModelListItemViewModel_HasFailureMessage_ReflectsPresenceOfMessage

**Scenario**: A failure explanation is recorded on a row.

**Expected**: The row reports a failure message present only when there is one to show.

**Requirement coverage**: `SpeechDemo-Models-StatePresentation`.

#### ModelListItemViewModel_State_Changed_NotifiesDerivedProperties

**Scenario**: A row's install state changes.

**Expected**: The caption, download enablement, and progress visibility are all re-announced, so
a bound row cannot go stale mid-download.

**Requirement coverage**: `SpeechDemo-Models-StatePresentation`.

### Requirements Coverage

- **`SpeechDemo-Models-Enumeration`**:
  `ModelCatalogViewModel_Constructor_PopulatedCatalog_BuildsOneRowPerModel`,
  `ModelCatalogViewModel_Constructor_PopulatedCatalog_SelectsFirstModel`
- **`SpeechDemo-Models-StatePresentation`**:
  `ModelListItemViewModel_Constructor_NullDescriptor_ThrowsArgumentNullException`,
  `ModelListItemViewModel_Constructor_Descriptor_CarriesDescriptorIdentityAndState`,
  `ModelListItemViewModel_StatusText_EachState_ReturnsReadableCaption`,
  `ModelListItemViewModel_CanDownload_EachState_ReflectsRetryPolicy`,
  `ModelListItemViewModel_IsDownloading_DownloadingState_ReturnsTrue`,
  `ModelListItemViewModel_HasFailureMessage_ReflectsPresenceOfMessage`,
  `ModelListItemViewModel_State_Changed_NotifiesDerivedProperties`
- **`SpeechDemo-Models-Refresh`**:
  `ModelCatalogViewModel_Refresh_StateChanged_RebuildsRowsAndKeepsSelection`,
  `ModelCatalogViewModel_Refresh_ModelNoLongerKnown_DropsRowAndFallsBack`,
  `ModelCatalogViewModel_RefreshCommand_Executed_ReReadsCatalog`
- **`SpeechDemo-Models-LibraryCatalogDelegation`**:
  `ModelCatalogService_Constructor_NullCatalog_ThrowsArgumentNullException`,
  `ModelCatalogService_Enumerate_LibraryKnowsRealModels_ReturnsThem`,
  `ModelCatalogService_DownloadAsync_UnknownModelId_ThrowsArgumentException`,
  `ModelCatalogService_Enumerate_AfterAnotherAdapterFallsOutOfUse_StillWorks`,
  `ModelCatalogViewModel_Constructor_NullService_ThrowsArgumentNullException`
- **`SpeechDemo-Models-DownloadProgress`**:
  `ModelCatalogViewModel_DownloadAsync_ProgressReported_UpdatesRowProgress`,
  `ModelCatalogViewModel_DownloadAsync_Installed_MarksModelDownloaded`
- **`SpeechDemo-Models-DownloadOutcomes`**:
  `ModelCatalogViewModel_DownloadAsync_ChecksumMismatch_ReportsExplainedFailure`,
  `ModelCatalogViewModel_DownloadAsync_Failed_ReportsLibraryErrorMessage`,
  `ModelCatalogViewModel_DownloadAsync_SeamThrows_ContainsFaultInRow`,
  `ModelCatalogViewModel_DownloadAsync_Canceled_ReportsNotDownloaded`
- **`SpeechDemo-Models-DownloadRetry`**:
  `ModelCatalogViewModel_DownloadAsync_RetryAfterFailure_ClearsPreviousFailureMessage`
- **`SpeechDemo-Models-DownloadGuard`**:
  `ModelCatalogViewModel_DownloadAsync_AlreadyInstalledModel_DoesNotDownload`,
  `ModelCatalogViewModel_DownloadAsync_NullModel_DoesNothing`
- **`SpeechDemo-Models-EmptyCatalogExplanation`**:
  `ModelCatalogViewModel_Constructor_EmptyCatalog_ReportsHonestEmptyState`,
  `ModelCatalogViewModel_EmptyCatalogMessage_Read_ExplainsNoModelsAreShippedYet`
- **`SpeechDemo-Models-InstallNotification`**:
  `ModelCatalogService_DownloadAsync_Installed_RaisesModelInstalledWithCorrectIdAndRole`,
  `ModelCatalogService_DownloadAsync_Failed_DoesNotRaiseModelInstalled`

### Acceptance Criteria

A ModelCatalogSubsystem test run passes when: the adapter forwards to the library without adding
or hiding anything and never disposes a catalog it does not own; the panel shows one row per
reported model and rebuilds correctly on refresh; every download outcome — success, progress,
checksum mismatch, transport failure, seam fault, and cancellation — is reported as a row state
with an honest explanation rather than as an application error; an installed model is never
re-downloaded; an empty catalog is explained rather than shown blank; and `ModelInstalled` fires
exactly once with the correct model id and role for a genuine install, and never for a failed
download.
