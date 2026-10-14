### AudioDeviceFactoryPlaybackDeviceSource

**Purpose**: Provide the production `ICliPlaybackDeviceSource`, resolving playback devices through
the library's real `AudioDeviceFactory`, including that factory's own hardware detection.

**Data Model**: The unit holds one field, the composed `AudioDeviceFactory` supplied at
construction. It does not own the factory and disposes nothing.

**Key Methods**:

- **Constructor(factory)**: Stores the factory.
- **PlaybackProbe**: Returns the factory's own playback probe.
- **CreatePlaybackDevice(selection)**: Returns whatever the factory's `CreatePlaybackDevice` returns
  for the same selection.

Both members forward to the factory without alteration, so `speak` resolves real playback devices
exactly as the library does, including the dependence on real PortAudio hardware being present.
The unit's tests construct the factory with an injected stub playback probe, so the probe
passthrough is asserted deterministically; for device creation, an empty or unknown selection is
asserted to yield the unavailable device, and for known devices the adapter's result is compared
with the factory's own result for the same call, without asserting whether hardware is present on
the machine running them.

**Error Handling**: The constructor throws `ArgumentNullException` for a null factory. The members
add no handling of their own.

**Dependencies**: The library's `AudioDeviceFactory`, `IAudioPlaybackDeviceProbe`,
`IAudioPlaybackDevice` and `AudioDeviceSelection`.

**Callers**: `SpeakCommand` and the conversation command subsystem's `AskCommand` construct it over an
`AudioDeviceFactory` when run as the real command.
