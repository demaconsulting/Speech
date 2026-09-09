## SpeechCli RecognitionCommandSubsystem Verification

### Verification Approach

The RecognitionCommandSubsystem is verified through deterministic unit tests against a
hand-written `FakeSpeechRecognizer` (recording `Start`/`Stop`/`Dispose` call counts, with a
settable `IsAvailable` and an `OnStart` callback letting a test simulate a recognizer's own
synchronous `Start()`-drives-the-device contract) and `FakeCliModelCatalog`'s two new delegate
overrides (`GetAudioFormatOverride`/`CreateRecognizerOverride`), reused from
`ModelCommandsSubsystem`'s own tests. `SilenceTimeoutRecognizerSession` is verified entirely in
isolation against a hand-written `FakeTimeProvider`/`FakeTimer` pair whose callback a test invokes
directly, so every idle/reset/timeout scenario runs deterministically with no real wall-clock
delay. The two new `ICliModelCatalog` seam members (`GetAudioFormat`/`CreateRecognizer`) are
additionally verified against a **real** `SpeechModelCatalogAdapter`, proving the production `is
IRecognitionModel` cast genuinely throws for a real, compiled-in synthesis-role model, in
`SpeechModelCatalogAdapterTests.cs`. Out-of-process integration tests in `IntegrationTests.cs`
invoke the built tool as a child process for the model-resolution and input-source error paths
that matter most from an operator's perspective.

The file-input EOF-driven-stop flow is verified against a **real** `WavFileAudioCaptureDevice`
constructed over a minimal, self-generated, valid mono/16-bit-PCM WAV file (written directly by
the test, not a shared binary fixture), paired with `FakeSpeechRecognizer`'s `OnStart` callback
driving that real device's own `Start()` - proving the reentrant `Stop()`-from-`EndOfFileReached`
wiring genuinely round-trips through the real device rather than only being exercised against a
fully-faked recognizer/device pair.

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
- **Isolation**: Every unit test constructs its own fake catalog/recognizer/time provider/audio
  probes; `SpeechModelCatalogAdapterTests` uses a freshly created, uniquely named temporary
  directory as its model-store root, deleted afterward via `IDisposable`
- **Test doubles**: `FakeSpeechRecognizer` (hand-written `ISpeechRecognizer` fake),
  `FakeTimeProvider`/`FakeTimer` (hand-written `TimeProvider`/`ITimer` fakes with a directly
  invocable callback), `FakeCliModelCatalog` (extended with `GetAudioFormatOverride`/
  `CreateRecognizerOverride`), `FakeAudioCaptureDeviceProbe` (reused from
  `DeviceCommandsSubsystem`'s own tests)

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
`RecognizeCommand_Run_Success_DisposesRecognizerOnce`,
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
fake recognizer exactly once; every error path is proven both in-process and, cleanly and
non-zero-exit, against the built tool; a real, downloaded recognition model recognizing a real WAV
fixture produces non-empty text; `recognize` no longer throws `NotImplementedException` when
dispatched - the last of all 10 subcommands to reach that state.

**Requirement coverage**: `SpeechCli-RecognitionCommands-Recognize`.

#### `--stt-param` Validation Reuse

**Tests**: `RecognizeCommand_ParseArguments_RepeatedParamFlags_AccumulatesInOrder`,
`RecognizeCommand_Run_ValidParam_ForwardsToCreateRecognizer`,
`RecognizeCommand_Run_InvalidParam_ThrowsArgumentException`

**Scenario/Expected**: Repeated `--stt-param` flags accumulate in the order given and are forwarded,
fully resolved via the unmodified `ParameterBagParser`, to `CreateRecognizer`'s `parameterValues`
argument; an invalid value for a declared parameter is rejected before any recognizer is created.
Full `ParameterBagParser` scenario coverage (numeric range/integer checks, choice matching,
boolean parsing, unrecognized-key rejection) already lives in
`SpeechCli-SynthesisCommands-ParamValidation`'s own tests and is not duplicated here.

**Requirement coverage**: `SpeechCli-RecognitionCommands-ParamValidation`.

#### File-Input EOF-Driven Stop Flow

**Tests**: `RecognizeCommand_Run_FileInput_StartsAndStopsRecognizerViaEndOfFile`,
`RecognizeCommand_Run_FileInput_PassesWavFileCaptureDeviceToCreateRecognizer`

**Scenario/Expected**: `--input <wav-path>` constructs a real `WavFileAudioCaptureDevice` over
that path and passes it into `CreateRecognizer`; a fake recognizer whose `Start()` drives that
real device's own `Start()` to completion (simulating a real recognizer's documented contract)
results in `Start`/`Stop`/`Dispose` each being called exactly once, with `Stop()` triggered
reentrantly by the device's own `EndOfFileReached` event and no explicit wait added by the
command.

**Requirement coverage**: `SpeechCli-RecognitionCommands-FileInputEofDrivenStop`.

#### Silence Timeout (`SilenceTimeoutRecognizerSession`)

**Tests**: `RecognizeCommand_ParseArguments_SilenceTimeoutFlag_ParsesSeconds`,
`RecognizeCommand_ParseArguments_NonPositiveSilenceTimeout_ThrowsArgumentException`,
`RecognizeCommand_ParseArguments_MalformedSilenceTimeout_ThrowsArgumentException`,
`SilenceTimeoutRecognizerSession_Construct_ArmsTimerWithGivenTimeout`,
`SilenceTimeoutRecognizerSession_PartialResultReceived_ResetsIdleTimer`,
`SilenceTimeoutRecognizerSession_FinalResultReceived_ResetsIdleTimer`,
`SilenceTimeoutRecognizerSession_IdleTimerFires_StopsRecognizerAndRaisesTimedOut`,
`SilenceTimeoutRecognizerSession_ResetThenFire_StopsOnlyOnActualFire`,
`SilenceTimeoutRecognizerSession_Dispose_UnsubscribesAndDisposesTimer`,
`SilenceTimeoutRecognizerSession_FireAfterDispose_DoesNotCallStopOrRaiseTimedOut`,
`SilenceTimeoutRecognizerSession_Construct_NullRecognizer_ThrowsArgumentNullException`,
`SilenceTimeoutRecognizerSession_Construct_NonPositiveTimeout_ThrowsArgumentOutOfRangeException`,
`SilenceTimeoutRecognizerSession_Construct_NullTimeProvider_UsesSystemTimeProvider`

**Scenario/Expected**: `--silence-timeout` parses a positive number of seconds, rejecting zero,
negative, or malformed values; constructing a session arms its idle timer once with the given
timeout; a partial or final result re-arms the timer; the timer firing with no reset calls
`Stop()` exactly once and raises `TimedOut`; `Dispose()` unsubscribes and disposes the timer,
is idempotent, and a subsequent result never re-arms the disposed timer; a timer fire after
`Dispose()` never calls `Stop()` or raises `TimedOut` on the torn-down session; a `null`
recognizer or a non-positive timeout is rejected at construction; a `null` `TimeProvider`
defaults to `TimeProvider.System` without throwing.

**Requirement coverage**: `SpeechCli-RecognitionCommands-SilenceTimeout`.

#### Start Timeout (Two-Phase Idle Window)

**Tests**: `RecognizeCommand_ParseArguments_StartTimeoutFlag_ParsesSeconds`,
`RecognizeCommand_ParseArguments_StartTimeoutOmitted_DefaultsToNull`,
`RecognizeCommand_ParseArguments_NonPositiveStartTimeout_ThrowsArgumentException`,
`RecognizeCommand_ParseArguments_MalformedStartTimeout_ThrowsArgumentException`,
`SilenceTimeoutRecognizerSession_Construct_StartTimeoutOmitted_ArmsTimerWithSilenceTimeout`,
`SilenceTimeoutRecognizerSession_Construct_StartTimeoutGiven_ArmsTimerWithStartTimeout`,
`SilenceTimeoutRecognizerSession_FirstResultReceived_ReArmsWithSilenceTimeoutNotStartTimeout`,
`SilenceTimeoutRecognizerSession_SecondResultReceived_StaysOnSilenceTimeout`,
`SilenceTimeoutRecognizerSession_IdleTimerFiresBeforeFirstResult_StopsRecognizerAndRaisesTimedOut`,
`SilenceTimeoutRecognizerSession_Construct_NonPositiveStartTimeout_ThrowsArgumentOutOfRangeException`

**Scenario/Expected**: `--start-timeout` parses a positive number of seconds, rejecting zero,
negative, or malformed values; parsing alone leaves `StartTimeoutSeconds` `null` when the flag is
omitted (no default is applied at parse time); constructing a session with `startTimeout` omitted
arms its idle timer with the same value as `idleTimeout` (today's exact behavior, preserved);
constructing a session with a distinct `startTimeout` value arms the timer with that value, not
`idleTimeout`; the first `ResultReceived` event (partial or final) re-arms the timer with
`idleTimeout`, not `startTimeout`; a second result stays re-armed with `idleTimeout`; the idle
timer firing before any result has arrived calls `Stop()` and raises `TimedOut`, proving
`startTimeout` genuinely governs the pre-first-result window rather than only being recorded; a
non-positive `startTimeout` is rejected at construction exactly like a non-positive `idleTimeout`.

**Requirement coverage**: `SpeechCli-RecognitionCommands-StartTimeout`.

#### `--interim`/`--final-only`/`--output-text` Filtering and Writing

**Tests**: `RecognizeCommand_ParseArguments_InterimAndFinalOnlyFlags_ParseTrue`,
`RecognizeCommand_ParseArguments_OutputFlag_ParsesOutputPath`,
`RecognizeCommand_Run_InterimAndFinalOnlyBothGiven_ThrowsArgumentException`,
`RecognizeCommand_Run_DefaultVerbosity_PrintsBothInterimAndFinal`,
`RecognizeCommand_Run_FinalOnly_SuppressesInterimConsoleOutput`,
`RecognizeCommand_Run_Interim_SuppressesFinalSettleConsoleOutput`,
`RecognizeCommand_Run_Output_WritesOnlyFinalResultsOverwritingPriorContent`

**Scenario/Expected**: `--interim`/`--final-only` each parse as flags; supplying both throws
`ArgumentException`; by default both an interim and a final result are printed to the console;
`--final-only` suppresses interim console output while still printing final results;
`--interim` suppresses the final "settle" console output while still printing interim results;
`--output-text <path>` writes only final results, one per line, to the file - never interim results -
overwriting any prior file content, regardless of the console verbosity flags in effect.

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
`SpeechModelCatalogAdapter_CreateRecognizer_SynthesisRoleModel_ThrowsArgumentException`,
`SpeechModelCatalogAdapter_CreateRecognizer_NullDescriptor_ThrowsArgumentNullException`,
`SpeechModelCatalogAdapter_CreateRecognizer_NullCaptureDevice_ThrowsArgumentNullException`

**Scenario/Expected**: Against a real, temp-directory-rooted `SpeechModelCatalogAdapter`, calling
either new seam member with a real, compiled-in synthesis-role model's descriptor throws
`ArgumentException` naming the `descriptor` parameter, proving the adapter's `is
IRecognitionModel` cast genuinely rejects a non-recognition model rather than only being exercised
through a fake; a `null` descriptor or capture device is rejected with `ArgumentNullException`
before the cast is even attempted.

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
- **`SpeechCli-RecognitionCommands-FileInputEofDrivenStop`**: see _File-Input EOF-Driven Stop
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
completion and stops the recognizer reentrantly via `EndOfFileReached` with no added wait;
silence-timeout logic resets and fires deterministically against a fake time provider;
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
