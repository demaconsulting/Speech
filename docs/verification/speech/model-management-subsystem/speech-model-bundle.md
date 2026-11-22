### SpeechModelBundle

#### Verification Approach

Unit-tested in `SpeechModelBundleTests.cs`. Construction and descriptor rules are tested directly with literal
URIs, checksums and vendor paths (a placeholder zip checksum is enough, since construction never fetches anything).
The multi-bundle install test uses two real synthetic bundle zips built in memory by `Fakes/ModelBundleTestZips.cs`
and staged in a temporary directory, so `SpeechModelBundle.InstallAsync` runs the real `SpeechModelBundleInstaller`
(its documented dependency) end to end. No network access is used.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **File system**: a temporary directory under the system temp path, created per test and deleted afterward

#### Acceptance Criteria

Tests pass when the models-release base URI is the project's `models` release; a valid bundle exposes its URI,
checksum, file name, name, contents and a download file named by the bundle file name; the planned bundle names are
accepted and every name breaking the naming rule, a non-HTTPS URI, a query or fragment, a malformed checksum, a
null argument and every reserved, colliding or non-portable vendor path are rejected; the descriptor lists each
bundle's zip in order and rejects colliding, duplicate-name, empty and null bundle sets; and a two-bundle install
extracts both bundles side by side with one license folder per bundle.

#### Test Scenarios

##### Models Release Location

**Test**: `SpeechModelBundle_ModelsReleaseBaseUri_IsProjectModelsRelease`

##### Valid Declaration

**Test**: `SpeechModelBundle_Constructor_ValidArguments_ExposesNameFileNameAndDownloadFile`

##### Naming Rule

**Test**: `SpeechModelBundle_Constructor_FileNameFollowsNamingRule_IsAccepted` (the Kokoro, Nemotron and Chatterbox
bundle names, and a two-digit revision)

**Test**: `SpeechModelBundle_Constructor_FileNameBreaksNamingRule_ThrowsArgumentException` (uppercase, an unknown
quantization token, a missing, zero or leading-zero revision, another extension, an empty token, an underscore)

##### Invalid Arguments

**Test**: `SpeechModelBundle_Constructor_NonHttpsOrQueryUri_ThrowsArgumentException`

**Test**: `SpeechModelBundle_Constructor_InvalidChecksum_ThrowsArgumentException`

**Test**: `SpeechModelBundle_Constructor_NullArgument_ThrowsArgumentNullException`

##### Reserved Or Colliding Vendor Paths

**Test**: `SpeechModelBundle_Constructor_ReservedOrCollidingContentsPath_ThrowsArgumentException` (a path under
`licenses/` in either case, `model.json`, `notice`, the zip's own name, a case-only duplicate, a file that is
another file's parent directory, and a reserved device name)

**Test**: `SpeechModelBundle_Constructor_ContentsPathWithColon_ThrowsArgumentException` (a colon inside a segment,
an alternate-data-stream style suffix and a trailing colon, rejected on every platform by the installer's shared
entry-name check)

##### Download Descriptor

**Test**: `SpeechModelBundle_CreateDescriptor_TwoBundles_ListsEachZip`

**Test**: `SpeechModelBundle_CreateDescriptor_CollidingBundles_ThrowsArgumentException` (vendor paths equal ignoring
case, a repeated bundle name, a zip name equal to another bundle's vendor path)

**Test**: `SpeechModelBundle_CreateDescriptor_NullOrEmptyBundles_Throws`

##### Multi-Bundle Install

**Test**: `SpeechModelBundle_InstallAsync_TwoBundles_InstallsBothWithSeparateLicenseFolders`

**Test**: `SpeechModelBundle_InstallAsync_EmptyStagingDirectory_ThrowsArgumentException`
