### SpeechModelDownloadDescriptor

#### Verification Approach

Verified through direct unit tests in `SpeechModelDownloadDescriptorTests.cs` proving order
preservation and the constructor's rejection of an empty, null, or duplicate-install-path file
list.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when multiple files preserve their declared order, an empty file list is rejected, a
null file list is rejected, and a file list with two entries sharing the same relative install
path is rejected.

#### Test Scenarios

##### Constructor: Multiple Files Preserves Order

**Test**: `SpeechModelDownloadDescriptor_Constructor_MultipleFiles_PreservesOrder`

##### Constructor: Empty File List Throws ArgumentException

**Test**: `SpeechModelDownloadDescriptor_Constructor_EmptyFileList_ThrowsArgumentException`

##### Constructor: Null File List Throws ArgumentNullException

**Test**: `SpeechModelDownloadDescriptor_Constructor_NullFileList_ThrowsArgumentNullException`

##### Constructor: Duplicate Install Paths Throws ArgumentException

**Test**: `SpeechModelDownloadDescriptor_Constructor_DuplicateInstallPaths_ThrowsArgumentException`
