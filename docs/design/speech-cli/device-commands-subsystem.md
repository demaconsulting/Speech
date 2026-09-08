## SpeechCli DeviceCommandsSubsystem Design

### Overview

The DeviceCommandsSubsystem implements the three device-related subcommands dispatched to by
`CommandDispatch`: `list-devices`, `devices test`, and `doctor`. It contains one command handler
per subcommand:

- **`ListDevicesCommand`**: implements `list-devices [--direction input|output]`
- **`DevicesTestCommand`**: implements `devices test [--device <name>] [--direction input|output]`
- **`DoctorCommand`**: implements `doctor`

### Why No CLI-Owned Seam

Unlike `ModelCommandsSubsystem`, which needed a CLI-owned `ICliModelCatalog` seam because the
library's `SpeechModelCatalog` test-friendly constructor is `internal`, this subsystem introduces
no equivalent wrapper. `IAudioCaptureDeviceProbe` and `IAudioPlaybackDeviceProbe` are already
public interfaces, directly implementable by a hand-written fake, and `AudioDeviceFactory` itself
exposes a public constructor
(`AudioDeviceFactory(IAudioCaptureDeviceProbe?, IAudioPlaybackDeviceProbe?, ISpeechDiagnostics?)`)
that lets a test compose a real factory instance over fake probes. Adding a second seam layer on
top of that would duplicate testability the library already provides, for no benefit - so every
command in this subsystem depends directly on the library's own public audio types.

### ListDevicesCommand

Parses `--direction input`/`output` (mapping to `AudioDeviceDirection.Capture`/`Playback`); no
flag means both directions are listed. Enumerates the matching probe(s)
(`IAudioCaptureDeviceProbe.Enumerate()`/`IAudioPlaybackDeviceProbe.Enumerate()`) and prints an
aligned table with columns Name, Direction, Channels, and Sample Rate. An empty result - whether
because no native audio backend is available on this platform (the library's own
`UnavailableAudioCaptureDeviceProbe`/`UnavailableAudioPlaybackDeviceProbe` fallback) or because a
requested direction genuinely has no devices - prints a plain "No devices found." message rather
than an empty table or an error, mirroring this library's established graceful-degradation
convention for optional audio hardware (see the `Speech-AudioSubsystem-UnavailableFallbacks`
design/verification docs).

### DevicesTestCommand

Recognizes exactly one sub-action, `test` (the first positional argument in
`Context.CommandArgs` must be that literal token), leaving room for `devices` to grow further
sub-actions later without changing its recognized top-level dispatch name. Parses
`--device <name>` (optional; omitted means the platform default device) and
`--direction input`/`output`.

**Default direction is `output`.** Playing a short test tone requires no operator consent beyond
running the command; recording real audio input is inherently more privacy-sensitive, so an
operator must explicitly opt into it with `--direction input` rather than have it happen as a
default side effect of a bare `devices test` invocation.

For the output direction, `ResolveDeviceSelectionOrThrow` first validates any requested
`--device` name against the currently enumerated playback devices, throwing a clean
`ArgumentException` naming the unknown device (and suggesting `list-devices`) before any device
is actually created - this keeps the "unknown device name" failure path fully unit-testable
without needing a real device to be created. The resolved device is then checked for
`IsAvailable`; an unavailable device is reported as a clean `InvalidOperationException` rather
than a stack trace, consistent with the library's own honest-failure convention for
`UnavailableAudioPlaybackDevice`/`UnavailableAudioCaptureDevice`. A short (2-second, 440 Hz) sine
wave is generated in-CLI by `GenerateToneSamples` - no TTS model is invoked for this command - and
written to the device, polling `PendingSampleCount` until playback finishes.

For the input direction, the same device-resolution and availability checks apply to the capture
probe/device. A short (2-3 second) clip is recorded via the device's `FrameCaptured` event, and
basic statistics (sample count, peak amplitude) are reported to prove capture worked, without
saving or playing back the recording.

### DoctorCommand

Runs a broader environment/health check than `--validate`'s five CI-safe checks (see
`SelfTest/Validation.cs`), reporting:

1. **Native runtimes**: whether PortAudio is resolvable (inferred from whether the resolved probe
   types are the library's `Unavailable*` fallback singletons - a safe, public proxy signal,
   since the library's own `PortAudioEnvironment` init status is `internal` and not exposed to
   the CLI) and whether the native `sherpa-onnx-c-api` shared library is loadable via
   `NativeLibrary.TryLoad`
2. **Model store**: root path, writability (a temporary marker file is written to and deleted
   from the root), and available disk space at that location
3. **Audio devices**: input/output device counts only (not a full enumeration - that is
   `list-devices`'s job)
4. **Models**: installed-versus-known model counts, via the `ICliModelCatalog` seam from Pass 3

Each check is printed as a `[ OK ]`/`[INFO]`/`[FAIL]` line, followed by an overall
`HEALTHY`/`UNHEALTHY` summary.

#### SherpaOnnx Resolvability Scope

There is no public, model-independent way to probe SherpaOnnx from the library - constructing a
`SherpaOnnxRecognitionEngine`/synthesizer requires an installed model and performs a real native
load. `doctor` instead attempts `NativeLibrary.TryLoad("sherpa-onnx-c-api", out _)`, a lightweight
probe that only proves the native binary for the current platform/architecture is present and
loadable; it does not prove a recognizer or synthesizer can actually be constructed or run
inference. This is a necessary-but-not-sufficient signal, and is documented as such directly in
`DoctorCommand`'s class remarks rather than presented as a full inference-path verification.

#### Hard Failure Versus Informational Note

Only the model store's writability affects `doctor`'s exit code. The native PortAudio/SherpaOnnx
runtime checks and the audio device count are reported as informational (`[INFO]`) notes that
never cause a non-zero exit, for two reasons:

- This mirrors the library's own designed graceful-degradation philosophy for optional hardware
  and native inference runtimes: missing audio hardware or an unresolvable native library does
  not prevent the rest of the tool from working, and every command that genuinely needs one of
  them (`devices test`, and the `speak`/`recognize` commands added in later passes) already
  reports its own clean, specific error at the point of use, so `doctor` reporting the same
  condition as a hard failure would be redundant and would make `doctor` fail on perfectly normal
  headless/CI machines with no audio hardware
- The model store, by contrast, is infrastructure this tool itself owns and depends on for every
  model-management command to function at all; its writability depends only on file system
  permissions, not on any optional external hardware or native library, so a `doctor` run that
  cannot even manage models is genuinely unhealthy and should be reported as such

### Interactions with Other Units

`ListDevicesCommand` and `DevicesTestCommand` depend only on the library's public
`AudioDeviceFactory`/`IAudioCaptureDeviceProbe`/`IAudioPlaybackDeviceProbe`/
`IAudioCaptureDevice`/`IAudioPlaybackDevice`/`AudioDeviceDescription`/`AudioDeviceSelection` types.
`DoctorCommand` additionally depends on the `ICliModelCatalog` seam and `SpeechModelStore` from
`ModelCommandsSubsystem`, reusing rather than duplicating that pass's composition. None of the
three commands touches any internal library type.
