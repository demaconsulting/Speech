## AppLaunchOptions Verification

### Verification Approach

`AppLaunchOptions` is verified through deterministic unit tests that call `Parse`, the option
translation methods and `PrintHelp` directly. Folder-existence checks use scratch directories under
the test output folder.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test parses its own argument array; voices folder tests create and remove
  their own scratch directory

### Acceptance Criteria

The unit passes when: defaults and every option parse onto the properties; bad arguments are
rejected with `ArgumentException`; the store and mirror options reflect the parsed values; and
`--voices-dir` is accepted only for an existing folder and is listed in the help text.

### Test Scenarios

#### Parse_NoArguments_ReturnsAllDefaults

**Scenario**: No arguments are supplied.

**Expected**: Every option is at its default.

#### Parse_HelpFlag_SetsHelp

**Scenario**: A help flag is supplied.

**Expected**: `Help` is set.

#### Parse_AllOptions_ParsesOntoProperties

**Scenario**: Every option is supplied.

**Expected**: Each value lands on its property.

#### Parse_UnrecognizedArgument_ThrowsArgumentException

**Scenario**: An unknown argument is supplied.

**Expected**: `ArgumentException`.

#### Parse_OptionMissingValue_ThrowsArgumentException

**Scenario**: An option is the last argument with no value.

**Expected**: `ArgumentException`.

#### Parse_NullArguments_ThrowsArgumentNullException

**Scenario**: Null arguments are supplied.

**Expected**: `ArgumentNullException`.

#### CreateStoreOptions_NoModelsDir_ReturnsNull

**Scenario**: No models folder was given.

**Expected**: `null`.

#### CreateStoreOptions_WithModelsDir_ReturnsMatchingRootPathOverride

**Scenario**: A models folder was given.

**Expected**: Store options whose root path override matches it.

#### CreateDownloaderOptions_NoMirrorUrl_ReturnsNull

**Scenario**: No mirror URL was given.

**Expected**: `null`.

#### CreateDownloaderOptions_ValidCredentialedMirror_ReturnsConfiguredMirror

**Scenario**: A mirror URL with a user and password was given.

**Expected**: Downloader options carrying the configured mirror.

#### CreateDownloaderOptions_BearerTokenMirror_ReturnsConfiguredMirror

**Scenario**: A mirror URL with a bearer token was given.

**Expected**: Downloader options carrying the configured mirror.

#### CreateDownloaderOptions_InvalidMirrorUrl_ThrowsArgumentException

**Scenario**: The mirror URL is not a valid absolute URI.

**Expected**: `ArgumentException`.

#### CreateDownloaderOptions_MirrorUserWithoutPassword_ThrowsArgumentException

**Scenario**: A mirror user is given without a password.

**Expected**: `ArgumentException`.

#### Parse_VoicesDir_ExistingDirectory_SetsVoicesDir

**Scenario**: `--voices-dir` names an existing folder.

**Expected**: `VoicesDir` is set.

#### Parse_VoicesDir_MissingDirectory_ThrowsArgumentException

**Scenario**: `--voices-dir` names a folder that does not exist.

**Expected**: `ArgumentException`.

#### Parse_VoicesDir_NoValue_ThrowsArgumentException

**Scenario**: `--voices-dir` has no value.

**Expected**: `ArgumentException`.

#### PrintHelp_ListsVoicesDirOption

**Scenario**: The help text is printed.

**Expected**: It lists the `--voices-dir` option.

#### Parse_EnvironmentMirrorUrl_UsedWhenOptionAbsent

**Scenario**: `DEMASPEECH_MIRROR_URL` is set (fake reader) and `--mirror-url` is absent.

**Expected**: `MirrorUrl` is the variable's value.

#### Parse_MirrorUrlOption_OverridesEnvironment

**Scenario**: Both `--mirror-url` and `DEMASPEECH_MIRROR_URL` are given.

**Expected**: `MirrorUrl` is the option's value.

#### Parse_EnvironmentBasicCredentials_UsedWhenOptionsAbsent

**Scenario**: The user and password variables are set and no credential option is given.

**Expected**: The configured mirror carries the Basic credentials.

#### Parse_EnvironmentBearerToken_UsedWhenOptionsAbsent

**Scenario**: The bearer token variable is set and no credential option is given.

**Expected**: The configured mirror carries the bearer token.

#### Parse_CredentialOption_DisablesEnvironmentCredentials

**Scenario**: `--mirror-bearer-token` is given while the user and password variables are set.

**Expected**: Only the option's bearer token is used; no Basic credential is applied.

#### Parse_EmptyEnvironmentVariables_TreatedAsUnset

**Scenario**: The mirror variables are set to empty strings.

**Expected**: No downloader options are produced.

#### CreateDownloaderOptions_EnvironmentCredentialWithoutUrl_ThrowsWithoutEchoingSecret

**Scenario**: Only a credential variable is set.

**Expected**: `ArgumentException` whose message does not contain the secret value.

#### CreateDownloaderOptions_InvalidMirrorUrl_ErrorDoesNotEchoValue

**Scenario**: `--mirror-url` is not a valid URL.

**Expected**: `ArgumentException` whose message does not contain the rejected value.

#### PrintHelp_ListsMirrorEnvironmentVariables

**Scenario**: The help text is printed.

**Expected**: It names the four `DEMASPEECH_MIRROR_*` variables.
