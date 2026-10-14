## SpeechCli RecognitionCommandSubsystem Design

![RecognitionCommandSubsystem Structure](RecognitionCommandSubsystemView.svg)

### Overview

The RecognitionCommandSubsystem implements the one speech-to-text subcommand dispatched to by
`CommandDispatch`: `recognize`. It contains two units, each documented in its own file:

- **RecognizeCommand**: implements
  `recognize --stt-model <id> (--input <wav-path> | --mic) [--capture-device <name>]
  [--silence-timeout <seconds>] [--start-timeout <seconds>] [--stt-param key=value ...]
  [--interim | --final-only] [--output-text <text-path>]` - see _RecognizeCommand_
- **SilenceTimeoutRecognizerSession**: an idle-timeout decorator around a recognition session's
  result stream, used in microphone mode (and reused by `AskCommand`) - see
  _SilenceTimeoutRecognizerSession_

### Interfaces

**Exposed**: `RecognizeCommand.RunAsync(Context)` (the `CommandDispatch` entry point) and the
`SilenceTimeoutRecognizerSession` decorator, which `AskCommand` also reuses. Operator mistakes are
reported as `ArgumentException` or `InvalidOperationException`, which `Program` renders as a clean,
non-zero-exit-code error.

**Consumed**: `ICliModelCatalog` (`Enumerate` and `CreateRecognizerEngineAsync`), the library's
`ISpeechRecognizerEngine`, `IRecognitionSession`, `SpeechRecognitionEvent`,
`SpeechRecognitionResult` and `AudioFormat`, the ConversationCommandSubsystem's
`ICliCaptureDeviceSource` interface (`CaptureProbe` and `CreateCaptureDevice`, the seam
`RecognizeCommand` resolves a real microphone through), `WavFileAudioCaptureDevice`,
`ParameterBagParser`, and `DevicesTestCommand.ResolveDeviceSelectionOrThrow`. The production
`AudioDeviceFactory` reaches `RecognizeCommand` only transitively: the `RunAsync(Context)` entry
point composes it and wraps it in the conversation subsystem's production `ICliCaptureDeviceSource`
implementation, whose ownership stays with that subsystem (see its own design document).

### Design

`RecognizeCommand` is the subsystem's only entry point; `SilenceTimeoutRecognizerSession` is its
collaborator for microphone mode. The two units interact as follows.

**File input (`--input`).** `RecognizeCommand` pumps the session's own result stream directly. No
`SilenceTimeoutRecognizerSession` is created, because a file session ends on its own once the whole
file has been delivered and drained. The idle-timeout flags are still parsed and validated but
have no effect in this mode.

**Microphone input (`--mic`).** `RecognizeCommand` always wraps the session's result stream in a
`SilenceTimeoutRecognizerSession`, even when both timeout flags are omitted, so listening can
never block forever with only `Ctrl+C` as an escape hatch. `RecognizeCommand` resolves the two
idle windows (start timeout 8 seconds, silence timeout 5 seconds by default, each independent of
the other) and hands them to the decorator. The decorator ends the session by stopping the wrapped
session when the active window elapses; stopping completes the result stream, which ends the
command's result pump. `Ctrl+C` reaches the same terminal state by stopping the session directly.
The run ends successfully when the idle timeout ends the session (the microphone idle-end
behavior) and the capture device, session and engine are then released.

**Cancellation in file mode.** When `Ctrl+C` lands while a file is being processed, the drain
`StopAsync` that honors the canceled token may itself throw a cancellation. `RecognizeCommand`
catches that and stops the session again with a non-cancelable token, so the session always reaches
a terminal state, the result pump ends, and the command completes gracefully rather than
surfacing an unhandled cancellation.

**Value-taking flags.** Every flag that takes a value (`--stt-model`, `--input`,
`--capture-device`, `--silence-timeout`, `--start-timeout`, `--stt-param`, `--output-text`)
rejects, with an `ArgumentException` naming the flag and stating that it requires a value, a
following token that is one of `recognize`'s own option tokens; a value that merely begins with a
dash but is not one of those options is accepted. Timeout values must also be finite, positive
numbers: `NaN` and infinities are rejected with the same error as any other malformed timeout.

**Role validation.** `RecognizeCommand` validates the model's role itself, before asking the seam
for an engine, so the operator receives a message naming the model and suggesting
`list-models --role stt`. The seam's own refusal to create an engine for a model that is not a
recognition model is a defensive second line owned by the ModelCommandsSubsystem; it is not
reachable through the CLI's own dispatch. The `IRecognitionModel` narrowing stays confined to
`SpeechModelCatalogAdapter`, so no unit in this subsystem touches `IRecognitionModel` or any other
internal library type.

**Global options.** Global options are recognized anywhere on the command line, so a `recognize`
value that equals a global flag is absorbed as that global option rather than passed to
`recognize` (see the system design); a value that must look like a global flag cannot be supplied.

### Interactions with Other Units

`RecognizeCommand` depends on the `ICliModelCatalog` seam (only `Enumerate` and
`CreateRecognizerEngineAsync`), the library's public recognition types and `AudioFormat`,
the conversation subsystem's `ICliCaptureDeviceSource` seam (the library's `AudioDeviceFactory`
is reached only through that seam's production implementation), and `WavFileAudioCaptureDevice`. It reuses the DeviceCommandsSubsystem's
`DevicesTestCommand.ResolveDeviceSelectionOrThrow` helper and the SynthesisCommandSubsystem's
`ParameterBagParser` rather than duplicating either. `SilenceTimeoutRecognizerSession` depends only
on the library's public `IRecognitionSession` and `SpeechRecognitionEvent` types and the BCL
`TimeProvider`. `CommandDispatch` calls `RecognizeCommand`, and the ConversationCommandSubsystem's
`AskCommand` reuses `SilenceTimeoutRecognizerSession`.
