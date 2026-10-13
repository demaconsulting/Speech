## Context Verification

### Verification Approach

`Context` is verified with unit tests that call `Context.Create` with argument lists and, for the
environment-variable behavior, an injected fake environment reader, then inspect the resulting
properties or the exception thrown. The process environment is never read or modified. Output
behavior (`--silent`, `--log`) is observable only through the console and a file, so it is verified
by out-of-process integration tests that run the built tool. `WriteError`'s color handling is
verified with a stand-in error writer that throws.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test builds its own context; tests that need a directory create and remove a
  uniquely named temporary one; tests that replace the console error writer restore it afterward and
  the class runs in the sequential collection
- **Test doubles**: A fake environment reader function; a text writer that throws on every write

### Acceptance Criteria

The unit passes when every global option is recognized before or after the subcommand and the
remaining arguments are collected for the subcommand; invalid values and missing values are
rejected with messages that have no parameter-name suffix; mirror options and environment variables
resolve with the documented precedence and validation without echoing a secret; `--silent` and
`--log` control console and file output; and `WriteError` restores the console color when its write
fails.

### Test Scenarios

#### Context_Create_ModelsDirAfterCommand_IsRecognizedAsGlobalOption

**Scenario**: `list-models --models-dir /tmp/models --format json` is parsed.

**Expected**: The command is `list-models`, `ModelsDir` is `/tmp/models`, and `CommandArgs` is only
`--format json`.

#### Context_Create_WithExistingVoicesDir_ParsesOntoContext

**Scenario**: `list-voices --voices-dir <existing folder>` is parsed.

**Expected**: `VoicesDir` carries the folder and `CommandArgs` is empty.

#### Context_Create_WithBadVoicesDir_ThrowsArgumentException

**Scenario**: `--voices-dir` names a missing folder, and separately is given with no value.

**Expected**: `ArgumentException` in both cases.

#### Context_Create_WithMirrorOptions_ParsesOntoContext

**Scenario**: `--mirror-url`, `--mirror-user` and `--mirror-password` follow the subcommand.

**Expected**: The three values are on the context and the bearer token is null.

#### Context_CreateMirror_NoMirrorUrl_ReturnsNull

**Scenario**: `CreateMirror` is called on a context with no mirror settings.

**Expected**: `null`.

#### Context_CreateMirror_ValidCredentialedMirror_ReturnsConfiguredMirror

**Scenario**: A valid HTTPS mirror URL with a user and password.

**Expected**: A mirror carrying the base URI and the Basic credentials, with no bearer token.

#### Context_CreateMirror_BearerTokenMirror_ReturnsConfiguredMirror

**Scenario**: A valid HTTPS mirror URL with a bearer token only.

**Expected**: A mirror carrying the token and no Basic credentials.

#### Context_CreateMirror_InvalidMirrorUrl_ThrowsArgumentException

**Scenario**: `--mirror-url "not a url"`.

**Expected**: `ArgumentException` while creating the context.

#### Context_CreateMirror_UnsupportedMirrorUrlShape_ThrowsArgumentException

**Scenario**: Four mirror URLs: with a query string, with a fragment, with user-info, and with an
`ftp` scheme.

**Expected**: `ArgumentException` for each (the rule is enforced by the library's mirror type that
`Context` forwards to).

#### Context_CreateMirror_WhitespaceBearerToken_ThrowsArgumentException

**Scenario**: A valid mirror URL with a whitespace-only bearer token.

**Expected**: `ArgumentException`.

#### Context_CreateMirror_CredentialOverPlainHttp_RejectedUnlessLoopback

**Scenario**: Basic credentials, then a bearer token, each with a non-loopback `http` mirror; then a
bearer token with a loopback `http` mirror.

**Expected**: `ArgumentException` for the first two; the loopback mirror is accepted.

#### Context_Create_UnsupportedArgument_MessageHasNoParameterSuffix

**Scenario**: `bogus-command` is the first non-global argument.

**Expected**: `ArgumentException` with the exact message `Unsupported argument 'bogus-command'` and no
parameter name.

#### Context_Create_OptionMissingValue_MessageHasNoParameterSuffix

**Scenario**: `--models-dir` is the last argument.

**Expected**: `ArgumentException` whose message contains no `(Parameter` text and has no parameter
name.

#### Context_WriteError_ErrorWriterThrows_RestoresForegroundColor

**Scenario**: The error writer throws on write and `WriteError` is called.

**Expected**: The `IOException` propagates and the console foreground color equals its prior value.

#### Context_Create_MirrorCredentialWithoutUrlAndHelp_ThrowsArgumentException

**Scenario**: `--help --mirror-user alice`.

**Expected**: `ArgumentException`: mirror options are validated even when no catalog command runs.

#### Context_CreateMirror_MirrorUserWithoutPassword_ThrowsArgumentException

**Scenario**: A valid mirror URL with a user and no password.

**Expected**: `ArgumentException`.

#### Context_CreateMirror_BasicCredentialsAndBearerTokenTogether_ThrowsArgumentException

**Scenario**: A valid mirror URL with a user, password and bearer token.

**Expected**: `ArgumentException`.

#### Context_CreateMirror_CredentialsWithoutMirrorUrl_ThrowsArgumentException

**Scenario**: User and password with no mirror URL.

**Expected**: `ArgumentException`.

#### Context_CreateMirror_BearerTokenWithoutMirrorUrl_ThrowsArgumentException

**Scenario**: A bearer token with no mirror URL.

**Expected**: `ArgumentException`.

#### Context_Create_EnvironmentMirrorUrl_UsedWhenOptionAbsent

**Scenario**: `DEMASPEECH_MIRROR_URL` is set and `--mirror-url` is absent.

**Expected**: `MirrorUrl` is the variable's value.

#### Context_Create_MirrorUrlOption_OverridesEnvironment

**Scenario**: Both `--mirror-url` and `DEMASPEECH_MIRROR_URL` are present.

**Expected**: `MirrorUrl` is the option's value.

#### Context_Create_EnvironmentBasicCredentials_UsedWhenOptionsAbsent

**Scenario**: The URL, user and password variables are set and no credential option is given.

**Expected**: The mirror carries the variables' Basic credentials.

#### Context_Create_EnvironmentBearerToken_UsedWhenOptionsAbsent

**Scenario**: The URL and bearer-token variables are set and no credential option is given.

**Expected**: The mirror carries the variable's token.

#### Context_Create_CredentialOption_DisablesEnvironmentCredentials

**Scenario**: `--mirror-bearer-token` is given while user and password variables are set.

**Expected**: The mirror carries the option's token and no Basic credentials.

#### Context_Create_EmptyEnvironmentVariables_TreatedAsUnset

**Scenario**: The URL, user and bearer-token variables are all empty.

**Expected**: `CreateMirror` returns `null`.

#### Context_Create_EnvironmentCredentialWithoutUrl_ThrowsWithoutEchoingSecret

**Scenario**: Only `DEMASPEECH_MIRROR_BEARER_TOKEN` is set.

**Expected**: `ArgumentException` whose message does not contain the token.

#### Context_Create_InvalidMirrorUrl_ErrorDoesNotEchoValue

**Scenario**: `DEMASPEECH_MIRROR_URL` holds an invalid value containing a marker string.

**Expected**: `ArgumentException` whose message does not contain the marker.

#### Context_Create_DepthWithinRange_ParsesOntoContext

**Scenario**: `--depth` of 1 and of 6.

**Expected**: `HeadingDepth` carries the value.

#### Context_Create_DepthOutOfRangeOrNonNumeric_ThrowsArgumentException

**Scenario**: `--depth` of `0`, `7`, `-1` and `deep`.

**Expected**: `ArgumentException` whose message names `--depth`.

#### Context_Create_DepthWithoutValue_ThrowsArgumentException

**Scenario**: `--depth` is the last argument.

**Expected**: `ArgumentException`.

#### Context_Create_VerboseOrDiagnosticsFlag_SetsDiagnostics

**Scenario**: `--verbose`, `--diagnostics`, and neither.

**Expected**: `Diagnostics` is true for the two flags and false by default.

#### Context_Create_VerboseAfterCommand_IsRecognizedAsGlobalOption

**Scenario**: `list-devices --verbose`.

**Expected**: `Diagnostics` is true and `CommandArgs` is empty.

#### Context_Create_NoEmotionsOnNonVoicesCommand_IsRecognizedAsGlobalOption

**Scenario**: `--no-emotions` before and after `list-devices`, and absent.

**Expected**: `NoEmotions` is true when present at either position (and not collected into
`CommandArgs`), false otherwise.

#### Context_Create_ResultsOption_ParsesPathAndRejectsMissingValue

**Scenario**: `--results out.trx`, `--result out.xml`, and `--results` with no value.

**Expected**: `ResultsFile` carries each path; the missing value is an `ArgumentException`.

#### SpeechCli_SilentFlag_Provided_SuppressesOutput

**Scenario**: The built tool is run as a child process with `--version --silent`.

**Expected**: Exit code 0 and no console output.

#### SpeechCli_LogFlag_Provided_WritesOutputToFile

**Scenario**: The built tool is run as a child process with `--log <file>`.

**Expected**: Exit code 0; the log file exists and contains the banner, which also appears on the
console.

### Not Covered By A Dedicated Test

- A global token supplied as a subcommand value (for example `speak --text -v`) is absorbed as the
  global option; no test pins this behavior.
- `ExitCode` becoming 1 after `WriteError`, and `--silent` still writing to the log file, are not
  asserted at the `Context` level (the exit-code effect is observed through `Program.Main` and
  `Validation` tests).
- A log file that cannot be opened (`InvalidOperationException`) has no test.
- The effect of `--models-dir` on the model-store root is verified only by commands that consume
  it; `Context` tests show that the value is carried.
