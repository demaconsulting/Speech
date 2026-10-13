### DownloadMirror

#### Verification Approach

Verified through direct unit tests in `DownloadMirrorTests.cs` proving the constructor's eager
validation and the record's disclosure behavior:

- base-URI scheme (`http`/`https` only), absoluteness, and null rejection;
- rejection of a query string, fragment, or embedded user-info component in the base URI;
- Credentials/BearerToken mutual exclusivity and exposure of each valid configuration unchanged;
- rejection of an empty or whitespace-only bearer token;
- rejection of credentialed plain-`http` mirrors, and the loopback-host exemption from that rule
  (`localhost`, `127.0.0.1`, `::1`);
- `ToString` redaction of the bearer-token value.

Every unit requirement (`Speech-Models-DownloadMirror-*`) is linked to the tests named below in
`download-mirror.yaml`. Preemptive-Basic application of `Credentials` is verified at the
`HttpModelDownloadClient` unit, not here.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when valid values (with either auth mechanism, or neither) are exposed unchanged, an
`http` or `https` scheme is accepted, a non-absolute or non-`http`/`https` `BaseUri` is rejected,
a `BaseUri` carrying a query string or fragment is rejected, supplying both `Credentials` and
`BearerToken` is rejected, an empty or whitespace-only `BearerToken` is rejected, supplying either
auth mechanism alongside a plain `http` `BaseUri` is rejected unless `BaseUri`'s host is a
loopback address (`localhost` or a loopback IP literal such as `127.0.0.1` or `::1`), in which
case it is accepted, a null `BaseUri` is rejected, a `BaseUri` with embedded user-info is
rejected, and `ToString` never reveals the bearer-token value.

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

##### Constructor: Query Or Fragment In Base URI Throws ArgumentException

**Test**: `DownloadMirror_Constructor_QueryOrFragmentInBaseUri_ThrowsArgumentException`

##### Constructor: Credentials With Http Base URI Throws ArgumentException

**Test**: `DownloadMirror_Constructor_CredentialsWithHttpBaseUri_ThrowsArgumentException`

##### Constructor: Bearer Token With Http Base URI Throws ArgumentException

**Test**: `DownloadMirror_Constructor_BearerTokenWithHttpBaseUri_ThrowsArgumentException`

##### Constructor: Credentials With Loopback Http Base URI Does Not Throw

**Test**: `DownloadMirror_Constructor_CredentialsWithLoopbackHttpBaseUri_DoesNotThrow`

##### Constructor: Bearer Token With Loopback Http Base URI Does Not Throw

**Test**: `DownloadMirror_Constructor_BearerTokenWithLoopbackHttpBaseUri_DoesNotThrow`

##### Constructor: Whitespace-Only BearerToken Throws ArgumentException

**Test**: `DownloadMirror_Constructor_WhitespaceOnlyBearerToken_ThrowsArgumentException`

##### Constructor: User Info In Base URI Throws ArgumentException

**Test**: `DownloadMirror_Constructor_UserInfoInBaseUri_ThrowsArgumentException`

Verifies that credentials embedded in the base URI's user-info component are rejected, so
secrets can only be supplied through the dedicated auth properties.

##### ToString: Bearer Token Is Redacted

**Tests**: `DownloadMirror_ToString_WithBearerToken_RedactsTokenValue`,
`DownloadMirror_ToString_WithoutBearerToken_PrintsNull`

Verifies that the textual representation never discloses the token value and prints null when no
token is configured.

#### Requirement Coverage

| Requirement | Tests |
| :---------- | :---- |
| `SchemeValidation` | Http/Https scheme accepted; relative base URI; non-http scheme |
| `ValidConfigurationAccepted` | Base URI only; Credentials only; BearerToken only |
| `MutualExclusivity` | Both Credentials and BearerToken |
| `NullArgumentValidation` | Null base URI |
| `QueryFragmentValidation` | Query or fragment in base URI |
| `UserInfoRejection` | User info in base URI |
| `CredentialedHttpRejection` | Credentials / BearerToken with non-loopback `http` base URI |
| `CredentialedLoopbackHttpExemption` | Credentials / BearerToken with loopback `http` base URI |
| `BearerTokenWhitespaceValidation` | Empty or whitespace-only BearerToken |
| `BearerTokenRedaction` | `ToString` with and without a bearer token |
