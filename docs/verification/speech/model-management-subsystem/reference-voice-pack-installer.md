### ReferenceVoicePackInstaller

#### Verification Approach

Unit-tested in `ReferenceVoicePackInstallerTests.cs` with small synthetic zip archives built in memory
(a typical archive, zip-slip variants, an archive with no voices, and non-zip bytes) staged in a
temporary directory. No network access and no real upstream archive are used.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when exactly the allow-listed entries are extracted with the root folder removed, `pack.json`
is written, the archive is deleted, every unsafe or malformed archive fails with
`InvalidDataException` without writing outside the staging directory, and cancellation is honored.

#### Test Scenarios

##### Selective Extraction

**Test**: `ReferenceVoicePackInstaller_Install_TypicalArchive_ExtractsOnlyAllowListedEntries`

**Test**: `ReferenceVoicePackInstaller_Install_TypicalArchive_WritesPackMetadata`

**Test**: `ReferenceVoicePackInstaller_Install_TypicalArchive_DeletesArchive`

**Test**: `ReferenceVoicePackInstaller_Install_EntriesOutsideRoot_AreIgnored`

##### Archive Safety

**Test**: `ReferenceVoicePackInstaller_Install_UnsafeEntryName_ThrowsAndWritesNothingOutside`

**Test**: `ReferenceVoicePackInstaller_Install_NoVoices_Throws`

**Test**: `ReferenceVoicePackInstaller_Install_NotAZip_Throws`

**Test**: `ReferenceVoicePackInstaller_Install_CanceledToken_Throws`
