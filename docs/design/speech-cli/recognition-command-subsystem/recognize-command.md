### RecognizeCommand

**Purpose**: Implement the `recognize` subcommand: stream speech-to-text results from a
pre-recorded mono WAV file (`--input`) or a real microphone (`--mic`) through a known, downloaded
recognition model, printing the recognized text and optionally writing the final results to a text
file.

**Data Model**: `RecognizeOptions`, an immutable record produced by argument parsing:

| Member | Type | Purpose |
| --- | --- | --- |
| `ModelId` | `string` | The required `--stt-model` id |
| `InputPath` | `string?` | The WAV file from `--input`; null selects microphone mode |
| `Mic` | `bool` | Whether `--mic` was given |
| `DeviceName` | `string?` | The `--capture-device` name; null selects the system default |
| `SilenceTimeoutSeconds` | `double?` | The `--silence-timeout` value; null selects the 5-second default |
| `StartTimeoutSeconds` | `double?` | The `--start-timeout` value; null selects the 8-second default |
| `RawParameters` | list of key/value pairs | The repeatable `--stt-param` tokens, in the order given |
| `InterimOnly` | `bool` | Whether `--interim` was given |
| `FinalOnly` | `bool` | Whether `--final-only` was given |
| `OutputPath` | `string?` | The `--output-text` file; null means console only |

A private per-run `ConsoleLineState` records the length of the longest interim text written since
the last time the console line was settled.

**Key Methods**:

- **RunAsync(context)**: The `CommandDispatch` entry point. It composes the real model catalog and
  `AudioDeviceFactory` and delegates to the injectable overloads, which exist so the command is
  testable without a real catalog, network or audio hardware. One overload owns a cancellation
  source tied to `Ctrl+C` for the whole call.
- **ParseArguments(args)**: Parses the command's own flags with the same hand-rolled loop every
  command in this tool uses. It requires `--stt-model`, rejects an unsupported argument or a
  value-less flag, and requires `--silence-timeout` and `--start-timeout` to be positive numbers
  of seconds. Each `--stt-param` token is shape-checked inline as it is parsed. Because global
  options are recognized anywhere on the command line, a value that equals a global flag is
  absorbed as that global option rather than reaching this parser (see the system design).
- **Validation ordering**: Input-source mutual exclusion (`--input` versus `--mic`) is checked
  first among the semantic checks, then `--interim` versus `--final-only`, then model resolution,
  so a cheap, input-independent usage mistake is reported before an unrelated unknown-model
  error. This is the same ordering `SpeakCommand` uses.
- **Model resolution**: Looks the requested id up in the catalog and distinguishes three
  failures, each with its own actionable message: an id absent from the catalog (suggesting
  `list-models`), an id with the wrong role (naming the model and suggesting
  `list-models --role stt`), and a recognition model that is not downloaded (suggesting
  `download <modelId>`). The resolved model's declared parameters then validate the `--stt-param`
  values through `ParameterBagParser`.
- **Capture device resolution**: With `--input` the device is a `WavFileAudioCaptureDevice` over
  the given file, and `--capture-device` is silently inert, mirroring `speak`'s treatment of
  `--playback-device` with a file destination. With `--mic` the command enumerates the capture
  devices, validates any requested `--capture-device` name against them before a device is
  created (the same validate-before-create pattern as `devices test` and `speak`), and creates
  the device through the `ICliCaptureDeviceSource` seam so a test can substitute a fake. A
  resolved device that is not available is an error that suggests `--input` as the alternative.
- **Session flow**: The command loads the engine through
  `ICliModelCatalog.CreateRecognizerEngineAsync` and binds a session with
  `engine.CreateSessionAsync(captureDevice, ...)`. Both are held with `await using`, so the
  session is disposed before the engine on every exit path, with no nested `try`/`finally`. The
  session is driven the same way in both input modes through `StartAsync`, `StopAsync` and
  `GetResultsAsync`.
- **File mode**: A session over a `WavFileAudioCaptureDevice` delivers every frame before
  `StartAsync` returns, because that device's capture is fully synchronous. The command follows
  `StartAsync` with an explicit `StopAsync`, the session's documented drain, so audio that is
  buffered but not yet decoded is flushed and every result for the whole file exists before the
  result pump settles. The command adds no settle wait and no fixed sleep, and no
  `SilenceTimeoutRecognizerSession`.
- **Microphone mode**: The command always wraps the result stream in a
  `SilenceTimeoutRecognizerSession` constructed from the resolved silence and start windows. It
  starts the result pump as a genuinely asynchronous call before awaiting `StartAsync`, so interim
  results print as they arrive: the session's buffer keeps only the latest interim update when
  its single consumer is not reading. A `Console.CancelKeyPress` handler stops the session with a
  fire-and-forget `StopAsync`, which completes the result stream and ends the pump; the
  cancellation token is also passed into the result stream as a safety net for the case where
  `StopAsync` on a session that never started does not itself complete the stream.
- **Console output**: By default an interim result is written with a carriage return and no
  trailing newline, so a live console redraws the hypothesis in place, and a final result is
  written newline-terminated, settling the line. With redirected output the carriage-return
  behavior degrades to one line per event, an accepted trade-off. `--interim` prints only interim
  results and `--final-only` prints only final results. Overwriting pads over the longest interim
  text printed since the last settle, so a hypothesis that shrinks never leaves stale trailing
  characters. Any pending interim line is settled before the closing "Recognition finished." (or
  "Recognition results written to '<path>'.") message.
- **--output-text**: Writes each final result as one line, flushed immediately. Interim results
  are a live-console concept and are never written. The file is opened once with overwrite
  semantics, consistent with `speak --output-audio`.

**Error Handling**: Operator mistakes throw `ArgumentException` with an actionable message: a
missing `--stt-model`, an unsupported argument, a value-less flag (including a flag directly followed
by another `recognize` option), a non-positive, non-finite or malformed
timeout, both or neither of `--input` and `--mic`, both `--interim` and `--final-only`, an
unknown, wrong-role or not-downloaded model, an invalid `--stt-param`, and an unknown
`--capture-device`. A capture device that is not available in microphone mode throws
`InvalidOperationException`; so does an `--input` file that is not a WAV file or is not mono
16-bit PCM, because the WAV device rejects it when its capture starts, and the message
names the file or the mono requirement. Null arguments to the entry points throw
`ArgumentNullException`. A cancellation that ends the result pump is treated as graceful
termination; in file mode, if the drain `StopAsync` itself throws a cancellation, the command
stops the session again with an uncancelable token before completing.
Neither `IAudioCaptureDevice` nor its implementations are disposable, so the command
needs no capture-device disposal; the engine and session are released by `await using`.

**Dependencies**: `ICliModelCatalog`, `ICliCaptureDeviceSource`, `AudioDeviceFactory`,
`WavFileAudioCaptureDevice`, the library's `ISpeechRecognizerEngine`, `IRecognitionSession`,
`SpeechRecognitionEvent` and `SpeechRecognitionResult`, `ParameterBagParser`,
`DevicesTestCommand.ResolveDeviceSelectionOrThrow`, `ConsoleCancellation`,
`SilenceTimeoutRecognizerSession`, and `Context`.

**Callers**: `CommandDispatch`, which dispatches the `recognize` subcommand to `RunAsync(Context)`.
