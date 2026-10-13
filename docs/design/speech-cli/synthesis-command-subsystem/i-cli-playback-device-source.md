### ICliPlaybackDeviceSource

**Purpose**: Give `SpeakCommand` a CLI-owned seam over real-playback-device resolution, so its
device-dispatch logic is unit tested deterministically on every machine, including headless ones.

The seam exists because the library's `AudioDeviceFactory` consults the shared PortAudio
initialization flag before it consults any injected playback probe, and returns the honestly
unavailable device whenever PortAudio is not initialized. A test that builds a factory with a fake
probe and expects a real device therefore passes only on a machine with audio hardware. The factory
has an internal constructor that accepts an explicit PortAudio environment for exactly this
purpose, but `DemaConsulting.Speech.Cli.Tests` is scoped to the library's public surface and has no
access to it; widening that boundary for one subsystem would weaken the isolation the CLI test
project provides everywhere else. A narrow seam is the smaller change, and mirrors the
`ICliModelCatalog` pattern of a CLI-owned seam over a library type that cannot be faked directly.

**Data Model**: The interface has no state. It exposes two members:

| Member | Returns | Behavior |
| --- | --- | --- |
| `PlaybackProbe` | `IAudioPlaybackDeviceProbe` | Enumerates the playback devices |
| `CreatePlaybackDevice(selection)` | `IAudioPlaybackDevice` | Resolves a device |

**Key Methods**:

- **PlaybackProbe**: The probe `SpeakCommand` enumerates to validate a requested device name.
- **CreatePlaybackDevice(selection)**: Creates the device for a selection, where a null selection
  requests the system default output. It returns an available device when one is known to the
  probe, otherwise an honestly unavailable device whose `IsAvailable` is false.

**Error Handling**: The contract reports a missing device through the unavailable device rather
than an exception; the caller (`SpeakCommand`) turns that into a clean error suggesting
`--output-audio`.

**Dependencies**: The library's `IAudioPlaybackDeviceProbe`, `IAudioPlaybackDevice` and
`AudioDeviceSelection`.

**Callers**: `SpeakCommand` and the conversation command subsystem's `AskCommand`. Implemented by
`AudioDeviceFactoryPlaybackDeviceSource` in production and by an in-memory fake in tests.
