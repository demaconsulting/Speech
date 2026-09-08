# SpeechCli System Verification Design

This document describes the system-level verification strategy for the SpeechCli application.

## Verification Approach

SpeechCli is verified through a mix of in-process unit/integration tests, which compose the real
`Context`, `Program`, `CommandDispatch`, and `Validation` types directly, and out-of-process
integration tests, which invoke the built tool as a child process the way an operator would, and
assert on its console output and exit code. This pass covers the scaffold behavior carried over
from Pass 2 - global-option and subcommand dispatch, version/help display, `--validate`
self-testing, and .NET global tool packaging - plus the five model-management subcommands
implemented in Pass 3 (`list-models`, `model-info`, `download`, `uninstall`, `clean`, verified in
detail in _SpeechCli ModelCommandsSubsystem Verification_), the three device-related subcommands
implemented in Pass 4 (`list-devices`, `devices test`, `doctor`, verified in detail in
_SpeechCli DeviceCommandsSubsystem Verification_), the one text-to-speech subcommand implemented
in Pass 5 (`speak`, verified in detail in _SpeechCli SynthesisCommandSubsystem Verification_),
and the one speech-to-text subcommand implemented in this pass (`recognize`, verified in detail
in _SpeechCli RecognitionCommandSubsystem Verification_) - the last of all ten subcommands to be
implemented; no subcommand handler remains a `NotImplementedException` stub.

Automated coverage **does not** extend to real audio hardware or to a real, downloaded speech
model: the `--validate` self-test's audio and model-store checks are composed exactly as the
Speech library itself guarantees never to throw during composition, and are proven correct
against whatever audio backend and model-store root the CI runner happens to have, not against a
specific expected device list or a real installed model.

System tests reside in `ProgramTests.cs` (in-process) and `IntegrationTests.cs` (out-of-process,
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

## System-Level Test Scenarios

### Version Display

**Tests**: `Program_Run_WithVersionFlag_DisplaysVersionOnly`,
`Program_Run_WithShortVersionFlag_DisplaysVersion`, `SpeechCli_VersionFlag_Provided_OutputsVersion`

Verifies that `-v`/`--version` prints only the version string and exits `0`, both in-process and
as a child process.

### Help Display

**Tests**: `Program_Run_WithHelpFlag_DisplaysUsageInformation`,
`Program_Run_WithShortHelpFlag_DisplaysUsage`, `Program_Run_WithQuestionMarkFlag_DisplaysUsage`,
`Program_Run_NoArguments_DisplaysBannerAndUsage`, `SpeechCli_HelpFlag_Provided_ListsAllSubcommands`,
`SpeechCli_NoArguments_Invoked_DisplaysBannerAndUsage`

Verifies that `-h`/`-?`/`--help`, and running with no arguments at all, print the banner and usage
text listing every one of the ten recognized subcommands, and exit `0`.

### Subcommand Dispatch

**Tests**: `Program_Run_WithListModelsCommand_DoesNotThrowNotImplemented`,
`Program_Run_WithRecognizeCommand_DoesNotThrowNotImplemented`,
`Program_Main_WithUnknownCommand_ReturnsNonZeroExitCodeAndReportsCommand`,
`SpeechCli_UnknownCommand_Provided_ReturnsCleanError`

Verifies that `list-models` (representative of the five model-management subcommands),
`list-devices`/`doctor` (representative of the three device-related subcommands), `speak`, and
`recognize` no longer throw `NotImplementedException` - `recognize` being the last of all ten
subcommands to reach that state - and that an unrecognized subcommand name is rejected cleanly
with a non-zero exit code rather than a stack trace. See _SpeechCli ModelCommandsSubsystem
Verification_, _SpeechCli DeviceCommandsSubsystem Verification_, _SpeechCli
SynthesisCommandSubsystem Verification_, and _SpeechCli RecognitionCommandSubsystem
Verification_ for the implemented subcommands' own detailed test scenarios.

### Global Options Recognized Regardless of Position

**Tests**: `Context_Create_ModelsDirAfterCommand_IsRecognizedAsGlobalOption`,
`SpeechCli_SilentFlag_Provided_SuppressesOutput`, `SpeechCli_LogFlag_Provided_WritesOutputToFile`

Verifies that a global option appearing after the subcommand name is still recognized as a global
option (not absorbed into the subcommand's raw argument list), and that `--silent`/`--log`
actually suppress console output/write a log file respectively.

### Self-Validation

**Tests**: `Program_Run_WithValidateFlag_RunsValidation`,
`SpeechCli_ValidateFlag_Provided_RunsValidation`,
`SpeechCli_ValidateWithTrxResults_Requested_GeneratesTrxFile`,
`SpeechCli_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel`

Verifies that `--validate` runs all five self-test checks (version display, help display,
the dispatch table being well formed, audio-backend probe composition, the model-store root being
writable) to completion without throwing regardless of the host machine's audio hardware or
installed models, reports a pass/fail summary, honors `--depth` for its markdown heading level,
and, when `--results <file>` is given, writes a `.trx`/`.xml` file.

### Argument Errors

**Test**: `Program_Main_WithInvalidArgs_ReturnsNonZeroExitCode`

Verifies that an unsupported flag is reported as a clean error with a non-zero exit code, using
the same `ArgumentException`/`InvalidOperationException` handling convention the reference
`TemplateDotNetTool` uses.

### Null Context Guard

**Test**: `Program_Run_WithNullContext_ThrowsArgumentNullException`

Verifies `Program.Run` rejects a `null` context argument immediately, since it is never a valid
runtime state for the process's own entry point to reach.

## Manual / Build-Time Verification

The following are verified manually for this pass, rather than by an automated test, because they
are properties of the build and packaging pipeline rather than of runtime behavior:

- `dotnet pack src/DemaConsulting.Speech.Cli/DemaConsulting.Speech.Cli.csproj -c Release` produces
  a `.nupkg` and a `.snupkg`, confirming the global tool packaging plumbing
  (`PackAsTool`/`ToolCommandName`/`PackageId`) actually works, not just that the `.csproj` looks
  correct
- `dotnet run --project src/DemaConsulting.Speech.Cli -- --help` produces sane, readable banner
  and usage output when run directly from source
