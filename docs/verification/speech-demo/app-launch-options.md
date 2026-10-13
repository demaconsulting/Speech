## AppLaunchOptions Verification

### Verification Approach

`AppLaunchOptions` is verified through deterministic unit tests that call `Parse`, the option
translation methods and `PrintHelp(TextWriter)` directly. Every parse test uses an injected fake
environment reader, so results never depend on the ambient `DEMASPEECH_MIRROR_*` variables.
Folder-existence checks use scratch directories under the test output folder.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test parses its own argument array; voices folder tests create and remove
  their own scratch directory; the single test of the arguments-only overload clears and restores
  the mirror environment variables and, like the console-redirecting test, runs in the non-parallel
  process-wide-state collection

### Acceptance Criteria

The unit passes when: defaults and every option parse onto their settings; bad arguments are
rejected with `ArgumentException`; every value-taking option followed by another recognized
option, or by nothing, is rejected as missing its value while a hyphen-leading non-option value is
accepted; the store and mirror options reflect the parsed values; a mirror user without a password
and a mirror password without a user are each rejected; and `--voices-dir` is accepted only for an
existing folder and is listed in the help text.

### Test Scenarios

#### Parse_NoArguments_ReturnsAllDefaults

**Scenario**: No arguments are supplied.

**Expected**: Every option is at its default.

#### Parse_HelpFlag_SetsHelp

**Scenario**: Each help flag (`--help`, `-h` and `-?`) is supplied.

**Expected**: `Help` is set.

#### Parse_AllOptions_ParsesOntoProperties

**Scenario**: Every option, including `--voices-dir`, is supplied.

**Expected**: Each value lands on its setting.

#### Parse_UnrecognizedArgument_ThrowsArgumentException

**Scenario**: An unknown argument is supplied.

**Expected**: `ArgumentException`.

#### Parse_OptionMissingValue_ThrowsArgumentException

**Scenario**: Each of the six value-taking options is the last argument with no value (error
scenario).

**Expected**: `ArgumentException` stating that the option requires a value.

#### Parse_ValueOptionFollowedByRecognizedOption_ThrowsArgumentException

**Scenario**: Each value-taking option is followed by `--help`, and several are followed by `-h`,
`-?`, or another value-taking option (regression for an option being swallowed as a value, such as
`--mirror-url --help`).

**Expected**: `ArgumentException` stating that the first option requires a value.

#### Parse_ValueOptionFollowedByLongFormOption_ThrowsArgumentException

**Scenario**: A value-taking option is followed by a recognized option in `--option=value` form.

**Expected**: `ArgumentException` naming the first option and not containing the following value.

#### Parse_ValueOptionFollowedByNonOptionHyphenValue_AcceptsValue

**Scenario**: A value-taking option is followed by `-models`, `-alice`, `-1`, `--not-an-option`, or
`-` (boundary: hyphen-leading values that are not recognized options).

**Expected**: The value is accepted onto the setting.

#### Parse_NullArguments_ThrowsArgumentNullException

**Scenario**: Null arguments are supplied.

**Expected**: `ArgumentNullException`.

#### Parse_NullEnvironmentReader_ThrowsArgumentNullException

**Scenario**: A null environment reader is supplied.

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

#### CreateDownloaderOptions_MirrorPasswordWithoutUser_ThrowsArgumentException

**Scenario**: A mirror password is given without a user.

**Expected**: `ArgumentException`.

#### CreateDownloaderOptions_MirrorCredentialsWithoutUrl_ThrowsArgumentException

**Scenario**: A mirror user and password are given without a URL.

**Expected**: `ArgumentException`.

#### CreateDownloaderOptions_MirrorBearerTokenWithoutUrl_ThrowsArgumentException

**Scenario**: A mirror bearer token is given without a URL.

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

#### PrintHelp_ListsHelpFlagsAndBearerTokenOption

**Scenario**: The help text is written to a `StringWriter`.

**Expected**: It lists `--help`, `-h`, `-?` and `--mirror-bearer-token`.

#### PrintHelp_NullWriter_ThrowsArgumentNullException

**Scenario**: A null writer is supplied.

**Expected**: `ArgumentNullException`.

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

#### Parse_UnrecognizedArgument_ErrorDoesNotEchoArgument

**Scenario**: A stray secret-looking argument is the third argument.

**Expected**: `ArgumentException` naming position 3 and not containing the argument text.

#### Parse_HelpWithMissingVoicesDir_StillReportsHelp

**Scenario**: `--help` is given beside a `--voices-dir` naming a missing folder.

**Expected**: Help is reported and no exception is thrown.

#### Parse_RepeatedOption_LastValueWins

**Scenario**: An option is given twice.

**Expected**: The last value is used.

#### Parse_ArgumentsOnlyOverload_ParsesHelpFlag

**Scenario**: The arguments-only `Parse` overload receives `--help` while the ambient
`DEMASPEECH_MIRROR_*` variables are cleared (and restored afterward) by the test.

**Expected**: Help is reported and no mirror setting is picked up from the environment.

#### Parse_EmptyMirrorUrlOption_FallsBackToEnvironment

**Scenario**: `--mirror-url ""` is given while the URL variable is set.

**Expected**: The environment URL is used.

#### Parse_EmptyCredentialOption_DoesNotDisableEnvironmentCredentials

**Scenario**: An empty credential option is given while credential variables are set.

**Expected**: The environment credentials still apply.

#### CreateDownloaderOptions_CommandLineUserWithEnvironmentPassword_IsRejectedNotMixed

**Scenario**: `--mirror-user` is given and only the password variable is set.

**Expected**: `ArgumentException`; the two sources are not combined.

#### CreateDownloaderOptions_EnvironmentUserWithoutPassword_ThrowsWithoutEchoingUser

**Scenario**: Only the user variable is set beside a mirror URL.

**Expected**: `ArgumentException` that does not contain the user name.

#### CreateDownloaderOptions_BasicCredentialsAndBearerToken_ThrowsWithoutEchoingSecrets

**Scenario**: Basic credentials and a bearer token are both supplied.

**Expected**: `ArgumentException` that contains neither secret.

#### CreateDownloaderOptions_BasicCredentialsOverPlainHttp_ThrowsWithoutEchoingPassword

**Scenario**: Basic credentials are supplied for a plain-HTTP remote mirror.

**Expected**: `ArgumentException` that does not contain the password.
