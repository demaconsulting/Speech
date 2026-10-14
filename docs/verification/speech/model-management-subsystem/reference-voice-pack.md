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

**Test**: `ReferenceVoicePack_Constructor_NullArguments_ThrowArgumentNull`

Verifies a null URI, checksum, license URL, source URL or id is rejected with `ArgumentNullException`.

**Test**: `ReferenceVoicePack_Constructor_NullTextArguments_ThrowArgumentNullNamingTheArgument`

Verifies a null display name, archive file name, root folder, license name or attribution is rejected with
`ArgumentNullException` naming that argument.

**Test**: `ReferenceVoicePack_Constructor_BlankArguments_ThrowArgument`

Verifies an empty checksum and a blank display name, archive file name, root folder, license name or attribution
are rejected with `ArgumentException`, the blank text arguments naming the offending argument.
(An empty checksum is also a case of `ReferenceVoicePack_Constructor_BadChecksum_Throws`.)

The unsafe-name theories also cover a trailing dot, leading or trailing whitespace, Windows reserved device names
and NUL for the id and the archive file name.

##### Size Metadata

**Test**: `ReferenceVoicePack_VoiceZero10_InstalledSizes_AreMeasuredValues`

**Test**: `ReferenceVoicePack_Constructor_InstalledSizes_DefaultZeroAndRejectNegative`

##### Descriptor and Presentation Fields

The download information (address, checksum, file name) is verified through the internal one-file download
descriptor; that shape is a design detail, so the requirement states only what the information must carry.

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

**Tests**: `ReferenceVoiceInfo_MoodClips_CallerChangesAfterCreation_DoNotAlterClips` (a later change to the
supplied dictionary does not show through; keys are in ordinal mood order),
`ReferenceVoiceInfo_MoodClips_WithExpressionThenCallerChanges_DoNotAlterClips` (the same holds for a `with`
expression assigning a dictionary, and the stored copy cannot be cast to a mutable dictionary),
`ReferenceVoiceInfo_MoodClips_ConsumerModification_IsRejected` (casting the exposed clips to a mutable
dictionary and writing throws)

##### Voice Sources

**Test**: `ReferenceVoiceSource_Values_AreDirectoryUserAndPack`

##### Mood Clip Lookup

**Test**: `ReferenceVoiceInfo_TryGetMoodClip_IsCaseInsensitiveAndTrimmed`

Verifies `TryGetMoodClip` resolves `sad`, `SAD` and ` Sad ` to the same path, and that null, blank, unknown names and a
voice without mood clips report no match.
