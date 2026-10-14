### SpeechModelStore

#### Verification Approach

Verified through direct unit tests in `SpeechModelStoreTests.cs` using a unique scratch
directory per test. These tests prove root resolution, the two-step replace with rollback of `current/` on both first
install and repair/re-download (including rollback when the swap fails), install-state honesty
(including manifest-missing, malformed, field-less, and unreadable manifests), leftover cleanup,
uninstall success/no-op/failure paths, and model-id validation. Nothing is mocked: every test
uses the real file system under a scratch directory, with no network access. Failure paths that
depend on Windows file-locking or ACL semantics run only on Windows and are linked in the
requirements with the `windows@` source filter.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- Each test constructs a `SpeechModelStore` rooted at a unique
  `Path.Combine(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid())` directory,
  deleted on test disposal

#### Acceptance Criteria

Tests pass when root resolution honors both the default and the override, `IsInstalled` never
throws and is honest about manifest/`current/` state (including after a failed uninstall), the two-step
replacement renames replace prior content without ever exposing partially replaced or stale content (a momentary
not-installed state between the renames is permitted and is not observable by the single-threaded tests) and roll back
to the prior install on failure, the
operation id and staging directory are validated, leftover `.tmp/*` directories are cleaned up, model
ids are validated before the file system is touched, and uninstall removes an installed model while being a
no-op for one that was never installed. Windows-only scenarios are skipped, not passed, on other platforms.

#### Test Scenarios

##### Construction: No Options Resolves Under LocalApplicationData

**Test**: `SpeechModelStore_Constructor_NoOptions_ResolvesUnderLocalApplicationData`

##### Construction: Root Path Override Used Verbatim

**Test**: `SpeechModelStore_Constructor_RootPathOverride_UsesOverrideVerbatim`

##### Install State: No Install Reports Not Installed

**Test**: `SpeechModelStore_IsInstalled_NoInstall_ReturnsFalse`

##### Install State: Current Without Manifest Reports Not Installed

**Test**: `SpeechModelStore_IsInstalled_CurrentWithoutManifest_ReturnsFalse`

##### Install: First Install Creates Current and Manifest

**Test**: `SpeechModelStore_CompleteInstall_FirstInstall_CreatesCurrentAndManifest`

##### Replacement: Existing Current is Replaced and Cleaned Up

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

##### Uninstall: Open File Handle Throws SpeechModelStoreException (Windows only)

**Test**: `SpeechModelStore_Uninstall_OpenFileHandleInCurrent_ThrowsSpeechModelStoreException`

After the exception the model must read as not installed (the manifest is removed before `current/`).
This scenario opens a file handle inside `current/` with `FileShare.Read` (which omits
`FileShare.Delete`), so that deleting the directory fails, and asserts the documented exception.
It is Windows-specific (POSIX allows deleting an open file) and calls `Assert.Skip(...)` when not
running on Windows, so CI
on other platforms records the scenario as skipped rather than failing.

##### Uninstall: Manifest Delete Failure Throws SpeechModelStoreException (Windows only)

**Test**: `SpeechModelStore_Uninstall_ManifestDeleteFails_ThrowsSpeechModelStoreException`

This scenario marks the install manifest read-only so deleting it fails, and asserts the documented
exception; it is skipped on non-Windows platforms.

##### Uninstall: Manifest Delete Failure Leaves the Model Installed (Windows only)

**Test**: `SpeechModelStore_Uninstall_ManifestDeleteFails_LeavesModelInstalled`

A read-only manifest makes the delete fail before anything is removed; `IsInstalled` stays `true` and
the content in `current/` is intact.

##### UninstallAsync: Failure Is Reported Through the Task (Windows only)

**Test**: `SpeechModelStore_UninstallAsync_OpenFileHandleInCurrent_FaultsWithSpeechModelStoreException`

##### Uninstall: Live Staging Is Left Alone

**Test**: `SpeechModelStore_Uninstall_LiveStagingDirectory_IsLeftAlone`

##### Uninstall: Replaced Directories Are Removed

**Test**: `SpeechModelStore_Uninstall_ReplacedDirectories_AreRemoved`

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

##### Replacement: Failed Replacement Rolls Back and Preserves the Previous Install

**Test**: `SpeechModelStore_CompleteInstall_SecondMoveFails_RollsBackPreviousInstall`

A staging directory that does not exist makes the second rename fail after the existing `current/` was renamed
aside. The test asserts the exception propagates, the model still reads as installed, the original payload is
back in `current/`, and no `current.replaced-*` directory is left behind. Boundary note: the contract permits a
momentary not-installed state between the two renames; that window is not asserted because it cannot be
observed deterministically, only the end states (success, or rollback after failure) are.

##### Repair: Failed Swap Restores the Previous Manifest

**Test**: `SpeechModelStore_CompleteInstall_RepairSwapFails_RestoresPreviousManifest`

##### Repair: Successful Swap Writes the New Manifest

**Test**: `SpeechModelStore_CompleteInstall_Repair_WritesNewManifest`

##### Model Id Validation: Dot Segments and Null Are Rejected

**Test**: `SpeechModelStore_GetModelDirectory_DotSegmentModelId_ThrowsArgumentException`

**Test**: `SpeechModelStore_GetModelDirectory_NullModelId_ThrowsArgumentNullException`

##### Model Id Validation: Invalid Character Is Rejected Without Touching the File System

**Test**: `SpeechModelStore_GetModelDirectory_InvalidCharacterModelId_ThrowsAndCreatesNothing`

A model id containing a NUL character is rejected by `GetModelDirectory`, `BeginStaging` and `Uninstall`,
and the scratch root stays empty.

##### Install: Manifest Is Written Only After the Rename

**Test**: `SpeechModelStore_CompleteInstall_ManifestWriteFails_ThrowsAndReadsAsNotInstalled`

A directory occupying the manifest path makes the manifest write fail after the rename; the install throws,
`current/` exists, and `IsInstalled` is `false`.

##### Install: Unsafe Operation Id or Foreign Staging Directory Is Rejected

**Test**: `SpeechModelStore_CompleteInstall_UnsafeOperationIdOrForeignStaging_ThrowsArgumentException`

Path-traversal operation ids and a staging directory outside the store are rejected with `ArgumentException`
and the installed content is unchanged.

##### Install: Another Operation's Staging Directory Is Rejected

**Test**: `SpeechModelStore_CompleteInstall_OtherOperationStaging_ThrowsArgumentExceptionAndTouchesNothing`

**Test**: `SpeechModelStore_CompleteInstall_NestedStagingDescendant_ThrowsArgumentException`

With two operations in progress for the same model, completing operation A with operation B's staging directory
(or with a directory nested inside A's own staging directory) throws `ArgumentException` for `stagingDirectory`;
both staging directories and the installed content are unchanged.

##### Staging: Concurrent Begin Creates Unique Empty Directories

**Test**: `SpeechModelStore_BeginStaging_ConcurrentCalls_CreateUniqueEmptyDirectories`

Sixteen parallel calls each create a distinct, empty staging directory under `.tmp`. The bounded retry for a
racing empty-`.tmp` cleanup is exercised by this contention but cannot be forced deterministically.

##### Staging: Abandon Deletes Staging and Keeps Current

**Test**: `SpeechModelStore_AbandonStaging_StagingDirectory_IsDeletedAndCurrentKept`

##### Cleanup: Locked Leftover Does Not Throw (Windows only)

**Test**: `SpeechModelStore_CleanUpLeftovers_LockedLeftoverDirectory_DoesNotThrow`

##### Construction: Whitespace, Null, or Empty Root Override Uses the Default

**Test**: `SpeechModelStore_Constructor_WhitespaceRootPathOverride_UsesDefaultRoot`

**Test**: `SpeechModelStore_Constructor_NullOrEmptyRootPathOverride_UsesDefaultRoot`

**Test**: `SpeechModelStore_Constructor_PaddedRootPathOverride_UsesOverrideVerbatim` (boundary: not whitespace-only)

##### Empty .tmp Cleanup After Install

**Test**: `SpeechModelStore_CompleteInstall_RemovesEmptyStagingRoot`

**Test**: `SpeechModelStore_CompleteInstall_OtherOperationStaging_KeepsStagingRoot`

These scenarios install a model and assert that the empty `.tmp` folder is gone, and that it is kept
while another operation's staging directory is still present.

##### Existing Install Stays Untouched During Staging; Replaced Directories Are Cleaned Up

**Test**: `SpeechModelStore_BeginInstall_ExistingInstall_LeavesCurrentUntouchedDuringStaging`

**Test**: `SpeechModelStore_CleanUpLeftovers_ReplacedDirectory_IsRemovedAndCurrentKept`

The first scenario stages while a model is installed and asserts `current/` is untouched; the second
leaves a `current.replaced-*` directory behind and asserts cleanup removes it while keeping `current/`.

##### CleanUpLeftoversAsync: Canceled Token Is Honored

**Test**: `SpeechModelStore_CleanUpLeftoversAsync_CanceledToken_ThrowsWithoutDeleting`

A canceled token throws `OperationCanceledException` before the cleanup starts and the leftover
staging directory is left in place.
