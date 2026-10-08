## RecognitionSubsystem Verification

### Verification Approach

The RecognitionSubsystem is verified entirely through deterministic unit tests that substitute a
fake `IRecognitionBackend`/`IRecognitionBackendFactory` pair behind the subsystem's internal
backend seam and an NSubstitute `IAudioCaptureDevice` in place of real hardware. This makes
composition decisions, audio-format conversion, the two-thread streaming pipeline, engine
exclusivity, session lifecycle, result ordering/backpressure, fault containment, and honest
degradation fully testable without a downloaded speech model, a microphone, or the
platform-specific native speech-inference runtime.

Determinism is structural rather than timing-based: `StopAsync` completes its internal queue and
awaits its pump task, so every result derived from a frame raised before the call has been
delivered or accounted for by the time it returns; cancellation/abandon-timeout behavior is
verified with an injectable abandon-timeout override rather than real multi-second waits.

Automated coverage **does not** include recognizing real speech. Proving that real audio from a
real microphone produces correct text through a real model requires both a downloaded production
model (which this phase deliberately does not ship) and audio hardware, so it remains a
manual/local verification activity. The real, native-backed `IRecognitionBackend`
implementation, `SherpaOnnxRecognitionEngine`, is no longer part of this subsystem: it ships in the
sibling SpeechSherpa library, and its bookkeeping and accuracy against real installed models (when
present) are verified there by `SherpaOnnxRecognitionEngineTests`/
`SherpaOnnxRecognitionEngineAccuracyTests` (see _SpeechSherpa RecognitionSubsystem
Verification_).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, no downloaded model, no native speech-inference runtime,
  and no physical audio hardware
- **Test doubles**: A fake `IRecognitionBackend`/`IRecognitionBackendFactory` pair, NSubstitute
  capture devices and diagnostics sinks, a fake recognition model, and a parameter-capturing fake
  recognition model used only to prove parameter-values pass-through
- **Isolation**: Composition tests create and delete their own scratch installed-model directory

### Acceptance Criteria

A RecognitionSubsystem test run passes when:

- Composition returns a real engine only when the model is installed, declares the recognition
  role, and the backend loads; `LoadAsync`'s returned task never faults for an ordinary
  unavailable machine state
- Every other composition outcome returns the honest unavailable engine without faulting the
  returned task
- An optional `parameterValues` bag supplied by the caller reaches the recognition model's own
  engine-configuration logic unchanged, and does not change behavior for a model that declares no
  parameters
- A supplied `parameterValues` key naming a parameter not declared by the requested model is
  silently ignored (with only an `Info` diagnostic reported) and composition still succeeds; a
  supplied value for a parameter the model _does_ declare that fails that parameter's own
  validation (wrong CLR type, out-of-range or non-integral for a `NumericParameter`, an invalid
  option for a `ChoiceParameter`, a non-`bool` for a `BooleanParameter`) faults the returned task
  with `ArgumentException`, before any installed/role/backend-load check runs
- `CreateSessionAsync` returns exactly one live session per engine at a time, throwing
  `RecognitionEngineBusyException` for a concurrent attempt, and permits a new session once the
  prior one is fully disposed
- Captured audio is downmixed and resampled to the model's declared `AudioFormat`, with
  above-target-Nyquist energy attenuated before downsampling decimation
- Every recognition result is delivered through `GetResultsAsync`, in order, with its
  provisional/final flag preserved, subject to the documented backpressure policy (coalesced
  provisionals, byte-capped finals)
- The session's `RecognitionSessionState` machine only ever makes forward-only, documented
  transitions, raising `StateChanged` for each one
- `StartAsync`/`StopAsync`/`DisposeAsync` behave idempotently, drain queued audio, and release
  backend/lease resources
- `StopAsync`/`DisposeAsync` flush trailing audio the backend had accepted but not yet decoded -
  the tail of an utterance released with no trailing silence - as one last final result before
  resetting the backend for the next session, so no accepted audio is silently lost
- Backend faults, a lost capture device, and throwing host handlers are reported/contained rather
  than propagated uncontrolled, and surface to an active `GetResultsAsync` consumer as
  `RecognitionSessionFaultedException`
- A non-cooperative native call is abandoned after its configured timeout rather than blocking a
  caller forever, with the abandonment reported through diagnostics
- The unavailable engine and unavailable session both stay honest and safe to hold, subscribe to,
  and dispose
- The automated verification boundary remains honest about the absence of real-speech coverage

### Test Scenarios

#### Composition: Real Engine for an Installed Model

**Tests**: `SpeechRecognizerFactory_LoadAsync_ModelInstalled_ReturnsRealEngine`,
`SpeechRecognizerFactory_LoadAsync_WithStoreModelInstalled_ReturnsRealEngine`,
`SpeechRecognizerFactory_LoadAsync_WithCatalogModelInstalled_ReturnsRealEngine`

Verifies that an installed recognition model composes a real `SpeechRecognizerEngine`
wired to the injected backend factory, with the installed-model directory passed through
unchanged, whether that directory is supplied directly as a `string`, resolved from a
`SpeechModelStore`, or resolved from a `SpeechModelCatalog`'s own store.

#### Composition: Honest Fallback for Every Unavailable State

**Tests**: `SpeechRecognizerFactory_LoadAsync_ModelNotInstalled_ReturnsUnavailableEngine`,
`SpeechRecognizerFactory_LoadAsync_ModelRoleIsNotRecognition_ReturnsUnavailableEngine`,
`SpeechRecognizerFactory_LoadAsync_EngineLoadFails_ReturnsUnavailableEngineAndDoesNotFaultTask`,
`SpeechRecognizerFactory_LoadAsync_WithStoreModelNotInstalled_ReturnsUnavailableEngine`,
`SpeechRecognizerFactory_LoadAsync_WithCatalogModelNotInstalled_ReturnsUnavailableEngine`

Verifies that a missing model, a wrong-role model, and a failed backend load all degrade to the
shared unavailable engine without faulting the returned task, and that no backend is loaded when
an earlier check already failed.

#### Composition: Null Arguments and Cancellation Are Programming/Caller Errors

**Tests**: `SpeechRecognizerFactory_LoadAsync_NullModel_FaultsWithArgumentNullException`,
`SpeechRecognizerFactory_LoadAsync_WithStoreNullModel_FaultsWithArgumentNullException`,
`SpeechRecognizerFactory_LoadAsync_WithStoreNullStore_FaultsWithArgumentNullException`,
`SpeechRecognizerFactory_LoadAsync_WithCatalogNullModel_FaultsWithArgumentNullException`,
`SpeechRecognizerFactory_LoadAsync_WithCatalogNullCatalog_FaultsWithArgumentNullException`,
`SpeechRecognizerFactory_LoadAsync_CancelledToken_FaultsWithOperationCanceledException`

Verifies that a null model, store, or catalog faults the returned task with
`ArgumentNullException`, and that a cancellation token already cancelled before loading completes
faults it with `OperationCanceledException`, distinguishing both from an ordinary machine state.

#### Composition: Parameter Value Bag Forwarding

**Tests**: `SpeechRecognizerFactory_LoadAsync_ParameterValuesSupplied_ReachesModelCreateBackend`,
`SpeechRecognizerFactory_LoadAsync_WithStoreParameterValuesSupplied_ReachesModelCreateBackend`,
`SpeechRecognizerFactory_LoadAsync_WithCatalogParameterValuesSupplied_ReachesModelCreateBackend`

Verifies that an optional `parameterValues` bag supplied by the caller (for example, a selected
recognition language built from a declared `ChoiceParameter`) genuinely reaches a model's own
two-argument `IRecognitionModel.CreateBackend` override rather than merely reaching the
backend factory.

#### Composition: Parameter Value Validation

**Tests**: `SpeechRecognizerFactory_LoadAsync_UnrecognizedParameterId_ComposesAndReportsInfo`,
`SpeechRecognizerFactory_LoadAsync_RecognizedNumericParameterOutOfRange_FaultsWithArgumentException`,
`SpeechRecognizerFactory_LoadAsync_RecognizedNumericParameterWrongType_FaultsWithArgumentException`,
`SpeechRecognizerFactory_LoadAsync_RecognizedChoiceParameterInvalidOption_FaultsWithArgumentException`,
`SpeechRecognizerFactory_LoadAsync_RecognizedBooleanParameterWrongType_FaultsWithArgumentException`

Verifies the deliberate, breaking-change split introduced for this behavior: a supplied
`parameterValues` key naming a parameter the model does not declare still composes a real engine
and reports only an `Info` diagnostic, never faulting the task (preserving cross-model
compatibility); a supplied value for a parameter the model _does_ declare, but that is invalid for
it, faults the returned task with `ArgumentException` - before any installed/role/backend-load
check runs - naming the parameter id, the model id, and the specific reason the value is invalid.

#### Engine Exclusivity and Lease Behavior

**Tests**: `SpeechRecognizerEngine_CreateSessionAsync_NoActiveSession_ReturnsSession`,
`SpeechRecognizerEngine_CreateSessionAsync_SessionAlreadyLeased_ThrowsRecognitionEngineBusyException`,
`SpeechRecognizerEngine_CreateSessionAsync_PriorSessionDisposing_ThrowsRecognitionEngineBusyException`,
`SpeechRecognizerEngine_CreateSessionAsync_AfterPriorSessionFullyDisposed_ReturnsNewSession`,
`SpeechRecognizerEngine_CreateSessionAsync_NullDevice_ThrowsArgumentNullException`,
`SpeechRecognizerEngine_DisposeAsync_WithActiveSession_DisposesSessionFirst`

Verifies that the engine's single lease permits exactly one live session at a time, fails fast
(no queueing) with `RecognitionEngineBusyException` for a concurrent attempt - including while the
prior session is still mid-`DisposeAsync` - and is released only once that prior session has fully
completed disposal, after which a new session can be created.

#### Session Lifecycle: State Machine Transitions

**Tests**: `RecognitionSession_StartAsync_FromCreated_TransitionsToRunning`,
`RecognitionSession_StartAsync_FromStopped_ThrowsInvalidOperationException`,
`RecognitionSession_StateChanged_EmitsEveryTransitionInOrder`,
`RecognitionSession_DeviceLostMidSession_TransitionsToFaulted`

Verifies the forward-only `RecognitionSessionState` machine: `StartAsync` transitions `Created ->
Starting -> Running`, a session is single-use (starting again after `Stopped` throws
`InvalidOperationException`), `StateChanged` raises every transition in order, and a capture
device going unavailable mid-session transitions the session to `Faulted`.

#### Session Lifecycle: Stop, Dispose, and Draining

**Tests**: `RecognitionSession_StopAsync_FlushesTrailingResultsBeforeCompleting`,
`RecognitionSession_StopAsync_CalledConcurrentlyTwice_BothCompleteOnceStopped`,
`RecognitionSession_StopAsync_BackendResetFails_CompletesAndReportsFault`

Verifies that `StopAsync` flushes trailing audio the backend had accepted but not yet decoded as
one last final result before completing, that two concurrent `StopAsync` callers both complete
only once the session has actually stopped, and that a backend reset failure during `StopAsync` is
reported rather than thrown while teardown still completes.

#### Session Pipeline: Capture Format Conversion and Text Normalization

**Tests**: `RecognitionSession_FrameCaptured_StereoAtModelRate_FeedsDownmixedMonoToBackend`,
`RecognitionSession_FrameCaptured_FinalResult_AppliesModelNormalizeTextWithIsFinalTrue`

Verifies that a stereo block reaches the backend as downmixed mono at the model's declared rate,
and that a final result's text has passed through the owning model's
`IRecognitionModel.NormalizeText(text, isFinal: true)` before being buffered.

#### Session Pipeline: Result Delivery and Backpressure

**Tests**: `RecognitionSession_GetResultsAsync_CalledConcurrently_ThrowsInvalidOperationException`,
`RecognitionSession_GetResultsAsync_CancelledToken_EndsEnumerationWithoutStoppingSession`,
`RecognitionSession_GetResultsAsync_SessionFaulted_ThrowsRecognitionSessionFaultedException`,
`RecognitionSession_GetResultsAsync_SlowConsumer_CoalescesProvisionalResults`,
`RecognitionSession_GetResultsAsync_SlowConsumer_NeverDropsFinalResultsUnderByteCap`

Verifies that `GetResultsAsync` is single-consumer (a concurrent second enumeration throws
`InvalidOperationException`), that cancelling the consumer's token ends its enumeration without
stopping the session itself, that a faulted session surfaces
`RecognitionSessionFaultedException` from the active enumeration, that a slow consumer only ever
sees the latest coalesced provisional rather than a queue of stale ones, and that final results
are never dropped while the byte cap is not exceeded.

#### Cooperative-Cancel-Then-Abandon Policy

**Tests**: `DedicatedWorker_Run_CooperativeCancellation_CompletesPromptly`,
`DedicatedWorker_Run_NonCooperativeDelegate_AbandonsAfterTimeoutAndReportsDiagnostics`,
`DedicatedWorker_Run_UsesLongRunningTaskCreationOption`,
`RecognitionSession_NativeCallExceedsAbandonTimeout_TaskCompletesAndDiagnosticsReportsWarning`

Verifies that a delegate which observes cancellation promptly completes its task immediately, that
a delegate which does not observe cancellation is abandoned after the configured timeout with a
`Warning` diagnostic rather than blocking the caller forever, that the worker always runs its
delegate with `TaskCreationOptions.LongRunning`, and that this same abandon behavior is exercised
end-to-end through a session's pump thread via an injectable abandon timeout.

#### Audio Conversion: Downmix, Rate Conversion, and Boundaries

**Tests**: `AudioFrameResampler_DownmixToMono_StereoInput_AveragesChannelsPerFrame`,
`AudioFrameResampler_DownmixToMono_TrailingPartialFrame_DiscardsPartialFrame`,
`AudioFrameResampler_DownmixToMono_SingleChannel_ReturnsSamplesUnchanged`,
`AudioFrameResampler_DownmixToMono_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException`,
`AudioFrameResampler_DownmixToMono_DestinationOverload_StereoInput_WritesAveragedFrames`,
`AudioFrameResampler_DownmixToMono_DestinationOverload_DestinationTooShort_ThrowsArgumentException`,
`AudioFrameResampler_Resample_Downsampling_ProducesProportionallyFewerSamples`,
`AudioFrameResampler_Resample_Upsampling_LinearlyInterpolatesBetweenSamples`,
`AudioFrameResampler_Resample_SingleSample_ClampsToThatSample`,
`AudioFrameResampler_Resample_OutputRoundsToZeroSamples_ReturnsEmptyResult`,
`AudioFrameResampler_Resample_AboveTargetNyquistTone_IsAttenuated`,
`AudioFrameResampler_Resample_ShortInputDuringDownsampling_DoesNotThrow`,
`AudioFrameResampler_Convert_MonoAtTargetRate_ReturnsSamplesUnchanged`,
`AudioFrameResampler_Convert_MonoAtDifferentRate_ResamplesWithoutDownmix`,
`AudioFrameResampler_Convert_EmptyInput_ReturnsEmptyResult`,
`AudioFrameResampler_Convert_StereoAtHigherRate_DownmixesAndResamples`,
`AudioFrameResampler_Constructor_NonPositiveSourceRate_ThrowsArgumentOutOfRangeException`,
`AudioFrameResampler_Constructor_NonPositiveTargetRate_ThrowsArgumentOutOfRangeException`,
`AudioFrameResampler_Constructor_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException`

Verifies channel averaging, partial-frame discard, exact identity pass-through, the
destination-buffer downmix overload (including its undersized-buffer rejection), proportional
up/down conversion with linear interpolation, anti-aliased downsampling, short-input safety,
empty/single-sample/rounds-to-empty boundaries, the mono fast path that resamples without an
intermediate downmix, and rejection of non-positive rates and channel counts.

#### Unavailable Fallback: Honest Degradation

**Tests**: `UnavailableSpeechRecognizerEngine_IsAvailable_Read_ReturnsFalse`,
`UnavailableSpeechRecognizerEngine_CreateSessionAsync_Always_ReturnsUnavailableSession`,
`UnavailableSpeechRecognizerEngine_CreateSessionAsync_NullDevice_ThrowsArgumentNullException`,
`UnavailableSpeechRecognizerEngine_DisposeAsync_CalledTwice_DoesNotThrow`,
`UnavailableRecognitionSession_IsAvailable_Read_ReturnsFalse`,
`UnavailableRecognitionSession_StartAsync_Always_ThrowsSpeechRecognizerUnavailableException`,
`UnavailableRecognitionSession_StopAsync_Always_IsSafeNoOp`,
`UnavailableRecognitionSession_GetResultsAsync_Always_ThrowsSpeechRecognizerUnavailableException`,
`UnavailableRecognitionSession_DisposeAsync_CalledTwice_DoesNotThrow`,
`SpeechRecognizerUnavailableException_Constructor_WithMessage_ExposesMessage`,
`SpeechRecognizerUnavailableException_Constructor_WithInnerException_ExposesBoth`,
`SpeechRecognizerUnavailableException_Constructor_Default_HasNonEmptyMessage`

Verifies that both shared fallbacks stay honest and safe to hold, subscribe to, and dispose
repeatedly, that `UnavailableSpeechRecognizerEngine.CreateSessionAsync` always returns the shared
unavailable session rather than throwing, that operational misuse of the unavailable session
throws the documented exception, and that the exception type conforms to the standard
three-constructor pattern.
