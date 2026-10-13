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

The unit passes when all of the following hold:

- It shows one row per reported model and rebuilds correctly on refresh, keeping the previous
  rows and reporting an error when the re-read faults.
- It refuses a refresh while a model download or pack install is in flight.
- Every download outcome is reported as a row state with an honest explanation rather than as an
  application error, and an installed model is never re-downloaded.
- An empty catalog is explained rather than shown blank.
- The mirror-settings fields apply a valid mirror (or revert to each model's public URI on a
  blank URL), treat blank credential fields as absent, never echo credentials, re-read the
  catalog afterwards, and report any validation, reconfiguration or re-read failure as an
  explained panel state.
- The reference voice pack section lists, installs and removes packs while refusing operations
  that would overlap a model download, a pack install or a mirror change.
- A fault reading an installed pack's emotion flag, or re-reading the catalog after a pack
  change, is contained.
- After any unsuccessful pack install or upgrade the row shows the state the seam reports (a
  pack still installed stays installed and removable) with its failure message, falling back to
  the attempt's outcome when the state cannot be read.
- A removal the seam refuses is explained on the row.

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

##### ModelCatalogViewModel_RefreshAsync_StateChanged_RebuildsRowsAndKeepsSelection

**Scenario**: A model's install state changes and the panel is refreshed.

**Expected**: Rows are rebuilt from the library's current view and the previously selected model
stays selected.

##### ModelCatalogViewModel_RefreshAsync_ModelNoLongerKnown_DropsRowAndFallsBack

**Scenario**: The selected model is no longer reported and the panel is refreshed.

**Expected**: Its row is dropped and another row is selected.

##### ModelCatalogViewModel_RefreshCommand_Executed_ReReadsCatalog

**Scenario**: The refresh command is executed the way a bound button does.

**Expected**: The catalog is re-read through the seam.

##### ModelCatalogViewModel_RefreshAsync_DownloadInFlight_KeepsRowsAndDoesNotRereadCatalog

**Scenario**: A model download is in flight (its seam call has not completed) and the panel is
refreshed.

**Expected**: The refresh is refused: the same downloading row stays in place and the catalog is
not enumerated again, so the transfer's progress and outcome are not discarded.

##### ModelCatalogViewModel_RefreshAsync_PackInstalling_KeepsRowsAndDoesNotRereadCatalog

**Scenario**: A voice pack row is installing and the panel is refreshed.

**Expected**: The refresh is refused: the same pack row stays in place and the catalog is not
enumerated again.

##### ModelCatalogViewModel_DownloadAsync_Installed_MarksModelDownloaded

**Scenario**: A download completes with the library reporting the model installed.

**Expected**: The row becomes installed at full progress.

##### ModelCatalogViewModel_DownloadAsync_ProgressReported_UpdatesRowProgress

**Scenario**: The library reports transfer progress during a download.

**Expected**: The downloading row's progress fraction tracks the reported value.

##### ModelCatalogViewModel_DownloadAsync_InFlight_ShowsDownloadingState

**Scenario**: A download is started and the seam has not yet completed it.

**Expected**: The row is in the downloading state until the download completes.

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

##### ModelCatalogViewModel_DownloadAsync_AlreadyDownloadingModel_DoesNotDownload

**Scenario**: A download is requested for a model that is already downloading.

**Expected**: No second download is started and the row stays downloading.

##### ModelCatalogViewModel_DownloadAsync_NullModel_DoesNothing

**Scenario**: The download command is invoked with no selected row.

**Expected**: Nothing happens and nothing throws.

##### ModelCatalogViewModel_Constructor_InitialMirrorValues_PrePopulatesFields

**Scenario**: The panel is constructed with launch-time mirror values supplied.

**Expected**: The mirror URL, user, password and bearer-token fields start pre-populated from
those values, so the GUI and launch-time entry points stay consistent.

##### ModelCatalogViewModel_ApplyMirrorAsync_ValidMirror_ForwardsConfiguredMirror

**Scenario**: Valid mirror fields are applied.

**Expected**: The seam's `ApplyMirror` is called with a `DownloadMirror` built from those fields.

##### ModelCatalogViewModel_ApplyMirrorAsync_BlankCredentials_TreatedAsNotSupplied

**Scenario**: A valid URL is applied while the user name, password and bearer token fields hold
empty or whitespace text.

**Expected**: The seam receives a mirror with no credentials (as if the fields were never filled),
and the panel reports success rather than a credential-validation error.

##### ModelCatalogViewModel_ApplyMirrorAsync_WithCredentials_MessageDoesNotEchoCredentials

**Scenario**: A mirror with a user name, password and bearer token is applied, and separately
fails.

**Expected**: Neither the confirmation nor the failure message contains the typed credentials.

##### ModelCatalogViewModel_ApplyMirrorAsync_ValidMirror_ReportsSuccess

**Scenario**: A valid mirror URL is applied.

**Expected**: The panel reports no error and shows a success confirmation naming the mirror.

##### ModelCatalogViewModel_ApplyMirrorAsync_ValidMirror_ReReadsCatalog

**Scenario**: A valid mirror is applied and the reconfigured catalog reports a different install
state for a model.

**Expected**: The catalog is enumerated again and the row shows the new state.

##### ModelCatalogViewModel_ApplyMirrorAsync_UnexpectedFault_ReportedInPanel

**Scenario**: Either the seam's `ApplyMirror` throws an unexpected exception, or the catalog
re-read after a successful reconfiguration throws.

**Expected**: Nothing escapes; the panel reports an error that names the cause and does not show
the success confirmation.

##### ModelCatalogViewModel_ApplyMirrorAsync_BearerToken_ForwardsToken

**Scenario**: A mirror URL and a bearer token are applied.

**Expected**: The seam's `ApplyMirror` receives a `DownloadMirror` carrying that bearer token.

##### ModelCatalogViewModel_ApplyMirrorAsync_BlankUrl_AppliesNullMirror

**Scenario**: The mirror URL field is blank when applied.

**Expected**: The seam's `ApplyMirror` is called with `null`, reverting to each model's own public
download URI.

##### ModelCatalogViewModel_ApplyMirrorAsync_InvalidUrl_ReportsErrorWithoutCallingService

**Scenario**: The mirror URL field holds an invalid URI.

**Expected**: The panel reports an explained error and never calls the seam.

##### ModelCatalogViewModel_ApplyMirrorAsync_CredentialsWithoutUrl_ReportsErrorWithoutCallingService

**Scenario**: The bearer-token field holds a value but the mirror URL field is blank.

**Expected**: `MirrorOptionsFactory.Create` rejects the stray credential and the panel reports an
explained error without ever calling the seam.

##### ModelCatalogViewModel_ApplyMirrorAsync_ServiceRejectsReconfiguration_ReportsError

**Scenario**: The seam throws `InvalidOperationException` because it was not built with a catalog
factory.

**Expected**: The panel reports the seam's explanation as an error rather than letting the
exception escape.

##### ModelCatalogViewModel_ApplyMirrorAsync_DownloadInProgress_RefusesAndReportsError

**Scenario**: One row is `Downloading` and the mirror URL field holds a valid URI.

**Expected**: The panel reports an explained error and never calls the seam's `ApplyMirror`,
because that call would dispose the catalog the in-flight download depends on.

##### ModelCatalogViewModel_Constructor_ListsVoicePacks

**Scenario**: The seam reports reference voice packs.

**Expected**: One pack row per reported pack appears in the reference voices section.

##### ModelCatalogViewModel_Constructor_VoicePacks_ShowsUserVoicesFolder

**Scenario**: The seam reports reference voice packs and a user voices folder.

**Expected**: The panel shows that folder.

##### ModelCatalogViewModel_Constructor_VoicePacks_ShowsConsentNote

**Scenario**: The seam reports reference voice packs.

**Expected**: The panel shows the voice-cloning consent note.

##### ModelCatalogViewModel_Constructor_NoVoicePacks_HidesSection

**Scenario**: The seam reports no reference voice packs.

**Expected**: The reference voices section is hidden.

##### ModelCatalogViewModel_InstallVoicePackAsync_Installed_RefreshesRows

**Scenario**: A pack install completes successfully.

**Expected**: The pack row refreshes to the installed state.

##### ModelCatalogViewModel_InstallVoicePackAsync_PassesEmotionChoice

**Scenario**: A pack install is requested with the emotion option off.

**Expected**: The choice is passed to the service unchanged.

##### ModelCatalogViewModel_InstallVoicePackAsync_AddEmotions_AlwaysIncludesEmotions

**Scenario**: "Add emotions" is requested for a voices-only install whose emotion option is off.

**Expected**: The service is asked to install with emotion variants.

##### ModelCatalogViewModel_InstallVoicePackAsync_FailureModes_ReportedInRow

**Scenario**: A pack install fails, is canceled, or the seam throws.

**Expected**: Each outcome is reported in the pack row rather than escaping.

##### ModelCatalogViewModel_InstallVoicePackAsync_UpgradeFailsAndRollsBack_KeepsPackInstalledWithMessage

**Scenario**: An "Add emotions" upgrade of an installed voices-only pack fails, hits a checksum
mismatch, is canceled, or the seam throws, and the seam reports the pack still installed because the
library rolled the upgrade back.

**Expected**: The row re-reads the pack from the seam: it stays installed (voices only) with removal
enabled and "Add emotions" still offered, no progress is shown, and the failure or cancellation
message is kept on the row.

##### ModelCatalogViewModel_InstallVoicePackAsync_StateReadFails_FallsBackToOutcome

**Scenario**: A pack install fails and the seam then throws when asked for the pack's state.

**Expected**: The row shows the failed outcome with its failure message and the panel does not crash.

##### ModelCatalogViewModel_RefreshAsync_InstalledPack_ReadsHasEmotionsFromService

**Scenario**: The seam reports an installed pack with and without emotion variants and the panel is
refreshed.

**Expected**: The row's emotion flag matches the seam's report, and "Add emotions" is offered only
for the voices-only install.

##### ModelCatalogViewModel_RemoveVoicePackAsync_StoreException_ShowsMessageAndKeepsPack

**Scenario**: The seam throws a store exception when the pack is removed.

**Expected**: The exception's message is shown on the pack's row, the pack stays installed and
removable, and nothing escapes.

##### ModelCatalogViewModel_RemoveVoicePackAsync_Canceled_ShowsCanceledMessage

**Scenario**: The seam's uninstall is canceled.

**Expected**: The row shows "Removal canceled." and the pack stays installed.

##### ModelCatalogViewModel_RemoveVoicePackAsync_Installed_UninstallsAndRefreshesRows

**Scenario**: An installed pack is removed.

**Expected**: The service uninstalls the pack and the catalog is re-read so the rows refresh.

##### ModelCatalogViewModel_RemoveVoicePackAsync_Fault_ReportsMessageInRow

**Scenario**: The uninstall throws.

**Expected**: The fault's message is shown on the row, the pack stays installed, and nothing
escapes.

##### ModelCatalogViewModel_RemoveVoicePackAsync_Command_UninstallsRequestedPack

**Scenario**: The generated remove command is invoked with a pack row.

**Expected**: The service is asked to uninstall that pack.

##### ModelCatalogViewModel_InstallVoicePackAsync_Command_InstallsRequestedPackWithProgress

**Scenario**: The generated install command is invoked with a pack row and the service reports
progress.

**Expected**: The service installs that pack and the progress reaches the row.

##### ModelCatalogViewModel_InstallVoicePackAsync_ReloadFaults_ReportedAsReReadFailure

**Scenario**: An install succeeds but the catalog re-read then throws.

**Expected**: The row shows that the pack installed but the catalog could not be re-read.

##### ModelCatalogViewModel_RemoveVoicePackAsync_ReloadFaults_ReportedAsReReadFailure

**Scenario**: A removal succeeds but the catalog re-read then throws.

**Expected**: The row shows that the pack was removed but the catalog could not be re-read.

##### ModelCatalogViewModel_RefreshAsync_SeamFault_ReportedAndRowsKept

**Scenario**: The refresh command runs while the seam's enumeration throws.

**Expected**: The panel shows a refresh error message, the previous rows stay, and nothing
escapes.

##### ModelCatalogViewModel_RefreshAsync_NoPreviousSelection_SelectsFirstRow

**Scenario**: A refresh runs with no row selected.

**Expected**: The first row is selected.

##### ModelCatalogViewModel_RemoveVoicePackAsync_ModelDownloading_RefusedWithoutCallingService

**Scenario**: A pack removal is requested while a model is downloading.

**Expected**: The request is refused with an explanation and the service is not called.

##### ModelCatalogViewModel_InstallVoicePackAsync_ModelDownloading_RefusedWithoutCallingService

**Scenario**: A pack install is requested while a model is downloading.

**Expected**: The request is refused with an explanation and the service is not called.

##### ModelCatalogViewModel_DownloadAsync_PackInstalling_RefusedWithoutCallingService

**Scenario**: A model download is requested while a pack is installing.

**Expected**: The request is refused with an explanation and the service is not called.

##### ModelCatalogViewModel_ApplyMirrorAsync_PackInstalling_IsRefused

**Scenario**: The mirror is applied while a pack is installing.

**Expected**: The request is refused with an explanation, because the rebuild would dispose the
catalog the install uses.

##### ModelCatalogViewModel_VoicePackCommands_AnotherPackInstalling_RefusedWithoutCallingService

**Scenario**: One pack row is installing and an install and a removal are requested for other pack
rows.

**Expected**: Both requests are refused with the busy explanation on their rows and the service is
not called.

##### ModelCatalogViewModel_Constructor_EmotionFlagReadFaults_InitialLoadCompletesWithFlagFalse

**Scenario**: A pack is installed at construction and the seam throws when asked whether it has
emotion variants.

**Expected**: `InitialLoadTask` completes without faulting and the row's emotion flag stays false.
