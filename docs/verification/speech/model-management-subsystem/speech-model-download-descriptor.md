### SpeechModelDownloadDescriptor

#### Verification Approach

Verified through direct unit tests in `SpeechModelDownloadDescriptorTests.cs` proving order
preservation, the constructor's rejection of an empty, null, null-element, or
duplicate-install-path (including differing-separator duplicates) file list, and the defensive
copy of the caller's list.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when multiple files preserve their declared order, an empty file list is rejected, a
null file list is rejected, a file list containing a null element is rejected, a file list with
two entries sharing the same relative install path (whether identical or differing only by path
separator) is rejected, and mutating the caller's own list after construction does not affect the
already-validated descriptor.

#### Test Scenarios

##### Constructor: Multiple Files Preserves Order

**Test**: `SpeechModelDownloadDescriptor_Constructor_MultipleFiles_PreservesOrder`

##### Constructor: Empty File List Throws ArgumentException

**Test**: `SpeechModelDownloadDescriptor_Constructor_EmptyFileList_ThrowsArgumentException`

##### Constructor: Null File List Throws ArgumentNullException

**Test**: `SpeechModelDownloadDescriptor_Constructor_NullFileList_ThrowsArgumentNullException`

##### Constructor: Duplicate Install Paths Throws ArgumentException

**Test**: `SpeechModelDownloadDescriptor_Constructor_DuplicateInstallPaths_ThrowsArgumentException`

##### Constructor: Duplicate Install Paths With Differing Separators Throws ArgumentException

**Test**: `SpeechModelDownloadDescriptor_Constructor_DuplicateInstallPathsWithDifferingSeparators_ThrowsArgumentException`

##### Constructor: Null Element In File List Throws ArgumentException

**Test**: `SpeechModelDownloadDescriptor_Constructor_NullElementInFileList_ThrowsArgumentException`

##### Constructor: Mutating Caller List Does Not Affect Descriptor

**Test**: `SpeechModelDownloadDescriptor_Constructor_MutatingCallerList_DoesNotAffectDescriptor`
