### RecognitionSession

#### Verification Approach

The streaming session, its companion Layer 3 engine, the recognition-backend seam, and the
audio-format converter are verified together, since the session's whole purpose is to move audio
from the converter into the backend on the right thread.

The backend seam is replaced by a deterministic fake that records the mono samples it was fed and
yields a scripted sequence of results, so no downloaded model and no platform-specific native
speech-inference binary is ever needed. The capture device is an NSubstitute
`IAudioCaptureDevice` whose `FrameCaptured` event the test raises directly, standing in for a
real audio callback. Diagnostics are an NSubstitute sink, so fault containment is verified by
observing what was reported rather than by asserting an absence of exceptions alone.

Threading is verified without timing assumptions. `StopAsync` completes its internal queue and
awaits its pump task, so a test raises a frame, calls `StopAsync`, and then asserts on the fully
drained result - no sleeps, polls, or timeouts appear anywhere for ordinary behavior. The
cooperative-cancel-then-abandon policy is the one place a bounded wait is genuinely under test,
and it is verified deterministically with an injectable abandon-timeout override rather than the
real multi-second default.

The converter is verified separately as a pure function over plain float arrays, with a
tolerance-based float comparer so assertions describe signal content rather than exact binary
floating-point representation.

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
  and no physical audio hardware for `RecognitionSessionTests`/
  `SpeechRecognizerEngineTests`/`DedicatedWorkerTests`
- **Test doubles**: Fake `IRecognitionBackend`/`IRecognitionBackendFactory` implementations plus
  NSubstitute capture devices and diagnostics sinks for `RecognitionSessionTests` and
  `SpeechRecognizerEngineTests`

#### Acceptance Criteria

The units are considered verified when captured audio reaches the backend downmixed and
resampled to the model's declared `AudioFormat`, with above-target-Nyquist energy attenuated
before any downsampling decimation; every decoded result is delivered through `GetResultsAsync` in
order with its provisional/final flag intact; `StartAsync`/`StopAsync`/`DisposeAsync` are
idempotent and drain queued audio before returning; the session's state machine only ever makes
forward-only, documented transitions; backend and handler faults are reported without escaping
uncontrolled and surface to an active `GetResultsAsync` consumer as
`RecognitionSessionFaultedException`; a capture-start or mid-session device failure surfaces the
documented exception; the engine's single lease permits exactly one live session at a time; a
non-cooperative native call is abandoned after its configured timeout with a `Warning`
diagnostic; and the converter produces the documented output for identity, upsampling,
downsampling, multi-channel, and boundary inputs while rejecting non-positive rates and channel
counts. `StopAsync`/`DisposeAsync` flush the backend's trailing audio - recovering, as one last
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

See the RecognitionSubsystem-level scenarios "Engine Exclusivity and Lease Behavior", "Session
Lifecycle: State Machine Transitions", "Session Lifecycle: Stop, Dispose, and Draining", "Session
Pipeline: Capture Format Conversion and Text Normalization", "Session Pipeline: Result Delivery
and Backpressure", "Cooperative-Cancel-Then-Abandon Policy", and "Audio Conversion: Downmix, Rate
Conversion, and Boundaries", plus the trailing-flush pipeline tests
`RecognitionSession_StopAsync_FlushesTrailingResultsBeforeCompleting` and
`RecognitionSession_StopAsync_BackendResetFails_CompletesAndReportsFault` described above.
