### AskCommand

**Purpose**: Run one voice-conversation turn as a single CLI invocation - `ask` speaks a prompt through a
synthesis model, then listens for a reply through a recognition model and delivers the first final recognized text.

**Data Model**: `AskOptions` is the parsed flag set: the required `TtsModelId` and `SttModelId`; the prompt source
(`Text` from `--text` or `FilePath` from `--file`; piped stdin is the third source); the optional
`PlaybackDeviceName` and `CaptureDeviceName` (null selects the system default device); the ordered raw
`--tts-param`/`--stt-param` pairs; the optional `SilenceTimeoutSeconds` and `StartTimeoutSeconds`; and the
optional `OutputPath` (`--output-text`, null prints to stdout). Two private records carry internal state:
`AskInvocation` bundles the context, catalog, options, and shared cancellation token, and `PrewarmedRecognizer`
pairs the already-created recognizer engine with its session. The built-in defaults are a 5-second silence
timeout and an 8-second start timeout.

**Key Methods**:

- `RunAsync(Context)` is the `CommandDispatch` entry point. It wires `Ctrl+C` once, for the whole call, to a shared
  cancellation token and a shared stop signal, then runs the turn. Internal overloads accept the catalog, the
  playback and capture device sources, the stop signal, and a recognizer-created callback so tests can drive the
  same flow.
- `ParseArguments` reads the flags with a hand-rolled loop. It requires both model ids, requires a value after every
  flag (a value that is itself a recognized option token is rejected as a missing value, while other dash-prefixed
  text is accepted), requires a positive finite number for each timeout, and
  validates each `--tts-param`/`--stt-param` token shape.
  Any other token, including `--interim`, `--final-only`, `--no-tags`, `--input`, and `--output-audio`, falls
  through to the same unsupported-argument rejection, so no separate rejection list exists.
- Text-source resolution runs first, because it is a cheap check that needs no model: `--text` and `--file`
  together are rejected; `--text` supplies the text; `--file` reads the whole file; with neither flag, redirected
  stdin supplies the text, and a console with no redirected input is rejected. Piped stdin is read only when
  neither flag is given. An unreadable or missing `--file` is reported as an `InvalidOperationException` naming the file,
  as
  `speak` does, so it is a clean error rather than an unexpected failure. Empty
  prompt text is not rejected; it is passed to the synthesis session as is.
- Model resolution runs next: the synthesis model first, then the recognition model, each failing with an
  actionable message naming its own flag for an unknown id, a wrong role, or a model not yet downloaded.
  `--tts-param`/`--stt-param` values are then resolved against the respective model's declared parameters with
  `ParameterBagParser.Resolve`, exactly as `speak` does for `--tts-param` and `recognize` does for `--stt-param`:
  a numeric value is parsed as a number and range-checked, a choice value is matched against the declared options,
  a boolean value is parsed as a boolean, and an undeclared key is rejected, all before any engine is loaded.
- `PrewarmRecognizerAsync` resolves the capture device, creates the recognizer engine, and creates a session bound
  to that device, without starting capture. `RunAsync` starts it on a background task before the prompt is spoken
  (see _Recognizer pre-warming_ below).
- `SpeakPromptAsync` runs the first phase: it resolves the playback device, creates the synthesizer engine and its
  session, and speaks the text, returning whether playback was canceled.
- `Listen` runs the second phase on the already-created session, wrapped in the reused
  `SilenceTimeoutRecognizerSession`, and returns the reply text and whether the turn was canceled.
- `DisposePrewarmedRecognizerAsync` releases a pre-warmed recognizer that will never be used.

**Design notes**:

_Reuse of existing catalog members._ `ask` needs one synthesis-role model and one recognition-role model, never one
model serving both roles. It therefore calls the existing synthesizer-engine and recognizer-engine members of
`ICliModelCatalog` once each, as `speak` and `recognize` do individually, and needs no combined catalog member. Each
engine is created with no device bound; the device is bound when the session is created.

_Two-phase execution._ The prompt is spoken to completion before listening begins: `ask` never starts microphone
capture while the prompt is still playing. The playback device is resolved, and found unknown or unavailable,
before any synthesis starts, so a playback-device failure happens before the prompt is spoken. The first final
recognition result ends the listen phase, because `ask` models one bounded question and answer rather than
open-ended transcription.

_Recognizer pre-warming._ Loading a recognition model is the expensive step in creating a recognizer. If the load
started only after the prompt finished, the reply window would open late and the start of a reply could be missed.
`RunAsync` therefore creates the recognizer on a background task while the prompt plays, so it is ready when
playback ends. Only the load and the session creation are brought forward: starting the session, which begins real
microphone capture, happens only once the listen phase runs, so capture never picks up the prompt. The capture
device is resolved inside this pre-warm step because creating a recognition session requires an existing capture
device. Three exit paths govern the pre-warmed recognizer:

- When the prompt is spoken successfully, `RunAsync` awaits the pre-warm task, which re-throws any failure it
  raised, for example an unknown or unavailable capture device or an invalid `--stt-param` value. A capture-device
  failure is therefore reported only after the prompt has been spoken, which is the capture-side counterpart of the
  playback-side ordering above. If session creation fails after the engine loaded, the engine is disposed before the
  failure is re-thrown.
- When the prompt is canceled, or the first phase throws, the pre-warm task is handed to
  `DisposePrewarmedRecognizerAsync` without being awaited. That helper later observes the task and disposes the
  session and the engine if they were produced; the engine is
  disposed in a `finally` so a throwing session dispose cannot leak it, and the first exception is preserved. It
  swallows any pre-warm failure, which is no longer actionable. Not
  awaiting keeps `Ctrl+C` and fast-failure paths from waiting for a slow model load.
- The pre-warm task is released on every path: a flag marks when `RunAsync` has adopted the recognizer, and a
  `finally` block disposes it otherwise, so no broad catch is needed.
- The listen phase disposes the session in its own `finally` block, and `RunAsync` disposes the engine afterward.

`SpeechModelCatalogAdapter`, the only production catalog implementation, wraps a read-mostly catalog with no
documented thread-safety concern for the concurrent engine creations. A future catalog implementation with
non-thread-safe shared side effects would need to account for that concurrency.

_Listen termination and timeouts._ The listen phase wraps the session in `SilenceTimeoutRecognizerSession`, always,
even when both timeout flags are omitted, so a reply that never starts or never finishes cannot block the command.
The timeouts interact with results as follows:

- The **start timeout** (`--start-timeout`, default 8 seconds) applies before any recognition result of any kind,
  interim or final, has arrived. If none arrives within it, the turn ends with empty text.
- The **silence timeout** (`--silence-timeout`, default 5 seconds) applies after the first result. Every interim
  result re-arms it, and if no further result arrives within it, the turn ends. If no final result has arrived by
  then, the text is empty; interim text is never delivered as the reply.
- The **first final result** ends the turn immediately, whatever timeout is still pending, and its text is the
  reply. A final result never waits on a timeout.
- A timeout ending the turn is a normal outcome (exit code zero), not a cancellation.

_Output._ The reply text, or an empty string when the turn ended by a timeout with no final result, is printed to
stdout by default or written to the `--output-text` file, replacing any previous content and leaving a completely
empty file for an empty reply. `ask` never prints interim results.

_Cancellation._ Cancellation during the setup steps (creating the synthesizer engine or session, or adopting the
pre-warmed recognizer) is reported with the same clean message as cancellation during playback.
`Ctrl+C` cancels one shared token,
stops the live listen session, and sets the shared stop signal,
so it works during playback or during listening. A canceled prompt skips the listen phase entirely and reports
"Speech was canceled." A `Ctrl+C` during the listen phase is told apart from a timeout by the shared token, which
only `Ctrl+C` cancels: it is reported the same way, and no text is printed and no file is written. The token is
re-checked after the listen phase returns and immediately before each output step, and the file write itself
receives the token, so a cancellation observed at any of those points is reported identically rather than as a
false success.

_Global-option absorption._ Global options are recognized in any position before `ask` reaches `ParseArguments`,
so a flag value that equals a global flag (for example `--text -v`) is absorbed as that global option and is never
passed to `ask`. A value that must look like a global flag cannot be supplied (see _SpeechCli Design_).

**Error Handling**: Operator mistakes are reported as `ArgumentException` (unsupported or valueless flag, missing
model id, bad timeout, conflicting or absent text source, unknown or wrong-role or not-downloaded model, rejected
parameter, unknown device name) or `InvalidOperationException` (no playback or capture device available), which
the entry point turns into a clean message and non-zero exit code. Playback problems surface before the prompt is
spoken; capture problems and recognizer-creation failures surface after it. Cancellation is reported through the
context's error output with a non-zero exit code and is not an exception. Every engine, session, and device is
disposed exactly once on every exit path.

**Dependencies**: `ICliModelCatalog` (model enumeration and the synthesizer and recognizer engine members); the
library's `ISpeechSynthesizerEngine`, `ISynthesisSession`, `ISpeechRecognizerEngine`, `IRecognitionSession`, and
audio device types; `ICliPlaybackDeviceSource` and `ParameterBagParser` from the SynthesisCommandSubsystem;
`SilenceTimeoutRecognizerSession` from the RecognitionCommandSubsystem; `DevicesTestCommand`'s device-selection
resolution from the DeviceCommandsSubsystem; and `ICliCaptureDeviceSource`.

**Callers**: `CommandDispatch`, which routes the `ask` subcommand here.
