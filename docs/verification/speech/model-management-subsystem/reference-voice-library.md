### ReferenceVoiceLibrary

#### Verification Approach

Unit-tested in `ReferenceVoiceLibraryTests.cs` against a temporary store root, using a synthetic pack
whose checksum is computed from a small in-memory zip and a fake `IModelDownloadClient` that serves
those bytes and records the requested URL. The library is constructed through its internal test
constructor to substitute the synthetic pack for the built-in one. No network access is used.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **File system**: a temporary directory under the system temp path, created per test and deleted afterward

#### Acceptance Criteria

Tests pass when the documented layout is produced and no folder is created until asked; install, repeat,
failure and state reporting behave as specified (including a mirror URL and a checksum mismatch
installing nothing); uninstall keeps the user folder, discards an upgrade backup and waits for an
in-flight install of the pack before removing it (a token canceled while waiting cancels only the uninstall
and leaves the install intact); disposal is idempotent
and a disposed library refuses installs; emotion variants install by default, are optional, attach as
`MoodClips` without adding voice ids, upgrade a voices-only install safely (a failed, canceled or
abandoned upgrade keeps the previous install), serialize concurrent installs and report variants
without throwing; and the merged voice list follows the documented order with the first id winning.

#### Test Scenarios

All scenarios are in `test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/ReferenceVoiceLibraryTests.cs`.

##### Store Layout

**Purpose**: Paths follow the documented layout, and constructing or enumerating creates nothing.

**Tests**: `ReferenceVoiceLibrary_Paths_FollowDocumentedLayout`,
`ReferenceVoiceLibrary_EnsureUserDirectory_CreatesFolderAndListsUserVoices`,
`ReferenceVoiceLibrary_EnumerateVoices_NothingPresent_ReturnsEmptyWithoutCreatingFolders`

##### Argument Validation

**Purpose**: Null arguments and unknown pack ids throw.

**Test**: `ReferenceVoiceLibrary_InvalidArguments_Throw`

##### Pack Install

**Purpose**: A verified archive installs once through the downloader (mirror-aware), a repeat is a
no-op, and failures leave nothing installed and report a failed state.

**Tests**: `ReferenceVoiceLibrary_InstallPack_Success_InstallsVoicesAndUpdatesState`,
`ReferenceVoiceLibrary_InstallPack_WithMirror_RequestsMirrorUri`,
`ReferenceVoiceLibrary_InstallPack_SecondCall_IsNoOp`,
`ReferenceVoiceLibrary_InstallPack_ChecksumMismatch_InstallsNothingAndReportsFailedState`,
`ReferenceVoiceLibrary_InstallPack_ArchiveWithoutVoices_FailsWithoutInstalling`

##### Pack Uninstall

**Purpose**: Uninstall removes the pack only, waits for an in-flight install of the same pack and then
removes it (a token canceled while waiting cancels only the uninstall), and discards any upgrade backup.

**Tests**: `ReferenceVoiceLibrary_UninstallPackAsync_RemovesPackKeepsUserFolder`,
`ReferenceVoiceLibrary_UninstallPackAsync_WhileInstalling_WaitsThenRemoves`,
`ReferenceVoiceLibrary_UninstallPackAsync_CanceledWhileWaiting_LeavesInstallIntact`,
`ReferenceVoiceLibrary_UninstallPackAsync_AlreadyCanceled_DoesNothingAndReleasesLock`,
`ReferenceVoiceLibrary_UninstallPackAsync_DiscardsUpgradeBackup`

##### Disposal

**Purpose**: Disposing twice is harmless and an install after disposal throws.

**Tests**: `ReferenceVoiceLibrary_Dispose_Twice_DoesNotThrow`,
`ReferenceVoiceLibrary_InstallPack_AfterDispose_Throws`

##### Voice Merge

**Purpose**: Voices merge in precedence order with the first id winning, duplicates are reported, ids
are humanized, and the consent notice is exposed.

**Tests**: `ReferenceVoiceLibrary_EnumerateVoices_MergesInPrecedenceOrderFirstIdWins`,
`ReferenceVoiceLibrary_EnumerateVoices_DuplicateId_ReportsInfoDiagnostic`,
`ReferenceVoiceLibrary_EnumerateVoices_MalformedDirectory_IsSkippedWithoutThrowing`,
`ReferenceVoiceLibrary_Humanize_FormatsName`,
`ReferenceVoiceLibrary_ConsentNotice_MentionsPermissionToClone`

##### Emotion Variants

**Purpose**: The default install includes emotions as `MoodClips` (no extra voice ids), `false` gives
voices only, and the progress overload includes emotions.

**Tests**: `ReferenceVoiceLibrary_InstallPack_Default_AttachesMoodClipsWithoutExtraVoices`,
`ReferenceVoiceLibrary_InstallPack_NoEmotions_LeavesVoicesOnly`,
`ReferenceVoiceLibrary_InstallPack_ProgressOverload_IncludesEmotions`

##### Emotion Upgrade

**Purpose**: Upgrading a voices-only install adds emotions; a failed or canceled upgrade, and an
abandoned backup, keep or restore the previous install.

**Tests**: `ReferenceVoiceLibrary_InstallPack_UpgradeFromVoicesOnly_AddsEmotions`,
`ReferenceVoiceLibrary_InstallPack_VoicesOnlyOverEmotionInstall_NeverDowngrades`,
`ReferenceVoiceLibrary_InstallPack_FailedUpgrade_KeepsPreviousInstall`,
`ReferenceVoiceLibrary_InstallPack_CanceledUpgrade_KeepsPreviousInstall`,
`ReferenceVoiceLibrary_InstallPack_AbandonedBackup_IsRestored`

##### Install Serialization

**Purpose**: Concurrent installs of one pack run one at a time.

**Test**: `ReferenceVoiceLibrary_InstallPack_ConcurrentCalls_AreSerialized`

##### Variant Reporting

**Purpose**: Missing or corrupt `pack.json` yields no variants and never throws.

**Test**: `ReferenceVoiceLibrary_PackVariants_MissingOrCorruptMetadata_AreEmptyAndNeverThrow`
