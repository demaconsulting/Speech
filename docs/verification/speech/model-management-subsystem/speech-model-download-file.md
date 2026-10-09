### SpeechModelDownloadFile

#### Verification Approach

Verified through direct unit tests in `SpeechModelDownloadFileTests.cs` proving the constructor's
eager validation of URI scheme, checksum format, and relative install path.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when valid values are exposed unchanged, a non-HTTPS URI is rejected, a malformed
checksum is rejected, an empty/whitespace, parent-escaping (`..`), current-directory (`.`), or
separators-only (no named segment) install path is rejected, a rooted install path is rejected,
any null argument is rejected, and `ResolveStagedPath` combines a staging directory with a
declared install path's segments using this platform's own directory separator regardless of
which separator the install path was declared with.

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

##### ResolveStagedPath: Any Separator Matches Downloader Staging

**Test**: `SpeechModelDownloadFile_ResolveStagedPath_AnySeparator_MatchesDownloaderStaging`

##### ResolveStagedPath: Null Arguments Throws ArgumentNullException

**Test**: `SpeechModelDownloadFile_ResolveStagedPath_NullArguments_ThrowsArgumentNullException`
