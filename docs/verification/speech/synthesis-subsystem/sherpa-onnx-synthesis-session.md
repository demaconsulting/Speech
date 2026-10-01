### SherpaOnnxSynthesisSession

#### Verification Approach

The real session, the Layer 2 rendering strategy, the sentence chunker, the synthesis-backend
seam, the dedicated-worker cancellation policy, and the playback-format converter are verified
together, since the session's whole purpose is to move text through rendering and chunking, into
the backend (via a dedicated worker), and the resulting audio through resampling onto the bound
playback device.

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
propagated or allowed to destabilize the session. The overlap rule is verified deterministically
with a `BlockingSynthesisEngine`-style test double that holds the first call's `Generate` open on
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

Cancellation is verified deterministically, not by timing: a `BlockingSynthesisEngine` test
double uses `SemaphoreSlim`s to hold the backend call open mid-segment until the test explicitly
releases it, letting a test assert that `StopAsync` cancels the in-flight operation only after the
in-flight `Generate` call (routed through `DedicatedWorker`) has genuinely returned - proving the
native call is never orphaned - and then, once released, that the operation unwound via
cancellation rather than completing normally and that disposing the session immediately
afterward is safe.

Speaker-id resolution is verified against a `FakeSynthesisModel` whose injectable
`resolveSpeakerId` delegate lets a test assert exactly which speaker id
`ISynthesisModel.ResolveSpeakerId` returns for a given `parameterValues` bag, and that the fake
backend's recorded `Generate` call received that same id - proving the resolved value genuinely
reaches the backend call rather than merely being computed and discarded. A further test supplies
both a `parameterValues` bag and a `[fast]` Natural Language Audio Tag on the same input,
asserting that the resolved speaker id and the tag-driven speed override both apply correctly and
independently in the same call, proving the two mechanisms coexist without either regressing the
other.

`DedicatedWorker` is verified directly and in isolation, with an injectable abandon timeout so
the non-cooperative-abandonment test does not need to wait out the real two-second default: a
cooperative delegate (one that observes and honors its cancellation token promptly) lets `Run`
complete promptly on cancellation, while a non-cooperative delegate (one that ignores
cancellation) is abandoned once the shortened timeout elapses, reporting a `Warning` diagnostic
through an NSubstitute sink while still returning control to its caller as cancelled; a separate
test asserts the delegate always runs on a `TaskCreationOptions.LongRunning` task by inspecting
the running thread's characteristics from inside the delegate.

`DefaultModelCapabilityProfile`, `SentenceChunker`, and `PlaybackAudioResampler` are each verified
separately as pure functions over plain inputs (a `StubSpeechModel` test double for the
capability-profile tests, plain strings for the chunker, and plain float arrays with a
tolerance-based comparer for the resampler), so their behavior can be asserted directly rather
than only observed indirectly through the session.

The real sherpa-onnx engine adapter itself is **not** covered by automated tests: exercising it
requires loading a real model through the native runtime, which is manual/local verification. The
seam is what keeps that uncovered surface as small as possible - it contains only the interop
calls, with all policy above it fully covered.

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

The units are considered verified when text is chunked, rendered, and synthesized into ordered
audio segments; a pause tag yields silence without a backend call; `SpeakAsync` plays segments in
order with correct pre/post silence; `SpeakAsync` genuinely waits for the playback device to
report a drained queue before stopping it; `StopAsync` cancels an in-flight operation
deterministically, only after the in-flight `Generate` call has genuinely returned, and is a safe
no-op when idle; a second `SpeakAsync`/`SynthesizeAsync` call while one is already in flight
throws `InvalidOperationException`; a session is reusable across repeated calls without
reconstruction; the `StateChanged` event raises the documented transition sequence for both a
successful and a faulting operation, with a subscriber's handler exception isolated; a faulted
session throws `SynthesisSessionFaultedException` on every subsequent call;
`SynthesizeAsync` returns the full-fidelity segment list including silence; backend faults and an
unavailable playback device fail the caller's task honestly rather than hanging; a playback write
failure still stops the device; a session's `parameterValues` bag resolves to the correct speaker
id via `ISynthesisModel.ResolveSpeakerId` once per segment, coexisting correctly with an
independent per-segment Natural Language Audio Tag speed override in the same call;
`DedicatedWorker` completes promptly on cooperative cancellation and abandons a non-cooperative
delegate after its timeout while always using a long-running task; and `PlaybackAudioResampler`
produces the documented output for identity, up/down conversion, upmix, and boundary inputs while
rejecting a non-positive channel count.

#### Test Scenarios

See the SynthesisSubsystem-level scenarios "Layer 2 Rendering: Pauses, Native, Parameter-Mapped,
and Stripped Tags", "Chunking: Boundary Splitting and Edge Cases", "Session: Lifecycle State
Machine and StateChanged Event", "Session: Overlap Rule - No Concurrent Operations", "Session:
Hot Reuse Across Repeated Calls", "Session: Faulted Is Terminal", "Session: SynthesizeAsync
Full-Fidelity Segment List", "Session: Chunked Synthesis and Ordered Playback", "Session: Fault
Containment", "Session: Genuine Playback Drain Before Stopping", "Session: Cancellation and
Disposal Lifecycle", "Session: Voice/Speaker Selection", "DedicatedWorker: Cooperative
Cancellation and Non-Cooperative Abandonment", and "Playback Format Conversion: Resampling,
Anti-Aliasing, and Upmix".
