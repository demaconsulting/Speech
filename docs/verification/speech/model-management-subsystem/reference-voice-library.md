### ReferenceVoiceLibrary

#### Verification Approach

Unit-tested in `ReferenceVoiceLibraryTests.cs` against a temporary store root, using a synthetic pack
whose checksum is computed from a small in-memory zip and a fake `IModelDownloadClient` that serves
those bytes and records the requested URL. The library is constructed through its internal test
constructor to substitute the synthetic pack for the built-in one. No network access is used.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when the documented layout is produced, install/repeat/failure/uninstall behave and report
state as specified (including a mirror URL and a checksum mismatch installing nothing), the user folder
survives uninstall, and the merged voice list follows the documented order with the first id winning.

#### Test Scenarios

##### Store Layout

**Test**: `ReferenceVoiceLibrary_Paths_FollowDocumentedLayout`

**Test**: `ReferenceVoiceLibrary_InvalidArguments_Throw`

**Test**: `ReferenceVoiceLibrary_EnsureUserDirectory_CreatesFolderAndListsUserVoices`

##### Pack Install

**Test**: `ReferenceVoiceLibrary_InstallPack_Success_InstallsVoicesAndUpdatesState`

**Test**: `ReferenceVoiceLibrary_InstallPack_WithMirror_RequestsMirrorUri`

**Test**: `ReferenceVoiceLibrary_InstallPack_SecondCall_IsNoOp`

**Test**: `ReferenceVoiceLibrary_InstallPack_ChecksumMismatch_InstallsNothingAndReportsFailedState`

**Test**: `ReferenceVoiceLibrary_InstallPack_ArchiveWithoutVoices_FailsWithoutInstalling`

**Test**: `ReferenceVoiceLibrary_UninstallPack_RemovesPackKeepsUserFolder`

**Test**: `ReferenceVoiceLibrary_InstallPack_AfterDispose_Throws`

##### Voice Merge

**Test**: `ReferenceVoiceLibrary_EnumerateVoices_MergesInPrecedenceOrderFirstIdWins`

**Test**: `ReferenceVoiceLibrary_EnumerateVoices_NothingPresent_ReturnsEmptyWithoutCreatingFolders`

**Test**: `ReferenceVoiceLibrary_Humanize_FormatsName`
