### RecognizeCommand Verification

#### Verification Approach

`RecognizeCommand` is verified through deterministic unit tests in `RecognizeCommandTests`, driving
`RunAsync` and `ParseArguments` against hand-written test doubles, plus out-of-process integration
tests that run the built tool for the error paths that matter most to an operator. No test needs
audio hardware. Console output is captured by replacing `Console.Out` with a `StringWriter` for the
duration of one test.

File-input behavior is verified against a real `WavFileAudioCaptureDevice` constructed over a
minimal, valid mono 16-bit PCM WAV file that the test writes itself, so no shared binary fixture is
needed. Microphone behavior is verified through the `ICliCaptureDeviceSource` seam with a fake
available capture device.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test builds its own catalog, engine, session and temporary files; the test
  class is in the sequential collection because tests replace `Console.Out`
- **Test doubles**: `FakeCliModelCatalog` (with `CreateRecognizerEngineOverride`),
  `FakeSpeechRecognizerEngine` and `FakeRecognitionSession` (counting start, stop and dispose
  calls and raising scripted results from an `OnStart` callback), `FakeCaptureDeviceSource` and
  `FakeAudioCaptureDeviceProbe`
- **Integration**: `IntegrationTests` run the built tool as a child process, using an isolated
  `--models-dir` so no network access occurs, except the real-model test described below

#### Acceptance Criteria

The unit passes when argument parsing and validation behave as specified, every model and input
mistake produces an actionable error, file input starts and then drains the session exactly once,
console and file output follow the verbosity flags, the capture device is validated before it is
used, and engine and session are disposed once each. The real-model integration test passes when a
downloaded model exists, and is reported as skipped otherwise (see its scenario below).

#### Test Scenarios

##### RecognizeCommand_ParseArguments_MissingModel_ThrowsArgumentException

**Scenario**: Arguments omit `--stt-model`.

**Expected**: `ArgumentException`.

##### RecognizeCommand_ParseArguments_InputFlag_ParsesInputPath

**Scenario**: Arguments give a model and `--input in.wav`.

**Expected**: The options carry the model id and the input path, and `Mic` is false.

##### RecognizeCommand_ParseArguments_MicFlag_ParsesTrue

**Scenario**: Arguments give a model and `--mic`.

**Expected**: `Mic` is true.

##### RecognizeCommand_ParseArguments_UnsupportedArgument_ThrowsArgumentException

**Scenario**: Arguments include a flag `recognize` does not support.

**Expected**: `ArgumentException`.

##### RecognizeCommand_ParseArguments_FlagMissingValue_ThrowsArgumentException

**Scenario**: A value-taking flag is the last argument with no value.

**Expected**: `ArgumentException`.

##### RecognizeCommand_ParseArguments_ValueFlagAtEndOfArgs_ThrowsArgumentException

**Scenario**: Each value-taking flag (theory) is the last argument with no value.

**Expected**: `ArgumentException` whose message names the flag and says it requires a value.

##### RecognizeCommand_ParseArguments_ValueFlagFollowedByRecognizedOption_ThrowsArgumentException

**Scenario**: Each value-taking flag (theory) is directly followed by another `recognize` option
(for example `--stt-model --mic`).

**Expected**: `ArgumentException` naming the flag as requiring a value; the option is not consumed
as the value.

##### RecognizeCommand_ParseArguments_NonFiniteTimeout_ThrowsArgumentException

**Scenario**: `--silence-timeout` or `--start-timeout` is given `NaN`, `Infinity` or `-Infinity`
(theory).

**Expected**: `ArgumentException`.

##### RecognizeCommand_Run_FileInputCanceledDuringStart_CompletesGracefully

**Scenario**: A file-mode run whose cancellation token is canceled while the session is starting,
over a fake session whose `StopAsync` throws when given a canceled token.

**Expected**: No exception escapes, the exit code is `0`, `StopAsync` was called twice (the second
with an uncancelable token) and the session and engine are disposed.

##### RecognizeCommand_ParseArguments_NullArgs_ThrowsArgumentNullException

**Scenario**: `ParseArguments` receives a null argument list.

**Expected**: `ArgumentNullException`.

##### RecognizeCommand_ParseArguments_RepeatedParamFlags_AccumulatesInOrder

**Scenario**: Two `--stt-param` flags are given.

**Expected**: Both key/value pairs are kept in the order given.

##### RecognizeCommand_ParseArguments_MalformedParamToken_ThrowsArgumentException

**Scenario**: A `--stt-param` token has no `key=value` separator.

**Expected**: `ArgumentException`.

##### RecognizeCommand_ParseArguments_SilenceTimeoutFlag_ParsesSeconds

**Scenario**: `--silence-timeout 2.5` is given.

**Expected**: The options carry 2.5 seconds.

##### RecognizeCommand_ParseArguments_NonPositiveSilenceTimeout_ThrowsArgumentException

**Scenario**: `--silence-timeout 0` is given.

**Expected**: `ArgumentException`.

##### RecognizeCommand_ParseArguments_MalformedSilenceTimeout_ThrowsArgumentException

**Scenario**: `--silence-timeout not-a-number` is given.

**Expected**: `ArgumentException`.

##### RecognizeCommand_ParseArguments_StartTimeoutFlag_ParsesSeconds

**Scenario**: `--start-timeout 2.5` is given.

**Expected**: The options carry 2.5 seconds.

##### RecognizeCommand_ParseArguments_StartTimeoutOmitted_DefaultsToNull

**Scenario**: `--start-timeout` is omitted.

**Expected**: The options carry no start timeout; no default is applied at parse time.

##### RecognizeCommand_ParseArguments_NonPositiveStartTimeout_ThrowsArgumentException

**Scenario**: `--start-timeout 0` is given.

**Expected**: `ArgumentException`.

##### RecognizeCommand_ParseArguments_MalformedStartTimeout_ThrowsArgumentException

**Scenario**: `--start-timeout not-a-number` is given.

**Expected**: `ArgumentException`.

##### RecognizeCommand_Run_SilenceTimeoutOmitted_ResolvesToFixedFiveSecondDefault

**Scenario**: Options without `--silence-timeout` are resolved.

**Expected**: The silence window is 5 seconds.

##### RecognizeCommand_Run_StartTimeoutOmitted_ResolvesToFixedEightSecondDefault

**Scenario**: Options with `--silence-timeout 2.5` but no `--start-timeout` are resolved.

**Expected**: The start window is 8 seconds and differs from the silence window, so the default
never borrows the silence value.

##### RecognizeCommand_Run_StartTimeoutGiven_ResolvesToGivenValue

**Scenario**: Options with `--start-timeout 2.5` are resolved.

**Expected**: The start window is exactly 2.5 seconds.

##### RecognizeCommand_ParseArguments_InterimAndFinalOnlyFlags_ParseTrue

**Scenario**: `--interim` and `--final-only` are given in separate parses.

**Expected**: Each sets its own flag.

##### RecognizeCommand_ParseArguments_OutputFlag_ParsesOutputPath

**Scenario**: `--output-text` is given with a path.

**Expected**: The options carry the output path.

##### RecognizeCommand_Run_InputAndMicBothGiven_ThrowsArgumentException

**Scenario**: Both `--input` and `--mic` are given.

**Expected**: `ArgumentException`, before the model is looked up.

##### RecognizeCommand_Run_NoInputSource_ThrowsArgumentException

**Scenario**: Neither `--input` nor `--mic` is given.

**Expected**: `ArgumentException`.

##### RecognizeCommand_Run_InterimAndFinalOnlyBothGiven_ThrowsArgumentException

**Scenario**: Both `--interim` and `--final-only` are given.

**Expected**: `ArgumentException`.

##### RecognizeCommand_Run_UnknownModelId_ThrowsArgumentException

**Scenario**: The model id is not in the catalog.

**Expected**: `ArgumentException` whose message contains the id.

##### RecognizeCommand_Run_WrongRoleModel_ThrowsArgumentException

**Scenario**: The model id names a synthesis-role model.

**Expected**: `ArgumentException` whose message contains the model id. The test asserts the
model name only; the `list-models --role stt` suggestion in the message is not asserted by any
test.

##### RecognizeCommand_Run_NotDownloadedModel_ThrowsArgumentExceptionWithDownloadHint

**Scenario**: The model is a recognition model that is not downloaded.

**Expected**: `ArgumentException` whose message contains `download model-1`.

##### RecognizeCommand_Run_ValidParam_ForwardsToCreateRecognizerEngine

**Scenario**: The model declares a numeric `beam` parameter and `--stt-param beam=6` is given.

**Expected**: The engine is requested once with the resolved numeric value 6.

##### RecognizeCommand_Run_InvalidParam_ThrowsArgumentException

**Scenario**: `--stt-param beam=999` exceeds the declared range.

**Expected**: `ArgumentException`, before an engine is created.

##### RecognizeCommand_Run_UnknownParamKey_ThrowsArgumentException

**Scenario**: `--stt-param nope=1` names a key the model does not declare.

**Expected**: `ArgumentException`, before an engine is created.

##### RecognizeCommand_Run_FileInput_StartsAndStopsRecognizerViaExplicitDrain

**Scenario**: `--input` names a valid WAV file; the fake session counts calls.

**Expected**: The session is started once and stopped once, and the session and engine are each
disposed once. The exit code is 0.

##### RecognizeCommand_Run_FileInput_PassesWavFileCaptureDeviceToCreateSession

**Scenario**: `--input` names a valid WAV file.

**Expected**: The device passed to `CreateSessionAsync` is a `WavFileAudioCaptureDevice`.

##### RecognizeCommand_Run_FileInputWithTimeouts_TimeoutsIgnoredAndRunSucceeds

**Scenario**: `--input` is combined with `--start-timeout 0.001` and `--silence-timeout 0.001`.

**Expected**: The flags are accepted and ignored: the session is started and stopped once each and
the exit code is 0.

##### RecognizeCommand_Run_InputFileIsNotWav_ThrowsInvalidOperationExceptionNamingFile

**Scenario**: `--input` names a file that is not RIFF data; the fake session starts the real
device when it starts.

**Expected**: `InvalidOperationException` whose message names the file.

##### RecognizeCommand_Run_InputFileIsStereoWav_ThrowsInvalidOperationExceptionAboutMono

**Scenario**: `--input` names a valid stereo WAV file.

**Expected**: `InvalidOperationException` whose message mentions mono.

##### RecognizeCommand_Run_DefaultVerbosity_PrintsBothInterimAndFinal

**Scenario**: The session raises an interim "hel" then a final "hello", with no verbosity flag.

**Expected**: The first physical console line contains "hel" as a carriage-return segment and ends
with "hello"; the next line is "Recognition finished.".

##### RecognizeCommand_Run_ShrinkingInterimSequence_DoesNotLeaveStaleCharacters

**Scenario**: The session raises interim "hello world", interim "hello", then final "hello".

**Expected**: The shorter interim write is padded to the longest length seen, the line ends as
exactly "hello", and no stale characters remain.

##### RecognizeCommand_Run_FinalOnly_SuppressesInterimConsoleOutput

**Scenario**: An interim and a final result are raised with `--final-only`.

**Expected**: The console shows "hello" on its own line followed by "Recognition finished.", with
no carriage returns.

##### RecognizeCommand_Run_Interim_SuppressesFinalSettleConsoleOutput

**Scenario**: An interim and a final result are raised with `--interim`.

**Expected**: The interim text is printed, the final text "hello" is not, and the completion
message follows.

##### RecognizeCommand_Run_Output_WritesOnlyFinalResultsOverwritingPriorContent

**Scenario**: Interim and final results alternate, and the `--output-text` file already holds
stale content.

**Expected**: The file contains exactly the two final results, one per line, and none of the stale
content or interim text. No test combines `--output-text` with `--interim` or `--final-only`.

##### RecognizeCommand_Run_UnknownDevice_ThrowsArgumentException

**Scenario**: `--mic --capture-device does-not-exist` against a probe with no devices.

**Expected**: `ArgumentException` whose message contains the name, before any recognition starts.

##### RecognizeCommand_Run_NoCaptureDeviceAvailable_ThrowsInvalidOperationException

**Scenario**: `--mic` against a factory whose probe reports no capture device.

**Expected**: `InvalidOperationException`.

##### RecognizeCommand_Run_MicMode_StreamsResultsFromCaptureDevice

**Scenario**: `--mic` with 0.2-second timeouts, a fake available device named "Fake Mic" and a
fake session that raises one final result when started.

**Expected**: The session is started once and bound to a device that is not a WAV file device,
"hello mic" appears in the console output, and the exit code is 0 after the idle timeout ends the
session. The test uses the default device; no test selects a device by a valid `--capture-device`
name.

##### RecognizeCommand_Run_Success_DisposesEngineAndSessionOnce

**Scenario**: A successful file-input run.

**Expected**: The session and the engine are each disposed exactly once.

##### RecognizeCommand_Run_NullContext_ThrowsArgumentNullException

**Scenario**: `RunAsync` receives a null context.

**Expected**: `ArgumentNullException`.

##### RecognizeCommand_Run_NullCatalog_ThrowsArgumentNullException

**Scenario**: `RunAsync` receives a null catalog.

**Expected**: `ArgumentNullException`.

##### RecognizeCommand_Run_NullFactory_ThrowsArgumentNullException

**Scenario**: `RunAsync` receives a null audio device factory.

**Expected**: `ArgumentNullException`.

##### SpeechCli_RecognizeCommandWithoutModel_Invoked_ReturnsCleanError

**Scenario**: The built tool runs `recognize --mic` with no model.

**Expected**: A non-zero exit code and output that mentions `--stt-model`.

##### Program_Run_WithRecognizeCommandAndNoModel_ThrowsArgumentException

**Scenario**: `Program.Run` dispatches `recognize` with no model.

**Expected**: `ArgumentException`, proving `recognize` is dispatched to the real command.

##### SpeechCli_RecognizeCommandWithUnknownModel_Invoked_ReturnsCleanError

**Scenario**: The built tool runs `recognize` with an unknown model id and an empty models directory.

**Expected**: A non-zero exit code and output that names the id.

##### SpeechCli_RecognizeCommandWithWrongRoleModel_Invoked_ReturnsCleanError

**Scenario**: The built tool runs `recognize` with a synthesis model id discovered through
`list-models --role tts`.

**Expected**: A non-zero exit code and output that names the model id.

##### SpeechCli_RecognizeCommandWithNotDownloadedModel_Invoked_ReturnsCleanErrorWithDownloadHint

**Scenario**: The built tool runs `recognize` with a recognition model id that is not downloaded.

**Expected**: A non-zero exit code and output that mentions `download`.

##### SpeechCli_RecognizeCommandWithConflictingInputSources_Invoked_ReturnsCleanError

**Scenario**: The built tool runs `recognize` with both `--input` and `--mic`.

**Expected**: A non-zero exit code and output that mentions both flags.

##### SpeechCli_RecognizeCommandWithRealSttModel_Invoked_ProducesNonEmptyRecognizedText

**Scenario**: The built tool runs `recognize --input` over a real WAV fixture against a downloaded
speech-to-text model found in the per-user model store.

**Expected**: Exit code 0, a closing "Recognition finished." line and at least one non-blank
recognized line. The test skips dynamically with `Assert.Skip` when no downloaded speech-to-text
model exists in the store, and then it is reported as skipped, not as a pass. In that case the
scenario is verified manually, as described in the subsystem verification document.
