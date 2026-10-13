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

A ModelCatalogSubsystem test run passes when: the panel shows, refreshes and downloads models
purely through the catalog service seam and reports every outcome as row state rather than an
application error; mirror changes reconfigure the catalog through the seam and are refused while a
download or pack install is in flight; and a voice pack install, removal or mirror change rebuilds
the catalog and reference voice library and raises the voices-changed notification so other panels
can refresh, even when an install fails or is canceled; a voice pack that is still installed after a
failed install or upgrade stays installed and removable with its failure message; and removing a
pack while its install is in flight waits for the install, and a removal that cannot complete or is
canceled is explained on the pack's row instead of failing.

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

#### ModelCatalogViewModel_ApplyMirror_DownloadInProgress_RefusesAndReportsError

**Scenario**: A mirror is applied while a model download is in flight.

**Expected**: The seam is not asked to reconfigure and the panel explains the refusal.

#### ModelCatalogViewModel_ApplyMirror_PackInstalling_IsRefused

**Scenario**: A mirror is applied while a voice pack install is in flight.

**Expected**: The change is refused and explained, and the seam is not called.

#### ModelCatalogViewModel_Refresh_StateChanged_RebuildsRowsAndKeepsSelection

**Scenario**: A model's install state changes and the panel is refreshed.

**Expected**: Rows are rebuilt from the seam and the selected model stays selected.

#### ModelCatalogViewModel_ApplyMirror_ValidMirror_ForwardsConfiguredMirrorAndReportsSuccess

**Scenario**: Valid mirror fields are applied.

**Expected**: The seam's `ApplyMirror` receives the configured mirror and the panel reports success
and refreshes.

#### ModelCatalogViewModel_ApplyMirror_ValidMirror_ReportsSuccess

**Scenario**: A valid mirror URL is applied.

**Expected**: The panel reports no error and confirms that the mirror was applied.

#### ModelCatalogService_UninstallReferenceVoicePack_RebuildsCatalogAndRaisesEvent

**Scenario**: A pack is uninstalled through a composed service.

**Expected**: The catalog and library pair is rebuilt and `ReferenceVoicesChanged` raised once.

#### ModelCatalogService_ApplyMirror_Composed_RebuildsPairAndRaisesEvent

**Scenario**: The mirror is changed on a composed service.

**Expected**: The pair is rebuilt and `ReferenceVoicesChanged` raised.

#### ModelCatalogViewModel_ApplyMirror_ValidMirror_ReReadsCatalog

**Scenario**: A valid mirror is applied through the panel.

**Expected**: The catalog is re-read so the rows reflect the rebuilt catalog.

#### ModelCatalogViewModel_RemoveVoicePack_UninstallsRefreshesAndReportsFaults

**Scenario**: A pack is removed through the panel.

**Expected**: The seam uninstalls it, the rows are re-read, and faults are reported on the row.

#### SynthesisPanelViewModel_ReferenceVoicesChanged_RefreshesAndKeepsChosenVoice

**Scenario**: The service raises `ReferenceVoicesChanged` to a subscribed synthesis panel.

**Expected**: The panel refreshes its voice choices and keeps the chosen voice; this is the
observable effect of every voices-changed notification the subsystem raises.

#### ModelCatalogService_InstallReferenceVoicePackAsync_Failure_StillRebuildsAndRaises

**Scenario**: A pack install fails.

**Expected**: The pair is still rebuilt and the notification still raised.

#### ModelCatalogViewModel_InstallVoicePackAsync_Installed_RefreshesRows

**Scenario**: A pack install completes through the panel.

**Expected**: The pack row refreshes to the installed state.

#### ModelCatalogViewModel_InstallVoicePackAsync_UpgradeFailsAndRollsBack_KeepsPackInstalledWithMessage

**Scenario**: An "Add emotions" upgrade of an installed voices-only pack fails, hits a checksum mismatch,
is canceled, or the service throws, and the service reports the pack still installed because the
library rolled the upgrade back.

**Expected**: The row stays installed with removal enabled and "Add emotions" still offered, and the
failure message is shown on the row.

#### ModelCatalogViewModel_RemoveVoicePack_StoreException_ShowsMessageAndKeepsPack

**Scenario**: The service rejects the uninstall of a pack with a store exception.

**Expected**: The exception's message is shown on the pack's row, the pack stays installed, and nothing
is thrown at the user.

#### ModelCatalogViewModel_RemoveVoicePack_Canceled_ShowsCanceledMessage

**Scenario**: The seam's uninstall is canceled.

**Expected**: The row shows "Removal canceled." and the pack stays installed.

#### ModelCatalogService_UninstallReferenceVoicePackAsync_InstallInFlight_WaitsThenUninstalls

**Scenario**: A pack is uninstalled through a composed service while an install of that pack is in flight.

**Expected**: The uninstall waits (the pair is not rebuilt and `ReferenceVoicesChanged` is not raised while
the install runs), then removes the pack once the install ends, rebuilding and raising the event.
