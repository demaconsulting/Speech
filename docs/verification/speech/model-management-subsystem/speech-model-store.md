### SpeechModelStore

#### Verification Approach

Verified through direct unit tests in `SpeechModelStoreTests.cs` using a unique scratch
directory per test. These tests prove root resolution, the atomic `current/` swap on both first
install and repair/re-download, install-state honesty (including a manifest-missing crash
scenario), leftover cleanup, and uninstall success/no-op paths, entirely on the local file
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

##### Cleanup: Leftover Tmp Directory is Removed

**Test**: `SpeechModelStore_CleanUpLeftovers_LeftoverTmpDirectory_IsRemoved`

##### Uninstall: Installed Model Removes Current and Manifest

**Test**: `SpeechModelStore_Uninstall_InstalledModel_RemovesCurrentAndManifest`

##### Uninstall: Not Installed Does Not Throw

**Test**: `SpeechModelStore_Uninstall_NotInstalled_DoesNotThrow`

##### Uninstall: Open File Handle Throws SpeechModelStoreException (Windows only)

**Test**: `SpeechModelStore_Uninstall_OpenFileHandleInCurrent_ThrowsSpeechModelStoreException`

This scenario opens a file handle inside `current/` with `FileShare.None` and asserts the
documented exception. It is Windows-specific (POSIX allows deleting an open file) and calls
`Assert.Skip(...)` when not running on Windows, so CI on other platforms records the scenario as
skipped rather than failing.
