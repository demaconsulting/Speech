## SpeechCli SynthesisCommandSubsystem Design

![SynthesisCommandSubsystem Structure](SynthesisCommandSubsystemView.svg)

### Overview

The SynthesisCommandSubsystem implements the one text-to-speech subcommand dispatched to by
`CommandDispatch`: `speak`. It contains four units, each with its own design document under
`synthesis-command-subsystem/`:

- **SpeakCommand**: implements
  `speak --tts-model <id> (--text <string> | --file <path> | stdin) [--output-audio <wav-path>]
  [--playback-device <name>] [--tts-param key=value ...] [--no-tags]`
- **ParameterBagParser**: parses and validates repeatable `--tts-param key=value` tokens against a
  resolved model's declared parameters; it is also the parser behind `--stt-param`
- **ICliPlaybackDeviceSource**: a small CLI-owned seam over real-playback-device resolution
- **AudioDeviceFactoryPlaybackDeviceSource**: the production implementation of that seam, backed
  by the library's real `AudioDeviceFactory`

The subsystem also consumes two members of the `ICliModelCatalog` seam owned by
`ModelCommandsSubsystem`: `GetPreferredAudioFormat` and `CreateSynthesizerEngineAsync`.

### Interfaces

**Exposed**: `SpeakCommand.RunAsync(Context)` (the `CommandDispatch` entry point, which awaits the
asynchronous work), `ParameterBagParser.Resolve` and `ParameterBagParser.ParseToken` (consumed by
the recognition and conversation command subsystems), and the CLI-owned `ICliPlaybackDeviceSource`
seam. Operator mistakes are reported as `ArgumentException` or `InvalidOperationException`, which
`Program` turns into a one-line error and a non-zero exit code.

**Consumed**: `ICliModelCatalog` (`Enumerate`, `GetPreferredAudioFormat`,
`CreateSynthesizerEngineAsync`), the library's `ISpeechSynthesizerEngine`/`ISynthesisSession`,
`AudioFormat`, `AudioTagParser`/`TaggedTextSpan`, `AudioDeviceFactory`, `WavFileAudioPlaybackDevice`,
and `DevicesTestCommand.ResolveDeviceSelectionOrThrow` from `DeviceCommandsSubsystem`.

### Design

`SpeakCommand` validates its arguments, resolves the text source, resolves the model through the
catalog seam, loads the engine, binds a playback device (real playback through
`ICliPlaybackDeviceSource`, or a WAV file for `--output-audio`), speaks, and disposes everything in
a fixed order. `ParameterBagParser` is a stateless helper.

**Ownership of ParameterBagParser.** It is a unit of this subsystem: its source, unit tests,
requirements and design live here, and it is reviewed with this subsystem. The recognition and
conversation command subsystems consume it through its internal `Resolve` and `ParseToken` methods,
exactly as they consume this subsystem's `ICliPlaybackDeviceSource`, and so depend on this
subsystem.

**Seam use.** `speak` needs a real `ISpeechSynthesizerEngine`, which requires the library's
synthesis-capable model interface. Narrowing a model to that role-specific interface is confined to
the catalog adapter behind `ICliModelCatalog`, so every other CLI component depends only on the
role-agnostic model contract. `SpeakCommand` therefore asks the catalog for the model's preferred
audio format and for a loaded engine, and never narrows a model itself. `SpeakCommand` validates
the model's role before calling either member, so a user mistake is reported by `speak` as a clean
error naming the model and suggesting `list-models --role tts`; the catalog's own refusal to create
an engine for a model of the wrong role is a defensive guard for other callers. The engine the
catalog returns is not yet bound to a device; `SpeakCommand` binds the resolved playback device by
creating a session from it. When a model is not installed or no playback device exists, the library
returns its own honestly-unavailable engine rather than throwing, a graceful-degradation contract
`SpeakCommand` relies on rather than duplicates.

**Cross-unit data flow.** The `--tts-param` tokens that `SpeakCommand` collects are split and
resolved by `ParameterBagParser` against the resolved model's declared parameters, and the typed
result is passed to the catalog when the engine is created. For real playback, `SpeakCommand`
enumerates devices through `ICliPlaybackDeviceSource`, validates any requested device name, and
asks the same source to create the device; `AudioDeviceFactoryPlaybackDeviceSource` forwards both
calls to the library's `AudioDeviceFactory`. Unit tests substitute a fake source that resolves
purely from an in-memory device list, so every device-dispatch scenario is deterministic on every
machine, with or without audio hardware.

**Design constraints.** Every unit in this subsystem reports operator mistakes as exceptions of
the two expected types, never as an unhandled failure. `DemaConsulting.Speech.Cli.Tests` is
scoped to the library's public surface, which is why a CLI-owned seam, rather than the library's
internal test constructor, provides device-resolution determinism.

### Interactions with Other Units

`SpeakCommand` depends on the `ICliModelCatalog` seam (`Enumerate`, `GetPreferredAudioFormat`, and
`CreateSynthesizerEngineAsync`), the library's public `AudioFormat`/`ISpeechSynthesizerEngine`/
`ISynthesisSession`/`AudioTagParser`/`TaggedTextSpan` types, `ParameterBagParser`, its own
`ICliPlaybackDeviceSource` seam (composed over `AudioDeviceFactory` from the library's audio
subsystem in production, via `AudioDeviceFactoryPlaybackDeviceSource`), and
`WavFileAudioPlaybackDevice`. It reuses `DevicesTestCommand.ResolveDeviceSelectionOrThrow` from
`DeviceCommandsSubsystem` rather than duplicating device resolution logic.
`ParameterBagParser` depends only on the library's public
`ISpeechModelParameter`/`NumericParameter`/`ChoiceParameter`/`BooleanParameter` types. The
recognition and conversation command subsystems consume `ParameterBagParser` and
`ICliPlaybackDeviceSource`. Nothing in this subsystem touches `ISynthesisModel` or any other
internal library type directly; that access is confined to the catalog adapter of
`ModelCommandsSubsystem`.
