### PortAudioCaptureDeviceProbe

**Purpose**: Enumerate capture-capable devices from the current platform's preferred PortAudio
host API.

**Data Model**: Holds one `PortAudioEnvironment` reference used to resolve the preferred host
API and access device metadata through the `IPortAudioApi` seam.

**Key Methods**:

- **Enumerate()**: Resolves the preferred host API, filters the PortAudio device catalog to
  devices on that host API with `MaxInputChannels > 0`, and maps each result to
  `AudioDeviceDescription`.

**Error Handling**: Returns an empty list when the PortAudio runtime is unavailable, the
preferred host API is missing, or no capture-capable devices exist.

**Dependencies**: `PortAudioEnvironment`, `IPortAudioApi`, and `AudioDeviceDescription`.

**Callers**: `AudioDeviceFactory` and hosts reading `AudioDeviceFactory.CaptureProbe`.
