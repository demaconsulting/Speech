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
the demo exists to provide — the demo owns a three-method interface over that surface:

| Member | Returns | Behavior |
| --- | --- | --- |
| `EnumerateCaptureDevices()` | `IReadOnlyList<AudioDeviceDescription>` | Never throws |
| `EnumeratePlaybackDevices()` | `IReadOnlyList<AudioDeviceDescription>` | Never throws |
| `RefreshDevices()` | `void` | Forwards to the factory; throws `AudioDeviceInUseException` if a device is in use |

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
| `RefreshCommand` | generated async command | Invokes every registered pre-refresh hook, then re-enumerates both |

Both lists are populated during construction, so the window shows real device names the instant
it opens rather than an empty list the user must manually refresh.

**Pre-refresh hooks.** `RegisterPreRefreshHook(Func<Task>)` / `UnregisterPreRefreshHook(Func<Task>)`
(both `internal`) let another panel sharing this device-selection state — currently
`RecognitionPanelViewModel` and `SynthesisPanelViewModel`, both wired onto the same underlying
`AudioDeviceService`/`AudioDeviceFactory` — register an asynchronous callback that `Refresh()`
invokes and awaits, in registration order, before it ever calls
`IAudioDeviceService.RefreshDevices()`. This is what lets a panel with an active
recognition/synthesis session stop it deterministically, and confirm the underlying device is
actually closed, before a shared refresh is attempted, so the `AudioDeviceInUseException` fallback
below becomes a rare defensive path rather than the primary way a refresh succeeds. Hooks are held
in a plain `List<Func<Task>>` rather than a standard C# event, because a multicast event cannot be
meaningfully awaited (invoking it only returns the last subscriber's return value, discarding every
other subscriber's task); each registered hook is expected to be a fast no-op when it has nothing
active.

**Refresh algorithm.** `Refresh()` is the `[RelayCommand]`-generated, `async Task`-returning
method bound to `RefreshCommand`; it awaits every registered pre-refresh hook in turn, then calls
the synchronous `RefreshCore()`, which holds the previously existing refresh logic unchanged.
`RefreshCore()` first calls `IAudioDeviceService.RefreshDevices()` to force the backend to
re-scan its device table. If that call throws `AudioDeviceInUseException` (because a device is
still in use despite every hook having run — the rare case a hook could not prevent, or no hook
was registered for whatever is holding the device), `CaptureStatus` and `PlaybackStatus` are both
set to the exception's message and `RefreshCore()` returns immediately, leaving both lists and
selections untouched - a crash-free, honest degrade rather than a re-enumeration that could not
have found anything new anyway. Any other exception the backend raises (for example, a native
`Terminate()` failure during `PortAudioEnvironment.Refresh()`) is handled identically: both
status properties are set to that exception's message and `RefreshCore()` returns immediately
with both lists and selections untouched, so a general refresh fault degrades the same way as the
in-use case rather than escaping and crashing the UI. Otherwise `RefreshCore()` delegates to
`EnumerateCore()`, which captures the currently chosen device *name* in each direction,
re-enumerates both lists in place, and then restores the selection:

1. If a device with the captured name is still reported, select it
2. Otherwise select the first remaining device
3. Otherwise leave the selection empty

`EnumerateCore()` is also called directly (not through `RefreshCore()`/`Refresh()`) from the
constructor, so both device lists remain populated synchronously at construction time with no
awaited step and without forcing a native PortAudio terminate/reinitialize cycle — a documented,
test-asserted contract. Forcing a refresh at construction would throw `AudioDeviceInUseException`
if any other device created from the shared environment already had an active stream, leaving a
newly constructed view model with empty lists even though the backend is perfectly usable for
enumeration; `EnumerateCore()` instead reads the already-initialized device table directly.

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
