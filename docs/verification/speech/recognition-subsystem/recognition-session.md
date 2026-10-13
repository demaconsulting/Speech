### RecognitionSession

#### Verification Approach

The streaming session is verified against its collaborators' test doubles. The Layer 3 engine,
recognition-backend seam, result buffer, dedicated worker, and audio-format converter each have
their own verification document; the session's whole purpose is to move audio from the converter
into the backend on the right thread, so it is exercised here with those units in place.

The backend seam is replaced by a deterministic fake that records the mono samples it was fed and
yields a scripted sequence of results, so no downloaded model and no platform-specific native
speech-inference binary is ever needed. The capture device is an NSubstitute
`IAudioCaptureDevice` whose `FrameCaptured` event the test raises directly, standing in for a
real audio callback. Diagnostics are an NSubstitute sink, so fault containment is verified by
observing what was reported rather than by asserting an absence of exceptions alone.

Threading is verified without timing-dependent assertions. `StopAsync` completes its internal queue
and awaits its pump task, so a test raises a frame, calls `StopAsync`, and then asserts on the
fully drained result - no test asserts on elapsed time or relies on a sleep or delay for ordinary
behavior. Bounded waits (`WaitAsync` with a multi-second limit, and one `SpinWait.SpinUntil` with a
5-second limit that waits for the backend to have received an expected number of blocks) appear
only as watchdogs that fail a hung test instead of hanging the run; they never decide a result.
Waiting for
the pump to be inside a blocked backend call uses a signal on the fake engine
(`AcceptSamplesEntered`), waiting for state transitions uses `StateChanged`, and "must not have
happened yet" assertions (lease not released, backend not reset, device not stopped) are made
against a backend gated by the test, with the lease-release ordering captured by a witness. The
cooperative-cancel-then-abandon policy is the one place a bounded wait is genuinely under test,
and it is verified deterministically with an injectable abandon-timeout override rather than the
real multi-second default.

The real, native-backed `IRecognitionBackend` implementation, `SherpaOnnxRecognitionEngine`, is
not part of the Speech library: it ships in the sibling SpeechSherpa library, where its
post-endpoint warm-up-replay bookkeeping, real-speech transcription accuracy, session-end
`Reset()` buffered-audio fix, and `TryFlush()` trailing-audio recovery are verified directly
against real, installed models and the real native runtime (see _SpeechSherpa
RecognitionSubsystem Verification_). The seam is what keeps that native surface out of this
library entirely - all policy above it is covered here against the fake backend.

#### Trailing-Audio Flush Pipeline Evidence

`RecognitionSession_StopAsync_FlushesTrailingResultsBeforeCompleting`
(`RecognitionSessionTests`, against the fake backend) verifies the pipeline
wiring: `StopAsync()` buffers the backend's flushed result for `GetResultsAsync` before resetting
the backend, and
`RecognitionSession_StopAsync_BackendResetFails_CompletesAndReportsFault` verifies a
faulting reset is contained the same way a faulting flush already is - reported, not thrown,
with teardown and the subsequent engine reset both still completing.

#### Finite-Source Backpressure Evidence

`RecognitionSession_WavFileSource_SlowBackend_DeliversEverySample` feeds a 200-block WAV file
(over the 64-block queue capacity) to a backend held blocked, then releases it, and verifies
every sample is accepted - nothing is dropped from the start of the file.
`RecognitionSession_WavFileSource_StopAsync_SlowDrain_DeliversFlushedResult` verifies that
`StopAsync` for a file source does not apply the 2-second abandon deadline to a slow backend,
so the flushed trailing result is still delivered.
`RecognitionSession_WavFileSource_BackendFaults_StartAsyncStillCompletes` feeds a 200-block file
to a backend that faults on its first block and verifies `StartAsync` still completes (the
pending queue is completed on a pump fault, releasing the blocked file source) with the session
faulted.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, no downloaded model, no native speech-inference runtime,
  and no physical audio hardware for `RecognitionSessionTests`
- **Test doubles**: Fake `IRecognitionBackend`/`IRecognitionBackendFactory` implementations plus
  NSubstitute capture devices and diagnostics sinks

#### Acceptance Criteria

The unit is considered verified when captured audio reaches the backend downmixed and
resampled to the model's declared `AudioFormat`, with above-target-Nyquist energy attenuated
before any downsampling decimation; every decoded result is delivered through `GetResultsAsync` in
order with its provisional/final flag intact; `StartAsync`/`StopAsync`/`DisposeAsync` are
single-use to start and idempotent to stop and dispose, draining queued audio before returning;
the session's state machine only ever makes
forward-only, documented transitions; backend and handler faults are reported without escaping
uncontrolled and surface to an active `GetResultsAsync` consumer as
`RecognitionSessionFaultedException`; a capture-start or mid-session device failure surfaces the
documented exception; and a non-cooperative native call is abandoned after its configured timeout with a `Warning`
diagnostic. `StopAsync`/`DisposeAsync` flush the backend's trailing audio - recovering, as one last
final result, even the tail of an utterance a streaming backend could not otherwise decode
without audio it will now never receive (for example a push-to-talk release with no trailing
silence) - before resetting the backend, so no accepted audio is silently lost; a fault in either
that flush or the subsequent reset during `StopAsync`/`DisposeAsync` is reported without escaping
and teardown still completes, in which case the trailing flush and/or the backend's clean state
cannot be guaranteed, and in rare cases restart (via a new session from the same engine) may not
fully recover the ability to decode. Acceptance criteria for the real sherpa-onnx backend's
warm-up replay, transcription accuracy, `Reset()`, and `TryFlush()` behavior are defined in
_SpeechSherpa RecognitionSubsystem Verification_.

#### Test Scenarios

##### Session Lifecycle: State Machine Transitions

**Tests**: `RecognitionSession_StartAsync_FromCreated_TransitionsToRunning`,
`RecognitionSession_StartAsync_FromStopped_ThrowsInvalidOperationException`,
`RecognitionSession_StateChanged_EmitsEveryTransitionInOrder`,
`RecognitionSession_StateChanged_StoppingAlwaysPrecedesStoppedAcrossManyIterations`,
`RecognitionSession_DeviceLostMidSession_TransitionsToFaulted`

Transitions are forward-only and raised in order; a stopped session cannot restart.

##### Session Lifecycle: Stop, Dispose, and Draining

**Tests**: `RecognitionSession_StopAsync_FlushesTrailingResultsBeforeCompleting`,
`RecognitionSession_StopAsync_CalledConcurrentlyTwice_BothCompleteOnceStopped`,
`RecognitionSession_StopAsync_BackendResetFails_CompletesAndReportsFault`

Trailing audio is flushed before completion, concurrent stops converge, and a reset fault is
reported without escaping.

##### Session Lifecycle: Concurrent Start/Stop/Dispose Convergence

**Tests**: `RecognitionSession_StartAsync_SlowDeviceStart_DoesNotBlockCaller`,
`RecognitionSession_StopAsync_ConcurrentWithStartAsync_DeviceEndsGenuinelyStopped`,
`RecognitionSession_DisposeAsync_CalledConcurrentlyTwice_BothCompleteAfterSingleTeardown`,
`RecognitionSession_StateChangedHandlerBlocksOnStopAsync_DoesNotDeadlock`

Slow device start does not block the caller; racing start/stop and concurrent dispose converge on
a single teardown; a blocking `StateChanged` handler cannot deadlock the session.

##### Session Lifecycle: Stop Before Start and Canceled Callers

**Tests**: `RecognitionSession_StopAsync_BeforeStartAsync_CompletesResultBuffer`,
`RecognitionSession_StopAsync_CallerTokenCanceled_ReturnsEarlyWithoutAbortingSharedTeardown`,
`RecognitionSession_StopAsync_PreCanceledToken_ThrowsButTeardownStillConverges`

Stopping before starting completes the result buffer; a canceled caller token never aborts the
shared teardown.

##### Session Lifecycle: Fault Containment and Device Loss

**Tests**: `RecognitionSession_BackendThrowsFromAcceptSamples_FaultsSessionAndCompletesResultBuffer`,
`RecognitionSession_DeviceLostMidSession_DisposeAsyncStillTearsDownBackendAndDevice`

A backend fault faults the session and completes the result buffer; disposal after device loss
still tears down the backend and device.

##### Session Pipeline: Capture Format Conversion and Text Normalization

**Tests**: `RecognitionSession_FrameCaptured_StereoAtModelRate_FeedsDownmixedMonoToBackend`,
`RecognitionSession_FrameCaptured_MonoAtHigherRate_FeedsResampledMonoToBackend`,
`RecognitionSession_FrameCaptured_FinalResult_AppliesModelNormalizeTextWithIsFinalTrue`

Captured audio is downmixed and resampled to the model format, and final text is normalized.

##### Session Pipeline: Synchronous Burst Not Dropped

**Test**: `RecognitionSession_StartAsync_DeviceEmitsManyBlocksSynchronouslyFromStart_NoneAreDropped`

Blocks raised synchronously from inside the device start are all delivered.

##### Session Pipeline: Result Delivery and Backpressure

**Tests**: `RecognitionSession_GetResultsAsync_CalledConcurrently_ThrowsInvalidOperationException`,
`RecognitionSession_GetResultsAsync_CanceledToken_EndsEnumerationWithoutStoppingSession`,
`RecognitionSession_GetResultsAsync_SessionFaulted_ThrowsRecognitionSessionFaultedException`,
`RecognitionSession_GetResultsAsync_SlowConsumer_CoalescesProvisionalResults`,
`RecognitionSession_GetResultsAsync_SlowConsumer_NeverDropsFinalResults`

Single consumer, cancellation ends only the enumeration, faults surface, provisionals coalesce,
and finals are never dropped.

##### Finite-Source Backpressure

**Tests**: `RecognitionSession_WavFileSource_SlowBackend_DeliversEverySample`,
`RecognitionSession_WavFileSource_StopAsync_SlowDrain_DeliversFlushedResult`,
`RecognitionSession_WavFileSource_BackendFaults_StartAsyncStillCompletes`

A finite file source is never dropped or timed out by a slow backend, and a backend fault still
lets `StartAsync` complete.

##### Cooperative-Cancel-Then-Abandon: Lease and Reset Ordering

**Tests**: `RecognitionSession_NativeCallExceedsAbandonTimeout_TaskCompletesAndDiagnosticsReportsWarning`,
`RecognitionSession_StartAsync_CanceledWhileDeviceStarting_AbandonsWithinTimeout`,
`RecognitionSession_DisposeAsync_AbandonedPumpWorker_DoesNotReleaseLeaseUntilWorkerExits`,
`RecognitionSession_AbandonedPumpWorker_DoesNotResetBackendUntilWorkerExits`

A non-cooperative call is abandoned after the timeout with a `Warning`; the lease is not
released and the backend is not reset until the abandoned worker exits.

See also the RecognitionSubsystem-level scenarios of the same names for the fuller descriptions.
