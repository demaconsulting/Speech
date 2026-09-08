### SherpaOnnxSpeechSynthesizer

#### Verification Approach

The streaming synthesizer, the Layer 2 rendering strategy, the sentence chunker, the
synthesis-engine seam, and the playback-format converter are verified together, since the
synthesizer's whole purpose is to move text through rendering and chunking, into the engine, and
the resulting audio through resampling onto the playback device, while pipelining synthesis with
playback.

The engine seam is replaced by a deterministic fake that records every `Generate` call and
returns a fixed, predictable amount of audio, so no downloaded model and no platform-specific
native speech-inference binary is ever needed. The playback device is an NSubstitute
`IAudioPlaybackDevice`, letting tests assert the exact ordered sequence of `Start`/`Write`/`Stop`
calls via `Received.InOrder`. Diagnostics are an NSubstitute sink, so fault containment is
verified by observing what was reported rather than by asserting an absence of exceptions alone.

The genuine-drain-wait fix (`PlayStreamAsync` no longer treats "every segment enqueued" as
"finished playing") is verified the same deterministic way: the NSubstitute playback device's
`PendingSampleCount` getter is stubbed with a callback that signals a semaphore on every read, so
the test can `await` that semaphore to know the drain-wait loop has genuinely started polling -
no sleep, and no race between the test and the implementation - before asserting the awaited
`PlayStreamAsync` task is not yet complete and `Stop()` has not yet been called.

Cancellation is verified deterministically, not by timing: a `BlockingSynthesisEngine` test
double uses `SemaphoreSlim`s to hold the producer mid-segment until the test explicitly releases
it, letting a test assert that neither `SpeakAsync` nor `SynthesizeStreamAsync`'s enumeration
completes while `Generate` is still in flight - proving the producer task is never orphaned - and
then, once released, that the pipeline unwound via cancellation rather than completing normally
and that disposing the synthesizer immediately afterward is safe. This is the regression coverage
for a fixed `AccessViolationException` crash: `SynthesizeStreamCore` previously could return
control to its caller (who could then dispose the owned engine) while the producer's native
`Generate` call was still genuinely running on a background thread; the assertions themselves
synchronize deterministically via semaphores and awaited tasks, with no polling-based sleeps or
waits anywhere in this coverage. Each test does carry a `[Fact(Timeout = ...)]` attribute, but only
as a safety-net deadlock guard that fails the test fast if the fix ever regressed, not as part of
the synchronization logic.

A further test proves the fix for the crash cannot itself hang: it drives a fast, non-blocking
fake engine through more sentences than the producer's bounded look-ahead capacity, consumes only
the first synthesized segment, then abandons enumeration by disposing the enumerator directly -
exactly what the compiler's `await foreach` cleanup does when a consumer's loop body throws for an
unrelated reason, without the stream's own `cancellationToken` ever being cancelled - and asserts
that disposal still completes promptly rather than hanging on the producer's now-permanently-full
channel write. This is regression coverage for a hang that an earlier, narrower version of the fix
could otherwise have introduced.

Speaker-id resolution is verified against a `FakeSynthesisModel` whose injectable
`resolveSpeakerId` delegate lets a test assert exactly which speaker id
`ISynthesisModel.ResolveSpeakerId` returns for a given `parameterValues` bag, and that the fake
engine's recorded `Generate` call received that same id - proving the resolved value genuinely
reaches the engine call rather than merely being computed and discarded. A further test supplies
both a `parameterValues` bag and a `[fast]` Natural Language Audio Tag on the same input,
asserting that the resolved speaker id and the tag-driven speed override both apply correctly and
independently in the same call, proving the two mechanisms coexist without either regressing the
other.

`DefaultModelCapabilityProfile`, `SentenceChunker`, and `PlaybackAudioResampler` are each verified
separately as pure functions over plain inputs (a `StubSpeechModel` test double for the
capability-profile tests, plain strings for the chunker, and plain float arrays with a
tolerance-based comparer for the resampler), so their behavior can be asserted directly rather
than only observed indirectly through the pipeline.

The real sherpa-onnx engine adapter itself is **not** covered by automated tests: exercising it
requires loading a real model through the native runtime, which is manual/local verification. The
seam is what keeps that uncovered surface as small as possible - it contains only the interop
calls, with all policy above it fully covered.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, no downloaded model, no native speech-inference
  runtime, and no physical audio hardware
- **Test doubles**: Fake `ISynthesisEngine`/`ISynthesisEngineFactory` implementations (including
  a semaphore-driven blocking variant for cancellation), NSubstitute playback devices (including
  a semaphore-signaling `PendingSampleCount` stub for the genuine-drain-wait test) and
  diagnostics sinks, and a `StubSpeechModel` test double for capability-profile tests

#### Acceptance Criteria

The units are considered verified when text is chunked, rendered, and synthesized into ordered
audio segments; a pause tag yields silence without an engine call; segments are played in order
with correct pre/post silence while a later chunk synthesizes during an earlier chunk's playback;
`PlayStreamAsync` genuinely waits for the playback device to report a drained queue before
stopping it, rather than stopping as soon as every segment has been enqueued; `Stop()` cancels an
in-flight session deterministically and is a safe no-op when idle; `SynthesizeStreamAsync` never
returns control to its caller while the producer's in-flight `Generate` call is still running, on
every exit path (normal completion, cancellation, or any other exception), so a caller can never
dispose the engine out from under a still-executing native call; engine faults and an unavailable
playback device fail the caller's task honestly rather than hanging; a playback write failure
still stops the device; a session's `parameterValues` bag resolves to the correct speaker
id via `ISynthesisModel.ResolveSpeakerId` once per segment, coexisting correctly with an
independent per-segment Natural Language Audio Tag speed override in the same call; and
`PlaybackAudioResampler` produces the documented output for
identity, up/down conversion, upmix, and boundary inputs while rejecting a non-positive channel
count.

#### Test Scenarios

See the SynthesisSubsystem-level scenarios "Layer 2 Rendering: Pauses, Native, Parameter-Mapped,
and Stripped Tags", "Chunking: Boundary Splitting and Edge Cases", "Pipeline: Chunked Synthesis
and Ordered Playback", "Pipeline: Fault Containment", "Pipeline: Genuine Playback Drain Before
Stopping", "Pipeline: Cancellation and Lifecycle", "Pipeline: Voice/Speaker Selection", and
"Playback Format Conversion: Resampling and Upmix".
