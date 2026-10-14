### IAudioPlaybackDevice

**Purpose**: Define the contract for an audio playback device, so hosts and tests can depend on
playback behavior without depending on native PortAudio types.

**Data Model**: `IsAvailable` indicates whether the device can currently render audio.
`ChannelCount` and `SampleRate`, mirroring `IAudioCaptureDevice`'s identical members, report the
format `Write` actually renders - the resolved device's own values, not requested or probed
ones - and both report `0` when the device is unavailable. Both are readable at any time, before or
after `Start()` and after `Stop()`, and never throw. `PendingSampleCount` is a `long`.

**Key Methods**:

- **Start()** / **Stop()**: Begin/end playback. `Start()` throws
  `AudioDeviceUnavailableException` when the device is unavailable, its native stream fails to
  open, or (for a PortAudio device) its device table was refreshed after it was created.
- **Write(IReadOnlyList&lt;float&gt;)**: Submit normalized interleaved float samples in [-1, 1]
  for playback. The list must not be null: an available device throws `ArgumentNullException`, whereas an
  unavailable device throws `AudioDeviceUnavailableException` without examining its argument. Real
  implementations may buffer the supplied block before the hardware callback requests it; `Write` is
  fire-and-forget and returns immediately
  regardless of how much audio is still pending, so it alone cannot tell a caller when playback
  has genuinely finished.
- **PendingSampleCount**: Reports how many samples handed to `Write` the hardware has not yet
  actually rendered. It reads `0` when `IsAvailable` is `false`, before any write, and after
  `Stop()`. This is the only honest way to observe real drain progress; a caller (notably
  `SynthesisSession`) that must know playback has truly finished polls this until it reaches `0`
  (plus a small safety margin) before stopping the device, rather than treating "every sample
  enqueued" as "finished playing".

**Error Handling**: Real implementations and the unavailable fallback both use
`AudioDeviceUnavailableException` when an operational call is invalid because no usable device is
available or the native stream fails at first use. `Stop()` throws the same exception when the native
stream fails to stop cleanly; the device is then still considered stopped for restart purposes (see
`PortAudioPlaybackDevice`). A `Write` after `Stop()` is not rejected by the PortAudio implementation:
the audio is queued and is rendered after the next `Start()`. Reading `IsAvailable`, `ChannelCount`,
`SampleRate`, or `PendingSampleCount` never throws. Implementations do not implement `IDisposable`;
the caller is responsible for calling `Stop()` once playback is no longer needed rather than
leaving a stream started and abandoned.

**Dependencies**: `AudioDeviceUnavailableException` (named in Error Handling); otherwise only
built-in types. `UnavailableAudioPlaybackDevice` and `PortAudioPlaybackDevice` implement it.

**Callers**: `AudioDeviceFactory.CreatePlaybackDevice()` and hosts that render synthesized audio.
The SynthesisSubsystem reads the reported format to resample and upmix synthesized audio into
what the resolved device requires.
