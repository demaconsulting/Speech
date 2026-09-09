## SpeechCli ConversationCommandSubsystem Design

### Overview

The ConversationCommandSubsystem implements the one voice-conversation subcommand dispatched to
by `CommandDispatch`: `ask` - a new, eleventh subcommand added after all ten scaffolded
subcommands were implemented. It contains one type, `AskCommand`, and introduces **no new
`ICliModelCatalog` seam member**: `ask` combines `SynthesisCommandSubsystem`'s existing
`GetPreferredAudioFormat`/`CreateSynthesizer` members and `RecognitionCommandSubsystem`'s existing
`GetAudioFormat`/`CreateRecognizer` members - both already extending the original
`ModelCommandsSubsystem` seam - to run a speak-then-listen turn as one CLI invocation.

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

`ask` needs to construct both a real `ISpeechSynthesizer` and a real `ISpeechRecognizer` for its
two phases. Both capabilities already exist on `ICliModelCatalog`, added by the synthesis and
recognition passes respectively: `CreateSynthesizer(descriptor, playbackDevice, parameterValues)`
and `CreateRecognizer(descriptor, captureDevice, parameterValues)`, each already validating (via
`SpeechModelCatalogAdapter`'s own internal casts) that the resolved model actually has the
corresponding role. Because `ask` requires exactly one synthesis-role model and exactly one
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

**Two-phase execution.** Phase 1 (speak) resolves a real playback device from the injected
`ICliPlaybackDeviceSource` (honoring `--playback-device`, defaulting to the system default device
exactly as `speak` does), constructs the TTS synthesizer via `CreateSynthesizer`, and calls
`SpeakAsync` on the resolved text, waiting for it to complete before Phase 2's listen phase can
end - Phase 2's recognizer construction now begins concurrently with Phase 1's wait rather than
strictly once it finishes (see "Recognizer Pre-Warming" below); the two phases' *listening*
remains strictly sequential, matching a natural question-then-answer conversational turn - `ask`
never starts real microphone capture while the prompt is still being spoken. Phase 2 (listen)
resolves a real capture device from the injected `ICliCaptureDeviceSource` (honoring
`--capture-device`, defaulting to the system default device exactly as `recognize --mic` does),
constructs the STT recognizer via `CreateRecognizer`, and blocks on a `ManualResetEventSlim` until
one of: the first **final** recognition result arrives, a `SilenceTimeoutRecognizerSession`
(constructed and armed exactly as `recognize --mic`'s own, reused unmodified from
`RecognitionCommandSubsystem`) times out, or `Ctrl+C` is pressed.

### Recognizer Pre-Warming (Concurrent Phase 1/Phase 2 Model Load)

Loading a recognition model into native memory - not `Start()`, which merely begins streaming
audio through an already-loaded model - is the expensive step in constructing an
`ISpeechRecognizer` (see `SpeechRecognizerFactory`'s own remarks). Deferring that load until
strictly after Phase 1's playback finishes therefore introduced an avoidable turnaround-gap
latency between finishing speaking and starting to listen. `AskCommand.RunAsync` now kicks off a
private `PrewarmRecognizer` method - which resolves Phase 2's capture device (via
`ICliCaptureDeviceSource`, honoring `--capture-device`) and then calls `CreateRecognizer`, exactly
what `Listen` used to do at its own start - on a background `Task.Run` immediately after both
phases' parameters are resolved, so this work overlaps with the `await SpeakPromptAsync(...)` call
that follows it. Capture-device resolution moved into this pre-warm step alongside recognizer
construction because `ICliModelCatalog.CreateRecognizer` requires an already-constructed
`IAudioCaptureDevice` as an argument - it cannot be deferred independently of the recognizer
itself. Critically, only the model *load* is pre-warmed: `ISpeechRecognizer.Start()` (which begins
real microphone capture) is still called only once Phase 2's `Listen` genuinely runs, so
pre-warming never risks capturing audio - including any acoustic bleed from the prompt still being
played - while Phase 1 is in progress.

`RunAsync` unconditionally awaits the pre-warm task before returning, on every exit path, so its
result and any exception it raises are never left unobserved:

- **Phase 1 canceled**: the pre-warm task (whether already completed, still in flight, or
  destined to fail) is awaited and, if it produced a recognizer, that recognizer is disposed,
  via a small `DisposePrewarmedRecognizerAsync` helper that swallows any pre-warm exception -
  Phase 1's own cancellation has already been reported, and a concurrently failed pre-warm is not
  separately actionable once the call is already ending in cancellation.
- **Phase 1 succeeds**: `RunAsync` awaits the pre-warm task directly, which re-throws (with its
  original exception type, message, and stack trace) any failure `PrewarmRecognizer` raised - an
  unknown/unavailable `--capture-device`, an invalid `--stt-param` value, etc. - at the start of
  Phase 2, exactly as a synchronous call to the same logic would have. `Listen` itself is
  simplified to accept the already-constructed recognizer directly, rather than constructing one
  of its own; it still checks `stopSignal.IsSet` first (now also covering the case where `Ctrl+C`
  landed during the concurrent pre-warm itself) before ever calling `Start()`, and still calls
  `onRecognizerCreated`/disposes the recognizer in its own `finally` block exactly as before, so
  the disposal-ordering/callback-timing contract several unit tests depend on is unchanged.

This is a design note for future `ICliModelCatalog` implementers: `SpeechModelCatalogAdapter`
(the only production implementation) wraps a read-mostly `SpeechModelCatalog` with no documented
thread-safety concerns for concurrent `CreateSynthesizer`/`CreateRecognizer` calls on the same
instance, which is what makes running `CreateRecognizer` concurrently with Phase 1's
`CreateSynthesizer`-backed playback safe today; a future catalog implementation with non-thread-safe
side effects shared across both calls would need to account for this concurrency.

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
`Console.CancelKeyPress`, exactly mirroring `SpeakCommand.Run`'s and `RecognizeCommand.Run`'s own
subscribe/unsubscribe-in-try/finally pattern, for the whole call rather than per-phase: a single
handler both cancels a shared `CancellationTokenSource` (aborting an in-flight `SpeakAsync` during
Phase 1) and, once a recognizer has been constructed, calls its own `Stop()` and signals the same
`ManualResetEventSlim` Phase 2 blocks on - so `Ctrl+C` cancels cleanly whether it lands during
playback or during listening, with no partial recognized-text or playback state ever leaked. A
canceled Phase 1 skips Phase 2 entirely (a canceled prompt is never followed by a listen attempt).
The synthesizer, playback device, recognizer, silence-timeout session (when present), and output
writer are each disposed exactly once via nested `finally` blocks in the same disposal order
`SpeakCommand`/`RecognizeCommand` each already established for their own resources.

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
recognition passes; no new member of its own), `SynthesisCommandSubsystem`'s
`ICliPlaybackDeviceSource`/`ParameterBagParser`, `RecognitionCommandSubsystem`'s
`SilenceTimeoutRecognizerSession`, and `DeviceCommandsSubsystem`'s
`DevicesTestCommand.ResolveDeviceSelectionOrThrow` internal helper - all four reused entirely
unmodified, rather than duplicated. This pass introduces two new types local to this subsystem:
`ICliCaptureDeviceSource` and its production implementation
`AudioDeviceFactoryCaptureDeviceSource`, mirroring `SynthesisCommandSubsystem`'s
`ICliPlaybackDeviceSource`/`AudioDeviceFactoryPlaybackDeviceSource` pair exactly. No new
`DemaConsulting.Speech` library-level API is introduced by either.
