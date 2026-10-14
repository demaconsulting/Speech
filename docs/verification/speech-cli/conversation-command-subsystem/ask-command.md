### AskCommand Verification

#### Verification Approach

`AskCommand` is verified through deterministic unit tests over in-memory test doubles. `FakeCliModelCatalog`
resolves the two engines through its synthesizer and recognizer override delegates. `FakeSynthesisSession` and
`FakeSpeechSynthesizerEngine` record `SpeakAsync` and disposal calls and can be set to throw, to simulate a canceled
prompt. `FakeRecognitionSession` and `FakeSpeechRecognizerEngine` record start, stop, and disposal calls and drive
the result stream from an in-memory sequence. Playback devices come from `FakePlaybackDeviceSource` with
`FakeAudioPlaybackDevice`, and capture devices from `FakeCaptureDeviceSource` with `FakeAudioCaptureDevice`, so no
test depends on a real `AudioDeviceFactory`, real PortAudio hardware, a real model catalog, or network access.

A complete speak-then-listen turn, including a listen phase that ends on the first final result, runs
single-threaded and without wall-clock delay. Two scenarios genuinely wait: the silence-timeout scenarios use a
real wait (a `0.05`-second timeout, and the built-in default), so the reused `SilenceTimeoutRecognizerSession`
actually fires.

A real `Ctrl+C` cannot be raised from a test, so the cancellation scenarios call the internal `RunAsync` overload
that the real `Ctrl+C` handler also uses. They supply their own cancellation source and stop signal and cancel or
signal them from inside a fake session callback to reproduce the interleaving the handler produces. The
pre-warm scenarios hold the fake synthesis session in flight, or hold recognizer creation in progress, with bounded
waits so a regression fails with a message instead of hanging.

A `--file` prompt is tested for both a readable file (its contents are spoken) and a missing one (a clean
`InvalidOperationException`). A prompt piped on stdin is tested through an injected standard-input source, so the
tests do not depend on how the test host wires stdin. An empty prompt and explicit
`--playback-device`/`--capture-device` names that resolve successfully are tested too. The start-timeout and
blocking-start scenarios use short real waits, bounded so that a regression fails instead of hanging.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test builds its own fake catalog, engines, sessions, and device sources; `--output-text`
  tests write to a uniquely named temporary file that is deleted afterward; tests that capture the console restore it
- **Test doubles**: `FakeCliModelCatalog`, `FakeSynthesisSession`, `FakeSpeechSynthesizerEngine`,
  `FakeRecognitionSession`, `FakeSpeechRecognizerEngine`, `FakePlaybackDeviceSource`, `FakeAudioPlaybackDevice` (with
  a disposal count), `FakeAudioPlaybackDeviceProbe`, `FakeCaptureDeviceSource`, `FakeAudioCaptureDevice`, and
  `FakeAudioCaptureDeviceProbe`

#### Acceptance Criteria

The unit passes when `ask` enforces both model ids and the text-source rules, rejects every unsupported option,
reports actionable model-resolution errors (each naming what to do next), rejects a value-taking
option that is followed by another recognized option or has no value at all while still accepting
dash-prefixed text as a value,
rejects non-finite timeouts, and forwards typed parameters to the right engine; the first final
result ends the listen phase while a timeout with no final result ends it with an empty reply; the reply goes to
stdout or the `--output-text` file; device failures are reported cleanly and in the right order relative to the
prompt; the recognizer is created before the prompt finishes and released when the prompt is canceled or setup is
canceled; Ctrl+C or cancellation during model-engine setup is reported cleanly;
`Ctrl+C` is
reported as a cancellation with no text or file written, and a timeout is not; and every created object is disposed
once.

#### Test Scenarios

##### AskCommand_ParseArguments_MissingTtsModel_ThrowsArgumentException

**Scenario**: The arguments omit `--tts-model`.

**Expected**: `ArgumentException` naming the missing option.

##### AskCommand_ParseArguments_MissingSttModel_ThrowsArgumentException

**Scenario**: The arguments omit `--stt-model`.

**Expected**: `ArgumentException` naming the missing option.

##### AskCommand_ParseArguments_TextFlag_ParsesText

**Scenario**: The arguments give `--text` with a prompt.

**Expected**: The parsed options carry that text.

##### AskCommand_ParseArguments_Interim_ThrowsArgumentException

**Scenario**: The arguments include `--interim`.

**Expected**: `ArgumentException`, because `ask` has no such option.

##### AskCommand_ParseArguments_FinalOnly_ThrowsArgumentException

**Scenario**: The arguments include `--final-only`.

**Expected**: `ArgumentException`, because `ask` has no such option.

##### AskCommand_ParseArguments_NoTags_ThrowsArgumentException

**Scenario**: The arguments include `--no-tags`.

**Expected**: `ArgumentException`, because `ask` has no such option.

##### AskCommand_ParseArguments_Input_ThrowsArgumentException

**Scenario**: The arguments include `--input`.

**Expected**: `ArgumentException`, because `ask` has no such option.

##### AskCommand_ParseArguments_OutputAudio_ThrowsArgumentException

**Scenario**: The arguments include `--output-audio`.

**Expected**: `ArgumentException`, because `ask` has no such option.

##### AskCommand_ParseArguments_UnsupportedArgument_ThrowsArgumentException

**Scenario**: The arguments include a token `ask` does not recognize.

**Expected**: `ArgumentException` naming the token.

##### AskCommand_ParseArguments_FlagMissingValue_ThrowsArgumentException

**Scenario**: A flag that needs a value is the last argument.

**Expected**: `ArgumentException` naming the flag.

##### AskCommand_ParseArguments_ValueFlagFollowedByRecognizedOption_ThrowsArgumentException

**Scenario**: Each of the 11 value-taking options is followed immediately by another recognized option token.

**Expected**: `ArgumentException` saying the first option requires a value; the next option is not swallowed.

##### AskCommand_ParseArguments_ValueFlagAtEndOfArguments_ThrowsArgumentException

**Scenario**: Each value-taking option is the last argument.

**Expected**: `ArgumentException` saying the option requires a value.

##### AskCommand_ParseArguments_DashPrefixedNonOptionValue_IsAcceptedAsValue

**Scenario**: `--text` is followed by dash-prefixed text that is not a recognized option.

**Expected**: The text is accepted unchanged as the prompt.

##### AskCommand_ParseArguments_NonFiniteTimeout_ThrowsArgumentException

**Scenario**: A timeout of `NaN` or infinity is given.

**Expected**: `ArgumentException` saying a positive number of seconds is required.

##### AskCommand_Run_ModelErrors_NameActionableGuidance

**Scenario**: Unknown, wrong-role and not-downloaded model ids.

**Expected**: Each error names the id and the corrective action (list the models, pick a model of the right kind, or
download it).

##### AskCommand_Run_FileFlag_SpeaksFileContents

**Scenario**: `--file` names a text file.

**Expected**: The file's contents are what the synthesizer is asked to speak.

##### AskCommand_Run_MissingTextFile_ThrowsInvalidOperationException

**Scenario**: `--file` names a file that does not exist.

**Expected**: `InvalidOperationException` saying the text file cannot be read.

##### AskCommand_Run_CanceledDuringSynthesizerEngineSetup_ReportsCleanCancellation

**Scenario**: Cancellation is signaled while the synthesizer engine is being created.

**Expected**: Exit code 1 with "Speech was canceled." on stderr, and nothing leaked.

##### AskCommand_Run_CanceledDuringSynthesisSessionSetup_ReportsCleanCancellation

**Scenario**: Cancellation is signaled while the synthesis session is being created.

**Expected**: The same clean cancellation, with the engine disposed.

##### AskCommand_RunAsync_CanceledDuringRecognizerSetup_ReportsCleanCancellation

**Scenario**: Cancellation is signaled while the pre-warmed recognizer is being adopted.

**Expected**: The same clean cancellation, with the recognizer released.

##### AskCommand_DisposePrewarmedRecognizer_Produced_DisposesSessionAndEngine

**Scenario**: A pre-warmed recognizer was produced.

**Expected**: Both its session and engine are disposed.

##### AskCommand_DisposePrewarmedRecognizer_FaultedPrewarm_CompletesWithoutThrowing

**Scenario**: The pre-warm task faulted.

**Expected**: Disposal completes without throwing.

##### AskCommand_DisposePrewarmedRecognizer_SessionDisposeThrows_StillDisposesEngineAndPreservesFirstException

**Scenario**: The session's dispose throws.

**Expected**: The engine is still disposed and the session's exception is the one surfaced.

##### AskCommand_Run_TextAndFileBothGiven_ThrowsArgumentException

**Scenario**: The arguments give both `--text` and `--file`.

**Expected**: `ArgumentException` that names `--file`, before either model id is looked up.

##### AskCommand_Run_NoTextSource_ThrowsArgumentException

**Scenario**: No text source is given and the injected standard-input source is not redirected.

**Expected**: `ArgumentException` naming `--text` and `--file`. The standard-input source is injected, so the test
does not depend on how the test host wires stdin.

##### AskCommand_Run_PipedStdin_SpeaksPipedText

**Scenario**: No text source is given and the injected standard-input source is redirected and holds a prompt.

**Expected**: The piped prompt is the text that is spoken.

##### AskCommand_Run_EmptyPrompt_SpeaksEmptyTextWithoutError

**Scenario**: `--text` is given an empty value.

**Expected**: The empty text is spoken and no error is raised.

##### AskCommand_Run_NamedDevices_UseTheNamedDevices

**Scenario**: `--playback-device` and `--capture-device` name devices that resolve successfully.

**Expected**: The named devices are the ones used for playback and capture.

##### AskCommand_Run_UnknownTtsModelId_ThrowsArgumentException

**Scenario**: `--tts-model` names an id the catalog does not know.

**Expected**: `ArgumentException` that suggests `list-models`.

##### AskCommand_Run_UnknownSttModelId_ThrowsArgumentException

**Scenario**: `--stt-model` names an id the catalog does not know.

**Expected**: `ArgumentException` that suggests `list-models`.

##### AskCommand_Run_WrongRoleTtsModel_ThrowsArgumentException

**Scenario**: `--tts-model` names a model that is not a synthesis model.

**Expected**: `ArgumentException` that suggests `list-models --role tts`.

##### AskCommand_Run_WrongRoleSttModel_ThrowsArgumentException

**Scenario**: `--stt-model` names a model that is not a recognition model.

**Expected**: `ArgumentException` that suggests `list-models --role stt`.

##### AskCommand_Run_NotDownloadedTtsModel_ThrowsArgumentExceptionWithDownloadHint

**Scenario**: `--tts-model` names a known synthesis model that is not downloaded.

**Expected**: `ArgumentException` that suggests `download <modelId>`.

##### AskCommand_Run_NotDownloadedSttModel_ThrowsArgumentExceptionWithDownloadHint

**Scenario**: `--stt-model` names a known recognition model that is not downloaded.

**Expected**: `ArgumentException` that suggests `download <modelId>`.

##### AskCommand_ParseArguments_RepeatedParamFlags_AccumulateInOrderIndependently

**Scenario**: The arguments repeat `--tts-param` and `--stt-param`.

**Expected**: Each flag's tokens accumulate separately, in the order given.

##### AskCommand_Run_ValidParams_ForwardToRespectiveCreateCalls

**Scenario**: Valid `--tts-param` and `--stt-param` values are given.

**Expected**: Each value, resolved to its declared type, reaches the synthesizer or recognizer engine creation call
of the model it names.

##### AskCommand_Run_InvalidTtsParam_ThrowsArgumentException

**Scenario**: A `--tts-param` value is outside the range or options its model declares.

**Expected**: `ArgumentException`, before any engine is created.

##### AskCommand_Run_InvalidSttParam_ThrowsArgumentException

**Scenario**: A `--stt-param` value is outside the range or options its model declares.

**Expected**: `ArgumentException`, before any engine is created.

##### AskCommand_Run_UnknownParamKey_ThrowsArgumentException

**Scenario**: A parameter key is not declared by the model.

**Expected**: `ArgumentException` naming the key.

##### AskCommand_ParseArguments_MalformedParamToken_ThrowsArgumentException

**Scenario**: A parameter token has no `key=value` separator.

**Expected**: `ArgumentException`.

##### AskCommand_ParseArguments_TimeoutFlags_ParseAsPositiveDoubles

**Scenario**: `--start-timeout` and `--silence-timeout` are given positive numbers.

**Expected**: Each is parsed as the given number of seconds.

##### AskCommand_Run_StartTimeoutWithNoResult_EndsTurnWithEmptyText

**Scenario**: The session emits no result of any kind, `--start-timeout` is a small value, and `--output-text` names
a file.

**Expected**: The start timeout fires, the session is stopped, and the file exists and is completely empty.

##### AskCommand_Run_BlockingSessionStart_StillDeliversFinalResult

**Scenario**: The fake session's start call blocks its calling thread until the session is stopped, and a final
result is already queued.

**Expected**: The final result is still delivered and the turn ends, so a blocking start cannot hang the turn.

##### AskCommand_Run_SessionStartFaults_ThrowsStartFailure

**Scenario**: The fake session's start call fails.

**Expected**: The turn ends promptly with the start failure instead of waiting for results that never arrive.

##### AskCommand_StopSessionObserved_StopFaults_CompletesWithoutThrowing

**Scenario**: The stop call of the session returns a faulted task.

**Expected**: The cancellation-time stop completes without throwing, so the failure is observed, not unobserved.

##### AskCommand_StopSessionObserved_StopThrowsSynchronously_CompletesWithoutThrowing

**Scenario**: The stop call of the session throws before returning a task.

**Expected**: The cancellation-time stop completes without throwing.

##### AskCommand_ParseArguments_NonPositiveSilenceTimeout_ThrowsArgumentException

**Scenario**: `--silence-timeout` is zero, negative, or not a number.

**Expected**: `ArgumentException`.

##### AskCommand_ParseArguments_NonPositiveStartTimeout_ThrowsArgumentException

**Scenario**: `--start-timeout` is zero, negative, or not a number.

**Expected**: `ArgumentException`.

##### AskCommand_Run_SilenceTimeoutWithNoFinalResult_EndsTurnWithEmptyText

**Scenario**: The recognizer raises only an interim result, `--silence-timeout 0.05` is given, and `--output-text`
names a file.

**Expected**: The silence timeout fires after the interim result, the session is stopped, and the file exists and is
completely empty, so interim text is not delivered.

##### AskCommand_Run_NoTimeoutFlagsGiven_StillEndsTurnViaDefaultSilenceTimeout

**Scenario**: The recognizer raises only an interim result and neither timeout flag is given.

**Expected**: The turn still ends on its own through the built-in default silence timeout. This is a real wait of
the default length.

##### AskCommand_Run_UnknownPlaybackDevice_ThrowsArgumentException

**Scenario**: `--playback-device` names a device the probe does not list.

**Expected**: `ArgumentException` naming the device, before any synthesizer is created.

##### AskCommand_Run_NoPlaybackDeviceAvailable_ThrowsInvalidOperationException

**Scenario**: The playback source resolves to an unavailable device.

**Expected**: `InvalidOperationException`; the concurrently created recognizer is still disposed by the background
cleanup.

##### AskCommand_Run_UnknownCaptureDevice_ThrowsArgumentException

**Scenario**: `--capture-device` names a device the probe does not list.

**Expected**: `ArgumentException` naming the device.

##### AskCommand_Run_NoCaptureDeviceAvailable_ThrowsInvalidOperationException

**Scenario**: The capture source resolves to an unavailable device.

**Expected**: `InvalidOperationException`, and the synthesis session has already spoken the prompt exactly once, so
the failure is reported after the prompt.

##### AskCommand_Run_Success_SpeaksThenListensAndPrintsFinalResult

**Scenario**: A full turn with valid models and available devices; the recognizer raises a final result.

**Expected**: The prompt is spoken, the recognizer starts, the final text is printed to stdout, the listen phase
ends on that first final result, every session and engine is started, stopped, and disposed once, and the playback
device is disposed once.

##### AskCommand_Run_PrewarmsRecognizerConcurrentlyWithPlayback_CreatesRecognizerBeforePlaybackCompletes

**Scenario**: The fake synthesis session holds playback in flight until the recognizer engine creation signals it,
with a five-second bounded wait.

**Expected**: The signal arrives while playback is still in flight, so the recognizer is created before the prompt
finishes; the turn then completes with the final text printed, one start, and one disposal. A regression that
creates the recognizer only after playback makes the bounded wait fail with a message.

##### AskCommand_Run_OutputText_WritesRecognizedTextToFile

**Scenario**: `--output-text` names a file and the recognizer raises a final result.

**Expected**: The file holds the reply text instead of stdout.

##### AskCommand_ParseArguments_OutputTextFlag_ParsesPath

**Scenario**: The arguments give `--output-text` with a path.

**Expected**: The parsed options carry that path.

##### AskCommand_Run_OutputTextExistingFile_OverwritesPriorContent

**Scenario**: The `--output-text` file already holds other content.

**Expected**: The file holds only the new reply.

##### AskCommand_Run_CanceledDuringSpeak_SkipsListenPhase

**Scenario**: The synthesis session's `SpeakAsync` is set to throw a cancellation.

**Expected**: Exit code 1, the synthesis session, synthesizer engine, and playback device are each disposed once, the
recognizer session is never started, and the concurrently created recognizer engine and session are eventually
disposed.

##### AskCommand_Run_ExceptionDuringSpeak_DisposesPlaybackDeviceAndRethrows

**Scenario**: `SpeakAsync` throws an exception that is not a cancellation.

**Expected**: The exception propagates and the playback device is disposed once.

##### AskCommand_RunAsync_CanceledDuringSpeak_ReturnsPromptlyWithoutAwaitingInFlightPrewarm

**Scenario**: The prompt is canceled while recognizer engine creation is held in progress by the test.

**Expected**: The command returns within the bounded wait with exit code 1 while creation is still held; the
recognizer is not yet disposed, and is released once the hold is lifted.

##### AskCommand_RunAsync_CtrlCDuringListen_ReportsCanceledAndDoesNotPrintText

**Scenario**: The cancellation token is canceled and the stop signal set from the recognizer's start callback.

**Expected**: Exit code 1 and an empty stdout.

##### AskCommand_RunAsync_CtrlCBeforeListenStarts_ReportsCanceledAndDoesNotPrintText

**Scenario**: The token is canceled and the stop signal set as soon as the listen phase receives its session.

**Expected**: Exit code 1, the recognizer session never started, and an empty stdout.

##### AskCommand_RunAsync_SilenceTimeoutWithNoCtrlC_ReportsSuccessNotCanceled

**Scenario**: The stop signal is set with no cancellation, as a timeout does.

**Expected**: Exit code 0, so a timeout is not reported as a cancellation.

##### AskCommand_RunAsync_CtrlCImmediatelyAfterListenReturns_ReportsCanceledAndDoesNotPrintText

**Scenario**: A final result arrives, then the token is canceled in the instant after the listen phase returns.

**Expected**: Exit code 1 and an empty stdout.

##### AskCommand_RunAsync_CtrlCImmediatelyBeforeFileWrite_ReportsCanceledAndDoesNotWriteFile

**Scenario**: As above with `--output-text` configured.

**Expected**: Exit code 1, no "Recognized text written" message, and no output file.

##### AskCommand_Run_NullContext_ThrowsArgumentNullException

**Scenario**: A null context is passed to the run entry point.

**Expected**: `ArgumentNullException`.

##### AskCommand_Run_NullCatalog_ThrowsArgumentNullException

**Scenario**: A null catalog is passed to the run entry point.

**Expected**: `ArgumentNullException`.

##### AskCommand_Run_NullDeviceSource_ThrowsArgumentNullException

**Scenario**: A null playback-device source is passed to the run entry point.

**Expected**: `ArgumentNullException`.

##### AskCommand_Run_NullCaptureSource_ThrowsArgumentNullException

**Scenario**: A null capture-device source is passed to the run entry point.

**Expected**: `ArgumentNullException`.

##### AskCommand_ParseArguments_NullArgs_ThrowsArgumentNullException

**Scenario**: A null argument list is passed to `ParseArguments`.

**Expected**: `ArgumentNullException`.

##### AskCommand_RunAsync_NullContext_ThrowsArgumentNullException

**Scenario**: A null context is passed to the internal `RunAsync` overload.

**Expected**: `ArgumentNullException`.

##### AskCommand_RunAsync_NullCatalog_ThrowsArgumentNullException

**Scenario**: A null catalog is passed to the internal `RunAsync` overload.

**Expected**: `ArgumentNullException`.

##### AskCommand_RunAsync_NullDeviceSource_ThrowsArgumentNullException

**Scenario**: A null playback-device source is passed to the internal `RunAsync` overload.

**Expected**: `ArgumentNullException`.

##### AskCommand_RunAsync_NullCaptureSource_ThrowsArgumentNullException

**Scenario**: A null capture-device source is passed to the internal `RunAsync` overload.

**Expected**: `ArgumentNullException`.

##### AskCommand_RunAsync_NullStopSignal_ThrowsArgumentNullException

**Scenario**: A null stop signal is passed to the internal `RunAsync` overload.

**Expected**: `ArgumentNullException`.

##### AskCommand_RunAsync_NullOnRecognizerCreated_ThrowsArgumentNullException

**Scenario**: A null recognizer-created callback is passed to the internal `RunAsync` overload.

**Expected**: `ArgumentNullException`.
