## SpeechDemo ModelCatalogSubsystem Verification

### Verification Approach

The ModelCatalogSubsystem is verified at subsystem level by the units working together: the
`ModelCatalogViewModel` driving the catalog through the `IModelCatalogService` seam, and the
`ModelCatalogService` adapter rebuilding its catalog and reference voice library when a voice pack
or the mirror changes. Unit-level behavior is verified in the unit verification documents for
ModelCatalogService, ModelCatalogViewModel, ModelListItemViewModel and
ReferenceVoicePackListItemViewModel.

The panel view model is exercised against an NSubstitute fake of `IModelCatalogService`, which
makes the whole download lifecycle verifiable with no network access and no downloadable model.
The adapter is exercised against a real `SpeechModelCatalog` rooted in an isolated scratch
directory. Download progress callbacks are made deterministic by an inline synchronization context,
because `Progress<T>` otherwise marshals to the thread pool; this changes nothing about production
behavior.

Automated tests do not claim proof of a real, multi-hundred-megabyte model download. The full
download lifecycle is proven against controlled catalog data, never a real network fetch.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Adapter tests root the library's model store in a fresh directory under the test
  output folder, so no test touches a developer's real installed-model store
- **Test doubles**: An NSubstitute fake of `IModelCatalogService`, a fake `ISpeechModel`, a fake
  `IModelDownloadClient`, and an inline synchronization context

### Acceptance Criteria

A ModelCatalogSubsystem test run passes when all of the following hold:

- The panel shows, refreshes and downloads models purely through the catalog service seam and
  reports every outcome as row state rather than an application error.
- Mirror changes reconfigure the catalog through the seam, take effect for later downloads and
  voice packs, and are refused while a download or pack install is in flight.
- A voice pack install attempt (including a failed or canceled one), a voice pack removal or a
  mirror change leaves the offered voice choices matching the installed packs, and the synthesis
  panel's voice choices reflect it while keeping the chosen voice.
- A voice pack removal that fails leaves the voice choices and dependent panels unchanged.
- A voice pack that is still installed after a failed install or upgrade stays installed and
  removable with its failure message.
- Removing a pack whose install is in flight takes effect only after the install has finished,
  and a removal that cannot complete or is canceled is explained on the pack's row instead of
  failing.
- Disposing the service releases its owned resources even when one release fails, and disposing it
  twice is harmless.

### Test Scenarios

#### ModelCatalogViewModel_Constructor_PopulatedCatalog_BuildsOneRowPerModel

**Scenario**: The panel is created over a service that enumerates several models.

**Expected**: One row per model is built, each with its installation state.

#### ModelCatalogViewModel_DownloadAsync_Installed_MarksModelDownloaded

**Scenario**: The panel downloads a model through the seam and the library reports it installed.

**Expected**: The row becomes installed at full progress.

#### ModelCatalogViewModel_DownloadAsync_Failed_ReportsLibraryErrorMessage

**Scenario**: The library reports a failed download.

**Expected**: The row shows the failure as row state with the library's message; nothing is thrown.

#### ModelCatalogViewModel_DownloadAsync_ChecksumMismatch_ReportsExplainedFailure

**Scenario**: The library reports a checksum mismatch.

**Expected**: The row shows an explained failure rather than raising an application error.

#### ModelCatalogViewModel_DownloadAsync_Canceled_ReportsNotDownloaded

**Scenario**: A download is canceled.

**Expected**: The row returns to not downloaded without an error.

#### ModelCatalogViewModel_ApplyMirrorAsync_DownloadInProgress_RefusesAndReportsError

**Scenario**: A mirror is applied while a model download is in flight.

**Expected**: The seam is not asked to reconfigure and the panel explains the refusal.

#### ModelCatalogViewModel_ApplyMirrorAsync_PackInstalling_IsRefused

**Scenario**: A mirror is applied while a voice pack install is in flight.

**Expected**: The change is refused and explained, and the seam is not called.

#### ModelCatalogViewModel_RefreshAsync_StateChanged_RebuildsRowsAndKeepsSelection

**Scenario**: A model's install state changes and the panel is refreshed.

**Expected**: Rows are rebuilt from the seam and the selected model stays selected.

#### ModelCatalogViewModel_ApplyMirrorAsync_ValidMirror_ForwardsConfiguredMirror

**Scenario**: Valid mirror fields are applied.

**Expected**: The seam's `ApplyMirror` receives the configured mirror.

#### ModelCatalogViewModel_ApplyMirrorAsync_ValidMirror_ReportsSuccess

**Scenario**: A valid mirror URL is applied.

**Expected**: The panel reports no error and confirms that the mirror was applied.

#### ModelCatalogService_UninstallReferenceVoicePack_RebuildsCatalogAndRaisesEvent

**Scenario**: A pack is uninstalled through a composed service.

**Expected**: The offered voice choices no longer include the removed pack, the catalog and library
pair is rebuilt (mechanism) and `ReferenceVoicesChanged` is raised once so dependent panels refresh.

#### ModelCatalogService_ApplyMirror_Composed_RebuildsPairAndRaisesEvent

**Scenario**: The mirror is changed on a composed service.

**Expected**: The new mirror takes effect for subsequent downloads and voice packs (the pair is
rebuilt with it) and `ReferenceVoicesChanged` is raised so dependent panels refresh.

#### ModelCatalogViewModel_ApplyMirrorAsync_ValidMirror_ReReadsCatalog

**Scenario**: A valid mirror is applied through the panel.

**Expected**: The catalog is re-read so the rows reflect the rebuilt catalog.

#### ModelCatalogViewModel_RemoveVoicePackAsync_Installed_UninstallsAndRefreshesRows

**Scenario**: A pack is removed through the panel.

**Expected**: The seam uninstalls it and the rows are re-read.

#### ModelCatalogViewModel_RemoveVoicePackAsync_Fault_ReportsMessageInRow

**Scenario**: The seam's uninstall throws.

**Expected**: The fault is reported on the row and the pack stays installed.

#### SynthesisPanelViewModel_ReferenceVoicesChanged_RefreshesAndKeepsChosenVoice

**Scenario**: The service raises `ReferenceVoicesChanged` to a subscribed synthesis panel.

**Expected**: The panel refreshes its voice choices and keeps the chosen voice; this is the
observable effect of every voices-changed notification the subsystem raises.

#### ModelCatalogService_InstallReferenceVoicePackAsync_Failure_StillRebuildsAndRaises

**Scenario**: A pack install fails.

**Expected**: The voice choices are refreshed to match the pack's actual installed state and the
notification is still raised.

#### ModelCatalogViewModel_InstallVoicePackAsync_Installed_RefreshesRows

**Scenario**: A pack install completes through the panel.

**Expected**: The pack row refreshes to the installed state.

#### ModelCatalogViewModel_InstallVoicePackAsync_UpgradeFailsAndRollsBack_KeepsPackInstalledWithMessage

**Scenario**: An "Add emotions" upgrade of an installed voices-only pack fails, hits a checksum mismatch,
is canceled, or the service throws, and the service reports the pack still installed because the
library rolled the upgrade back.

**Expected**: The row stays installed with removal enabled and "Add emotions" still offered, and the
failure message is shown on the row.

#### ModelCatalogViewModel_RemoveVoicePackAsync_StoreException_ShowsMessageAndKeepsPack

**Scenario**: The service rejects the uninstall of a pack with a store exception.

**Expected**: The exception's message is shown on the pack's row, the pack stays installed, and nothing
is thrown at the user.

#### ModelCatalogViewModel_RemoveVoicePackAsync_Canceled_ShowsCanceledMessage

**Scenario**: The seam's uninstall is canceled.

**Expected**: The row shows "Removal canceled." and the pack stays installed.

#### ModelCatalogService_Dispose_CatalogDisposalThrows_StillDisposesVoiceLibrary

**Scenario**: A composed service is disposed while releasing its catalog throws.

**Expected**: The voice library is still released and the catalog's failure reaches the caller.

#### ModelCatalogService_Dispose_CalledTwice_DoesNotThrow

**Scenario**: A service is disposed twice.

**Expected**: The second disposal does nothing and does not throw.

#### ModelCatalogService_UninstallReferenceVoicePackAsync_Fails_DoesNotRebuildOrRaise

**Scenario**: A pack removal fails.

**Expected**: The voice choices stay as they were and no voices-changed notification is raised.

#### ModelCatalogService_UninstallReferenceVoicePackAsync_InstallInFlight_WaitsThenUninstalls

**Scenario**: A pack is uninstalled through a composed service while an install of that pack is in flight.

**Expected**: The uninstall does not take effect while the install runs (no voice change is
reported), then removes the pack once the install ends, after which the voice choices change and the
notification is raised.

#### ModelCatalogService_InstallReferenceVoicePackAsync_Canceled_StillRebuildsAndRaises

**Scenario**: A fresh pack install is canceled, so the install throws a cancellation.

**Expected**: The cancellation reaches the caller, the catalog and library pair is rebuilt so the
voice choices match the pack's actual state, and `ReferenceVoicesChanged` is raised once so
dependent panels refresh.

#### ModelCatalogViewModel_InstallVoicePackAsync_FreshInstallCanceled_ShowsNotInstalledWithMessage

**Scenario**: A pack that is not installed is installed through the panel and the install is
canceled.

**Expected**: Nothing is thrown at the user, the row stays not installed with installation still
offered and removal unavailable, and "Install canceled." is shown on the row.

#### ModelCatalogViewModel_DownloadAsync_ProgressReported_UpdatesRowProgress

**Scenario**: The library reports progress while a model downloads.

**Expected**: The downloading model's row shows the reported progress.

#### ModelCatalogViewModel_DownloadAsync_InFlight_ShowsDownloadingState

**Scenario**: A model download is still running.

**Expected**: The row is in the downloading state until the download finishes.

#### ModelListItemViewModel_IsDownloading_DownloadingState_ReturnsTrue

**Scenario**: A row is in the downloading state.

**Expected**: The row reports that it is downloading, so progress is shown.

#### ModelCatalogViewModel_DownloadAsync_RetryAfterFailure_ClearsPreviousFailureMessage

**Scenario**: A failed download is retried and the retry succeeds.

**Expected**: The row is installed and the earlier failure message is cleared.

#### ModelCatalogViewModel_Constructor_EmptyCatalog_ReportsHonestEmptyState

**Scenario**: The panel is created over a catalog with no models.

**Expected**: No rows are built, the panel reports the catalog as empty and selects nothing.

#### ModelCatalogViewModel_EmptyCatalogMessage_Read_ExplainsCatalogHasNoKnownModels

**Scenario**: The empty-catalog message is read.

**Expected**: The text explains that the catalog has no known models.

#### ModelCatalogViewModel_Constructor_NoVoicePacks_HidesSection

**Scenario**: The panel is created over a service that has no voice library.

**Expected**: The voice packs list is empty and the reference voices section is hidden.

#### ModelCatalogViewModel_Constructor_InitialMirrorValues_PrePopulatesFields

**Scenario**: The panel is created with launch-time mirror values.

**Expected**: The mirror settings fields start with those values.

#### ModelCatalogViewModel_Constructor_VoicePacks_ShowsConsentNote

**Scenario**: The panel is created over a service that offers voice packs.

**Expected**: The voice-cloning consent note is shown in the panel.

#### ModelCatalogViewModel_RefreshAsync_SeamFault_ReportedAndRowsKept

**Scenario**: The service throws while the catalog is re-read.

**Expected**: The panel reports an explained error and keeps the rows it already shows.

#### ModelCatalogViewModel_ApplyMirrorAsync_BlankUrl_AppliesNullMirror

**Scenario**: The mirror URL is cleared and applied.

**Expected**: The service receives no mirror, so later downloads use each model's own public URI.

#### ModelCatalogViewModel_ApplyMirrorAsync_ReloadFault_ReportedInPanel

**Scenario**: The mirror is applied but re-reading the catalog afterwards faults.

**Expected**: The fault is reported in the panel rather than thrown.

#### ModelCatalogViewModel_ApplyMirrorAsync_ServiceRejectsReconfiguration_ReportsError

**Scenario**: The service refuses to reconfigure for the mirror.

**Expected**: The panel reports a mirror error with the service's message instead of throwing.
