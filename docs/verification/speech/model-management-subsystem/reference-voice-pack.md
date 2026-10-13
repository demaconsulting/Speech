### ReferenceVoicePack

#### Verification Approach

Unit-tested in `ReferenceVoicePackTests.cs`, constructing descriptors directly and asserting the
built-in voice-zero 1.0 metadata against the published values. No network access is involved.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Isolation**: descriptors are built in memory; there is no file system, network or mocking involved.

#### Acceptance Criteria

Tests pass when the built-in descriptor carries exactly the published URL, size, checksum, license and
root folder and is listed in `All`, and when every invalid constructor argument is rejected.

#### Test Scenarios

##### Published Metadata

**Test**: `ReferenceVoicePack_VoiceZero10_HasPublishedMetadata`

**Test**: `ReferenceVoicePack_Constructor_ValidArguments_ExposesValues`

##### Argument Validation

**Test**: `ReferenceVoicePack_Constructor_NonHttpsUri_Throws`

**Test**: `ReferenceVoicePack_Constructor_BadChecksum_Throws`

**Test**: `ReferenceVoicePack_Constructor_UnsafeId_Throws`

**Test**: `ReferenceVoicePack_Constructor_UnsafeRootFolder_Throws`

**Test**: `ReferenceVoicePack_Constructor_RootedRootFolder_Throws`

Drive-qualified (`C:`, `C:\x`) and leading-slash names for the id, archive file name and root folder are
rejected on every platform, not only where the platform's invalid-character set happens to include them.

**Test**: `ReferenceVoicePack_Constructor_NonPositiveSize_Throws` (zero and negative sizes)

**Test**: `ReferenceVoicePack_Constructor_NullOrBlankArguments_Throw`

Verifies a null URI or checksum, an empty checksum and a blank id, display name, archive file name, root folder,
license name or attribution are rejected with the documented exception types.

The unsafe-name theories also cover a trailing dot, leading or trailing whitespace, Windows reserved device names
and NUL for the id and the archive file name.

##### Size Metadata

**Test**: `ReferenceVoicePack_VoiceZero10_InstalledSizes_AreMeasuredValues`

**Test**: `ReferenceVoicePack_Constructor_InstalledSizes_DefaultZeroAndRejectNegative`

##### Descriptor and Presentation Fields

**Tests**: `ReferenceVoicePack_DownloadDescriptor_DescribesTheArchive`,
`ReferenceVoicePack_VoiceZero10_HasPublishedPresentationFields`,
`ReferenceVoicePack_Constructor_UnsafeArchiveFileName_Throws`

##### Reference Identity

**Test**: `ReferenceVoicePack_Equals_SameValuesDifferentInstances_AreNotEqual`

**Test**: `ReferenceVoicePack_Constructor_ValidArguments_KeepsSuppliedUriInstances`

Verifies two descriptors built from equal values are distinct, unequal instances.

##### Known Moods

**Test**: `ReferenceVoiceInfo_KnownMoods_ListsThirteenDistinctLowerCaseMoods`

Also asserts the exact ordinal list of the 13 mood names.

##### Mood Clips Immutability

**Test**: `ReferenceVoiceInfo_MoodClips_AreAnImmutableOrderedCopy`

##### Voice Sources

**Test**: `ReferenceVoiceSource_Values_AreDirectoryUserAndPack`

##### Mood Clip Lookup

**Test**: `ReferenceVoiceInfo_TryGetMoodClip_IsCaseInsensitiveAndTrimmed`

Verifies `TryGetMoodClip` resolves `sad`, `SAD` and ` Sad ` to the same path, and that null, blank, unknown names and a
voice without mood clips report no match.
