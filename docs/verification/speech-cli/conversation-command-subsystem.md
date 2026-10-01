## SpeechCli ConversationCommandSubsystem Verification

### Verification Approach

The ConversationCommandSubsystem is verified entirely through deterministic unit tests against
the same test doubles the synthesis and recognition passes already established: no new fake test
double is needed for the engine/session/catalog types themselves. `FakeCliModelCatalog`'s existing
`CreateSynthesizerEngineOverride`/`CreateRecognizerEngineOverride` delegates resolve `AskCommand`'s
two engines; `FakeSynthesisSession`/`FakeSpeechSynthesizerEngine` (recording `SpeakAsync`/
`DisposeAsync` call counts, with a settable exception to simulate a canceled Phase 1) and
`FakeRecognitionSession`/`FakeSpeechRecognizerEngine` (recording `StartAsync`/`StopAsync`/
`DisposeAsync` call counts, driving `GetResultsAsync` from an in-memory result sequence) drive both
phases. Device resolution reuses `FakePlaybackDeviceSource` (unmodified, from
`SynthesisCommandSubsystem`'s own tests) for Phase 1, and this pass's own new
`FakeCaptureDeviceSource`/`FakeAudioCaptureDevice` (mirroring `FakePlaybackDeviceSource`/
`FakeAudioPlaybackDevice` exactly) for Phase 2 - never a real `AudioDeviceFactory` - so every
`AskCommandTests` scenario that expects a successfully resolved capture device is deterministic
on any machine, including headless CI runners with no real PortAudio hardware at all. No real
model catalog, network access, or audio hardware is required by any `AskCommandTests` scenario.

Because `FakeRecognitionSession.GetResultsAsync()` and `FakeSynthesisSession.SpeakAsync()` both
yield/complete deterministically from in-memory configuration, a fully deterministic,
single-threaded test can simulate a complete speak-then-listen turn - including a genuine
final-result-driven listen-phase stop - with no real threading or wall-clock delay. The one
scenario that does need a real elapsed delay
(`AskCommand_Run_SilenceTimeoutWithNoFinalResult_EndsTurnWithEmptyText`) uses a tiny (`0.05`s) real
`--silence-timeout` value so the reused, unmodified `SilenceTimeoutRecognizerSession`'s real
`Task.Delay`-backed idle-timeout race genuinely fires, mirroring the same technique
`SilenceTimeoutRecognizerSessionTests`/`RecognizeCommandTests` use for their own silence-timeout
scenarios.

A genuine `Ctrl+C` landing during Phase 2 cannot be simulated by raising a real
`Console.CancelKeyPress` event from a test (there is no supported way to do so), so the
`AskCommand_RunAsync_*` cancellation scenarios drive `AskCommand.RunAsync` directly - the same
internal entry point the public `Run` overload's real `Console.CancelKeyPress` handler calls -
supplying their own `CancellationTokenSource`/`ManualResetEventSlim` pair and canceling/signaling
them from within a fake session's result sequence (or before `RunAsync` is even invoked, for the
early-return edge case) to reproduce the exact interleaving the real handler produces.
`AskCommand_Run_PrewarmsRecognizerConcurrentlyWithPlayback_CreatesRecognizerBeforePlaybackCompletes`
proves the recognizer engine/session pre-warm genuinely overlaps Phase 1's wait, using
`FakeSynthesisSession.SpeakAsyncAwaiter` to hold Phase 1's `SpeakAsync` in flight until
`FakeCliModelCatalog.CreateRecognizerEngineOverride` signals a `ManualResetEventSlim`, with a
bounded wait so a future regression that reorders pre-warming back to strictly after playback
fails the test explicitly rather than deadlocking.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own fake catalog/engine/session/device probes;
  `--output-text` tests write to a uniquely named temporary file, deleted afterward
- **Test doubles**: `FakeCliModelCatalog`, `FakeSynthesisSession`/`FakeSpeechSynthesizerEngine`,
  `FakeRecognitionSession`/`FakeSpeechRecognizerEngine`, `FakePlaybackDeviceSource`,
  `FakeAudioPlaybackDevice` (tracking a `DisposeCallCount`, so a test can prove the playback device
  resolved by `SpeakPromptAsync` is disposed exactly once), `FakeAudioPlaybackDeviceProbe` (reused,
  mostly unmodified, from `SynthesisCommandSubsystem`'s own tests), and this pass's own new
  `FakeCaptureDeviceSource`/`FakeAudioCaptureDevice`/`FakeAudioCaptureDeviceProbe` (mirroring
  `FakePlaybackDeviceSource`/`FakeAudioPlaybackDevice` exactly), so no `AskCommandTests` scenario
  ever depends on a real `AudioDeviceFactory`

### Test Scenarios

#### AskCommand — Argument Parsing, Text Source, and Model Resolution

**Tests**: `AskCommand_ParseArguments_MissingTtsModel_ThrowsArgumentException`,
`AskCommand_ParseArguments_MissingSttModel_ThrowsArgumentException`,
`AskCommand_ParseArguments_TextFlag_ParsesText`,
`AskCommand_ParseArguments_Interim_ThrowsArgumentException`,
`AskCommand_ParseArguments_FinalOnly_ThrowsArgumentException`,
`AskCommand_ParseArguments_NoTags_ThrowsArgumentException`,
`AskCommand_ParseArguments_Input_ThrowsArgumentException`,
`AskCommand_ParseArguments_OutputAudio_ThrowsArgumentException`,
`AskCommand_ParseArguments_UnsupportedArgument_ThrowsArgumentException`,
`AskCommand_ParseArguments_FlagMissingValue_ThrowsArgumentException`,
`AskCommand_Run_TextAndFileBothGiven_ThrowsArgumentException`,
`AskCommand_Run_UnknownTtsModelId_ThrowsArgumentException`,
`AskCommand_Run_UnknownSttModelId_ThrowsArgumentException`,
`AskCommand_Run_WrongRoleTtsModel_ThrowsArgumentException`,
`AskCommand_Run_WrongRoleSttModel_ThrowsArgumentException`,
`AskCommand_Run_NotDownloadedTtsModel_ThrowsArgumentExceptionWithDownloadHint`,
`AskCommand_Run_NotDownloadedSttModel_ThrowsArgumentExceptionWithDownloadHint`,
`AskCommand_Run_Success_SpeaksThenListensAndPrintsFinalResult`

**Scenario/Expected**: A missing `--tts-model`/`--stt-model`, an unsupported argument, or a
value-less flag are all rejected with `ArgumentException`; `--interim`/`--final-only`/`--no-tags`/
`--input`/`--output-audio` are each rejected as unsupported arguments, since none is a recognized
flag on `ask`; `--text` parses correctly; supplying both `--text` and `--file` throws
`ArgumentException` before either model id is even looked up; an unknown model id, a wrong-role
model id, and a not-yet-downloaded model id are each rejected with a distinct, actionable
`ArgumentException` message (naming `--tts-model` or `--stt-model` as appropriate, suggesting
`list-models`, `list-models --role tts`/`--role stt`, and `download <modelId>` respectively); a
successful run awaits `CreateSynthesizerEngineAsync`/`CreateSessionAsync`/`SpeakAsync` through the
resolved TTS model, then `CreateRecognizerEngineAsync`/`CreateSessionAsync`/`StartAsync`/
`GetResultsAsync` through the resolved STT model, then prints the final recognized text.

**Requirement coverage**: `SpeechCli-ConversationCommands-Ask`.

#### `--tts-param`/`--stt-param` Validation Reuse

**Tests**: `AskCommand_ParseArguments_RepeatedParamFlags_AccumulateInOrderIndependently`,
`AskCommand_Run_ValidParams_ForwardToRespectiveCreateCalls`,
`AskCommand_Run_InvalidTtsParam_ThrowsArgumentException`

**Scenario/Expected**: Repeated `--tts-param`/`--stt-param` flags each accumulate independently,
in the order given, and are forwarded, fully resolved via the shared `ParameterBagParser`, to
`CreateSynthesizerEngineAsync`'s and `CreateRecognizerEngineAsync`'s own `parameterValues` argument
respectively; an invalid value for a declared TTS parameter is rejected before any engine is
loaded. Full `ParameterBagParser` scenario coverage already lives in
`SpeechCli-SynthesisCommands-ParamValidation`'s own tests and is not duplicated here.

**Requirement coverage**: `SpeechCli-ConversationCommands-ParamValidation`.

#### Listen-Phase Termination

**Tests**: `AskCommand_ParseArguments_TimeoutFlags_ParseAsPositiveDoubles`,
`AskCommand_ParseArguments_NonPositiveSilenceTimeout_ThrowsArgumentException`,
`AskCommand_Run_SilenceTimeoutWithNoFinalResult_EndsTurnWithEmptyText`,
`AskCommand_Run_NoTimeoutFlagsGiven_StillEndsTurnViaDefaultSilenceTimeout`

**Scenario/Expected**: `--start-timeout`/`--silence-timeout` each parse a positive number of
seconds, rejecting a non-positive value; the listen phase ends as soon as the first final
recognition result arrives (verified as part of the success-path scenario above); when no final
result ever arrives, a `--silence-timeout` firing (via the reused, unmodified
`SilenceTimeoutRecognizerSession`) ends the turn with an empty recognized-text result rather than
blocking forever; omitting both `--start-timeout` and `--silence-timeout` still ends the turn via
the built-in default silence timeout rather than listening indefinitely.

**Requirement coverage**: `SpeechCli-ConversationCommands-ListenTermination`.

#### Output Dispatch

**Tests**: `AskCommand_ParseArguments_OutputTextFlag_ParsesPath`,
`AskCommand_Run_OutputText_WritesRecognizedTextToFile`

**Scenario/Expected**: `--output-text <path>` parses correctly and, when given, writes the final
recognized text to the named file (overwriting any prior content) instead of printing it to
stdout.

**Requirement coverage**: `SpeechCli-ConversationCommands-OutputDispatch`.

#### Playback and Capture Device Dispatch

**Tests**: `AskCommand_Run_UnknownPlaybackDevice_ThrowsArgumentException`,
`AskCommand_Run_NoPlaybackDeviceAvailable_ThrowsInvalidOperationException`,
`AskCommand_Run_UnknownCaptureDevice_ThrowsArgumentException`,
`AskCommand_Run_NoCaptureDeviceAvailable_ThrowsInvalidOperationException`

**Scenario/Expected**: Requesting an unrecognized `--playback-device`/`--capture-device` name
throws `ArgumentException` before any engine is loaded; an unavailable resolved real playback or
capture device throws `InvalidOperationException`.

**Requirement coverage**: `SpeechCli-ConversationCommands-DeviceDispatch`.

#### Cancellation and Disposal

**Tests**: `AskCommand_Run_CanceledDuringSpeak_SkipsListenPhase`,
`AskCommand_Run_ExceptionDuringSpeak_DisposesPlaybackDeviceAndRethrows`,
`AskCommand_Run_Success_SpeaksThenListensAndPrintsFinalResult`,
`AskCommand_RunAsync_CanceledDuringSpeak_ReturnsPromptlyWithoutAwaitingInFlightPrewarm`,
`AskCommand_RunAsync_CtrlCDuringListen_ReportsCanceledAndDoesNotPrintText`,
`AskCommand_RunAsync_CtrlCBeforeListenStarts_ReportsCanceledAndDoesNotPrintText`,
`AskCommand_RunAsync_SilenceTimeoutWithNoCtrlC_ReportsSuccessNotCanceled`,
`AskCommand_RunAsync_CtrlCImmediatelyAfterListenReturns_ReportsCanceledAndDoesNotPrintText`,
`AskCommand_RunAsync_CtrlCImmediatelyBeforeFileWrite_ReportsCanceledAndDoesNotWriteFile`

**Scenario/Expected**: A cancellation raised during Phase 1 (speak) is reported and causes Phase 2
(listen) to be skipped entirely - no session is ever *started*, the concurrently pre-warmed
engine/session (whether already constructed or still in flight) is disposed rather than leaked via
the fire-and-forget `DisposePrewarmedRecognizerAsync` path, and `RunAsync` returns promptly without
awaiting an in-flight pre-warm, proving the fast-failure/`Ctrl+C`-responsiveness contract the
pre-warm design depends on. A genuine `Ctrl+C` landing during Phase 2 - whether mid-listen or in
the narrow window before Phase 2 even starts a session - is reported via `context.WriteError` with
a non-zero exit code, and no recognized text (even if some was already captured) is ever printed or
written to `--output-text`/stdout. A `Ctrl+C` landing in the narrow window immediately after
`Listen()` returns - whether or not `--output-text` is configured - is likewise reported as a
cancellation and never writes the output file or prints "Recognized text written", proving
`RunAsync`'s output-writing step (which passes the real `cancellationToken` into
`File.WriteAllTextAsync` wrapped in a `try`/`catch` mirroring every other cancellation exit point
in `RunAsync`) never falsely reports success once cancellation has been observed. Conversely, a
legitimate empty result from a `--silence-timeout`/`--start-timeout` firing with no `Ctrl+C`
involved is still reported as a normal, zero-exit-code success, proving the two outcomes are
correctly distinguished rather than both being silently treated as success.

The resolved playback device's disposal - `SpeakPromptAsync`'s own `using var playbackDeviceLease`
cast - is verified as disposed exactly once across all three of Phase 1's possible exits: the
ordinary success path (`AskCommand_Run_Success_SpeaksThenListensAndPrintsFinalResult`), a canceled
Phase 1 (`AskCommand_Run_CanceledDuringSpeak_SkipsListenPhase`), and a non-cancellation exception
thrown from Phase 1's `SpeakAsync`
(`AskCommand_Run_ExceptionDuringSpeak_DisposesPlaybackDeviceAndRethrows`), using
`FakeAudioPlaybackDevice.DisposeCallCount` - proving playback-device disposal is not only correct
on the ordinary path but also never skipped when playback is canceled or faults.

**Requirement coverage**: `SpeechCli-ConversationCommands-CancellationAndDisposal`.

#### Recognizer Pre-Warming

**Tests**: `AskCommand_Run_PrewarmsRecognizerConcurrentlyWithPlayback_CreatesRecognizerBeforePlaybackCompletes`,
`AskCommand_Run_CanceledDuringSpeak_SkipsListenPhase`,
`AskCommand_Run_NoCaptureDeviceAvailable_ThrowsInvalidOperationException`

**Scenario/Expected**: The recognizer engine/session is constructed concurrently with Phase 1's
speak/playback wait, not only after it finishes. The primary test proves this deterministically
rather than with a flaky timing assertion: `FakeSynthesisSession.SpeakAsyncAwaiter` holds Phase 1's
`SpeakAsync` "in flight" until a `ManualResetEventSlim` set from inside `FakeCliModelCatalog`'s
`CreateRecognizerEngineOverride` is signaled, with a bounded (five-second) wait. If a future
regression moves recognizer engine construction back to strictly after playback finishes,
`SpeakAsync` would never observe the signal being set (since `CreateRecognizerEngineAsync` would
not run until *after* `SpeakAsync` itself returns), and the bounded wait fails the test explicitly
with a descriptive message instead of either silently passing or deadlocking the test run
indefinitely. The same scenario also proves Phase 2 still proceeds correctly once Phase 1
completes (the session starts, stops on the first final result, and the engine/session are each
disposed exactly once). The two supporting tests prove pre-warm failure/cancellation is handled
correctly rather than merely proving pre-warm construction itself: a canceled Phase 1 disposes the
concurrently pre-warmed engine/session rather than leaking it, and a pre-warm failure (no capture
device available) surfaces only *after* Phase 1 has already spoken the prompt - proving Phase 1 is
never skipped or reordered even when Phase 2's setup fails.

**Requirement coverage**: `SpeechCli-ConversationCommands-PrewarmRecognizer`.

#### Null Guards

**Tests**: `AskCommand_Run_NullContext_ThrowsArgumentNullException`,
`AskCommand_Run_NullCatalog_ThrowsArgumentNullException`,
`AskCommand_Run_NullDeviceSource_ThrowsArgumentNullException`,
`AskCommand_Run_NullCaptureSource_ThrowsArgumentNullException`,
`AskCommand_ParseArguments_NullArgs_ThrowsArgumentNullException`,
`AskCommand_RunAsync_NullContext_ThrowsArgumentNullException`,
`AskCommand_RunAsync_NullCatalog_ThrowsArgumentNullException`,
`AskCommand_RunAsync_NullDeviceSource_ThrowsArgumentNullException`,
`AskCommand_RunAsync_NullCaptureSource_ThrowsArgumentNullException`,
`AskCommand_RunAsync_NullStopSignal_ThrowsArgumentNullException`,
`AskCommand_RunAsync_NullOnRecognizerCreated_ThrowsArgumentNullException`

**Scenario/Expected**: Every entry point rejects a `null` context, catalog, playback-device
source, or capture-device source immediately with `ArgumentNullException`, proving `AskCommand`
never silently proceeds with a missing dependency; the internal `RunAsync` overload additionally
rejects a `null` `stopSignal` or `onRecognizerCreated` callback. `ParseArguments` itself
independently rejects a `null` argument list with `ArgumentNullException`, proving the guard holds
even when `ParseArguments` is invoked directly rather than only through `Run`/`RunAsync`.

**Requirement coverage**: `SpeechCli-ConversationCommands-NullGuards`.

### Requirements Coverage

- **`SpeechCli-ConversationCommands-Ask`**: see *AskCommand — Argument Parsing, Text Source, and
  Model Resolution* above
- **`SpeechCli-ConversationCommands-ParamValidation`**: see *`--tts-param`/`--stt-param`
  Validation Reuse* above
- **`SpeechCli-ConversationCommands-ListenTermination`**: see *Listen-Phase Termination* above
- **`SpeechCli-ConversationCommands-OutputDispatch`**: see *Output Dispatch* above
- **`SpeechCli-ConversationCommands-DeviceDispatch`**: see *Playback and Capture Device Dispatch*
  above
- **`SpeechCli-ConversationCommands-CancellationAndDisposal`**: see *Cancellation and Disposal*
  above
- **`SpeechCli-ConversationCommands-PrewarmRecognizer`**: see *Recognizer Pre-Warming* above
- **`SpeechCli-ConversationCommands-NullGuards`**: see *Null Guards* above

### Acceptance Criteria

A ConversationCommandSubsystem test run passes when: `ask` correctly enforces both-model
validation and text-source mutual exclusion, reporting actionable model-resolution errors for
either model; `--tts-param`/`--stt-param` values are each forwarded through the reused
`ParameterBagParser` to the correct `Create*` call; the listen phase ends on the first final
recognition result, and, absent one, ends cleanly on a `--silence-timeout` fire instead of
blocking forever; `--output-text` writes exactly as `recognize`'s own flag does; an unknown/
unavailable playback or capture device is rejected cleanly; a cancellation during the speak phase
skips the listen phase entirely with no partial state leaked; a genuine `Ctrl+C` during the listen
phase is reported as a cancellation (non-zero exit code, no text printed/written) rather than a
silent, false success, correctly distinguished from a legitimate empty `--silence-timeout`/
`--start-timeout` result; and every entry point rejects a missing required dependency.

## Manual / Build-Time Verification

`AskCommandTests` covers every scenario above without needing a real, downloaded TTS/STT model
pair or real audio hardware. A genuine two-model, real-hardware `ask` run still needs manual
verification on a machine with both a downloaded TTS and STT model and a real microphone/speakers
present, exactly as `RecognitionCommandSubsystem`'s own verification doc documents for
`recognize --mic`:

- `dotnet run --project src/DemaConsulting.Speech.Cli -- ask --tts-model <tts-id>
  --stt-model <stt-id> --text "What is your name?" --start-timeout 20 --silence-timeout 1.5`, run
  on a machine with a real microphone and speakers and both models downloaded, speaks the prompt
  aloud, then listens for a spoken reply - waiting up to `--start-timeout`'s grace period for the
  reply to begin and ending capture `--silence-timeout` seconds after the reply's own pause - and
  prints the recognized reply text once the turn ends
- The same invocation with `--output-text <path>` writes the recognized reply text to the named
  file instead of printing it to stdout
- Pressing `Ctrl+C` during either playback or listening ends the command cleanly with a non-zero
  exit code and no crash, in both cases
