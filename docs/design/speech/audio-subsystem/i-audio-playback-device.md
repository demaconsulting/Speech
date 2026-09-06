### IAudioPlaybackDevice

**Purpose**: Define the contract for an audio playback device, so hosts and tests can depend on
playback behavior without depending on native PortAudio types.

**Data Model**: `IsAvailable` indicates whether the device can currently render audio.
`ChannelCount` and `SampleRate`, added in Sub-phase 4b mirroring `IAudioCaptureDevice`'s identical
Phase 3 addition, report the format `Write` actually renders - the resolved device's own values,
not requested or probed ones - and both report `0` when the device is unavailable.

**Key Methods**:

- **Start()** / **Stop()**: Begin/end playback.
- **Write(IReadOnlyList&lt;float&gt;)**: Submit normalized interleaved float samples for playback.
  Real implementations may buffer the supplied block before the hardware callback requests it;
  `Write` is fire-and-forget and returns immediately regardless of how much audio is still
  pending, so it alone cannot tell a caller when playback has genuinely finished.
- **PendingSampleCount**: Added to fix a synthesis-panel bug where playback was cut off almost
  instantly. Reports how many samples handed to `Write` the hardware has not yet actually
  rendered, or `0` when `IsAvailable` is `false`. This is the only honest way to observe real
  drain progress; a caller (notably `SherpaOnnxSpeechSynthesizer.PlayStreamAsync`) that must know
  playback has truly finished polls this until it reaches `0` (plus a small safety margin) before
  stopping the device, rather than treating "every sample enqueued" as "finished playing".

**Error Handling**: Real implementations and the unavailable fallback both use
`AudioDeviceUnavailableException` when an operational call is invalid because no usable device is
available or the native stream fails at first use. Reading `IsAvailable`, `ChannelCount`,
`SampleRate`, or `PendingSampleCount` never throws.

**Dependencies**: None beyond built-in types.

**Callers**: `AudioDeviceFactory.CreatePlaybackDevice()` and hosts that render synthesized audio.
The SynthesisSubsystem reads the reported format to resample and upmix synthesized audio into
what the resolved device requires.
