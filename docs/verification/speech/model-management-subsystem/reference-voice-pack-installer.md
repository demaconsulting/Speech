### ReferenceVoicePackInstaller

#### Verification Approach

Unit-tested in `ReferenceVoicePackInstallerTests.cs` with small synthetic zip archives built in memory
(a typical archive, zip-slip variants, an archive with no voices, and non-zip bytes) staged in a
temporary directory. No network access and no real upstream archive are used.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **File system**: a temporary directory under the system temp path, created per test and deleted afterward

#### Acceptance Criteria

Tests pass when exactly the allow-listed entries are extracted with the root folder removed (and, when
requested, the emotion clips), `pack.json` is written with the variants and clip counts, the archive is
deleted, every unsafe, non-portable, duplicate or malformed archive fails with `InvalidDataException` without
writing outside the staging directory, content over the per-entry or total size cap fails while content
exactly at the (inclusive) caps succeeds, and cancellation is honored.

#### Test Scenarios

##### Selective Extraction

**Test**: `ReferenceVoicePackInstaller_Install_TypicalArchive_ExtractsOnlyAllowListedEntries`

**Test**: `ReferenceVoicePackInstaller_Install_TypicalArchive_WritesPackMetadata`

**Test**: `ReferenceVoicePackInstaller_Install_TypicalArchive_DeletesArchive`

**Test**: `ReferenceVoicePackInstaller_Install_EntriesOutsideRoot_AreIgnored`

**Test**: `ReferenceVoicePackInstaller_Install_StagingDirectoryWithTrailingSeparator_Extracts`

##### Archive Safety

**Test**: `ReferenceVoicePackInstaller_Install_UnsafeEntryName_ThrowsAndWritesNothingOutside`

**Test**: `ReferenceVoicePackInstaller_Install_PlatformInvalidEntryName_ThrowsInvalidData`

**Test**: `ReferenceVoicePackInstaller_Install_WithEmotions_UnsafeEmotionEntry_Throws` (also covers a colon, a
trailing dot or space and a control character in an emotion speaker folder)

**Test**: `ReferenceVoicePackInstaller_Install_NoVoices_Throws` (asserts the message names the expected
`voice-zero/voices/*.flac` layout and "voice clips")

**Test**: `ReferenceVoicePackInstaller_Install_DuplicateEntry_Throws`

**Test**: `ReferenceVoicePackInstaller_Install_NotAZip_Throws`

**Test**: `ReferenceVoicePackInstaller_Install_CanceledToken_Throws`

**Test**: `ReferenceVoicePackInstaller_Install_NoVoices_KeepsArchive`

**Test**: `ReferenceVoicePackInstaller_Install_MissingArchive_ThrowsIOException`

**Test**: `ReferenceVoicePackInstaller_Install_NullPackOrEmptyStaging_Throws`

The unsafe-name theory also covers a `.` segment and a NUL in the entry name.

##### Size Caps

**Test**: `ReferenceVoicePackInstaller_Install_EntryExceedsEntryCap_Throws`

**Test**: `ReferenceVoicePackInstaller_Install_TotalExceedsTotalCap_Throws`

**Test**: `ReferenceVoicePackInstaller_Install_ForgedHeaderSize_NeverExtractsMoreThanCap`

Patches the central-directory header of a 5,000-byte entry to declare 1 byte and asserts the install either
fails or leaves no clip larger than the cap.

**Test**: `ReferenceVoicePackInstaller_Install_AtCaps_Succeeds`

These use the internal `InstallAsync` overload that takes the two caps, with limits of 50-100 bytes, so
no large allocation is needed; the last test also asserts the default constants are 128 MiB and 2 GiB.

##### Emotion Variants

**Test**: `ReferenceVoicePackInstaller_Install_WithEmotions_ExtractsMoodClipsAndRecordsVariant`

**Test**: `ReferenceVoicePackInstaller_Install_WithoutEmotions_SkipsMoodClips`

**Test**: `ReferenceVoicePackInstaller_Install_WithEmotions_IgnoresMisshapenEmotionEntries`

**Test**: `ReferenceVoicePackInstaller_Install_WithEmotions_UnsafeEmotionEntry_Throws`

**Test**: `ReferenceVoicePackInstaller_Install_OnlyEmotionClips_Throws`
