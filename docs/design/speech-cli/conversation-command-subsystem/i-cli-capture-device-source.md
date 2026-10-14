### ICliCaptureDeviceSource

**Purpose**: Give the CLI commands a small seam over capture-device resolution, mirroring the playback-side
`ICliPlaybackDeviceSource`, so a unit test can substitute an in-memory capture device for the sealed library
`AudioDeviceFactory` and its real PortAudio hardware.

**Data Model**: The interface has no stored state. `CaptureProbe` exposes the probe used to enumerate the available
capture devices. `CreateCaptureDevice(selection)` takes an optional `AudioDeviceSelection` (null requests the
system default input device) and returns a capture device.

**Key Methods**: `CreateCaptureDevice` returns a real, available device when the selection is known to
`CaptureProbe`; otherwise it returns an honestly unavailable device whose `IsAvailable` is false. It does not throw
for an unknown device or for the default when no capture devices exist, because callers decide how to
report an unavailable device.

The seam exists because `AudioDeviceFactory.CreateCaptureDevice` checks real PortAudio initialization before it
consults any injected probe, so on a machine with no audio hardware, including a headless build agent, it resolves
to the unavailable fallback whatever probe a test injects. A test double implementing this interface never touches
PortAudio state, so the happy-path listening tests are deterministic everywhere. The seam adds no public API to
the library and leaves the library's own hardware detection as it is.

**Error Handling**: The interface raises nothing of its own; an unavailable device is a normal return value.

**Dependencies**: The library's `IAudioCaptureDeviceProbe`, `IAudioCaptureDevice`, and `AudioDeviceSelection`
types.

**Callers**: `AskCommand` and `RecognizeCommand`, which resolve their capture devices through it.
