## SpeechCli RecognitionCommandSubsystem Verification

### Verification Approach

The RecognitionCommandSubsystem is verified through deterministic unit tests against a
hand-written `FakeRecognitionSession`/`FakeSpeechRecognizerEngine` pair (recording
`StartAsync`/`StopAsync`/`DisposeAsync` call counts, with a settable `IsAvailable` and an
`OnStartAsync` callback letting a test simulate a session's own synchronous, blocking
`StartAsync`-drives-the-device file-mode contract) and `FakeCliModelCatalog`'s two new delegate
overrides (`GetAudioFormatOverride`/`CreateRecognizerEngineOverride`), reused from
`ModelCommandsSubsystem`'s own tests. `SilenceTimeoutRecognizerSession` is verified entirely in
isolation against a hand-written `FakeTimeProvider`/`FakeTimer` pair whose callback a test invokes
directly, so every idle/reset/timeout scenario runs deterministically with no real wall-clock
delay. The two new `ICliModelCatalog` seam members (`GetAudioFormat`/
`CreateRecognizerEngineAsync`) are additionally verified against a **real**
`SpeechModelCatalogAdapter`, proving the production `is IRecognitionModel` cast genuinely throws
for a real, compiled-in synthesis-role model, in `SpeechModelCatalogAdapterTests.cs`.
Out-of-process integration tests in `IntegrationTests.cs` invoke the built tool as a child process
for the model-resolution and input-source error paths that matter most from an operator's
perspective.

The file-input, explicit-drain flow is verified against a **real** `WavFileAudioCaptureDevice`
constructed over a minimal, self-generated, valid mono/16-bit-PCM WAV file (written directly by
the test, not a shared binary fixture), paired with `FakeRecognitionSession` wired to the real
`FakeSpeechRecognizerEngine.CreateSessionAsync` call - proving `RecognizeCommand`'s own
`StartAsync`-then-explicit-`StopAsync` sequencing for file mode genuinely drives a real device
rather than only being exercised against a fully-faked recognizer/device pair.

This environment does have at least one real, downloaded speech-to-text model available (unlike
the SynthesisCommandSubsystem pass's TTS gap), so `SpeechCli_RecognizeCommandWithRealSttModel_
Invoked_ProducesNonEmptyRecognizedText` runs a genuine end-to-end `recognize --input` invocation
against the built tool and asserts non-empty recognized text, skipping cleanly (rather than
failing or fabricating a pass) if no downloaded recognition model is present when the test suite
runs elsewhere. The fixture WAV file it recognizes against
(`test/DemaConsulting.Speech.Tests/TestData/crossing-the-bar-16k-mono.wav`) is located at test
time by walking up from the test assembly's own output directory to the repository root
(identified by `Speech.slnx`), reusing the existing binary fixture rather than duplicating it or
adding a cross-project `.csproj` content-copy item.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every unit test constructs its own fake catalog/engine/session/time provider/audio
  probes; `SpeechModelCatalogAdapterTests` uses a freshly created, uniquely named temporary
  directory as its model-store root, deleted afterward via `IDisposable`
- **Test doubles**: `FakeRecognitionSession`/`FakeSpeechRecognizerEngine` (hand-written
  `IRecognitionSession`/`ISpeechRecognizerEngine` fakes), `FakeTimeProvider`/`FakeTimer`
  (hand-written `TimeProvider`/`ITimer` fakes with a directly invocable callback),
  `FakeCliModelCatalog` (extended with `GetAudioFormatOverride`/`CreateRecognizerEngineOverride`),
  `FakeAudioCaptureDeviceProbe` (reused from `DeviceCommandsSubsystem`'s own tests)

### Test Scenarios

#### RecognizeCommand — Input Source, Model Resolution, and Argument Parsing

**Tests**: `RecognizeCommand_ParseArguments_MissingModel_ThrowsArgumentException`,
`RecognizeCommand_ParseArguments_InputFlag_ParsesInputPath`,
`RecognizeCommand_ParseArguments_MicFlag_ParsesTrue`,
`RecognizeCommand_ParseArguments_UnsupportedArgument_ThrowsArgumentException`,
`RecognizeCommand_ParseArguments_FlagMissingValue_ThrowsArgumentException`,
`RecognizeCommand_Run_InputAndMicBothGiven_ThrowsArgumentException`,
`RecognizeCommand_Run_NoInputSource_ThrowsArgumentException`,
`RecognizeCommand_Run_UnknownModelId_ThrowsArgumentException`,
`RecognizeCommand_Run_WrongRoleModel_ThrowsArgumentException`,
`RecognizeCommand_Run_NotDownloadedModel_ThrowsArgumentExceptionWithDownloadHint`,
`RecognizeCommand_Run_Success_DisposesEngineAndSessionOnce`,
`SpeechCli_RecognizeCommandWithoutModel_Invoked_ReturnsCleanError`,
`SpeechCli_RecognizeCommandWithUnknownModel_Invoked_ReturnsCleanError`,
`SpeechCli_RecognizeCommandWithWrongRoleModel_Invoked_ReturnsCleanError`,
`SpeechCli_RecognizeCommandWithNotDownloadedModel_Invoked_ReturnsCleanErrorWithDownloadHint`,
`SpeechCli_RecognizeCommandWithConflictingInputSources_Invoked_ReturnsCleanError`,
`SpeechCli_RecognizeCommandWithRealSttModel_Invoked_ProducesNonEmptyRecognizedText`,
`Program_Run_WithRecognizeCommand_DoesNotThrowNotImplemented`

**Scenario/Expected**: A missing `--stt-model`, an unsupported argument, or a value-less flag are all
rejected with `ArgumentException`; `--input`/`--mic` each parse correctly; supplying both
`--input` and `--mic`, or neither, throws `ArgumentException` before the model id is even looked
up; an unknown model id, a wrong-role model id, and a not-yet-downloaded model id are each
rejected with a distinct, actionable `ArgumentException` message (suggesting `list-models`,
`list-models --role stt`, and `download <modelId>` respectively); a successful run disposes the
fake engine and session exactly once each, in declaration order (session before engine); every
error path is proven both in-process and, cleanly and non-zero-exit, against the built tool; a
real, downloaded recognition model recognizing a real WAV fixture produces non-empty text;
`recognize` no longer throws `NotImplementedException` when dispatched.

**Requirement coverage**: `SpeechCli-RecognitionCommands-Recognize`.

#### `--stt-param` Validation Reuse

**Tests**: `RecognizeCommand_ParseArguments_RepeatedParamFlags_AccumulatesInOrder`,
`RecognizeCommand_Run_ValidParam_ForwardsToCreateRecognizerEngine`,
`RecognizeCommand_Run_InvalidParam_ThrowsArgumentException`

**Scenario/Expected**: Repeated `--stt-param` flags accumulate in the order given and are forwarded,
fully resolved via the shared `ParameterBagParser`, to `CreateRecognizerEngineAsync`'s
`parameterValues` argument; an invalid value for a declared parameter is rejected before any
engine is loaded. Full `ParameterBagParser` scenario coverage (numeric range/integer checks,
choice matching, boolean parsing, unrecognized-key rejection) already lives in
`SpeechCli-SynthesisCommands-ParamValidation`'s own tests and is not duplicated here.

**Requirement coverage**: `SpeechCli-RecognitionCommands-ParamValidation`.

#### File-Input Explicit-Drain Flow

**Tests**: `RecognizeCommand_Run_FileInput_StartsAndStopsRecognizerViaExplicitDrain`,
`RecognizeCommand_Run_FileInput_PassesWavFileCaptureDeviceToCreateSession`

**Scenario/Expected**: `--input <wav-path>` constructs a real `WavFileAudioCaptureDevice` over
that path and passes it into `engine.CreateSessionAsync`; a fake session whose `StartAsync`
synchronously drives that real device's own delivery to completion (simulating a real session's
documented file-mode contract), followed by `RecognizeCommand`'s own explicit drain
`StopAsync` call, results in `StartAsync`/`StopAsync`/`DisposeAsync` each being called exactly
once, with no `SilenceTimeoutRecognizerSession` wrapper and no added wait by the command in file
mode.

**Requirement coverage**: `SpeechCli-RecognitionCommands-FileInputEofDrivenStop`.

#### Silence Timeout (`SilenceTimeoutRecognizerSession`)

**Tests**: `RecognizeCommand_ParseArguments_SilenceTimeoutFlag_ParsesSeconds`,
`RecognizeCommand_ParseArguments_NonPositiveSilenceTimeout_ThrowsArgumentException`,
`RecognizeCommand_ParseArguments_MalformedSilenceTimeout_ThrowsArgumentException`,
`SilenceTimeoutRecognizerSession_Construct_NullSession_ThrowsArgumentNullException`,
`SilenceTimeoutRecognizerSession_Construct_NonPositiveIdleTimeout_ThrowsArgumentOutOfRangeException`,
`SilenceTimeoutRecognizerSession_Construct_NullTimeProvider_DoesNotThrow`,
`SilenceTimeoutRecognizerSession_GetResultsAsync_SecondResultReceived_StaysOnIdleTimeout`,
`SilenceTimeoutRecognizerSession_GetResultsAsync_TimeoutAfterResult_StopsSessionAndRaisesTimedOutOnce`,
`SilenceTimeoutRecognizerSession_GetResultsAsync_CancellationRequested_PropagatesOperationCanceledException`

**Scenario/Expected**: `--silence-timeout` parses a positive number of seconds, rejecting zero,
negative, or malformed values; a `null` wrapped session or a non-positive idle timeout is rejected
at construction, while a `null` `TimeProvider` does not throw (defaulting to `TimeProvider.System`);
a second yielded result keeps re-arming with the idle timeout rather than reverting to the start
timeout; the idle timeout elapsing after a result has already been yielded calls
`_session.StopAsync` exactly once and raises `TimedOut` exactly once; a canceled
`CancellationToken` passed to `GetResultsAsync` propagates `OperationCanceledException` rather
than being silently swallowed.

**Requirement coverage**: `SpeechCli-RecognitionCommands-SilenceTimeout`.

#### Start Timeout (Two-Phase Idle Window)

**Tests**: `RecognizeCommand_ParseArguments_StartTimeoutFlag_ParsesSeconds`,
`RecognizeCommand_ParseArguments_StartTimeoutOmitted_DefaultsToNull`,
`RecognizeCommand_ParseArguments_NonPositiveStartTimeout_ThrowsArgumentException`,
`RecognizeCommand_ParseArguments_MalformedStartTimeout_ThrowsArgumentException`,
`SilenceTimeoutRecognizerSession_Construct_NonPositiveStartTimeout_ThrowsArgumentOutOfRangeException`,
`SilenceTimeoutRecognizerSession_GetResultsAsync_StartTimeoutOmitted_ArmsTimerWithIdleTimeout`,
`SilenceTimeoutRecognizerSession_GetResultsAsync_StartTimeoutGiven_ArmsTimerWithStartTimeout`,
`SilenceTimeoutRecognizerSession_GetResultsAsync_FirstResultReceived_ReArmsWithIdleTimeoutNotStartTimeout`,
`SilenceTimeoutRecognizerSession_GetResultsAsync_TimeoutBeforeAnyResult_StopsSessionAndRaisesTimedOutOnce`

**Scenario/Expected**: `--start-timeout` parses a positive number of seconds, rejecting zero,
negative, or malformed values; parsing alone leaves `StartTimeoutSeconds` `null` when the flag is
omitted (no default is applied at parse time); a non-positive `startTimeout` is rejected at
construction exactly like a non-positive `idleTimeout`; omitting `startTimeout` arms the very
first race with `idleTimeout` (today's exact behavior, preserved); supplying a distinct
`startTimeout` arms the first race with that value instead; the first yielded result (partial or
final) re-arms subsequent races with `idleTimeout`, not `startTimeout`; the idle timeout elapsing
before any result has been yielded calls `_session.StopAsync` and raises `TimedOut`, proving
`startTimeout` genuinely governs the pre-first-result window rather than only being recorded.

**Requirement coverage**: `SpeechCli-RecognitionCommands-StartTimeout`.

#### `--interim`/`--final-only`/`--output-text` Filtering and Writing

**Tests**: `RecognizeCommand_ParseArguments_InterimAndFinalOnlyFlags_ParseTrue`,
`RecognizeCommand_ParseArguments_OutputFlag_ParsesOutputPath`,
`RecognizeCommand_Run_InterimAndFinalOnlyBothGiven_ThrowsArgumentException`,
`RecognizeCommand_Run_DefaultVerbosity_PrintsBothInterimAndFinal`,
`RecognizeCommand_Run_ShrinkingInterimSequence_DoesNotLeaveStaleCharacters`,
`RecognizeCommand_Run_FinalOnly_SuppressesInterimConsoleOutput`,
`RecognizeCommand_Run_Interim_SuppressesFinalSettleConsoleOutput`,
`RecognizeCommand_Run_Output_WritesOnlyFinalResultsOverwritingPriorContent`

**Scenario/Expected**: `--interim`/`--final-only` each parse as flags; supplying both throws
`ArgumentException`; by default both an interim and a final result are printed to the console;
`--final-only` suppresses interim console output while still printing final results;
`--interim` suppresses the final "settle" console output while still printing interim results;
a hypothesis that later shrinks still pads over every stale trailing character from the longer
prior write before repositioning the cursor; `--output-text <path>` writes only final results,
one per line, to the file - never interim results - overwriting any prior file content, regardless
of the console verbosity flags in effect.

**Requirement coverage**: `SpeechCli-RecognitionCommands-VerbosityAndOutput`.

#### Capture Device Dispatch

**Tests**: `RecognizeCommand_Run_UnknownDevice_ThrowsArgumentException`,
`RecognizeCommand_Run_NoCaptureDeviceAvailable_ThrowsInvalidOperationException`

**Scenario/Expected**: Requesting an unrecognized `--capture-device` name in `--mic` mode throws
`ArgumentException` before any device is created; an unavailable resolved real capture device
throws `InvalidOperationException` suggesting `--input` as an alternative.

**Requirement coverage**: `SpeechCli-RecognitionCommands-DeviceDispatch`.

#### ICliModelCatalog Seam Extension (Real Catalog)

**Tests**: `SpeechModelCatalogAdapter_GetAudioFormat_SynthesisRoleModel_ThrowsArgumentException`,
`SpeechModelCatalogAdapter_GetAudioFormat_NullDescriptor_ThrowsArgumentNullException`,
`SpeechModelCatalogAdapter_CreateRecognizerEngineAsync_SynthesisRoleModel_ThrowsArgumentException`,
`SpeechModelCatalogAdapter_CreateRecognizerEngineAsync_NullDescriptor_ThrowsArgumentNullException`

**Scenario/Expected**: Against a real, temp-directory-rooted `SpeechModelCatalogAdapter`, calling
either new seam member with a real, compiled-in synthesis-role model's descriptor throws
`ArgumentException` naming the `descriptor` parameter, proving the adapter's `is
IRecognitionModel` cast genuinely rejects a non-recognition model rather than only being exercised
through a fake; a `null` descriptor is rejected with `ArgumentNullException` before the cast is
even attempted.

**Requirement coverage**: `SpeechCli-RecognitionCommands-CatalogSeamExtension`.

#### Null Guards

**Tests**: `RecognizeCommand_Run_NullContext_ThrowsArgumentNullException`,
`RecognizeCommand_Run_NullCatalog_ThrowsArgumentNullException`,
`RecognizeCommand_Run_NullFactory_ThrowsArgumentNullException`,
`RecognizeCommand_ParseArguments_NullArgs_ThrowsArgumentNullException`

**Scenario/Expected**: Every entry point rejects a `null` context, catalog, factory, or argument
list immediately with `ArgumentNullException`, proving no command silently proceeds with a
missing dependency.

**Requirement coverage**: `SpeechCli-RecognitionCommands-NullGuards`.

### Requirements Coverage

- **`SpeechCli-RecognitionCommands-Recognize`**: see _RecognizeCommand — Input Source, Model
  Resolution, and Argument Parsing_ above
- **`SpeechCli-RecognitionCommands-ParamValidation`**: see _`--stt-param` Validation Reuse_ above
- **`SpeechCli-RecognitionCommands-FileInputEofDrivenStop`**: see _File-Input Explicit-Drain
  Flow_ above
- **`SpeechCli-RecognitionCommands-SilenceTimeout`**: see _Silence Timeout
  (`SilenceTimeoutRecognizerSession`)_ above
- **`SpeechCli-RecognitionCommands-StartTimeout`**: see _Start Timeout (Two-Phase Idle Window)_
  above
- **`SpeechCli-RecognitionCommands-VerbosityAndOutput`**: see _`--interim`/`--final-only`/
  `--output-text` Filtering and Writing_ above
- **`SpeechCli-RecognitionCommands-DeviceDispatch`**: see _Capture Device Dispatch_ above
- **`SpeechCli-RecognitionCommands-CatalogSeamExtension`**: see _ICliModelCatalog Seam Extension
  (Real Catalog)_ above
- **`SpeechCli-RecognitionCommands-NullGuards`**: see _Null Guards_ above

### Acceptance Criteria

A RecognitionCommandSubsystem test run passes when: `recognize` correctly enforces input-source
mutual exclusion and reports actionable model-resolution errors; `--stt-param` values are forwarded
through the reused `ParameterBagParser`; file-input mode drives a real capture device to
completion via `StartAsync` followed by an explicit drain `StopAsync`, with no added wait;
silence-timeout logic races and times out deterministically against a fake time provider;
`--start-timeout` correctly arms the two-phase idle window (start-timeout before the first
result, silence-timeout thereafter) deterministically against a fake time provider;
`--interim`/`--final-only`/`--output-text` each filter/write exactly as specified; an unknown/
unavailable capture device is rejected cleanly; the two new `ICliModelCatalog` seam members
correctly reject a non-recognition model against a real catalog; every entry point rejects a
missing required dependency; and, when a real downloaded speech-to-text model is present, a real
end-to-end `recognize --input` run against a real WAV fixture produces non-empty recognized text.

## Manual / Build-Time Verification

A real, downloaded speech-to-text model was available in the environment this pass was
implemented in, so `SpeechCli_RecognizeCommandWithRealSttModel_Invoked_ProducesNonEmptyRecognizedText`
is an automated test, not a manual verification gap - unlike the SynthesisCommandSubsystem pass's
TTS model, which required manual verification because no TTS model was cached. If this repository
is later built in an environment with no downloaded recognition model at all (for example, a
network-isolated CI runner that has never run `download`), that one test skips cleanly (asserting
nothing, rather than failing or fabricating a pass) and the following becomes a manual/local
verification step instead:

- `dotnet run --project src/DemaConsulting.Speech.Cli -- recognize --stt-model <id> --input
  test/DemaConsulting.Speech.Tests/TestData/crossing-the-bar-16k-mono.wav`, run against a real,
  downloaded recognition model, prints non-empty recognized text and exits cleanly
- `recognize --stt-model <id> --mic --silence-timeout <seconds>`, run on a machine with a real
  microphone, prints live interim/final results while speaking and ends the session automatically
  after the configured idle window once speech stops
- `recognize --stt-model <id> --mic --silence-timeout <seconds> --start-timeout <seconds>`, run on a
  machine with a real microphone, waits up to the `--start-timeout` grace period for speech to
  begin (verified by staying silent past `--silence-timeout`'s own, shorter value without the
  session ending early) and then, once speech has started, ends the session automatically after
  `--silence-timeout`'s idle window elapses with no further result
