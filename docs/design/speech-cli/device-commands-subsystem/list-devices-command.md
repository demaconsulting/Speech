### ListDevicesCommand

**Purpose**: Implement the `list-devices [--direction input|output]` subcommand: enumerate the audio
capture (input) and playback (output) devices currently available on the host machine, optionally
filtered to one direction, and print them as an aligned table.

**Data Model**: The unit is a static class with no state. Its only data is the parsed direction
filter, a nullable `AudioDeviceDirection`: `Capture` for `--direction input`, `Playback` for
`--direction output`, and `null` (no flag) meaning both directions. The devices it prints are the
library's `AudioDeviceDescription` values (name, direction, channel count, sample rate), reported
exactly as the library's probes return them.

**Key Methods**:

- `Run(Context)`: the dispatch entry point; composes a real `AudioDeviceFactory` and passes its
  capture and playback probes to the overload below.
- `Run(Context, IAudioCaptureDeviceProbe, IAudioPlaybackDeviceProbe)`: the testable overload. It
  parses the arguments, enumerates the capture probe when the filter is `null` or `Capture` and
  the playback probe when the filter is `null` or `Playback`, and writes the combined list. Capture
  devices are listed before playback devices.
- `WriteTable`: prints a header row (Name, Direction, Channels, Sample Rate), a dashed separator
  row and one row per device, each column padded to its widest value. The Direction column prints
  the library enum name (`Capture` or `Playback`), whereas the `--direction` flag takes `input` or
  `output`. Numbers are formatted with the invariant culture.

An empty result prints a plain "No devices found." message instead of an empty table or an error.
The result is empty either because no native audio backend is available on this platform (the
library's `UnavailableAudioCaptureDeviceProbe`/`UnavailableAudioPlaybackDeviceProbe` fallbacks
enumerate nothing) or because the requested direction genuinely has no devices; both are normal
conditions on a headless or CI machine, so the command follows the library's graceful-degradation
convention for optional audio hardware (see the `Speech-AudioSubsystem-UnavailableFallbacks`
design and verification documents). The unit introduces no CLI-owned seam: the probe interfaces
are public and directly implementable by a hand-written fake (see the DeviceCommandsSubsystem
design).

**Error Handling**: A null `Context`, capture probe or playback probe throws
`ArgumentNullException`. An unrecognized `--direction` value, a `--direction` with no value, and
any other argument throw `ArgumentException` naming the offending token; `Program.Main` reports
these as a one-line message with a non-zero exit code. The unit does not guard the probes'
`Enumerate()` calls: an exception thrown by a probe propagates to the caller (unlike `doctor`,
whose device counts are informational and are guarded).

**Dependencies**: The library's `AudioDeviceFactory`, `IAudioCaptureDeviceProbe`,
`IAudioPlaybackDeviceProbe`, `AudioDeviceDescription` and `AudioDeviceDirection` types; the CLI's
`Context` for the command arguments and output.

**Callers**: `CommandDispatch`, through its subcommand table.
