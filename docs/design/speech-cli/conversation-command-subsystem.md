## SpeechCli ConversationCommandSubsystem Design

### Overview

The ConversationCommandSubsystem implements the one voice-conversation subcommand dispatched to
by `CommandDispatch`: `ask` - a new, eleventh subcommand added after all ten scaffolded
subcommands were implemented. It contains one type, `AskCommand`, and introduces **no new
`ICliModelCatalog` seam member**: `ask` combines `SynthesisCommandSubsystem`'s existing
`GetPreferredAudioFormat`/`CreateSynthesizerEngineAsync` members and
`RecognitionCommandSubsystem`'s existing `GetAudioFormat`/`CreateRecognizerEngineAsync` members -
both already extending the original `ModelCommandsSubsystem` seam - to run a speak-then-listen
turn as one CLI invocation, built entirely on the async Engine/Session API.

- **`AskCommand`**: implements
  `ask --tts-model <id> --stt-model <id> (--text <text> | --file <path>)
  [--playback-device <name>] [--capture-device <name>] [--tts-param key=value ...]
  [--stt-param key=value ...] [--silence-timeout <seconds>] [--start-timeout <seconds>]
  [--output-text <text-path>]`
- **`ICliCaptureDeviceSource`**: a small CLI-owned seam over capture-device resolution, mirroring
  `SynthesisCommandSubsystem`'s `ICliPlaybackDeviceSource` exactly (a `CaptureProbe` property and
  a `CreateCaptureDevice(selection)` method), letting unit tests substitute an in-memory fake
  capture device instead of depending on a real, sealed `AudioDeviceFactory` and real PortAudio
  hardware.
- **`AudioDeviceFactoryCaptureDeviceSource`**: the production `ICliCaptureDeviceSource`
  implementation, forwarding every call unchanged to a composed, real `AudioDeviceFactory`, mirroring
  `AudioDeviceFactoryPlaybackDeviceSource` exactly.

### Reuse of the Existing ModelCommandsSubsystem Seam (No New Member)

`ask` needs to load both a real `ISpeechSynthesizerEngine` and a real `ISpeechRecognizerEngine` for
its two phases. Both capabilities already exist on `ICliModelCatalog`, added by the synthesis and
recognition passes respectively: `CreateSynthesizerEngineAsync(descriptor, parameterValues,
cancellationToken)` and `CreateRecognizerEngineAsync(descriptor, parameterValues,
cancellationToken)`, each already validating (via `SpeechModelCatalogAdapter`'s own internal
casts) that the resolved model actually has the corresponding role, and each returning an engine
with no device bound yet - the device is bound afterward via `engine.CreateSessionAsync(device,
cancellationToken)`. Because `ask` requires exactly one synthesis-role model and exactly one
recognition-role model - never a single model serving both roles - it has no need for a third,
combined seam member; it simply calls the two existing members once each, exactly as `speak` and
`recognize` already do individually. This keeps `AskCommand`'s own logic fully unit-testable
against `FakeCliModelCatalog`'s existing delegate overrides, with no new fake or seam member
required anywhere in the test project.

### AskCommand

Parses its own flags (`--tts-model`, `--stt-model`, `--text`, `--file`, `--playback-device`,
`--capture-device`, repeatable `--tts-param`, repeatable `--stt-param`, `--silence-timeout`,
`--start-timeout`, `--output-text`) via the same hand-rolled loop style as every other command in
this tool, requiring both `--tts-model` and `--stt-model` and rejecting an unsupported argument or
a value-less flag with `ArgumentException`. Because `ask` never supports `--interim`,
`--final-only`, `--no-tags`, `--input`, or `--output-audio`, none of those tokens is a recognized
`case` in the parser's `switch`, so each falls through to the same unsupported-argument
`ArgumentException` every other unrecognized token already receives - no separate rejection list
is needed.

**Validation ordering mirrors `SpeakCommand`'s/`RecognizeCommand`'s own verified ordering
exactly**: text-source resolution (`--text`/`--file`/piped stdin mutual exclusion) runs first,
since it is a cheap, model-independent usage check; then the TTS model is resolved, then the STT
model. Resolving the TTS model before the STT model (rather than the reverse, or in parallel)
matches `ask`'s own natural phase order - Phase 1 (speak) happens first - and reuses
`SpeakCommand`'s and `RecognizeCommand`'s own resolution error messages verbatim (unknown id,
wrong role, not downloaded), each naming its own flag (`--tts-model`/`--stt-model`) so the correct
next step (`list-models --role tts`/`--role stt`, or `download <modelId>`) is always unambiguous.
`--tts-param`/`--stt-param` values are each validated against their own resolved model's declared
parameters, reusing `ParameterBagParser.Resolve` unmodified from the synthesis pass, exactly as
`recognize`'s own `--stt-param` handling already does.

**Two-phase execution.** `RunAsync` kicks off Phase 2's recognizer engine/session pre-warm on a
background `Task.Run` immediately (see _Recognizer Pre-Warming_ below), then runs Phase 1: resolves
a real playback device from the injected `ICliPlaybackDeviceSource` (honoring `--playback-device`,
defaulting to the system default device exactly as `speak` does), awaits
`catalog.CreateSynthesizerEngineAsync(...)` then `engine.CreateSessionAsync(playbackDevice, ...)`,
and calls `session.SpeakAsync(text, cancellationToken)`, waiting for it to complete before Phase
2's listen phase can begin - Phase 2's recognizer engine/session construction runs concurrently
with Phase 1's wait, but the two phases' *listening* remains strictly sequential, matching a
natural question-then-answer conversational turn - `ask` never starts real microphone capture
while the prompt is still being spoken. Once Phase 1 completes, `RunAsync` adopts the pre-warmed
`PrewarmedRecognizer` (engine + session) and calls `Listen`, which drives the already-constructed
session via `StartAsync`/`GetResultsAsync`/`StopAsync`, wrapped in a `SilenceTimeoutRecognizerSession`
(constructed and armed exactly as `recognize --mic`'s own, reused unmodified from
`RecognitionCommandSubsystem`), blocking until one of: the first **final** recognition result
arrives, the silence/start timeout fires, or `Ctrl+C` is pressed (signaled externally via a shared
`ManualResetEventSlim`).

### Recognizer Pre-Warming (Concurrent Phase 1/Phase 2 Engine Load)

Loading a recognition model into native memory - not `StartAsync`, which merely begins streaming
audio through an already-loaded engine - is the expensive step in constructing an
`ISpeechRecognizerEngine` (see `SpeechRecognizerFactory`'s own remarks). Deferring that load until
strictly after Phase 1's playback finishes therefore introduced an avoidable turnaround-gap
latency between finishing speaking and starting to listen. `AskCommand.RunAsync` kicks off a
private `PrewarmRecognizerAsync` method - which resolves Phase 2's capture device (via
`ICliCaptureDeviceSource`, honoring `--capture-device`), calls
`catalog.CreateRecognizerEngineAsync(...)`, then `engine.CreateSessionAsync(captureDevice, ...)`,
bundling both into a `PrewarmedRecognizer(Engine, Session)` record struct - on a background
`Task.Run` immediately after both phases' parameters are resolved, so this work overlaps with the
`await SpeakPromptAsync(...)` call that follows it. Capture-device resolution moved into this
pre-warm step alongside engine/session construction because `ISpeechRecognizerEngine.CreateSessionAsync`
requires an already-constructed `IAudioCaptureDevice` as an argument - it cannot be deferred
independently of the engine itself. If session creation fails after the engine has already loaded,
`PrewarmRecognizerAsync` disposes the orphaned engine before rethrowing, so a session-creation
failure never leaks the engine. Critically, only the model *load* and session creation are
pre-warmed: `session.StartAsync(...)` (which begins real microphone capture) is still called only
once Phase 2's `Listen` genuinely runs, so pre-warming never risks capturing audio - including any
acoustic bleed from the prompt still being played - while Phase 1 is in progress.

On every exit path, the pre-warm task's result and any exception it raises are always eventually
observed and never left unobserved, but `RunAsync` only *synchronously* awaits it on the success
path (Phase 2 genuinely starting); on the canceled/failed paths it hands the cleanup off
fire-and-forget so a slow in-flight model load never delays `Ctrl+C`/fast-failure responsiveness:

- **Phase 1 canceled (or Phase 1/earlier resolution logic throws)**: the pre-warm task (whether
  already completed, still in flight, or destined to fail) is handed off, not awaited, to a small
  `DisposePrewarmedRecognizerAsync` helper via `_ = DisposePrewarmedRecognizerAsync(prewarmTask);`,
  so `RunAsync` can return/rethrow immediately instead of blocking on the recognizer's model load
  finishing. `DisposePrewarmedRecognizerAsync` itself still awaits the task in the background and,
  if it produced a `PrewarmedRecognizer`, disposes its session and then its engine (via
  `DisposeAsync`), swallowing any pre-warm exception - Phase 1's own cancellation/failure has
  already been reported, and a concurrently failed pre-warm is not separately actionable once the
  call is already ending that way.
- **Phase 1 succeeds**: `RunAsync` awaits the pre-warm task directly, which re-throws (with its
  original exception type, message, and stack trace) any failure `PrewarmRecognizerAsync` raised -
  an unknown/unavailable `--capture-device`, an invalid `--stt-param` value, etc. - at the start
  of Phase 2, exactly as a synchronous call to the same logic would have. `RunAsync` adopts the
  engine via `await using`, then calls `Listen` with the already-constructed session; `Listen`
  itself still checks `stopSignal.IsSet` first (now also covering the case where `Ctrl+C` landed
  during the concurrent pre-warm itself) before ever calling `StartAsync`, and still calls
  `onRecognizerCreated`/disposes the session in its own `finally` block exactly as before, so the
  disposal-ordering/callback-timing contract several unit tests depend on is unchanged.

This is a design note for future `ICliModelCatalog` implementers: `SpeechModelCatalogAdapter`
(the only production implementation) wraps a read-mostly `SpeechModelCatalog` with no documented
thread-safety concerns for concurrent `CreateSynthesizerEngineAsync`/`CreateRecognizerEngineAsync`
calls on the same instance, which is what makes running `CreateRecognizerEngineAsync` concurrently
with Phase 1's `CreateSynthesizerEngineAsync`-backed playback safe today; a future catalog
implementation with non-thread-safe side effects shared across both calls would need to account
for this concurrency.

### Capture-Device Resolution Through `ICliCaptureDeviceSource` (New Seam)

Unlike `RecognizeCommand`, which resolves its capture device directly from an injected
`AudioDeviceFactory`, Phase 2 resolves its capture device through `ICliCaptureDeviceSource` - a
small, CLI-owned seam introduced in this pass and mirroring `ICliPlaybackDeviceSource` exactly
(same `*Probe` property/`Create*Device(selection)` method shape, same rationale). This exists
because `AudioDeviceFactory.CreateCaptureDevice` checks real PortAudio initialization state
*before* even consulting an injected probe, so a test that only fakes the probe (as
`RecognizeCommandTests` does for its own, narrower error-path-only coverage) still resolves to the
honestly unavailable fallback on any machine - including every headless CI runner - where real
PortAudio never initializes, regardless of the fake probe. `ICliCaptureDeviceSource` lets a test
substitute a fully in-memory fake (`FakeCaptureDeviceSource`, returning a fake, always-available
`IAudioCaptureDevice`) that never touches real PortAudio state at all, so `ask`'s mic-mode
happy-path tests are deterministic everywhere. `AudioDeviceFactoryCaptureDeviceSource` is the
production implementation, forwarding every call unchanged to a real `AudioDeviceFactory` - Phase
2's real capture-device resolution is exactly what it was before this seam was introduced.
`RecognizeCommand` itself is unchanged and still resolves its capture device directly from an
injected `AudioDeviceFactory`; it has the same latent limitation this seam works around for `ask`,
but this is a known, pre-existing, out-of-scope limitation, since none of `RecognizeCommand`'s own
unit tests currently exercise a happy path that resolves a real capture device.

**Listen-termination rule (first final result ends the turn).** Unlike `recognize --mic`, which
listens indefinitely until `--silence-timeout`/`Ctrl+C` alone, `ask`'s Phase 2 additionally stops
as soon as the first final recognition result arrives: `ask` models one bounded question/answer
turn ("listen for the reply"), not open-ended transcription, so exactly one final result is always
enough to end the turn and return control to the caller. `--silence-timeout`/`--start-timeout`
retain their exact `recognize --mic` meaning and defaulting as a safety net for a reply that never
finishes (or never starts) - both are entirely optional; omitting both leaves `Ctrl+C` as the only
way to end a turn with no reply at all, mirroring `recognize --mic`'s own behavior when
`--silence-timeout` is omitted.

**Output.** The final recognized text (or an empty string, if the session ended via
`--silence-timeout`/`--start-timeout`/`Ctrl+C` with no final result yet received) is printed to
stdout by default, or written to the file named by `--output-text` instead - mirroring
`recognize`'s own `--output-text` behavior exactly, including overwrite-not-append file semantics.
`ask` never prints interim results (there is no `--interim`/`--final-only` distinction to make),
so no carriage-return-overwrite console logic from `recognize` is reused here.

**Cancellation and disposal.** `Ctrl+C` is wired to cooperative cancellation via
`Console.CancelKeyPress` in `AskCommand.Run(Context)`, for the whole call rather than per-phase: a
single handler cancels a shared `CancellationTokenSource`, calls `StopAsync(CancellationToken.None)`
(fire-and-forget) on the live session once Phase 2 has created one (tracked under a lock since the
handler runs on a separate thread), and signals the same `ManualResetEventSlim` Phase 2 blocks on -
so `Ctrl+C` cancels cleanly whether it lands during playback or during listening, with no partial
recognized-text or playback state ever leaked. A canceled Phase 1 skips Phase 2 entirely (a
canceled prompt is never followed by a listen attempt), and the concurrently pre-warmed
engine/session is disposed via the fire-and-forget `DisposePrewarmedRecognizerAsync` path described
above. Phase 1's own session, engine, and playback-device lease are disposed via `await using`/
`using` in `SpeakPromptAsync`; Phase 2's session is disposed inside `Listen`'s own `finally` block,
and its engine is disposed by `RunAsync` itself (via `await using var engineLease = prewarmed.Engine;`)
once `Listen` returns.

**Distinguishing a genuine `Ctrl+C` from a legitimate empty Phase 2 result.** The same
`ManualResetEventSlim` unblocks Phase 2's wait identically whether the final wake-up came from a
final recognition result, a silence/start timeout, or `Ctrl+C` - and only the last of those is a
failure, not a successful (if empty) turn. Phase 2 therefore also receives the shared
`CancellationToken` (already threaded into Phase 1's `SpeakAsync` call) and, once its wait
unblocks, checks `cancellationToken.IsCancellationRequested` - true only when the `Ctrl+C` handler
canceled it, never for a timeout or a normal final result - to decide which case applies. This
check covers both the ordinary in-progress-listen race and the narrower edge case where `Ctrl+C`
already landed (and the shared `ManualResetEventSlim` was already set) before Phase 2 even started
a recognizer. When a genuine cancellation is detected, Phase 2 reports it the same way Phase 1
already does - `context.WriteError("Speech was canceled.")` - and the caller skips writing the
(irrelevant) recognized text to `--output-text`/stdout entirely, rather than silently treating an
interrupted turn as a successful, empty one. `RunAsync` re-checks
`cancellationToken.IsCancellationRequested` once more immediately after `Listen()` returns,
covering the narrow race where `Ctrl+C` lands in the gap between `Listen()` unblocking and
`RunAsync` resuming; and the `--output-text` file write itself passes that same
`CancellationToken` through to `File.WriteAllTextAsync` (rather than `CancellationToken.None`),
wrapped in a `try`/`catch (OperationCanceledException)` that reports and returns identically to
every other cancellation exit point above - so a cancellation observed only once the write is
already underway is handled the same way as one caught earlier, never silently completing and
reporting a false "Recognized text written" success.

### Interactions with Other Units

`AskCommand` depends on the `ICliModelCatalog` seam (all members added by both the synthesis and
recognition passes; no new member of its own), the library's public `ISpeechSynthesizerEngine`/
`ISynthesisSession`/`ISpeechRecognizerEngine`/`IRecognitionSession` types, `SynthesisCommandSubsystem`'s
`ICliPlaybackDeviceSource`/`ParameterBagParser`, `RecognitionCommandSubsystem`'s
`SilenceTimeoutRecognizerSession`, and `DeviceCommandsSubsystem`'s
`DevicesTestCommand.ResolveDeviceSelectionOrThrow` internal helper - all four reused entirely
unmodified, rather than duplicated. This pass introduces two new types local to this subsystem:
`ICliCaptureDeviceSource` and its production implementation
`AudioDeviceFactoryCaptureDeviceSource`, mirroring `SynthesisCommandSubsystem`'s
`ICliPlaybackDeviceSource`/`AudioDeviceFactoryPlaybackDeviceSource` pair exactly. No new
`DemaConsulting.Speech` library-level API is introduced by either.
