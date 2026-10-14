### AudioDeviceInUseException

**Purpose**: Refuse a PortAudio device-table refresh while any capture/playback device created
from the environment currently has an active (open/started) stream.

**Data Model**: No additional fields beyond the standard `Exception` base members.

**Key Methods**: Standard three-constructor exception pattern (default, message, message with
inner exception), mirroring `AudioDeviceUnavailableException`.

**Error Handling**: This type is itself the error-handling mechanism, and is the one deliberate
exception to this library's "never throws at composition" policy. Refreshing PortAudio's device
table requires tearing down and reinitializing the native runtime, which would invalidate any
stream currently open against it; rather than silently tearing down a live stream, the refresh is
refused outright. Unlike `AudioDeviceUnavailableException`, this is caller-driven, recoverable
misuse - the caller can stop the active capture/playback session(s) via
`IAudioCaptureDevice.Stop()`/`IAudioPlaybackDevice.Stop()` and retry `RefreshDevices()` - not an
honest composition-time unavailability. It exposes no structured device list; any identifying
detail is only ever present as free-form text in the exception's message.

**Dependencies**: `Exception`.

**Callers**: `PortAudioEnvironment.Refresh()`, `AudioDeviceFactory.RefreshDevices()`.
