### IAudioCaptureDevice

**Purpose**: Define the contract for an audio capture device, so hosts and tests can depend on
capture behavior without depending on native PortAudio types.

**Data Model**: `IsAvailable` indicates whether the device can currently capture audio.
`ChannelCount` and `SampleRate` report the format of the audio the device actually delivers -
the resolved device's own values, not requested or probed ones - and both report `0` when the
device is unavailable. The adjacent `AudioCaptureFrameEventArgs` record carries one `Samples`
payload containing normalized, interleaved float samples; because that payload carries no format
metadata of its own, the two format properties are the only way a consumer can learn how to
interpret it.

**Key Methods**:

- **Start()** / **Stop()**: Begin/end capture.
- **FrameCaptured**: Raised when a capture frame is available. Real implementations may raise it
  from a high-priority audio callback thread, so handlers must remain lightweight.

**Error Handling**: Real implementations and the unavailable fallback both use
`AudioDeviceUnavailableException` when an operational call is invalid because no usable device is
available or the native stream fails at first use. Reading `IsAvailable`, `ChannelCount`, or
`SampleRate` never throws.

**Dependencies**: `AudioCaptureFrameEventArgs`.

**Callers**: `AudioDeviceFactory.CreateCaptureDevice()`, and the RecognitionSubsystem, which
reads the reported format to downmix and resample captured audio into what a model requires.
