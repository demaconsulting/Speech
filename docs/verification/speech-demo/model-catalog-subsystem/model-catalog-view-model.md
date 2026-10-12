### ModelCatalogViewModel Verification

#### Verification Approach

`ModelCatalogViewModel` is verified through deterministic unit tests against an NSubstitute fake
of `IModelCatalogService`, which makes the whole download lifecycle - progress, success, checksum
mismatch, transport failure, unexpected seam fault and cancellation - verifiable with no network
access and no downloadable model. Download progress callbacks are made deterministic by
installing an inline synchronization context for the duration of the test, because
`Progress<T>` otherwise marshals to the thread pool; this changes nothing about production
behavior and only removes a race from the assertions.

Automated tests do not claim proof of a real, multi-hundred-megabyte model download. The full
download lifecycle is proven against controlled catalog data, never a real network fetch.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test builds its own view model; no shared state exists between tests
- **Test doubles**: An NSubstitute fake of `IModelCatalogService`, a fake `ISpeechModel`, and an
  inline synchronization context for deterministic progress delivery

#### Acceptance Criteria

The unit passes when: it shows one row per reported model and rebuilds correctly on refresh;
every download outcome is reported as a row state with an honest explanation rather than as an
application error; an installed model is never re-downloaded; an empty catalog is explained
rather than shown blank; the mirror-settings fields apply a valid mirror (or revert to each
model's public URI on a blank URL) while reporting any validation or reconfiguration failure as
an explained panel state; and the reference voice pack section lists, installs and removes packs
while refusing operations that would overlap a model download, a pack install or a mirror change.

#### Test Scenarios

##### ModelCatalogViewModel_Constructor_NullService_ThrowsArgumentNullException

**Scenario**: The panel is constructed with no catalog seam.

**Expected**: `ArgumentNullException`.

##### ModelCatalogViewModel_Constructor_PopulatedCatalog_BuildsOneRowPerModel

**Scenario**: The seam reports several models.

**Expected**: Exactly one row per reported model, with nothing added or hidden.

##### ModelCatalogViewModel_Constructor_PopulatedCatalog_SelectsFirstModel

**Scenario**: The seam reports several models.

**Expected**: The first row is preselected, so the panel is immediately usable.

##### ModelCatalogViewModel_Constructor_EmptyCatalog_ReportsHonestEmptyState

**Scenario**: The seam reports no models.

**Expected**: No rows, and the panel reports itself empty so the explanatory message is shown.

##### ModelCatalogViewModel_EmptyCatalogMessage_Read_ExplainsCatalogHasNoKnownModels

**Scenario**: The empty-catalog message is read.

**Expected**: It states that no speech models are currently known to this catalog, without
claiming the library itself ships no downloadable models.

##### ModelCatalogViewModel_Refresh_StateChanged_RebuildsRowsAndKeepsSelection

**Scenario**: A model's install state changes and the panel is refreshed.

**Expected**: Rows are rebuilt from the library's current view and the previously selected model
stays selected.

##### ModelCatalogViewModel_Refresh_ModelNoLongerKnown_DropsRowAndFallsBack

**Scenario**: The selected model is no longer reported and the panel is refreshed.

**Expected**: Its row is dropped and another row is selected.

##### ModelCatalogViewModel_RefreshCommand_Executed_ReReadsCatalog

**Scenario**: The refresh command is executed the way a bound button does.

**Expected**: The catalog is re-read through the seam.

##### ModelCatalogViewModel_DownloadAsync_Installed_MarksModelDownloaded

**Scenario**: A download completes with the library reporting the model installed.

**Expected**: The row becomes installed at full progress.

##### ModelCatalogViewModel_DownloadAsync_ProgressReported_UpdatesRowProgress

**Scenario**: The library reports transfer progress during a download.

**Expected**: The downloading row's progress fraction tracks the reported value.

##### ModelCatalogViewModel_DownloadAsync_ChecksumMismatch_ReportsExplainedFailure

**Scenario**: The library reports a checksum mismatch.

**Expected**: The row becomes failed with an explanation stating the files did not match their
expected checksums and were discarded.

##### ModelCatalogViewModel_DownloadAsync_Failed_ReportsLibraryErrorMessage

**Scenario**: The library reports a transport failure carrying an error.

**Expected**: The row becomes failed carrying the library's own error message.

##### ModelCatalogViewModel_DownloadAsync_SeamThrows_ContainsFaultInRow

**Scenario**: The seam throws an unexpected exception.

**Expected**: The row becomes failed carrying that message; the application does not crash.

##### ModelCatalogViewModel_DownloadAsync_Canceled_ReportsNotDownloaded

**Scenario**: A download is canceled.

**Expected**: The row returns to not-downloaded with a cancellation note, because the library
guarantees a canceled download installs nothing.

##### ModelCatalogViewModel_DownloadAsync_RetryAfterFailure_ClearsPreviousFailureMessage

**Scenario**: A failed download is retried.

**Expected**: The previous failure explanation is cleared when the retry starts.

##### ModelCatalogViewModel_DownloadAsync_AlreadyInstalledModel_DoesNotDownload

**Scenario**: A download is requested for an already-installed model.

**Expected**: No download is started, so working installed content is never disturbed.

##### ModelCatalogViewModel_DownloadAsync_NullModel_DoesNothing

**Scenario**: The download command is invoked with no selected row.

**Expected**: Nothing happens and nothing throws.

##### ModelCatalogViewModel_Constructor_InitialMirrorValues_PrePopulatesFields

**Scenario**: The panel is constructed with launch-time mirror values supplied.

**Expected**: The mirror URL, user, password and bearer-token fields start pre-populated from
those values, so the GUI and launch-time entry points stay consistent.

##### ModelCatalogViewModel_ApplyMirror_ValidMirror_ForwardsConfiguredMirrorAndReportsSuccess

**Scenario**: Valid mirror fields are applied.

**Expected**: The seam's `ApplyMirror` is called with a `DownloadMirror` built from those fields
and the panel reports success and refreshes.

##### ModelCatalogViewModel_ApplyMirror_BlankUrl_AppliesNullMirror

**Scenario**: The mirror URL field is blank when applied.

**Expected**: The seam's `ApplyMirror` is called with `null`, reverting to each model's own public
download URI.

##### ModelCatalogViewModel_ApplyMirror_InvalidUrl_ReportsErrorWithoutCallingService

**Scenario**: The mirror URL field holds an invalid URI.

**Expected**: The panel reports an explained error and never calls the seam.

##### ModelCatalogViewModel_ApplyMirror_CredentialsWithoutUrl_ReportsErrorWithoutCallingService

**Scenario**: The bearer-token field holds a value but the mirror URL field is blank.

**Expected**: `MirrorOptionsFactory.Create` rejects the stray credential and the panel reports an
explained error without ever calling the seam.

##### ModelCatalogViewModel_ApplyMirror_ServiceRejectsReconfiguration_ReportsError

**Scenario**: The seam throws `InvalidOperationException` because it was not built with a catalog
factory.

**Expected**: The panel reports the seam's explanation as an error rather than letting the
exception escape.

##### ModelCatalogViewModel_ApplyMirror_DownloadInProgress_RefusesAndReportsError

**Scenario**: One row is `Downloading` and the mirror URL field holds a valid URI.

**Expected**: The panel reports an explained error and never calls the seam's `ApplyMirror`,
because that call would dispose the catalog the in-flight download depends on.

##### ModelCatalogViewModel_Constructor_ListsVoicePacks

**Scenario**: The seam reports reference voice packs.

**Expected**: One pack row per reported pack appears in the reference voices section.

##### ModelCatalogViewModel_Constructor_NoVoicePacks_HidesSection

**Scenario**: The seam reports no reference voice packs.

**Expected**: The reference voices section is hidden.

##### ModelCatalogViewModel_InstallVoicePackAsync_Installed_RefreshesRows

**Scenario**: A pack install completes successfully.

**Expected**: The pack row refreshes to the installed state.

##### ModelCatalogViewModel_InstallVoicePackAsync_PassesEmotionChoice

**Scenario**: A pack install is requested with the emotion option on and off.

**Expected**: The choice is passed to the service unchanged.

##### ModelCatalogViewModel_InstallVoicePackAsync_FailureModes_ReportedInRow

**Scenario**: A pack install fails, is canceled, or the seam throws.

**Expected**: Each outcome is reported in the pack row rather than escaping.

##### ModelCatalogViewModel_RemoveVoicePack_UninstallsRefreshesAndReportsFaults

**Scenario**: A pack is removed, and the uninstall is made to throw.

**Expected**: The service uninstalls the pack and the rows refresh; a fault is reported on the row.

##### ModelCatalogViewModel_InstallVoicePackAsync_ModelDownloading_RefusedWithoutCallingService

**Scenario**: A pack install is requested while a model is downloading.

**Expected**: The request is refused with an explanation and the service is not called.

##### ModelCatalogViewModel_DownloadAsync_PackInstalling_RefusedWithoutCallingService

**Scenario**: A model download is requested while a pack is installing.

**Expected**: The request is refused with an explanation and the service is not called.

##### ModelCatalogViewModel_ApplyMirror_PackInstalling_IsRefused

**Scenario**: The mirror is applied while a pack is installing.

**Expected**: The request is refused with an explanation, because the rebuild would dispose the
catalog the install uses.
