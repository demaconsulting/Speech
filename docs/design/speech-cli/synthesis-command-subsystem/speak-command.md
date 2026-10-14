### SpeakCommand

**Purpose**: Implement the `speak` subcommand: turn operator-supplied text into speech through a
known, downloaded synthesis model, either playing it on a real playback device or writing it to a
WAV file.

**Data Model**: The parsed flags are held in an internal `SpeakOptions` record: the required
`--tts-model` id, the optional `--text`, `--file`, `--output-audio` and `--playback-device`
values, the `--no-tags` switch, and the repeatable `--tts-param` tokens as raw `(key, value)` pairs
in the order given.

**Key Methods**:

- **RunAsync(Context)**: The `CommandDispatch` entry point. It composes the real model catalog and a
  real playback-device source over the library's `AudioDeviceFactory`, wires `Ctrl+C` to a
  cancellation source through the shared `ConsoleCancellation.Register` helper (which owns the
  subscribe/unsubscribe lifetime), and delegates to the injectable overloads.
- **RunAsync(Context, ICliModelCatalog, ICliPlaybackDeviceSource, CancellationToken)**: The full
  command, in this order:
  1. Parse the flags. A missing `--tts-model`, an unsupported argument, or a flag without its value
     is rejected. A flag value that equals a global flag (for example `speak --text -v`) is
     consumed as the global option before `speak` sees it, as the system design describes, so it
     is not available as flag text.
  2. Resolve the text source, before the model is looked up. Exactly one of `--text`, `--file`, or a
     redirected stdin stream supplies the text. Supplying both `--text` and `--file`, or none while
     stdin is not redirected, is rejected immediately. A text-source mistake is cheap and
     model-independent, so it is reported ahead of an unrelated "unknown model" error. Whether stdin
     is redirected, and the reader that supplies piped text, come from two optional trailing
     parameters of the full `RunAsync` overload that default to the console's own state and input;
     tests substitute them so the stdin paths run deterministically on any host.
  3. When `--no-tags` is given, strip every Natural Language Audio Tag unconditionally by parsing
     the text with `AudioTagParser` and keeping only the plain-text spans, whatever audio-tag support
     the model declares. An operator who asks for tags to be stripped wants literal narration only.
     Text that is empty or whitespace-only at this point (including text that held only tags and
     was stripped) is rejected with an `ArgumentException` before any model or engine work.
  4. Resolve the model by looking `--tts-model` up in the catalog's enumeration. Three distinct
     failures each carry an actionable message: an id absent from the catalog (suggests
     `list-models`), an id with the wrong role (names the model and its actual role, and suggests
     `list-models --role tts`), and a synthesis model that is not downloaded (suggests
     `download <modelId>`).
  5. Resolve each `--tts-param` against the model's declared parameters through
     `ParameterBagParser`, which yields typed values (numeric to a number, choice to its option
     string, boolean to a bool).
  6. Resolve the playback device. With `--output-audio <path>`, the model's preferred audio format,
     obtained from the catalog, sizes a `WavFileAudioPlaybackDevice` at that path, and
     `--playback-device` is ignored because an explicit file destination wins and supplying both is
     not an error. Otherwise the requested `--playback-device` name is validated against the devices
     the source enumerates (through `DevicesTestCommand.ResolveDeviceSelectionOrThrow`, before any
     device is created), then the source creates the device; an unavailable device is rejected with
     a message suggesting `--output-audio`.
  7. Load the engine through the catalog, create a session bound to the device, speak, and report
     `Speech playback finished.` or `Speech written to '<path>'.`.
- **Disposal**: The session and the engine are held with `await using`, declared after the device's
  disposal lease, so disposal runs session first, then engine, then the device. Session disposal
  guarantees any in-flight playback or write has quiesced before the engine is torn down, and the
  engine is torn down before the device. The device lease is a conditional `IDisposable` cast
  because `IAudioPlaybackDevice` does not itself declare `IDisposable`, while
  `WavFileAudioPlaybackDevice` does (it finalizes its RIFF header on disposal); the cast disposes it
  when present and is a no-op for every other device kind.

**Error Handling**: Operator mistakes (the flag, text-source, model, parameter and device-name
failures above) are thrown as `ArgumentException`; an unavailable playback device is thrown as
`InvalidOperationException`; both reach `Program`, which prints a one-line error and returns a
non-zero exit code. A cancellation during speaking (for example `Ctrl+C`) is caught and reported as
the one-line error `Speech was canceled.`, so the exit code is non-zero and no stack trace appears.
A null context, catalog or device source is rejected with `ArgumentNullException`.

Two text-source situations are handled as follows. A `--file` that names a missing or unreadable
file is reported as an `InvalidOperationException` wrapping the file-system failure, so `Program`
prints a clean one-line error naming the file and returns a non-zero exit code; a test covers it.
Empty or whitespace-only text is rejected with an `ArgumentException` (reported as a one-line error
and a non-zero exit code) rather than being passed to the session, so a scripting mistake such as an
empty variable does not silently "succeed".

**Value-taking flags.** `--tts-model`, `--text`, `--file`, `--output-audio`, `--playback-device`
and `--tts-param` each require a value. If the next token is absent, or is one of `speak`'s own
option tokens, parsing is rejected with an `ArgumentException` saying the flag requires a value.
A value that merely begins with a dash but is not one of those options (for example `-1` or
narration text) is accepted.

**Dependencies**: `ICliModelCatalog` (`Enumerate`, `GetPreferredAudioFormat`,
`CreateSynthesizerEngineAsync`); `ICliPlaybackDeviceSource`; `ParameterBagParser`;
`DevicesTestCommand.ResolveDeviceSelectionOrThrow` from `DeviceCommandsSubsystem`;
`ConsoleCancellation`; the library's `ISpeechSynthesizerEngine`, `ISynthesisSession`,
`AudioFormat`, `AudioTagParser`, `TaggedTextSpan` and `WavFileAudioPlaybackDevice`.

**Callers**: `CommandDispatch`, which routes the `speak` subcommand here.
