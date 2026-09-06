#### IPortAudioStream

**Purpose**: Define the internal seam for the lifecycle of one opened PortAudio stream.

**Data Model**: N/A - this is a pure behavioral contract.

**Key Methods**:

- **Start()**: Begin PortAudio callback processing.
- **Stop()**: End PortAudio callback processing.
- **Dispose()**: Release the underlying native stream resources.

**Error Handling**: Implementations may throw when the wrapped native stream fails. Higher-level
callers convert those faults into `AudioDeviceUnavailableException` at first use.

**Dependencies**: `IDisposable`.

**Callers**: `PortAudioCaptureDevice` and `PortAudioPlaybackDevice`.
