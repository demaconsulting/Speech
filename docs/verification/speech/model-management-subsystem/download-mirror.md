### DownloadMirror

#### Verification Approach

Verified through direct unit tests in `DownloadMirrorTests.cs` proving the constructor's eager
validation of scheme, absoluteness, and Credentials/BearerToken mutual exclusivity.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when valid values (with either auth mechanism, or neither) are exposed unchanged, an
`http` or `https` scheme is accepted, a non-absolute or non-`http`/`https` `BaseUri` is rejected,
supplying both `Credentials` and `BearerToken` is rejected, and a null `BaseUri` is rejected.

#### Test Scenarios

##### Constructor: Base URI Only Exposes Values With Null Auth

**Test**: `DownloadMirror_Constructor_BaseUriOnly_ExposesValuesWithNullAuth`

##### Constructor: Http Or Https Scheme Succeeds

**Test**: `DownloadMirror_Constructor_HttpOrHttpsScheme_Succeeds`

##### Constructor: Credentials Only Exposes Credentials

**Test**: `DownloadMirror_Constructor_CredentialsOnly_ExposesCredentials`

##### Constructor: Bearer Token Only Exposes Bearer Token

**Test**: `DownloadMirror_Constructor_BearerTokenOnly_ExposesBearerToken`

##### Constructor: Both Credentials And Bearer Token Throws ArgumentException

**Test**: `DownloadMirror_Constructor_BothCredentialsAndBearerToken_ThrowsArgumentException`

##### Constructor: Null Base URI Throws ArgumentNullException

**Test**: `DownloadMirror_Constructor_NullBaseUri_ThrowsArgumentNullException`

##### Constructor: Relative Base URI Throws ArgumentException

**Test**: `DownloadMirror_Constructor_RelativeBaseUri_ThrowsArgumentException`

##### Constructor: Non-Http Scheme Throws ArgumentException Naming Scheme

**Test**: `DownloadMirror_Constructor_NonHttpScheme_ThrowsArgumentExceptionNamingScheme`
