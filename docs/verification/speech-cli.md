# SpeechCli System Verification Design

This document describes the system-level verification strategy for the SpeechCli application.

## Verification Approach

SpeechCli is verified through a mix of in-process unit/integration tests, which compose the real
`Context`, `Program`, `CommandDispatch`, and `Validation` types directly, and out-of-process
integration tests, which invoke the built tool as a child process the way an operator would, and
assert on its console output and exit code. It covers the tool's own behavior - global-option and
subcommand dispatch, version/help display, `--validate` self-testing, and .NET global tool
packaging - plus all thirteen subcommands: the five model-management subcommands and the two
reference-voice subcommands (`list-models`, `model-info`, `download`, `uninstall`, `clean`,
`list-voices`, `voices`, verified in detail in _SpeechCli ModelCommandsSubsystem Verification_),
the three device-related subcommands (`list-devices`, `devices test`, `doctor`, verified in
_SpeechCli DeviceCommandsSubsystem Verification_), the text-to-speech subcommand (`speak`, verified
in _SpeechCli SynthesisCommandSubsystem Verification_), the speech-to-text subcommand (`recognize`,
verified in _SpeechCli RecognitionCommandSubsystem Verification_), and the voice-conversation
subcommand (`ask`, verified in _SpeechCli ConversationCommandSubsystem Verification_).

Automated coverage **does not** extend to real audio hardware or to a real, downloaded speech
model: the `--validate` self-test's audio and model-store checks are composed exactly as the
Speech library itself guarantees never to throw during composition, and are proven correct
against whatever audio backend and model-store root the CI runner happens to have, not against a
specific expected device list or a real installed model.

System tests reside in `ProgramTests.cs` (in-process; Program entry point, dispatch priority, exit codes),
`Cli/ContextTests.cs` (Context parsing, mirror and environment handling), `Cli/CommandDispatchTests.cs`
(dispatch table) and `IntegrationTests.cs` (out-of-process,
child-process) within the `DemaConsulting.Speech.Cli.Tests` project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, no network access, and no guaranteed physical audio
  hardware or installed speech model
- **Isolation**: Out-of-process tests invoke the built `DemaConsulting.Speech.Cli.dll` directly
  via `dotnet <dll>` through the `Runner` helper, rather than the installed `speech-cli` tool
  shim, so tests never depend on a prior `dotnet tool install`

## External Interface Simulation

No simulation is used for composition: the real `Context`, `CommandDispatch`, `Validation`,
`AudioDeviceFactory`, and `SpeechModelStore` types are constructed directly. Where a
machine-independent result is required (audio backend probing), the test only asserts that
composition and enumeration complete without throwing, matching the library's own contract for
those types.

## Acceptance Criteria

The SpeechCli system is accepted when all of the following hold (IEC 62304 §5.7.2):

- Every test linked to a SpeechCli system requirement in `docs/reqstream/speech-cli.yaml`, and
  every test linked to a requirement of the five command subsystems, passes with no skipped or
  failing result in the CI test run on every supported platform
- `speech-cli --validate` reports `Failed: 0`, with every self-test check passing
- `--version` and `--help` exit `0`, and `--help` lists all thirteen subcommands and every global
  option
- An unrecognized subcommand or option exits non-zero with a one-line error and no stack trace
- The ReqStream requirements-to-tests enforcement reports every SpeechCli requirement as covered
- The manual build-time checks listed in _Build-Time Verification_ are performed for each
  release and produce the expected result

## Test Scenarios

The scenarios below are system-level; each subsystem's detailed scenarios are in its own
verification document.

### Version Display

**Tests**: `Program_Run_WithVersionFlag_DisplaysVersionOnly`,
`Program_Run_WithShortVersionFlag_DisplaysVersion`, `SpeechCli_VersionFlag_Provided_OutputsVersion`

Verifies that `-v`/`--version` prints only the version string and exits `0`, both in-process and
as a child process.

### Help Display

**Tests**: `Program_Run_WithHelpFlag_DisplaysUsageInformation`,
`Program_Run_WithShortHelpFlag_DisplaysUsage`, `Program_Run_WithQuestionMarkFlag_DisplaysUsage`,
`Program_Run_NoArguments_DisplaysBannerAndUsage`, `Program_Run_Help_DocumentsVoicesOptionsAndConsentNote`, `SpeechCli_HelpFlag_Provided_ListsAllSubcommands`,
`SpeechCli_NoArguments_Invoked_DisplaysBannerAndUsage`

Verifies that `-h`/`-?`/`--help`, and running with no arguments at all, print the banner and usage
text listing every one of the thirteen recognized subcommands (including `list-voices` and `voices`),
the `--voices-dir` option, and the reference-voice consent note, and exit `0`.

### Top-Level Action Priority

**Tests**: `Program_Run_VersionHelpValidateTogether_HonorsPriorityOrder`

Verifies that `--validate --help --version` prints only the version, and `--validate --help` prints only the usage
text without running the self-validation.

### Subcommand Dispatch

**Tests**: `Program_Run_WithListModelsCommand_ListsModelsAndSucceeds`,
`Program_Run_WithListDevicesCommand_CompletesWithSuccessExitCode`,
`Program_Run_WithDoctorCommand_ProducesReportWithoutThrowing`,
`Program_Run_WithSpeakCommandAndNoModel_ThrowsArgumentException`,
`Program_Run_WithRecognizeCommandAndNoModel_ThrowsArgumentException`,
`Program_Run_WithAskCommandAndNoModel_ThrowsArgumentException`,
`CommandDispatch_Find_VoiceCommands_AreRegistered`,
`Program_Main_WithUnknownCommand_ReturnsNonZeroExitCodeAndReportsCommand`,
`Program_Main_WithUnknownCommandAndSilentFlag_SuppressesErrorOutput`,
`SpeechCli_UnknownCommand_Provided_ReturnsCleanError`

Verifies that `list-models` (representative of the five model-management subcommands),
`list-devices`/`doctor` (representative of the three device-related subcommands), `speak`,
`recognize`, and `ask` dispatch to their real handlers and do not throw `NotImplementedException`,
that the `list-voices` and `voices` subcommands are registered in the dispatch table, and that an
unrecognized subcommand name is rejected cleanly with a non-zero exit code rather than a stack
trace (and that `--silent` suppresses that error output). See _SpeechCli ModelCommandsSubsystem
Verification_, _SpeechCli DeviceCommandsSubsystem Verification_, _SpeechCli
SynthesisCommandSubsystem Verification_, _SpeechCli RecognitionCommandSubsystem Verification_,
and _SpeechCli ConversationCommandSubsystem Verification_ for the subcommands' own detailed test
scenarios.

### Global Options Recognized Regardless of Position

**Tests**: `Context_Create_ModelsDirAfterCommand_IsRecognizedAsGlobalOption`,
`Context_Create_WithMirrorOptions_ParsesOntoContext`,
`Context_Create_WithExistingVoicesDir_ParsesOntoContext`, `Context_Create_WithBadVoicesDir_ThrowsArgumentException`,
`SpeechCli_SilentFlag_Provided_SuppressesOutput`, `SpeechCli_LogFlag_Provided_WritesOutputToFile`,
`Context_Create_DepthWithinRange_ParsesOntoContext`,
`Context_Create_DepthOutOfRangeOrNonNumeric_ThrowsArgumentException`,
`Context_Create_DepthWithoutValue_ThrowsArgumentException`,
`Context_Create_VerboseOrDiagnosticsFlag_SetsDiagnostics`,
`Context_Create_VerboseAfterCommand_IsRecognizedAsGlobalOption`,
`Context_Create_NoEmotionsOnNonVoicesCommand_IsRecognizedAsGlobalOption`,
`Context_Create_ResultsOption_ParsesPathAndRejectsMissingValue`

Verifies that a global option appearing after the subcommand name is still recognized as a global
option (not absorbed into the subcommand's raw argument list), and that `--silent`/`--log`
actually suppress console output/write a log file respectively.
`--voices-dir` is parsed onto the context when it names an existing folder and rejected with `ArgumentException` otherwise.
`--depth` accepts 1 through 6 and rejects 0, 7, negative, non-numeric, and missing values;
`--verbose` and its `--diagnostics` alias set the diagnostics flag before or after the subcommand;
`--no-emotions` is recognized before or after any subcommand; and `--results`/`--result` carry
the results file path.

### Download Mirror Configuration

**Tests**: `Context_CreateMirror_NoMirrorUrl_ReturnsNull`,
`Context_Create_MirrorCredentialWithoutUrlAndHelp_ThrowsArgumentException`,
`Context_CreateMirror_ValidCredentialedMirror_ReturnsConfiguredMirror`,
`Context_CreateMirror_BearerTokenMirror_ReturnsConfiguredMirror`,
`Context_CreateMirror_InvalidMirrorUrl_ThrowsArgumentException`,
`Context_CreateMirror_MirrorUserWithoutPassword_ThrowsArgumentException`,
`Context_CreateMirror_BasicCredentialsAndBearerTokenTogether_ThrowsArgumentException`,
`Context_CreateMirror_CredentialsWithoutMirrorUrl_ThrowsArgumentException`,
`Context_CreateMirror_BearerTokenWithoutMirrorUrl_ThrowsArgumentException`,
`Context_CreateMirror_UnsupportedMirrorUrlShape_ThrowsArgumentException`,
`Context_CreateMirror_WhitespaceBearerToken_ThrowsArgumentException`,
`Context_CreateMirror_CredentialOverPlainHttp_RejectedUnlessLoopback`
Verifies `Context.CreateMirror()` returns `null` when `--mirror-url` is not supplied, builds a
`DownloadMirror` carrying the configured base URI plus either HTTP Basic credentials or a bearer
token when they are, and rejects an invalid `--mirror-url` or an incomplete `--mirror-user`/
`--mirror-password` pairing with a clean `ArgumentException` rather than letting the invalid
configuration surface later from inside the download transport. The mutually-exclusive-token test
confirms that supplying both a complete Basic credential pair and a bearer token together is
rejected rather than silently preferring one over the other - `Context.CreateMirror` forwards
both to `DownloadMirror`'s constructor, which enforces this exclusivity (along with the mirror
URL's scheme/query/fragment/user-info shape, the non-empty-bearer-token rule, and the
non-loopback-`http`-with-credentials rule) - see _Speech ModelManagementSubsystem Design_ for
`DownloadMirror`'s own full validation rules. The without-mirror-url tests confirm that
`--mirror-user`/`--mirror-password` and `--mirror-bearer-token` are each rejected outright when
supplied with no `--mirror-url`, rather than being silently ignored - a credential with nowhere to
apply almost always indicates a missing or misspelled `--mirror-url`. A mirror URL of an unsupported
shape (scheme, query, fragment, or embedded user-info), a whitespace-only bearer token, and Basic or
bearer credentials sent over cleartext `http` to a non-loopback host are each rejected with
`ArgumentException`; a loopback `http` mirror is accepted.

### Mirror Environment Variables

**Tests**: `Context_Create_EnvironmentMirrorUrl_UsedWhenOptionAbsent`,
`Context_Create_MirrorUrlOption_OverridesEnvironment`,
`Context_Create_EnvironmentBasicCredentials_UsedWhenOptionsAbsent`,
`Context_Create_EnvironmentBearerToken_UsedWhenOptionsAbsent`,
`Context_Create_CredentialOption_DisablesEnvironmentCredentials`,
`Context_Create_EmptyEnvironmentVariables_TreatedAsUnset`,
`Context_Create_EnvironmentCredentialWithoutUrl_ThrowsWithoutEchoingSecret`,
`Context_Create_InvalidMirrorUrl_ErrorDoesNotEchoValue`,
`Program_Run_Help_DocumentsMirrorEnvironmentVariables`,
`Program_Run_Help_DoesNotPrintEnvironmentSecretValues`

Each test drives `Context.Create(args, getEnvironmentVariable)` with a fake environment reader, never
the process environment. They verify that each `DEMASPEECH_MIRROR_*` variable is used when its option is
absent; that `--mirror-url` overrides the URL variable; that any credential option disables all credential
variables; that empty variables are treated as unset; that a credential variable without a URL is rejected
with an error that does not contain the secret; that an invalid URL is not echoed; and that help names the
variables but never prints their values.

### Self-Validation

**Tests**: `Program_Run_WithValidateFlag_RunsValidation`,
`SpeechCli_ValidateFlag_Provided_RunsValidation`

Verifies that `--validate` runs all five self-test checks (version display, help display,
the dispatch table being well formed, audio-backend probe composition, the model-store root being
writable) to completion without throwing regardless of the host machine's audio hardware or
installed models, and reports a pass/fail summary. The version and help checks run their inner
invocation with the process environment hidden, so an ambient `DEMASPEECH_MIRROR_*` variable cannot
change the self-test outcome.

### Self-Validation Results File

**Tests**: `SpeechCli_ValidateWithTrxResults_Requested_GeneratesTrxFile`,
`SpeechCli_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel`,
`Context_Create_ResultsOption_ParsesPathAndRejectsMissingValue`,
`Program_Main_ValidateWithXmlResults_WritesJUnitFile`,
`Program_Main_ValidateWithUnsupportedResultsExtension_ReturnsNonZeroExitCode`,
`Program_Main_ValidateWithUnwritableResultsPath_ReturnsNonZeroExitCode`

Verifies that `--results <file>` (alias `--result`) writes a TRX file for a `.trx` path and a JUnit
file for a `.xml` path, that `--depth` sets the report's markdown heading level, that a missing
`--results` value is rejected, and that an unsupported extension or an unwritable path produces an
error message and a non-zero exit code (the failure path is driven through `Program.Main` without
any production test seam).

### ConsoleCancellation

**Tests**: `ConsoleCancellation_Register_Raise_CancelsSourceRunsCallbackAndSuppressesTermination`,
`ConsoleCancellation_Register_NoCallback_CancelsSource`,
`ConsoleCancellation_Observe_Raise_RunsCallbackWithoutSuppressingTermination`,
`ConsoleCancellation_Dispose_CalledTwice_DoesNotThrow`,
`ConsoleCancellation_Register_NullSource_ThrowsArgumentNullException`,
`ConsoleCancellation_Observe_NullCallback_ThrowsArgumentNullException`

Verifies that `Register` cancels the source, runs the optional callback and suppresses process termination when the
cancel event is raised; that `Observe` runs its callback without suppressing termination; that disposal is idempotent;
and that null arguments are rejected.

### PathHelpers

**Tests**: `PathHelpers_SafePathCombine_RelativeBasePath_ReturnsAbsolutePath`,
`PathHelpers_SafePathCombine_AbsoluteBasePath_ReturnsAbsolutePath`,
`PathHelpers_SafePathCombine_EscapingRelativePath_ThrowsArgumentException`,
`PathHelpers_SafePathCombine_RootedRelativePath_ThrowsArgumentException`,
`PathHelpers_SafePathCombine_NullArguments_ThrowArgumentNullException`

Verifies that `SafePathCombine` returns an absolute path for relative or absolute bases and rejects a relative path
that escapes its base, a rooted relative path, and null arguments.

### Program Version Property

**Test**: `Program_Version_ReturnsNonEmptyString`

Verifies `Program.Version` returns a non-empty string.

### Argument Errors

Context argument-error messages carry no "(Parameter 'x')" suffix (`Context_Create_UnsupportedArgument_MessageHasNoParameterSuffix`,
`Context_Create_OptionMissingValue_MessageHasNoParameterSuffix`), and `WriteError` restores the console foreground color even
when the error writer throws (`Context_WriteError_ErrorWriterThrows_RestoresForegroundColor`).

**Tests**: `Program_Main_WithInvalidArgs_ReturnsNonZeroExitCode`,
`Program_Main_WithInvalidArgsAndSilentFlag_SuppressesErrorOutput`,
`SpeechCli_UnknownOption_Provided_ReturnsCleanError`

Verifies that an unsupported flag is reported as a clean error with a non-zero exit code (and that
`--silent` suppresses the message), using
the same `ArgumentException`/`InvalidOperationException` handling convention the reference
`TemplateDotNetTool` uses.

### Null Context Guard

**Test**: `Program_Run_WithNullContext_ThrowsArgumentNullException`

Verifies `Program.RunAsync` rejects a `null` context argument immediately (as a faulted task), since it is never a valid
runtime state for the process's own entry point to reach.

**Test**: `CommandDispatch_Handlers_ReturnNonNullTasks`

Verifies every `CommandDispatch` handler (a `Func<Context, Task>`) is non-null and yields a non-null
`Task` when invoked, confirming the asynchronous dispatch pipeline has no missing entry.

## Build-Time Verification

Global tool packaging is a property of the build and packaging pipeline rather than of runtime behavior, so it is
verified by the pack verification step in `build.ps1` (run locally and by any caller of the build script) instead of
a unit test. After the Release build, the step runs the CI-equivalent
`dotnet pack --no-build --no-restore --configuration Release --property:PackageVersion=0.0.1-test` into a temporary
directory and fails the build unless all of the following hold:

- the pack reports no NuGet (`NU*`) warnings
- a `.nupkg` exists for every packable `src/` project
- the `DemaConsulting.Speech.Cli` package contains a `DotnetToolSettings.xml` whose command name is `speech-cli`,
  confirming the global tool packaging plumbing (`PackAsTool`/`ToolCommandName`/`PackageId`) actually works, not just
  that the `.csproj` looks correct

This evidence covers `SpeechCli-Packaging-GlobalTool` together with the linked `--version` and `--help` tests.

The following remains a manual check:

- `dotnet run --project src/DemaConsulting.Speech.Cli -- --help` produces sane, readable banner
  and usage output when run directly from source
