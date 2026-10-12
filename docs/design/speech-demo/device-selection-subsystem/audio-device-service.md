### AudioDeviceService

**Purpose**: Provide a demo-owned seam over the library's `AudioDeviceFactory` so the device
panel and the session-owning panels can be exercised without audio hardware, while the device
list the demo shows stays exactly what the library reports.

**Why a Demo-Owned Seam**: The library exposes device enumeration through a sealed
`AudioDeviceFactory` and its `CaptureProbe`/`PlaybackProbe` properties, not through an injectable
interface. Rather than add public API to the library purely to make a demo testable - which would
compromise the evidence the demo exists to provide - the demo owns a five-member interface over
that surface. `IAudioDeviceService` and `AudioDeviceService` are documented as one unit because the
interface has no independently observable behavior of its own: every test exercises it through
`AudioDeviceService`, its sole production implementation.

**Data Model**:

| Member | Returns | Behavior |
| --- | --- | --- |
| `EnumerateCaptureDevices()` | `IReadOnlyList<AudioDeviceDescription>` | Never throws |
| `EnumeratePlaybackDevices()` | `IReadOnlyList<AudioDeviceDescription>` | Never throws |
| `CreateCaptureDevice(AudioDeviceSelection?)` | `IAudioCaptureDevice` | Creates the selected capture device |
| `CreatePlaybackDevice(AudioDeviceSelection?)` | `IAudioPlaybackDevice` | Creates the selected playback device |
| `RefreshDevices()` | `void` | Forwards to the factory; throws `AudioDeviceInUseException` if a device is in use |

**Key Methods**: `AudioDeviceService` holds one `AudioDeviceFactory` and delegates each call to
the corresponding probe or factory member. It adds no filtering, sorting, or caching, because any
of those would make the panel show something other than what the library reports; each
enumeration therefore reflects the probe's current answer.

**Error Handling**: The constructor throws `ArgumentNullException` for a null factory. The
library's probes never throw and degrade to empty enumerations when the audio backend is
unavailable, and the factory's creation methods answer with the library's unavailable device when
no matching device is known, so the adapter inherits those contracts with no handling of its own.
`RefreshDevices()` propagates the factory's `AudioDeviceInUseException` and any native fault
unchanged; `DeviceSelectionViewModel` is the unit that handles them.

**Dependencies**: The library's `AudioDeviceFactory`, `AudioDeviceDescription`,
`AudioDeviceSelection`, `IAudioCaptureDevice` and `IAudioPlaybackDevice`.

**Callers**: `DeviceSelectionViewModel` (enumeration and refresh), `SynthesisPanelViewModel` and
`RecognitionPanelViewModel` (device creation), and the `App` composition root (construction).
