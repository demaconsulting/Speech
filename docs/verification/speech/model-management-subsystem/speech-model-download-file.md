### SpeechModelDownloadFile

#### Verification Approach

Verified through direct unit tests in `SpeechModelDownloadFileTests.cs` proving two behaviors: (1) the
constructor's eager validation of URI scheme, checksum format, relative install path, and null
arguments; and (2) staged-path resolution - that `ResolveStagedPath` combines a staging directory with the
file's install path using the current platform's directory separator regardless of which separator the path was
declared with, and rejects a null directory.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when valid values are exposed unchanged, a non-HTTPS URI is rejected, a malformed
checksum is rejected, an empty/whitespace, parent-escaping (`..`), current-directory (`.`), or
separators-only (no named segment) install path is rejected, a rooted install path is rejected,
any null argument is rejected, and the instance `ResolveStagedPath` method combines a staging
directory with this already-validated instance's install path segments using this platform's own
directory separator regardless of which separator the install path was declared with.

#### Test Scenarios

##### Constructor: Valid Values Exposes Values

**Test**: `SpeechModelDownloadFile_Constructor_ValidValues_ExposesValues`

##### Constructor: HTTP URI Throws ArgumentException

**Test**: `SpeechModelDownloadFile_Constructor_HttpUri_ThrowsArgumentException`

##### Constructor: Relative URI Throws ArgumentException

**Test**: `SpeechModelDownloadFile_Constructor_RelativeUri_ThrowsArgumentException`

##### Constructor: Invalid Checksum Throws ArgumentException

**Test**: `SpeechModelDownloadFile_Constructor_InvalidChecksum_ThrowsArgumentException`

##### Constructor: Invalid Install Path Throws ArgumentException

**Test**: `SpeechModelDownloadFile_Constructor_InvalidInstallPath_ThrowsArgumentException`

The cases include separator-only paths (`/`, `//` and two backslashes), which name no segment at all.

##### Constructor: Rooted Install Path Throws ArgumentException

**Test**: `SpeechModelDownloadFile_Constructor_RootedInstallPath_ThrowsArgumentException`

##### Constructor: Null Arguments Throws ArgumentNullException

**Test**: `SpeechModelDownloadFile_Constructor_NullArguments_ThrowsArgumentNullException`

##### ResolveStagedPath: Any Separator Matches Downloader Staging

**Test**: `SpeechModelDownloadFile_ResolveStagedPath_AnySeparator_MatchesDownloaderStaging`

Staged-path resolution scenario: files declared with forward-slash and with backslash separators each resolve,
for a given staging directory, to the platform's `Path.Combine` form with the platform separator (the same
form the downloader uses to stage the file, as both call this method).

##### ResolveStagedPath: Null Argument Throws ArgumentNullException

**Test**: `SpeechModelDownloadFile_ResolveStagedPath_NullArgument_ThrowsArgumentNullException`

##### Requirement Coverage

| Requirement | Tests |
| :--- | :--- |
| `...DownloadFile-UriValidation` | `..._ValidValues_ExposesValues`, `..._HttpUri_...`, `..._RelativeUri_...` |
| `...DownloadFile-ChecksumValidation` | `..._Constructor_InvalidChecksum_ThrowsArgumentException` |
| `...DownloadFile-InstallPathValidation` | `..._InvalidInstallPath_Throws...`, `..._RootedInstallPath_Throws...` |
| `...DownloadFile-NullArgumentValidation` | `..._Constructor_NullArguments_ThrowsArgumentNullException` |
| `...DownloadFile-ResolveStagedPath` | `..._ResolveStagedPath_AnySeparator_MatchesDownloaderStaging` |
| `...DownloadFile-ResolveStagedPathNullArgument` | `..._ResolveStagedPath_NullArgument_ThrowsArgumentNullException` |
