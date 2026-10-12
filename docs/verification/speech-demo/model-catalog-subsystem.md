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
pack while its install is in flight is refused with an explanation instead of failing.

### Test Scenarios

#### ModelCatalogViewModel_DownloadAsync_Installed_MarksModelDownloaded

**Scenario**: The panel downloads a model through the seam and the library reports it installed.

**Expected**: The row becomes installed at full progress.

#### ModelCatalogViewModel_Refresh_StateChanged_RebuildsRowsAndKeepsSelection

**Scenario**: A model's install state changes and the panel is refreshed.

**Expected**: Rows are rebuilt from the seam and the selected model stays selected.

#### ModelCatalogViewModel_ApplyMirror_ValidMirror_ForwardsConfiguredMirrorAndReportsSuccess

**Scenario**: Valid mirror fields are applied.

**Expected**: The seam's `ApplyMirror` receives the configured mirror and the panel reports success
and refreshes.

#### ModelCatalogService_UninstallReferenceVoicePack_RebuildsCatalogAndRaisesEvent

**Scenario**: A pack is uninstalled through a composed service.

**Expected**: The catalog and library pair is rebuilt and `ReferenceVoicesChanged` raised once.

#### ModelCatalogService_ApplyMirror_Composed_RebuildsPairAndRaisesEvent

**Scenario**: The mirror is changed on a composed service.

**Expected**: The pair is rebuilt and `ReferenceVoicesChanged` raised.

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

#### ModelCatalogViewModel_RemoveVoicePack_InstallInFlight_ShowsMessageAndKeepsPack

**Scenario**: The service refuses to uninstall a pack because an install of that pack is in flight.

**Expected**: The refusal's message is shown on the pack's row, the pack stays installed, and nothing
is thrown at the user.

#### ModelCatalogService_UninstallReferenceVoicePack_InstallInFlight_ThrowsStoreExceptionWithoutRebuild

**Scenario**: A pack is uninstalled through a composed service while an install of that pack is in flight.

**Expected**: The library's store exception propagates, the catalog and library pair is not rebuilt, and
`ReferenceVoicesChanged` is not raised.
