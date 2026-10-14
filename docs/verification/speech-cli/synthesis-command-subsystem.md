## SpeechCli SynthesisCommandSubsystem Verification

### Verification Approach

The SynthesisCommandSubsystem is verified at two levels. Each unit has its own verification
document under `synthesis-command-subsystem/` that covers the unit in isolation: `SpeakCommand`
against fake catalog, engine, session and playback-device collaborators; `ParameterBagParser`
against hand-built parameter instances; and the playback-device seam and its production
implementation. This document covers the subsystem as a whole: the command wired to the parser and
the device seam, and the built tool run as a child process for the paths that matter most to an
operator.

The subsystem-level behavior (a successful `speak` that resolves text, validates parameters, binds a
device and disposes everything) is exercised in-process through `SpeakCommand` with a fake
playback-device source and fake engine and session, so it is deterministic on every machine,
headless or not. Out-of-process integration tests in `IntegrationTests.cs` invoke the built tool for
the model-resolution and text-source error paths, proving each reaches the operator as a clean,
non-zero-exit error rather than a stack trace.

Automated tests do not exercise a real, downloaded synthesis model's `--output-audio` WAV-writing
path end to end: CI has no cached synthesis model, and downloading one needs network access and a
multi-hundred-megabyte transfer that is unsuitable for every test run. That path is verified
manually (see _Manual-Only Verification_ below), following the repository's convention for genuinely
environment-dependent behavior.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every in-process test constructs its own fake catalog, engine, session and device
  source; integration tests use an isolated, empty temporary model directory
- **Test doubles**: `FakeCliModelCatalog`, `FakeSpeechSynthesizerEngine`, `FakeSynthesisSession`,
  `FakePlaybackDeviceSource`, `FakeAudioPlaybackDevice`

### Acceptance Criteria

A SynthesisCommandSubsystem test run passes when a `speak` invocation with a valid fake model
completes, disposing its resources, and when every operator mistake covered by the unit
verification documents is also reported cleanly by the built tool with a non-zero exit code.

### Test Scenarios

#### ParamFlagNameReported

**Tests**: `ParameterBagParser_Resolve_UnrecognizedKey_ThrowsArgumentException`,
`ParameterBagParser_Resolve_NumericMalformed_ThrowsArgumentException`,
`ParameterBagParser_ParseToken_NoSeparator_ThrowsArgumentException`,
`SpeakCommand_RunAsync_InvalidParam_ThrowsArgumentException`,
`RecognizeCommand_Run_InvalidParam_ThrowsArgumentException`,
`RecognizeCommand_Run_UnknownParamKey_ThrowsArgumentException`,
`RecognizeCommand_ParseArguments_MalformedParamToken_ThrowsArgumentException`

**Scenario/Expected**: A malformed or undeclared `--tts-param` for `speak` and `--stt-param` for
`recognize` is rejected with an `ArgumentException` whose message names the invoking flag verbatim
(`--tts-param` or `--stt-param`), so the operator sees the flag they typed.

#### SpeakCommand_RunAsync_TextFlag_SynthesizesText

**Scenario**: `speak --text` runs end to end over a fake catalog, device source, engine and session.

**Expected**: The session receives exactly the given text and the exit code is zero.

#### SpeakCommand_RunAsync_ReadableFile_SynthesizesFileContents

**Scenario**: `speak --file` names a readable temporary file.

**Expected**: The session receives the file's exact contents.

#### SpeakCommand_RunAsync_RedirectedStdin_SynthesizesPipedText

**Scenario**: `speak` has neither `--text` nor `--file` and the stdin seam supplies piped text.

**Expected**: The session receives the piped text.

#### SpeakCommand_RunAsync_UnknownModelId_ThrowsArgumentException

**Tests**: `SpeakCommand_RunAsync_UnknownModelId_ThrowsArgumentException`,
`SpeakCommand_RunAsync_WrongRoleModel_ThrowsArgumentException`,
`SpeakCommand_RunAsync_NotDownloadedModel_ThrowsArgumentExceptionWithDownloadHint`

**Scenario/Expected**: An unknown, wrong-role or not-downloaded `--tts-model` is refused with an
`ArgumentException` whose message names the model id, and nothing is synthesized.

#### SpeakCommand_RunAsync_Success_DisposesEngineAndSessionOnce

**Scenario**: `speak` runs end to end over a fake catalog, device source, engine and session.

**Expected**: The text reaches the session, the engine and session are each disposed exactly once,
and the exit code is zero.

#### SpeechCli_SpeakCommandWithoutModel_Invoked_ReturnsCleanError

**Scenario**: The built tool is invoked with `speak` and no `--tts-model`.

**Expected**: A non-zero exit code and an error naming the missing option, with no stack trace.

#### SpeechCli_SpeakCommandWithUnknownModel_Invoked_ReturnsCleanError

**Scenario**: The built tool is invoked with an unknown model id against an empty model directory.

**Expected**: A non-zero exit code and an error naming the model id.

#### SpeechCli_SpeakCommandWithWrongRoleModel_Invoked_ReturnsCleanError

**Scenario**: The built tool is invoked with the id of a recognition model.

**Expected**: A non-zero exit code and an error naming the model.

#### SpeechCli_SpeakCommandWithNotDownloadedModel_Invoked_ReturnsCleanErrorWithDownloadHint

**Scenario**: The built tool is invoked with a known synthesis model that is not downloaded.

**Expected**: A non-zero exit code and an error that mentions downloading.

#### SpeechCli_SpeakCommandWithConflictingTextSources_Invoked_ReturnsCleanError

**Scenario**: The built tool is invoked with both `--text` and `--file`.

**Expected**: A non-zero exit code and an error naming both `--text` and `--file`.

#### Program_Run_WithSpeakCommandAndNoModel_ThrowsArgumentException

**Scenario**: The program entry point is run in-process with `speak` and no model.

**Expected**: The dispatch reports the missing option as an `ArgumentException`.

### Manual-Only Verification (Hardware and Real Model)

The following is verified manually, because it needs a real, downloaded synthesis model and audio
hardware that CI does not have. It is the only behavior not covered by automated evidence:

- `dotnet run --project src/DemaConsulting.Speech.Cli -- speak --tts-model <id> --text "hello world"
  --output-audio <path>.wav`, against a downloaded synthesis model, produces a valid, non-trivial WAV
  file (correct RIFF header, non-zero sample count, audible speech on playback).
- The same invocation without `--output-audio`, on a machine with real playback hardware, plays
  audible speech through the default or `--playback-device`-selected device.

### Known Gaps

None beyond the manual-only items above. The `--file` (readable, missing), piped stdin, and empty or
whitespace-only text cases, and the remedy text of the model errors, are automated by the `SpeakCommand`
unit tests listed in `synthesis-command-subsystem/speak-command.md`.
