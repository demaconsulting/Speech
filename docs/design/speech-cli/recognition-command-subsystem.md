## SpeechCli RecognitionCommandSubsystem Design

### Overview

The RecognitionCommandSubsystem implements the one speech-to-text subcommand dispatched to by
`CommandDispatch`: `recognize`. It contains two types and extends `ModelCommandsSubsystem`'s
existing catalog seam with two further members:

- **`RecognizeCommand`**: implements
  `recognize --stt-model <id> (--input <wav-path> | --mic) [--capture-device <name>]
  [--silence-timeout <seconds>] [--start-timeout <seconds>] [--stt-param key=value ...]
  [--interim | --final-only] [--output-text <text-path>]`
- **`SilenceTimeoutRecognizerSession`**: a stateless `IAsyncEnumerable<SpeechRecognitionEvent>`
  decorator around a mic-mode `IRecognitionSession`'s `GetResultsAsync`, racing a two-phase idle
  timeout (a start-timeout grace period before the first recognition result, a silence-timeout
  window resetting on every result thereafter) against each `MoveNextAsync`, stopping the wrapped
  session when the currently-active window elapses with no result
- **`ICliModelCatalog.GetAudioFormat`/`CreateRecognizerEngineAsync`**: the two new seam members
  (see _Extending the ModelCommandsSubsystem Seam_ below), implemented by
  `SpeechModelCatalogAdapter`

### Extending the ModelCommandsSubsystem Seam

`recognize` needs to load a real `ISpeechRecognizerEngine`, which requires an `IRecognitionModel` -
the library's recognition-capable model interface, exposing `AudioFormat`. Exactly as
`SpeakCommand`'s pass established for `ISynthesisModel`, that cast is only safe inside
`SpeechModelCatalogAdapter` (same assembly as the library's internal model types), never inside a
hand-written `FakeSpeechModel` test double. Two further members, symmetric to the synthesis pair,
are added to `ICliModelCatalog`:

| Member | Returns | Behavior |
| --- | --- | --- |
| `GetAudioFormat(descriptor)` | `AudioFormat` | Throws for a non-recognition model |
| `CreateRecognizerEngineAsync(...)` | `Task<ISpeechRecognizerEngine>` | Non-recognition model throws; no device bound |

`SpeechModelCatalogAdapter` implements both via a private `RequireRecognitionModel` helper,
mirroring `RequireSynthesisModel` exactly: it performs the cast once and throws a clean
`ArgumentException` naming the offending model id when it fails. In production this branch is
unreachable through the CLI's own dispatch - `RecognizeCommand` already validates
`descriptor.Role == SpeechModelRole.Recognition` itself before calling either new member - but is
retained for the same defensive reason `SpeakCommand`'s pass documented.
`CreateRecognizerEngineAsync`'s production implementation forwards unchanged to
`SpeechRecognizerFactory.LoadAsync(IRecognitionModel, SpeechModelCatalog, ISpeechDiagnostics?,
IReadOnlyDictionary<string,object>?, CancellationToken)`, returning an `ISpeechRecognizerEngine`
with no device bound; `RecognizeCommand` later binds the resolved capture device by calling
`engine.CreateSessionAsync(captureDevice, cancellationToken)`.

This keeps `RecognizeCommand`'s own logic - argument parsing, input-source resolution, `--stt-param`
validation, verbosity filtering, capture device dispatch, disposal ordering, cancellation - fully
unit-testable against `FakeCliModelCatalog`'s delegate overrides for the two new members, with no
dependency on `IRecognitionModel` anywhere in the test project.

### RecognizeCommand

Parses its own flags (`--stt-model`, `--input`, `--mic`, `--capture-device`, `--silence-timeout`,
`--start-timeout`, repeatable `--stt-param`, `--interim`, `--final-only`, `--output-text`) via the same
hand-rolled loop style as every other command in this tool, requiring `--stt-model` and rejecting an
unsupported argument or a value-less flag with `ArgumentException`. `--silence-timeout` and
`--start-timeout` each additionally require a positive number of seconds when given.

**Validation ordering mirrors `SpeakCommand`'s own verified ordering exactly**: parse-time
`--stt-param` token shape is validated inline as each token is parsed; input-source mutual exclusion
(`--input`/`--mic`) is checked first among the semantic validations, then `--interim`/
`--final-only` mutual exclusion, then model resolution. A cheap, input-independent usage mistake
is reported before an unrelated "unknown model id" error whenever both happen to be present in
the same invocation.

**Model resolution** looks up the requested `--stt-model <id>` in `catalog.Enumerate()`, throwing a
user-facing `ArgumentException` for the same three distinct failure modes `SpeakCommand`
established: an id absent from the catalog entirely (suggesting `list-models`), an id present but
with the wrong role (suggesting `list-models --role stt`), and an id present with the recognition
role but not yet downloaded (suggesting `download <modelId>` by name). Once resolved, `--stt-param`
values are validated against the resolved model's own declared parameters, reusing
`ParameterBagParser.Resolve` unmodified from the synthesis pass.

**Async composition flow.** `Run` resolves the capture device, then awaits `catalog
.CreateRecognizerEngineAsync(descriptor, parameterValues, cancellationToken)` followed by
`engine.CreateSessionAsync(captureDevice, cancellationToken)`, both bound with `await using` so
they are disposed (session first, then engine, reflecting declaration order reversed) on every
exit path. The returned `IRecognitionSession` is driven uniformly via `StartAsync`/`StopAsync`/
`GetResultsAsync` in both input modes, rather than the two modes using structurally different
APIs.

**File-input mode (`--input`) requires no explicit "wait until done" loop, and no wrapper
session.** `IRecognitionSession.StartAsync` drives the supplied `WavFileAudioCaptureDevice`'s own
delivery of every frame synchronously before returning (a `WavFileAudioCaptureDevice`'s own
capture is fully blocking, not threaded), so by the time `StartAsync` returns in file mode, every
frame has already been delivered to the backend. This command follows that `await
session.StartAsync(cancellationToken)` with an explicit drain `await
session.StopAsync(cancellationToken)`, exactly mirroring `IRecognitionSession.StopAsync`'s own
documented drain contract, so any buffered-but-not-yet-decoded audio is flushed through and every
result derived from the whole file is guaranteed to have been produced by the time the pump loop
over `GetResultsAsync` settles. No `SilenceTimeoutRecognizerSession` wrapper, settle-wait, or fixed
sleep is added for file mode; none is needed, since `StartAsync` + the explicit `StopAsync` are
together already sufficient to reach a terminal state.

**Mic-input mode (`--mic`)** always constructs a `SilenceTimeoutRecognizerSession` - even when
both `--silence-timeout` and `--start-timeout` are omitted (each then defaults to its own
documented default-seconds value) - so listening cannot block `recognize --mic` forever with only
`Ctrl+C` as an escape hatch. The command pumps
`silenceTimeout.GetResultsAsync(cancellationToken)` (rather than `session.GetResultsAsync`
directly) concurrently with `await session.StartAsync(cancellationToken)`, started first as a
genuinely async, non-blocking call so live interim results print as they arrive rather than only
once `StartAsync` returns. The session enforces two distinct idle windows: it races the wrapped
enumerator's `MoveNextAsync` against `--start-timeout` (defaulting to `--silence-timeout`'s value
when `--start-timeout` is omitted) until the first recognition result arrives, then re-arms with
`--silence-timeout` for every result from the first onward - giving the user a separate,
typically longer grace period to start speaking without weakening the brief end-of-utterance
pause `--silence-timeout` alone controls. `Ctrl+C` is wired to cooperative cancellation via a
synchronous, fire-and-forget `Console.CancelKeyPress` handler that calls `session.StopAsync
(CancellationToken.None)` directly - stopping the session completes its result stream, ending the
pump naturally, with `cancellationToken` forwarded into `GetResultsAsync` as a belt-and-suspenders
safety net for the documented edge case where `StopAsync` called while the session never started
does not itself complete that stream. `--capture-device` resolves a real capture device from the
injected `AudioDeviceFactory`, reusing `DevicesTestCommand.ResolveDeviceSelectionOrThrow`
(internal, same assembly, different namespace) to validate any requested `--capture-device` name
before a device is actually created, exactly mirroring `devices test`'s and `speak`'s own
validate-before-create pattern; an unavailable resolved device throws `InvalidOperationException`
suggesting `--input` as an alternative.

**`--interim`/`--final-only` console UX.** The default (neither flag) prints both: an interim
(`IsFinal == false`) result is written with a carriage-return overwrite and no trailing newline,
so a live console redraws the evolving hypothesis in place, while a final result is written
newline-terminated, "settling" that line before the next utterance's interim results begin
overwriting again. This mirrors familiar live-transcription UX without cluttering scripted/piped
output with a per-line prefix; when stdout is redirected the carriage-return behavior degrades
gracefully to one line per event, which is an accepted trade-off, not a defect. `--interim` prints
only interim results (still overwritten); `--final-only` prints only final results, one per line.
Overwriting tracks the _longest_ interim text printed since the last settle (not just the most
recent write), so a hypothesis that later shrinks (e.g. "hello world" revised to "hello") still
pads over every stale trailing character from the longer prior write before repositioning the
cursor, rather than leaving them visible on the line.

**`--output-text <text-path>`** writes only final results to the file, one line each, flushed
immediately - interim results are a live-console-only concept and are never written to the file,
regardless of `--interim`/`--final-only`'s effect on console output. The file is opened once with
overwrite (not append) semantics, consistent with `speak --output-audio`'s WAV semantics. `--capture-device` is
not rejected when given alongside `--input`: it is simply inert in that case (file mode never
consults it), mirroring `speak`'s own documented precedent that `--capture-device` is silently ignored,
not an error, when a file destination is also given.

**Disposal.** Neither `IAudioCaptureDevice`, `WavFileAudioCaptureDevice`, nor the real
PortAudio-backed capture device implement `IDisposable` (confirmed directly from all three source
files), so - unlike `speak`'s playback-device side - this command never needs a conditional
capture-device disposal cast. The engine and session are both `IAsyncDisposable` and bound with
`await using`, guaranteeing exactly-once disposal on every exit path (file-mode drain completion,
mic-mode silence/start timeout, `Ctrl+C`, or an error) with no nested `try`/`finally` needed for
them.

### SilenceTimeoutRecognizerSession

A stateless `IAsyncEnumerable<SpeechRecognitionEvent>` decorator wrapping an
`IRecognitionSession`'s `GetResultsAsync`, not a background event-subscriber: it never intercepts
`StartAsync`/`StopAsync` calls made by its owner on the underlying session directly, and calls
`IRecognitionSession.StopAsync` itself only proactively, on timeout, from inside its own iterator.
Its `GetResultsAsync(CancellationToken)` is an `async IAsyncEnumerable<SpeechRecognitionEvent>`
method that obtains `IAsyncEnumerator<SpeechRecognitionEvent>` from the wrapped session (disposed
via the compiler-generated `await using` on that enumerator, needing no `IDisposable` of its own)
and, in a loop, races `enumerator.MoveNextAsync().AsTask()` against `Task.Delay(timeout,
_timeProvider, cancellationToken)` via `Task.WhenAny`:

- If the enumerator wins, the current result is yielded and the next iteration re-arms the
  timeout with `_idleTimeout` (the `--silence-timeout` value).
- If the delay wins first, and the session has not already been stopped by a prior timeout on this
  same enumeration, it awaits `_session.StopAsync(CancellationToken.None)` then raises `TimedOut`
  once - then **continues looping** rather than returning immediately, so any already-buffered or
  in-flight trailing result the stop drains through still surfaces to the caller before the
  enumerable genuinely completes.

It enforces two distinct, sequential idle windows using the constructor's `startTimeout` parameter
(defaulting to `idleTimeout` when omitted): the very first race uses `startTimeout`, before any
result has been yielded; every subsequent race, from the first yielded result onward, uses
`idleTimeout`. No additional "first result seen" boolean flag beyond a simple loop-local variable
is needed to implement this phase transition, since the loop already distinguishes "before the
first yield" from "after".

The idle timeout is implemented against the injectable `System.TimeProvider` abstraction
(available in the BCL since .NET 8, requiring no new package reference) via `Task.Delay(TimeSpan,
TimeProvider, CancellationToken)` rather than a hard-coded `Timer`/`Thread.Sleep`, so a unit test
can exercise the full idle/reset/timeout sequence deterministically with a fake `TimeProvider`
(`FakeTimeProvider`, driving its own `FakeTimer`) and no real wall-clock delay. Because this type
is now a pure, stateless async-enumerable decorator with cleanup delegated entirely to the inner
enumerator's own `await using` disposal, it needs **no locks, no wait-handles, and no
`IDisposable`/`Dispose()` of its own** - the prior timer/event-based design's `_gate`/
`_idleCallbackDone` concurrency guards no longer exist because there is no longer a
background-thread timer callback racing a concurrent caller-driven disposal.

### Interactions with Other Units

`RecognizeCommand` depends on the `ICliModelCatalog` seam (both its original five members, the two
added by the synthesis pass, and the two added by this pass), the library's public
`AudioFormat`/`ISpeechRecognizerEngine`/`IRecognitionSession`/`SpeechRecognitionEvent`/
`SpeechRecognitionResult` types, `AudioDeviceFactory` from the library's audio subsystem,
`WavFileAudioCaptureDevice`, and reuses `DeviceCommandsSubsystem`'s `DevicesTestCommand
.ResolveDeviceSelectionOrThrow` internal helper and `SynthesisCommandSubsystem`'s
`ParameterBagParser` unmodified, rather than duplicating either. `SilenceTimeoutRecognizerSession`
depends only on the library's public `IRecognitionSession`/`SpeechRecognitionEvent` types and the
BCL `System.TimeProvider` abstraction. None of this subsystem touches `IRecognitionModel` or any
other internal library type directly - that access is confined entirely to
`SpeechModelCatalogAdapter`, exactly as `ModelCommandsSubsystem`'s original seam already
established.
