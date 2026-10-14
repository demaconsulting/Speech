## SpeechCli DeviceCommandsSubsystem Design

![DeviceCommandsSubsystem Structure](DeviceCommandsSubsystemView.svg)

### Overview

The DeviceCommandsSubsystem implements the three device-related subcommands dispatched to by
`CommandDispatch`: `list-devices`, `devices test`, and `doctor`. It contains one command handler
per subcommand, each documented in its own file:

- **ListDevicesCommand**: implements `list-devices [--direction input|output]` - see
  _ListDevicesCommand Design_
- **DevicesTestCommand**: implements `devices test [--device <name>] [--direction input|output]` -
  see _DevicesTestCommand Design_
- **DoctorCommand**: implements `doctor` - see _DoctorCommand Design_

### Interfaces

**Exposed**: each unit exposes a static `Run(Context)` entry point (invoked from the
`CommandDispatch` table), which writes to `Context` and sets its exit code. Argument errors are
reported as `ArgumentException`, and unavailable or stalled devices as `InvalidOperationException`;
`Program.Main` turns both into a one-line message and a non-zero exit code.
`DevicesTestCommand.ResolveDeviceSelectionOrThrow` is additionally exposed to `AskCommand` in
`ConversationCommandSubsystem`.

**Consumed**: the library's public `AudioDeviceFactory`, `IAudioCaptureDeviceProbe`,
`IAudioPlaybackDeviceProbe`, `IAudioCaptureDevice`, `IAudioPlaybackDevice`, `AudioDeviceDescription`
and `AudioDeviceSelection` types; and, for `doctor` only, `ICliModelCatalog` and `SpeechModelStore`
from `ModelCommandsSubsystem`.

### Design

The three units are independent of one another and share no state: each parses its own flags from
`Context.CommandArgs`, composes an `AudioDeviceFactory` (or receives an injected fake in tests), and
writes its report through `Context`. See each unit's own design document for its data model, key
methods and error handling.

The subsystem owns no seam of its own. `IAudioCaptureDeviceProbe` and `IAudioPlaybackDeviceProbe`
are public interfaces that a hand-written fake can implement, and `AudioDeviceFactory` has a public
constructor
(`AudioDeviceFactory(IAudioCaptureDeviceProbe?, IAudioPlaybackDeviceProbe?, ISpeechDiagnostics?)`)
that lets a test compose a real factory over fake probes. A second seam layer would duplicate
testability the library already provides, so every unit depends directly on the library's own
public audio types. (`ModelCommandsSubsystem` needs its own `ICliModelCatalog` seam only because
the library's catalog test constructor is internal.)

All three units treat a machine without audio hardware as a normal condition rather than an
error. `list-devices` prints "No devices found."; `doctor` reports the missing PortAudio backend as
an informational note; `devices test` fails with a clean message at the point where a device is
needed. This mirrors the library's graceful-degradation convention for optional audio hardware.

### Interactions with Other Units

`ListDevicesCommand` and `DevicesTestCommand` depend only on the library's public audio types.
`DoctorCommand` additionally depends on the `ICliModelCatalog` seam and `SpeechModelStore` from
`ModelCommandsSubsystem`, reusing rather than duplicating that composition. `AskCommand` calls
`DevicesTestCommand.ResolveDeviceSelectionOrThrow` so that conversation device options are validated
the same way as `devices test`. None of the three units touches any internal library type.
