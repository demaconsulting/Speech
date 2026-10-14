## MirrorOptionsFactory Verification

### Verification Approach

`MirrorOptionsFactory` is verified directly by `MirrorOptionsFactoryTests`, which call `Create` with
raw strings and assert on the returned `DownloadMirror?` or the thrown `ArgumentException`. The
`AppLaunchOptions.CreateDownloaderOptions_*` and `ModelCatalogViewModel_ApplyMirrorAsync_*` tests
additionally verify that each caller presents the factory's rules, and the panel tests are linked as
supporting evidence for the URL and credential rejection requirements.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test supplies its own inputs; the factory holds no state

### Acceptance Criteria

The unit passes when: a URL with Basic credentials, a bearer token, or no credentials builds the
matching mirror; a blank (null, empty, or whitespace-only) URL with no credentials returns `null`;
whitespace-only credential fields count as not supplied; a credential without a URL, a user
without a password, a password without a user, an invalid URL, and a credential for a plain-HTTP
remote host are each rejected with `ArgumentException`; and the messages do not echo a secret or an
invalid URL value.

### Test Scenarios

#### MirrorOptionsFactory_Create_UrlWithBasicCredentials_ReturnsConfiguredMirror

**Scenario**: A valid URL with a user and password.

**Expected**: A mirror carrying the base URI and Basic credentials.

#### MirrorOptionsFactory_Create_UrlWithBearerToken_ReturnsConfiguredMirror

**Scenario**: A valid URL with a bearer token.

**Expected**: A mirror carrying the base URI and the token.

#### MirrorOptionsFactory_Create_UrlOnly_ReturnsUnauthenticatedMirror

**Scenario**: A valid URL with no credentials.

**Expected**: A mirror with no credentials.

#### MirrorOptionsFactory_Create_BlankUrlNoCredentials_ReturnsNull

**Scenario**: A null, empty, or whitespace-only URL and no credentials (boundary).

**Expected**: `null`.

#### MirrorOptionsFactory_Create_AllWhitespace_ReturnsNull

**Scenario**: The URL and every credential field are whitespace-only (boundary).

**Expected**: `null`.

#### MirrorOptionsFactory_Create_UrlWithWhitespaceCredentials_ReturnsUnauthenticatedMirror

**Scenario**: A valid URL with whitespace-only user, password, and token (regression for whitespace
being treated as a supplied credential).

**Expected**: An unauthenticated mirror: no credentials and no bearer token.

#### MirrorOptionsFactory_Create_WhitespaceUserWithPassword_ThrowsArgumentException

**Scenario**: A whitespace-only user and a real password.

**Expected**: `ArgumentException`: the pair is incomplete.

#### MirrorOptionsFactory_Create_WhitespaceTokenWithBasicCredentials_ReturnsBasicMirror

**Scenario**: Basic credentials and a whitespace-only bearer token.

**Expected**: A mirror carrying only the Basic credentials.

#### MirrorOptionsFactory_Create_WhitespaceUrlWithCredential_ThrowsCredentialWithoutUrl

**Scenario**: A whitespace-only URL with a bearer token.

**Expected**: `ArgumentException` stating a mirror URL is required, without the token.

#### MirrorOptionsFactory_Create_CredentialWithoutUrl_ThrowsWithoutEchoingSecret

**Scenario**: A credential or token with a null URL.

**Expected**: `ArgumentException` whose message does not contain the secret.

#### MirrorOptionsFactory_Create_UserWithoutPassword_ThrowsArgumentException

**Scenario**: A user without a password.

**Expected**: `ArgumentException`.

#### MirrorOptionsFactory_Create_PasswordWithoutUser_ThrowsArgumentException

**Scenario**: A password without a user.

**Expected**: `ArgumentException`.

#### MirrorOptionsFactory_Create_InvalidUrl_ThrowsWithoutEchoingValue

**Scenario**: A URL that is not a valid absolute URI.

**Expected**: `ArgumentException` whose message does not contain the rejected value.

#### MirrorOptionsFactory_Create_CredentialOverPlainHttpToRemoteHost_ThrowsArgumentException

**Scenario**: A credential with an `http://` URL on a non-loopback host.

**Expected**: `ArgumentException`.

#### ModelCatalogViewModel_ApplyMirrorAsync_InvalidUrl_ReportsErrorWithoutCallingService

**Scenario**: The panel applies an invalid URL.

**Expected**: An explained error is shown and the service is not called.

#### MirrorOptionsFactory_Create_BasicCredentialsOverPlainHttpToRemoteHost_ThrowsWithoutEchoingSecret

**Scenario**: Basic credentials are given for a plain-HTTP remote host.

**Expected**: `ArgumentException` that does not contain the password.

#### MirrorOptionsFactory_Create_CredentialOverPlainHttpToLoopback_ReturnsMirror

**Scenario**: A credential is given for a plain-HTTP loopback host.

**Expected**: A configured mirror is returned.

#### MirrorOptionsFactory_Create_BasicCredentialsAndBearerToken_ThrowsWithoutEchoingSecrets

**Scenario**: Basic credentials and a bearer token are both given.

**Expected**: `ArgumentException` that contains neither secret.

#### MirrorOptionsFactory_Create_NonHttpScheme_ThrowsWithoutEchoingValue

**Scenario**: The URL uses `ftp://` or is a pasted `user:token@host` string.

**Expected**: `ArgumentException` that does not contain the value.

#### MirrorOptionsFactory_Create_RelativeUrl_ThrowsArgumentException

**Scenario**: The URL is relative.

**Expected**: `ArgumentException`.

#### MirrorOptionsFactory_Create_BlankUrlWithEmptyCredentials_ReturnsNull

**Scenario**: The URL is blank and the credentials are empty or null.

**Expected**: `null` is returned.

#### MirrorOptionsFactory_Create_UrlWithEmptyCredentials_ReturnsUnauthenticatedMirror

**Scenario**: A valid URL is given with empty credentials.

**Expected**: An unauthenticated mirror is returned.

#### MirrorOptionsFactory_Create_EmptyUserWithPassword_ThrowsArgumentException

**Scenario**: The user is empty and a password is given.

**Expected**: `ArgumentException`.

#### ModelCatalogViewModel_ApplyMirrorAsync_CredentialsWithoutUrl_ReportsErrorWithoutCallingService

**Scenario**: The panel applies a credential with a blank URL.

**Expected**: An explained error is shown and the service is not called.
