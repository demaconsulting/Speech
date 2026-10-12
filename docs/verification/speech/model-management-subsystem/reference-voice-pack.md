### ReferenceVoicePack

#### Verification Approach

Unit-tested in `ReferenceVoicePackTests.cs`, constructing descriptors directly and asserting the
built-in voice-zero 1.0 metadata against the published values. No network access is involved.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

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

**Test**: `ReferenceVoicePack_Constructor_NonPositiveSize_Throws`

##### Size Metadata

**Test**: `ReferenceVoicePack_VoiceZero10_InstalledSizes_AreMeasuredValues`

**Test**: `ReferenceVoicePack_Constructor_InstalledSizes_DefaultZeroAndRejectNegative`
