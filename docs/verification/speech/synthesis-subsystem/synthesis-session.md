### SynthesisSession

#### Verification Approach

The real session is verified; the seam, rendering, chunking, dedicated-worker, and
playback-format-conversion units it coordinates are verified in their own documents (see below).
The session's whole purpose is to move text through rendering and chunking, into the backend
(via a dedicated worker), and the resulting audio onto the bound playback device, so its tests
exercise those units together through fakes.

The backend seam is replaced by a deterministic fake that records every `Generate` call and
returns a fixed, predictable amount of audio, so no downloaded model and no platform-specific
native speech-inference binary is ever needed. The playback device is an NSubstitute
`IAudioPlaybackDevice`, letting tests assert the exact ordered sequence of `Start`/`Write`/`Stop`
calls via `Received.InOrder`. Diagnostics are an NSubstitute sink, so fault containment is
verified by observing what was reported rather than by asserting an absence of exceptions alone.

The session's `SynthesisSessionState` lifecycle is verified by subscribing to `StateChanged` and
asserting the exact ordered sequence of transitions raised for one successful operation
(`Created → Starting → Running → Stopping → Stopped`), and separately for a faulting operation
(ending in `Faulted`, reported through diagnostics). A handler that throws from `StateChanged` is
asserted to be isolated: the exception is caught and reported through diagnostics rather than
propagated or allowed to destabilize the session. A handler that blocks while another thread reads
`State` proves handlers are never invoked under the session lock
(`SynthesisSession_StateChanged_HandlerBlocksOnOtherThreadReadingState_DoesNotDeadlock`).
No test uses a `Task.Delay` window to assert that something has not yet happened. "Not yet
complete" assertions (for example that `StopAsync`, `DisposeAsync` or the lease release has not
happened while the native call is blocked) are made against a backend gated by a `SemaphoreSlim`,
so the asserted state is stable until the test releases the gate, and the "only after the native
call returned" ordering is additionally captured by a witness recorded at the instant the lease is
released or `StopAsync` completes (the backend's `GenerateReturned` flag at that moment) and
asserted afterwards, independent of scheduling. Abandonment is awaited through
`StateChanged` reaching `Faulted`, never a sleep. The overlap rule is verified deterministically
with a `BlockingSynthesisEngine` (a private nested test double in `SynthesisSessionTests.cs`) that holds the first call's `Generate` open on
a `SemaphoreSlim` until the test releases it, letting a test assert that a second
`SpeakAsync`/`SynthesizeAsync` call made while the first is still in flight throws
`InvalidOperationException` immediately. Hot reuse across calls is verified by calling
`SpeakAsync` twice in sequence on the same session instance and asserting both calls succeed with
no reconstruction. The terminal `Faulted` state is verified by making the fake backend throw,
asserting the session transitions to `Faulted` and the fault is reported, and then asserting a
further call throws `SynthesisSessionFaultedException` wrapping that same fault.
`SynthesizeAsync`'s full-fidelity segment list is verified by rendering text containing a pause
tag and asserting the returned list includes a pure-silence segment for it, not only
speech-bearing segments.

The genuine-drain-wait behavior (not treating "every segment written" as "finished playing") is
verified deterministically: the NSubstitute playback device's `PendingSampleCount` getter is
stubbed with a callback that signals a semaphore on every read, so the test can `await` that
semaphore to know the drain-wait loop has genuinely started polling - no sleep, and no race
between the test and the implementation - before asserting the awaited `SpeakAsync` task is not
yet complete and `Stop()` has not yet been called.

Cancellation is verified with semaphore-held test doubles rather than timing assumptions where
possible: the same `BlockingSynthesisEngine` test double uses `SemaphoreSlim`s to hold the backend call
open mid-segment until the test explicitly releases it, letting a test assert that `StopAsync`
cancels the in-flight operation only after the in-flight `Generate` call (routed through
`DedicatedWorker`) has genuinely returned - proving the native call is never orphaned - and then,
once released, that the operation unwound via cancellation rather than completing normally and
that disposing the session immediately afterward is safe.

Disposal teardown running outside the session lock is verified by a handler subscribed to
`StateChanged` that blocks while another thread reads `State` during `DisposeAsync`
(`SynthesisSession_DisposeAsync_IdleSessionHandlerBlocksOnOtherThreadReadingState_DoesNotDeadlock`).
Very long pauses are verified by a 150-second pause whose silence is asserted to be written in
bounded chunks (`SynthesisSession_SpeakAsync_VeryLongSilence_IsWrittenInBoundedChunks`). That
diagnostics never carry synthesized text is verified by running a successful and a faulting
operation and asserting no recorded report contains the text
(`SynthesisSession_Operations_SynthesizedText_IsNeverReportedThroughDiagnostics`).

Speaker-id resolution is verified against a `FakeSynthesisModel` whose injectable
`resolveSpeakerId` delegate lets a test assert exactly which speaker id
`ISynthesisModel.ResolveSpeakerId` returns for a given `parameterValues` bag, and that the fake
backend's recorded `Generate` call received that same id - proving the resolved value genuinely
reaches the backend call rather than merely being computed and discarded. A further test supplies
both a `parameterValues` bag and a `[fast]` Natural Language Audio Tag on the same input,
asserting that the resolved speaker id and the tag-driven speed override both apply correctly and
independently in the same call, proving the two mechanisms coexist without either regressing the
other.

`DedicatedWorker`, `DefaultModelCapabilityProfile`, `SentenceChunker`, `PlaybackAudioResampler`
and the synthesis-backend seam are verified directly in their own unit verification documents
(_dedicated-worker.md_, _layer2-rendering.md_, _sentence-chunker.md_,
_playback-audio-resampler.md_, _synthesis-backend.md_); this document covers the session's use of
them, including the semaphore-held cancellation tests through `DedicatedWorker` described above.

The real, native-backed `ISynthesisBackend` implementation, `SherpaOnnxSynthesisEngine`, is not
part of the Speech library: it ships in the sibling SpeechSherpa library, and its verification
status - including its current lack of any automated unit test - is recorded in _SpeechSherpa
SynthesisSubsystem Verification_. The seam is what keeps that native surface out of this library
entirely - it contains only the interop calls, with all policy above it fully covered here.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, no downloaded model, no native speech-inference
  runtime, and no physical audio hardware
- **Test doubles**: Fake `ISynthesisBackend`/`ISynthesisBackendFactory` implementations
  (including a semaphore-driven blocking variant for cancellation/overlap tests), NSubstitute
  playback devices (including a semaphore-signaling `PendingSampleCount` stub for the
  genuine-drain-wait test) and diagnostics sinks, and a `StubSpeechModel`/`FakeSynthesisModel`
  test double for capability-profile and speaker-id-resolution tests

#### Acceptance Criteria

The units are considered verified when text is rendered and synthesized into ordered
audio segments; a pause tag yields silence without a backend call; `SpeakAsync` plays segments in
order with correct pre/post silence; `SpeakAsync` genuinely waits for the playback device to
report a drained queue before stopping it; `StopAsync` cancels an in-flight operation
deterministically, only after the in-flight `Generate` call has genuinely returned, and is a safe
no-op when idle; a second `SpeakAsync`/`SynthesizeAsync` call while one is already in flight
throws `InvalidOperationException`; a session is reusable across repeated calls without
reconstruction; the `StateChanged` event raises the documented transition sequence for both a
successful and a faulting operation, with a subscriber's handler exception isolated; a faulted
session throws `SynthesisSessionFaultedException` on every subsequent call;
`SynthesizeAsync` returns the full-fidelity segment list including silence, completing only once
every segment is synthesized, with at most one backend call in flight at a time; a backend fault
fails the caller's task honestly, an unavailable playback device does likewise, and neither
hangs; an unrequested backend `OperationCanceledException` faults the session rather than stopping
it; a playback write
failure still stops the device; a session's `parameterValues` bag resolves to the correct speaker
id via `ISynthesisModel.ResolveSpeakerId` once per segment, coexisting correctly with an
independent per-segment Natural Language Audio Tag speed override in the same call; volume
overrides scale and clamp amplitude; a canceled operation whose native call cannot be interrupted
is abandoned so the caller's task returns promptly (a slow device `Start` does not block the
caller), and the session then transitions to `Faulted`
with the abandonment reported through diagnostics; a canceled `StopAsync` caller token throws
without aborting the teardown, and a stopped operation unwinds to `Stopped`; `StopAsync` and `DisposeAsync` do not complete
and the backend lease is not released until the in-flight native call has returned, and the lease
is released exactly once even when disposal is called repeatedly or concurrently; a device `Stop`
that is deferred while a native call is in flight happens only after that call returns; and a
disposed session rejects further calls with `ObjectDisposedException`. The
`DedicatedWorker`, Layer 2 rendering, `SentenceChunker`, and `PlaybackAudioResampler` acceptance
criteria are in their own unit verification documents.

#### Test Scenarios

See the SynthesisSubsystem-level scenarios "Layer 2 Rendering: Pauses, Native, Parameter-Mapped,
and Stripped Tags", "Chunking: Boundary Splitting and Edge Cases", "Session: Lifecycle State
Machine and StateChanged Event", "Session: Overlap Rule - No Concurrent Operations", "Session:
Hot Reuse Across Repeated Calls", "Session: Faulted Is Terminal", "Session: SynthesizeAsync
Full-Fidelity Segment List", "Session: Chunked Synthesis and Ordered Playback", "Session: Fault
Containment", "Session: Genuine Playback Drain Before Stopping", "Session: Cancellation and
Disposal Lifecycle", "Session: Abandoned Native Calls Fault the Session", "Session: Silence,
Volume, and Unrequested Cancellation", "Session:
Voice/Speaker Selection", "DedicatedWorker: Cooperative
Cancellation and Non-Cooperative Abandonment", and "Playback Format Conversion: Resampling,
Anti-Aliasing, and Upmix".
