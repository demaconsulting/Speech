## MirrorOptionsFactory Verification

### Verification Approach

`MirrorOptionsFactory` has no test class of its own. Its rules are verified through its two callers:
the `AppLaunchOptions.CreateDownloaderOptions_*` unit tests and the
`ModelCatalogViewModel_ApplyMirror_*` panel tests. Every rule in the factory is reached through at
least one of these tests; direct tests are not needed because the factory is a pure function.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test supplies its own inputs; the factory holds no state

### Acceptance Criteria

The unit passes when a valid mirror builds a configured mirror, and an invalid URL, a user without
a password, or a credential or token without a URL is rejected with an explained error through both
the launch options and the panel.

### Test Scenarios

#### CreateDownloaderOptions_ValidCredentialedMirror_ReturnsConfiguredMirror

**Scenario**: A valid URL with user and password reaches the factory through the launch options.

**Expected**: A configured mirror is returned.

#### CreateDownloaderOptions_InvalidMirrorUrl_ThrowsArgumentException

**Scenario**: An invalid URL reaches the factory.

**Expected**: `ArgumentException`.

#### CreateDownloaderOptions_MirrorUserWithoutPassword_ThrowsArgumentException

**Scenario**: A user without a password reaches the factory.

**Expected**: `ArgumentException`.

#### CreateDownloaderOptions_MirrorCredentialsWithoutUrl_ThrowsArgumentException

**Scenario**: Credentials with a blank URL reach the factory.

**Expected**: `ArgumentException`, so the credentials are not silently discarded.

#### CreateDownloaderOptions_MirrorBearerTokenWithoutUrl_ThrowsArgumentException

**Scenario**: A bearer token with a blank URL reaches the factory.

**Expected**: `ArgumentException`.

#### ModelCatalogViewModel_ApplyMirror_InvalidUrl_ReportsErrorWithoutCallingService

**Scenario**: The panel applies an invalid URL.

**Expected**: An explained error is shown and the service is not called.

#### ModelCatalogViewModel_ApplyMirror_CredentialsWithoutUrl_ReportsErrorWithoutCallingService

**Scenario**: The panel applies a credential with a blank URL.

**Expected**: An explained error is shown and the service is not called.
