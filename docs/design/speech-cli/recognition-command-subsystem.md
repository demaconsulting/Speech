## SpeechCli RecognitionCommandSubsystem Design

### Overview

The RecognitionCommandSubsystem implements the one speech-to-text subcommand dispatched to by
`CommandDispatch`: `recognize` - the last of all 10 subcommands to be implemented. It contains two
types and extends `ModelCommandsSubsystem`'s existing catalog seam with two further members:

- **`RecognizeCommand`**: implements
  `recognize --model <id> (--input <wav-path> | --mic) [--device <name>]
  [--silence-timeout <seconds>] [--start-timeout <seconds>] [--param key=value ...]
  [--interim | --final-only] [--output <text-path>]`
- **`SilenceTimeoutRecognizerSession`**: a two-phase idle-timeout observer for mic-mode sessions,
  arming its timer with a start-timeout grace period before the first recognition result and a
  silence-timeout window (resetting on every result) thereafter, stopping the recognizer when the
  currently-active window elapses with no reset
- **`ICliModelCatalog.GetAudioFormat`/`CreateRecognizer`**: the two new seam members (see
  _Extending the ModelCommandsSubsystem Seam_ below), implemented by `SpeechModelCatalogAdapter`

### Extending the ModelCommandsSubsystem Seam

`recognize` needs to construct a real `ISpeechRecognizer`, which requires an `IRecognitionModel` -
the library's recognition-capable model interface, exposing `AudioFormat`. Exactly as
`SpeakCommand`'s pass established for `ISynthesisModel`, that cast is only safe inside
`SpeechModelCatalogAdapter` (same assembly as the library's internal model types), never inside a
hand-written `FakeSpeechModel` test double. Two further members, symmetric to the synthesis pair,
are added to `ICliModelCatalog`:

| Member | Returns | Behavior |
| --- | --- | --- |
| `GetAudioFormat(descriptor)` | `AudioFormat` | Throws for a non-recognition model |
| `CreateRecognizer(descriptor, captureDevice, values)` | `ISpeechRecognizer` | Throws for a non-recognition model |

`SpeechModelCatalogAdapter` implements both via a private `RequireRecognitionModel` helper,
mirroring `RequireSynthesisModel` exactly: it performs the cast once and throws a clean
`ArgumentException` naming the offending model id when it fails. In production this branch is
unreachable through the CLI's own dispatch - `RecognizeCommand` already validates
`descriptor.Role == SpeechModelRole.Recognition` itself before calling either new member - but is
retained for the same defensive reason `SpeakCommand`'s pass documented.
`CreateRecognizer`'s production implementation forwards to the public
`SpeechRecognizerFactory.Create(IRecognitionModel, SpeechModelCatalog, IAudioCaptureDevice,
ISpeechDiagnostics?, IReadOnlyDictionary<string,object>?)` overload.

This keeps `RecognizeCommand`'s own logic - argument parsing, input-source resolution, `--param`
validation, verbosity filtering, capture device dispatch, disposal ordering, cancellation - fully
unit-testable against `FakeCliModelCatalog`'s delegate overrides for the two new members, with no
dependency on `IRecognitionModel` anywhere in the test project.

### RecognizeCommand

Parses its own flags (`--model`, `--input`, `--mic`, `--device`, `--silence-timeout`,
`--start-timeout`, repeatable `--param`, `--interim`, `--final-only`, `--output`) via the same
hand-rolled loop style as every other command in this tool, requiring `--model` and rejecting an
unsupported argument or a value-less flag with `ArgumentException`. `--silence-timeout` and
`--start-timeout` each additionally require a positive number of seconds when given.
`--start-timeout` follows `--silence-timeout`'s own existing "inert without `--mic`" convention:
it parses and validates as its own positive-number flag but is only ever consulted inside the
`options.Mic && options.SilenceTimeoutSeconds is { } seconds` guard in `Run` - no new cross-flag
validator rejects `--start-timeout` given without `--silence-timeout` or `--mic`.

**Validation ordering mirrors `SpeakCommand`'s own verified ordering exactly**: parse-time
`--param` token shape is validated inline as each token is parsed; input-source mutual exclusion
(`--input`/`--mic`) is checked first among the semantic validations, then `--interim`/
`--final-only` mutual exclusion, then model resolution. A cheap, input-independent usage mistake
is reported before an unrelated "unknown model id" error whenever both happen to be present in
the same invocation.

**Model resolution** looks up the requested `--model <id>` in `catalog.Enumerate()`, throwing a
user-facing `ArgumentException` for the same three distinct failure modes `SpeakCommand`
established: an id absent from the catalog entirely (suggesting `list-models`), an id present but
with the wrong role (suggesting `list-models --role stt`), and an id present with the recognition
role but not yet downloaded (suggesting `download <modelId>` by name). Once resolved, `--param`
values are validated against the resolved model's own declared parameters, reusing
`ParameterBagParser.Resolve` unmodified from the synthesis pass.

**File-input mode (`--input`) requires no explicit "wait until done" loop.**
`ISpeechRecognizer.Start()` calls the supplied capture device's own `Start()` synchronously (not
on a background thread); a `WavFileAudioCaptureDevice`'s own `Start()` is itself fully synchronous
and blocking, delivering every frame before returning. This command subscribes its own handler
directly to the device instance's own `EndOfFileReached` event (a member additional to
`IAudioCaptureDevice`, not the recognizer's internal subscription), and that handler calls
`recognizer.Stop()` - which runs **reentrantly, on the same thread, from inside
`ISpeechRecognizer.Start()`'s own call to the device's `Start()`** - immediately after the last
frame is delivered and immediately before the device's own `Start()` returns.
`ISpeechRecognizer.Stop()`'s drain is a genuine, synchronous block (confirmed by reading
`SherpaOnnxSpeechRecognizer.StopCore`/`WaitForConsumer`, which calls
`consumerTask.GetAwaiter().GetResult()`), so by the time `ISpeechRecognizer.Start()` returns to
this command, every result derived from the whole file has already been raised and the recognizer
has already fully stopped. No settle-wait or fixed sleep is added anywhere in this design; none is
needed.

**Mic-input mode (`--mic`)** blocks the calling thread on a `ManualResetEventSlim` set either by a
`Ctrl+C` handler or, when `--silence-timeout` was given, by a `SilenceTimeoutRecognizerSession`'s
`TimedOut` event (the session itself already called `Stop()` before raising that event). The
session enforces two distinct idle windows: it arms its timer with `--start-timeout` (defaulting
to `--silence-timeout`'s value when `--start-timeout` is omitted) until the first recognition
result arrives, then re-arms with `--silence-timeout` for every result from the first onward -
giving the user a separate, typically longer grace period to start speaking without weakening the
brief end-of-utterance pause `--silence-timeout` alone controls. `Ctrl+C`
is wired to cooperative cancellation via `Console.CancelKeyPress`, exactly mirroring
`SpeakCommand.Run`'s own subscribe/unsubscribe-in-try/finally pattern. `--device` resolves a real
capture device from the injected `AudioDeviceFactory`, reusing
`DevicesTestCommand.ResolveDeviceSelectionOrThrow` (internal, same assembly, different namespace)
to validate any requested `--device` name before a device is actually created, exactly mirroring
`devices test`'s and `speak`'s own validate-before-create pattern; an unavailable resolved device
throws `InvalidOperationException` suggesting `--input` as an alternative.

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

**`--output <text-path>`** writes only final results to the file, one line each, flushed
immediately - interim results are a live-console-only concept and are never written to the file,
regardless of `--interim`/`--final-only`'s effect on console output. The file is opened once with
overwrite (not append) semantics, consistent with `speak --output`'s WAV semantics. `--device` is
not rejected when given alongside `--input`: it is simply inert in that case (file mode never
consults it), mirroring `speak`'s own documented precedent that `--device` is silently ignored,
not an error, when a file destination is also given.

**Disposal.** Neither `IAudioCaptureDevice`, `WavFileAudioCaptureDevice`, nor the real
PortAudio-backed capture device implement `IDisposable` (confirmed directly from all three source
files), so - unlike `speak`'s playback-device side - this command never needs a conditional
capture-device disposal cast. Only the recognizer and, in mic mode with a silence timeout, the
`SilenceTimeoutRecognizerSession` need disposal, both handled in nested `finally` blocks so every
exit path (EOF stop, silence-timeout stop, `Ctrl+C`, or an error) disposes them exactly once.

### SilenceTimeoutRecognizerSession

A pure event-driven observer composed alongside a recognizer, not a decorator around its
lifecycle API: it never intercepts `Start()`/`Stop()` calls made by its owner, and calls
`ISpeechRecognizer.Stop()` itself only proactively, on timeout. It enforces two distinct,
sequential idle windows via two separate `TimeSpan` fields: `_startTimeout` (defaulting to
`idleTimeout` when the constructor's optional `startTimeout` parameter is omitted) arms the
single-shot idle timer exactly once, at construction, before `ResultReceived` is even subscribed;
`_idleTimeout` (the `--silence-timeout` value) then re-arms the timer
(`Change(idleTimeout, Timeout.InfiniteTimeSpan)`) on every subsequent `ResultReceived` event,
partial or final, starting with the very first. When the timer fires with no reset since it was
last armed, it calls `Stop()`, then raises its own `TimedOut` event. No additional "first result
seen" boolean flag is needed to implement this phase transition: construction and
`OnResultReceived` are already distinct call sites, so arming with `_startTimeout` once at
construction and unconditionally re-arming with `_idleTimeout` on every `OnResultReceived` call
naturally implements "start-timeout governs only the pre-first-result window; silence-timeout
governs every re-arm from the first result onward" with zero new mutable state and zero new
lock-guarded reads/writes - the existing `_gate`/`_idleCallbackDone` concurrency design is
unchanged.

The idle timer is implemented with the injectable `System.TimeProvider` abstraction (available in
the BCL since .NET 8, requiring no new package reference) rather than a hard-coded
`Timer`/`Thread.Sleep`, so a unit test can exercise the full idle/reset/timeout sequence
deterministically with a fake `TimeProvider` and no real wall-clock delay. A `volatile bool
_isDisposed` flag guards against a timer callback racing a concurrent `Dispose()` call, since the
callback runs on a thread-pool thread independent of the constructing/disposing thread; `Dispose()`
itself is idempotent.

### Interactions with Other Units

`RecognizeCommand` depends on the `ICliModelCatalog` seam (both its original five members, the two
added by the synthesis pass, and the two added by this pass), the library's public
`AudioFormat`/`ISpeechRecognizer`/`SpeechRecognitionEvent`/`SpeechRecognitionResult` types,
`AudioDeviceFactory` from the library's audio subsystem, `WavFileAudioCaptureDevice`, and reuses
`DeviceCommandsSubsystem`'s `DevicesTestCommand.ResolveDeviceSelectionOrThrow` internal helper and
`SynthesisCommandSubsystem`'s `ParameterBagParser` unmodified, rather than duplicating either.
`SilenceTimeoutRecognizerSession` depends only on the library's public `ISpeechRecognizer`/
`SpeechRecognitionEvent` types and the BCL `System.TimeProvider` abstraction. None of this
subsystem touches `IRecognitionModel` or any other internal library type directly - that access is
confined entirely to `SpeechModelCatalogAdapter`, exactly as `ModelCommandsSubsystem`'s original
seam already established.
