### AudioFormat

**Purpose**: Represent an audio format as plain library-owned data, so model declarations and
audio-device composition can exchange sample-rate/channel-count intent without leaking any native
backend type into the public API.

**Data Model**: An immutable sealed record carrying `SampleRate` and `ChannelCount`. Both values
are validated at construction and must be greater than zero. Record semantics provide value-based
equality, so two instances with the same rate and channel count compare equal.

**Key Methods**:

- **AudioFormat(sampleRate, channelCount)**: Validates both values are positive, then stores them
  unchanged.
- **SampleRate / ChannelCount**: Expose the validated values.
- **Mono(sampleRate)**: Convenience factory returning `new AudioFormat(sampleRate, 1)`, reusing
  the same validation as the main constructor.

**Error Handling**: Rejects a zero or negative sample rate or channel count with
`ArgumentOutOfRangeException`.

**Dependencies**: None beyond the Base Class Library.

**Callers**: `IRecognitionModel.AudioFormat`, `ISynthesisModel.PreferredAudioFormat`,
`AudioDeviceFactory.CreateCaptureDevice(...)`, and `AudioDeviceFactory.CreatePlaybackDevice(...)`.
