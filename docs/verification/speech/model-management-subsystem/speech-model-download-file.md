### SpeechModelDownloadFile

#### Verification Approach

Verified through direct unit tests in `SpeechModelDownloadFileTests.cs` proving the constructor's
eager validation of URI scheme, checksum format, and relative install path.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when valid values are exposed unchanged, a non-HTTPS URI is rejected, a malformed
checksum is rejected, an empty/whitespace or parent-escaping install path is rejected, a rooted
install path is rejected, and any null argument is rejected.

#### Test Scenarios

##### Constructor: Valid Values Exposes Values

**Test**: `SpeechModelDownloadFile_Constructor_ValidValues_ExposesValues`

##### Constructor: HTTP URI Throws ArgumentException

**Test**: `SpeechModelDownloadFile_Constructor_HttpUri_ThrowsArgumentException`

##### Constructor: Invalid Checksum Throws ArgumentException

**Test**: `SpeechModelDownloadFile_Constructor_InvalidChecksum_ThrowsArgumentException`

##### Constructor: Invalid Install Path Throws ArgumentException

**Test**: `SpeechModelDownloadFile_Constructor_InvalidInstallPath_ThrowsArgumentException`

##### Constructor: Rooted Install Path Throws ArgumentException

**Test**: `SpeechModelDownloadFile_Constructor_RootedInstallPath_ThrowsArgumentException`

##### Constructor: Null Arguments Throws ArgumentNullException

**Test**: `SpeechModelDownloadFile_Constructor_NullArguments_ThrowsArgumentNullException`
