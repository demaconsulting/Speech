### UnavailableAudioCaptureDeviceProbe

**Purpose**: Provide a safe `IAudioCaptureDeviceProbe` fallback that reports no devices rather
than throwing.

**Data Model**: No instance fields. Exposes a single static `Instance` singleton.

**Key Methods**:

- **Enumerate()**: Always returns an empty list.

**Error Handling**: N/A - absence is represented as an empty list.

**Dependencies**: `AudioDeviceDescription`; implements `IAudioCaptureDeviceProbe`.

**Callers**: `AudioDeviceFactory` when PortAudio cannot initialize.
