## SpeechCli DeviceCommandsSubsystem Design

![DeviceCommandsSubsystem Structure](DeviceCommandsSubsystemView.svg)

### Overview

The DeviceCommandsSubsystem implements the three device-related subcommands dispatched to by
`CommandDispatch`: `list-devices`, `devices test`, and `doctor`. It contains one command handler
per subcommand:

- **`ListDevicesCommand`**: implements `list-devices [--direction input|output]`
- **`DevicesTestCommand`**: implements `devices test [--device <name>] [--direction input|output]`
- **`DoctorCommand`**: implements `doctor`

### Interfaces

**Exposed**: each command handler exposes a static `Run(Context)` entry point (invoked from the
`CommandDispatch` table), which writes to `Context` and sets its exit code; argument errors are
reported as `ArgumentException`, and unavailable devices as `InvalidOperationException`.

**Consumed**: the library's public `AudioDeviceFactory`, `IAudioCaptureDeviceProbe`,
`IAudioPlaybackDeviceProbe`, `IAudioCaptureDevice`, `IAudioPlaybackDevice`, `AudioDeviceDescription`
and `AudioDeviceSelection` types; and, for `doctor` only, `ICliModelCatalog` and `SpeechModelStore`
from `ModelCommandsSubsystem`. `DevicesTestCommand.ResolveDeviceSelectionOrThrow` is also called by
`AskCommand` in `ConversationCommandSubsystem`.

### Design

The three commands are independent of one another and share no state: each parses its own flags
from `Context.CommandArgs`, composes an `AudioDeviceFactory` (or an injected fake in tests), and
writes its report through `Context`. The sections below describe each unit.

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
wave is generated in-CLI by `GenerateToneSamples` - no TTS model is invoked for this command. The tone is a
sine at an amplitude of 0.2 (well below full scale, so it is audible but not painful), and on a
multi-channel device every channel carries the same tone (a mono or stereo device plays an identical signal on each
channel). The tone is written to the device, polling `PendingSampleCount` until playback finishes. The poll is bounded: if the device
still has queued samples after the tone duration plus a fixed margin (5 seconds), `PlayTestTone` stops the device and
throws an `InvalidOperationException` (clean message, non-zero exit code) instead of hanging on a stalled device.
Durations in the report are formatted with the invariant culture so the text is identical on every machine.
`RequireAvailable` (an internal overload per device type) performs the availability check so tests can exercise the
unavailable-device error without a real device. Every command rejects a null `Context` with
`ArgumentNullException`.
Once a device is resolved and
available, `PlayTestTone` (which takes the device directly) does the playing and reporting, so tests can drive it
with a fake `IAudioPlaybackDevice`.

For the input direction, the same device-resolution and availability checks apply to the capture
probe/device. `RecordTestClip` (which takes the resolved device and a duration, so tests can drive it with a fake
`IAudioCaptureDevice`) records a 2-second clip via the device's `FrameCaptured` event and reports basic
statistics (sample count, peak amplitude) to prove capture worked, without saving or playing back the recording.

**An empty capture is a failure.** If the device delivers no samples during the recording window, capture has not
been proven, so the command throws an `InvalidOperationException` (a clean error message and a non-zero exit code)
instead of reporting `Samples captured: 0` with a zero peak amplitude as if the test had passed.

### DoctorCommand

Runs a broader environment/health check than `--validate`'s CI-safe self-tests, reporting:

1. **Native runtimes**: whether PortAudio is resolvable (inferred from whether the resolved probe
   types are the library's `Unavailable*` fallback singletons - a safe, public proxy signal,
   since the library's own `PortAudioEnvironment` init status is `internal` and not exposed to
   the CLI) and whether the native `sherpa-onnx-c-api` shared library is loadable, preferring its
   concrete `runtimes/<rid>/native/` path (see the SherpaOnnx Resolvability Scope section below)
2. **Model store**: root path, writability (a temporary marker file is written to and deleted
   from the root), and available disk space at that location
3. **Audio devices**: input/output device counts only (not a full enumeration - that is
   `list-devices`'s job)
4. **Models**: installed-versus-known model counts, via the `ICliModelCatalog` seam

Each check is printed as a `[ OK ]`/`[INFO]`/`[FAIL]` line, followed by an overall
`HEALTHY`/`UNHEALTHY` summary. `[FAIL]` lines and the `UNHEALTHY` summary are written to stderr (via
`WriteError`); every other line goes to stdout. `doctor` accepts no arguments (an extra argument is an
`ArgumentException`). It creates the model-store root directory if it does not exist, so that writability can be
probed. A device or model enumeration that throws is reported as "(could not be determined)" for that count rather
than aborting the report. The `list-devices` Direction column prints the library enum names (`Capture`/`Playback`),
whereas the `--direction` flag takes `input`/`output`.

#### SherpaOnnx Resolvability Scope

There is no public, model-independent way to probe SherpaOnnx from the library - constructing a
`SherpaOnnxRecognitionEngine`/synthesizer requires an installed model and performs a real native
load. `doctor` instead probes the native `sherpa-onnx-c-api` shared library directly. A bare
`NativeLibrary.TryLoad("sherpa-onnx-c-api", out _)` call alone under-reports availability for a
framework-dependent build: NuGet copies the platform-specific native asset to
`runtimes/<rid>/native/` under the application's base directory, not to the base directory
itself, and a bare-name probe never searches that nested folder (unlike ordinary P/Invoke
resolution, which consults it via the app's `deps.json`). `doctor` therefore first resolves the
current platform/architecture's runtime identifier (for example `win-x64`) and loads the
concrete `runtimes/<rid>/native/<file>` path directly when present, falling back to the bare-name
probe for other layouts (for example self-contained or single-file publishes, where native assets
are flattened into the base directory). Either way, this only proves the native binary for the
current platform/architecture is present and loadable; it does not prove a recognizer or
synthesizer can actually be constructed or run inference. This is a necessary-but-not-sufficient
signal, and is documented as such directly in `DoctorCommand`'s class remarks rather than
presented as a full inference-path verification.

#### Hard Failure Versus Informational Note

Only the model store's writability affects `doctor`'s exit code. The native PortAudio/SherpaOnnx
runtime checks and the audio device count are reported as informational (`[INFO]`) notes that
never cause a non-zero exit, for two reasons:

- This mirrors the library's own designed graceful-degradation philosophy for optional hardware
  and native inference runtimes: missing audio hardware or an unresolvable native library does
  not prevent the rest of the tool from working, and every command that genuinely needs one of
  them (`devices test`, `speak`, `recognize`, and `ask`) already
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
`ModelCommandsSubsystem`, reusing rather than duplicating that composition. None of the
three commands touches any internal library type.
