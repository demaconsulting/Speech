## SpeechDemo DeviceSelectionSubsystem Design

![DeviceSelectionSubsystem Structure](DeviceSelectionSubsystemView.svg)

### Overview

The DeviceSelectionSubsystem provides the demo's audio capture and playback device pickers. It
contains the following units, each documented in its own file:

- **AudioDeviceService** (`IAudioDeviceService` / `AudioDeviceService`): the demo-owned
  enumeration seam and its real implementation over the library's `AudioDeviceFactory` - see
  _AudioDeviceService Design_
- **DeviceSelectionViewModel**: the presentation state for both pickers - see
  _DeviceSelectionViewModel Design_

The subsystem also contains **DeviceSelectionView**, the Avalonia view bound to
`DeviceSelectionViewModel`.

### Interfaces

The subsystem exposes `DeviceSelectionViewModel` (bound by the device panel view and shared by
the synthesis and recognition panels, which read its `CaptureSelection`/`PlaybackSelection` and
register pre-refresh hooks) and `IAudioDeviceService` (consumed by the panels to create
capture and playback devices). It consumes the library's `AudioDeviceFactory`,
`AudioDeviceDescription` and `AudioDeviceSelection`. `DeviceSelectionViewModel` also exposes the
`RefreshAsync` command and `RegisterPreRefreshHook`/`UnregisterPreRefreshHook`, through which a panel
that holds a device open stops its session before a refresh; their contracts are in the
_DeviceSelectionViewModel Design_.

### Design

The subsystem is a seam plus a view model. The demo-owned `IAudioDeviceService` wraps the
library's sealed factory so the view model can be verified without audio hardware, and
`DeviceSelectionViewModel` depends only on that seam interface and on the library's value types.
It never touches `AudioDeviceFactory`, PortAudio, or Avalonia, which is what allows every
presentation behavior to be verified with no audio hardware present.

A refresh flows through both units: the view model first awaits every pre-refresh hook registered
by the panels that share it, then asks `AudioDeviceService` to refresh, which forwards to the
library factory; only if that succeeds does the view model re-enumerate through the same seam.
Construction instead reads the already-initialized device table without a refresh. If the refresh is
refused (a device is in use, `AudioDeviceInUseException`) or fails in the native layer, the view
model leaves both lists and the selections untouched and shows the exception's message in the status
text of both pickers instead of re-enumerating. See each
unit's own design document for its data model, algorithms, and error handling.
