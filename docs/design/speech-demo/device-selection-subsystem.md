## SpeechDemo DeviceSelectionSubsystem Design

![DeviceSelectionSubsystem Structure](DeviceSelectionSubsystemView.svg)

### Overview

The DeviceSelectionSubsystem provides the demo's audio capture and playback device pickers. It
contains the following units:

- **IAudioDeviceService** / **AudioDeviceService**: the demo-owned enumeration seam and its real
  implementation over the library's `AudioDeviceFactory` probes
- **DeviceSelectionViewModel**: the presentation state for both pickers

### Why a Demo-Owned Seam

The library exposes device enumeration through a sealed `AudioDeviceFactory` and its
`CaptureProbe`/`PlaybackProbe` properties, not through an injectable interface. Rather than add
public API to the library purely to make a demo testable — which would compromise the evidence
the demo exists to provide — the demo owns a two-method interface over that surface:

| Member | Returns | Behavior |
| --- | --- | --- |
| `EnumerateCaptureDevices()` | `IReadOnlyList<AudioDeviceDescription>` | Never throws |
| `EnumeratePlaybackDevices()` | `IReadOnlyList<AudioDeviceDescription>` | Never throws |

`AudioDeviceService` is the only production implementation. It holds an `AudioDeviceFactory`,
rejects a null factory at construction, and delegates each call to the corresponding probe. It
adds no filtering, sorting, or caching, because any of those would make the panel show something
other than what the library reports.

### DeviceSelectionViewModel

| Member | Type | Purpose |
| --- | --- | --- |
| `CaptureDevices` / `PlaybackDevices` | `ObservableCollection<AudioDeviceDescription>` | The listed devices |
| `SelectedCaptureDevice` / `SelectedPlaybackDevice` | `AudioDeviceDescription?` | The chosen device |
| `CaptureStatus` / `PlaybackStatus` | `string` | The list's current state, in words |
| `HasCaptureDevices` / `HasPlaybackDevices` | `bool` | Whether the picker has anything to offer |
| `CaptureSelection` / `PlaybackSelection` | `AudioDeviceSelection` | The library selection value |
| `RefreshCommand` | generated command | Re-enumerates both directions |

Both lists are populated during construction, so the window shows real device names the instant
it opens rather than an empty list the user must manually refresh.

**Refresh algorithm.** `Refresh()` captures the currently chosen device *name* in each
direction, re-enumerates both lists in place, and then restores the selection:

1. If a device with the captured name is still reported, select it
2. Otherwise select the first remaining device
3. Otherwise leave the selection empty

Name is used as the identity key because name is the library's only stable device identity;
comparing object references would drop a still-present device whose reported format changed, and
comparing whole descriptions would do the same. Collections are updated in place rather than
reassigned so existing bindings never have to re-subscribe.

**Status text.** A populated list reports its count. An empty list reports an explanatory message
naming the two plausible causes — an unavailable audio backend, or no device of that kind
connected — because "no devices" and "the backend failed" look identical to a user, and a blank
picker with no explanation reads as a broken application.

**Selection values.** `CaptureSelection` and `PlaybackSelection` project the chosen device into
the library's `AudioDeviceSelection`, falling back to `AudioDeviceSelection.SystemDefault` when
nothing is chosen. This means a caller always has a usable selection to hand to the library and
never has to special-case the no-device machine. Both projections are re-announced whenever the
corresponding chosen device changes.

### Interactions with Other Units

The view model depends only on the seam interface and on the library's `AudioDeviceDescription`
and `AudioDeviceSelection` value types. It never touches `AudioDeviceFactory`, PortAudio, or
Avalonia, which is what allows every behavior above to be verified with no audio hardware
present.
