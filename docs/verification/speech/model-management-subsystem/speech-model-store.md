### SpeechModelStore

#### Verification Approach

Verified through direct unit tests in `SpeechModelStoreTests.cs` using a unique scratch
directory per test. These tests prove root resolution, the atomic `current/` swap on both first
install and repair/re-download (including rollback when the swap fails), install-state honesty
(including manifest-missing, malformed, field-less, and unreadable manifests), leftover cleanup,
uninstall success/no-op/failure paths, and model-id validation, entirely on the local file
system with no real network access.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- Each test constructs a `SpeechModelStore` rooted at a unique
  `Path.Combine(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid())` directory,
  deleted on test disposal

#### Acceptance Criteria

Tests pass when root resolution honors both the default and the override, `IsInstalled` never
throws and is honest about manifest/`current/` state, the atomic swap rename replaces prior
content without leaving stale content readable, leftover `.tmp/*` directories are cleaned up, and
uninstall removes an installed model while being a no-op for one that was never installed.

#### Test Scenarios

##### Construction: No Options Resolves Under LocalApplicationData

**Test**: `SpeechModelStore_Constructor_NoOptions_ResolvesUnderLocalApplicationData`

##### Construction: Root Path Override Used Verbatim

**Test**: `SpeechModelStore_Constructor_RootPathOverride_UsesOverrideVerbatim`

##### Install State: No Install Reports Not Installed

**Test**: `SpeechModelStore_IsInstalled_NoInstall_ReturnsFalse`

##### Install State: Current Without Manifest Reports Not Installed

**Test**: `SpeechModelStore_IsInstalled_CurrentWithoutManifest_ReturnsFalse`

##### Atomic Swap: First Install Creates Current and Manifest

**Test**: `SpeechModelStore_CompleteInstall_FirstInstall_CreatesCurrentAndManifest`

##### Atomic Swap: Existing Current is Replaced and Cleaned Up

**Test**: `SpeechModelStore_CompleteInstall_ExistingCurrent_ReplacesContentAndCleansUpOldDirectory`

##### Staging: Existing Current is Untouched Until the Swap

**Test**: `SpeechModelStore_BeginStaging_ExistingCurrent_LeavesInstalledContentUntouched`

##### Cleanup: Leftover Tmp Directory is Removed

**Test**: `SpeechModelStore_CleanUpLeftovers_LeftoverTmpDirectory_IsRemoved`

##### Uninstall: Installed Model Removes Current and Manifest

**Test**: `SpeechModelStore_Uninstall_InstalledModel_RemovesCurrentAndManifest`

##### UninstallAsync and CleanUpLeftoversAsync

**Tests**: `SpeechModelStore_UninstallAsync_InstalledModel_RemovesCurrentAndManifest`,
`SpeechModelStore_UninstallAsync_InvalidIdAndCanceledToken_AreRejected`,
`SpeechModelStore_CleanUpLeftoversAsync_LeftoverTmpDirectory_IsRemoved`

##### Uninstall: Not Installed Does Not Throw

**Test**: `SpeechModelStore_Uninstall_NotInstalled_DoesNotThrow`

##### Uninstall: Live Staging Is Kept, Replaced Directories Are Removed

**Test**: `SpeechModelStore_Uninstall_LeavesLiveStagingAndRemovesReplacedDirectories`

##### Uninstall: Open File Handle Throws SpeechModelStoreException (Windows only)

**Test**: `SpeechModelStore_Uninstall_OpenFileHandleInCurrent_ThrowsSpeechModelStoreException`

This scenario opens a file handle inside `current/` with `FileShare.Read` (which omits
`FileShare.Delete`), so that deleting the directory fails, and asserts the documented exception.
It is Windows-specific (POSIX allows deleting an open file) and calls `Assert.Skip(...)` when not
running on Windows, so CI
on other platforms records the scenario as skipped rather than failing.

##### Uninstall: Manifest Delete Failure Throws SpeechModelStoreException (Windows only)

**Test**: `SpeechModelStore_Uninstall_ManifestDeleteFails_ThrowsSpeechModelStoreException`

This scenario marks the install manifest read-only so deleting it fails, and asserts the documented
exception; it is skipped on non-Windows platforms.

##### Uninstall: Invalid Model Id Throws ArgumentException

**Test**: `SpeechModelStore_Uninstall_InvalidModelId_ThrowsArgumentException`

##### Install State: Malformed Manifest Reports Not Installed

**Test**: `SpeechModelStore_IsInstalled_ManifestIsMalformedJson_ReturnsFalse`

##### Install State: Field-less Manifest Reports Not Installed

**Test**: `SpeechModelStore_IsInstalled_ManifestIsEmptyJsonObject_ReturnsFalse`

##### Install State: Unreadable Manifest Reports Not Installed (Windows only)

**Test**: `SpeechModelStore_IsInstalled_ManifestReadAccessDenied_ReturnsFalse`

This scenario denies the current user read access to a valid manifest through an ACL rule and
asserts `IsInstalled` returns `false` without throwing; it is skipped on non-Windows platforms.

##### Atomic Swap: Failed Swap Rolls Back the Previous Install

**Test**: `SpeechModelStore_CompleteInstall_SecondMoveFails_RollsBackPreviousInstall`

##### Repair: Failed Swap Restores the Previous Manifest

**Test**: `SpeechModelStore_CompleteInstall_RepairSwapFails_RestoresPreviousManifest`

##### Repair: Successful Swap Writes the New Manifest

**Test**: `SpeechModelStore_CompleteInstall_Repair_WritesNewManifest`

##### Model Id Validation: Dot Segments and Null Are Rejected

**Test**: `SpeechModelStore_GetModelDirectory_DotSegmentModelId_ThrowsArgumentException`

**Test**: `SpeechModelStore_GetModelDirectory_NullModelId_ThrowsArgumentNullException`

##### Empty .tmp Cleanup After Install

**Test**: `SpeechModelStore_CompleteInstall_RemovesEmptyStagingRoot`

**Test**: `SpeechModelStore_CompleteInstall_OtherOperationStaging_KeepsStagingRoot`

##### Existing Install Stays Untouched During Staging; Replaced Directories Are Cleaned Up

**Test**: `SpeechModelStore_BeginInstall_ExistingInstall_LeavesCurrentUntouchedDuringStaging`
**Test**: `SpeechModelStore_CleanUpLeftovers_ReplacedDirectory_IsRemovedAndCurrentKept`

These scenarios install a model and assert that the empty `.tmp` folder is gone, and that it is kept
while another operation's staging directory is still present.
