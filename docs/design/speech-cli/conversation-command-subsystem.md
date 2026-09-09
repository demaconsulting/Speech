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
`SpeakAsync` on the resolved text, waiting for it to complete before Phase 2 begins - there is no
concurrent speak/listen; the two phases are strictly sequential, matching a natural
question-then-answer conversational turn. Phase 2 (listen) resolves a real capture device from
the injected `AudioDeviceFactory` (honoring `--capture-device`, defaulting to the system default
device exactly as `recognize --mic` does), constructs the STT recognizer via `CreateRecognizer`,
and blocks on a `ManualResetEventSlim` until one of: the first **final** recognition result
arrives, a `SilenceTimeoutRecognizerSession` (constructed and armed exactly as `recognize --mic`'s
own, reused unmodified from `RecognitionCommandSubsystem`) times out, or `Ctrl+C` is pressed.

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

### Interactions with Other Units

`AskCommand` depends on the `ICliModelCatalog` seam (all members added by both the synthesis and
recognition passes; no new member of its own), `SynthesisCommandSubsystem`'s
`ICliPlaybackDeviceSource`/`ParameterBagParser`, `RecognitionCommandSubsystem`'s
`SilenceTimeoutRecognizerSession`, and `DeviceCommandsSubsystem`'s
`DevicesTestCommand.ResolveDeviceSelectionOrThrow` internal helper - all four reused entirely
unmodified, rather than duplicated. `AskCommand` is the only unit in this subsystem; no new
seam, session, or parser type is introduced anywhere in this pass.
