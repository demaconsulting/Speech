### UnavailableAudioPlaybackDeviceProbe

**Purpose**: Provide a safe `IAudioPlaybackDeviceProbe` fallback that reports no devices rather
than throwing.

**Data Model**: No instance fields. Exposes a single static `Instance` singleton.

**Key Methods**:

- **Enumerate()**: Always returns an empty list.

**Error Handling**: N/A - absence is represented as an empty list.

**Dependencies**: `AudioDeviceDescription`; implements `IAudioPlaybackDeviceProbe`.

**Callers**: `AudioDeviceFactory` when PortAudio cannot initialize.
