## SpeechDemo DeviceSelectionSubsystem Verification

### Verification Approach

The DeviceSelectionSubsystem is verified through deterministic unit tests in two layers. The
`DeviceSelectionViewModel` is tested against an NSubstitute fake of `IAudioDeviceService`, which
lets every enumeration outcome — populated, empty, device added, device removed, device
re-reported in a different format — be produced on demand with no audio hardware present. The
`AudioDeviceService` adapter is tested against an `AudioDeviceFactory` composed with substitute
probes, proving it really does return what the library's probes report and nothing else.

Automated tests do **not** claim proof that real microphones and speakers are enumerated
correctly; that depends on the library's PortAudio integration and remains a manual/local
verification activity on hardware-equipped machines.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own view model and fakes
- **Test doubles**: NSubstitute fakes of `IAudioDeviceService`, `IAudioCaptureDeviceProbe`, and
  `IAudioPlaybackDeviceProbe`

### Test Scenarios

#### AudioDeviceService_Constructor_NullFactory_ThrowsArgumentNullException

**Scenario**: The adapter is constructed with no device factory.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Devices-LibraryProbeDelegation`.

#### AudioDeviceService_EnumerateCaptureDevices_ProbeReportsDevices_ReturnsProbeDevices

**Scenario**: The library's capture probe reports devices.

**Expected**: The adapter returns exactly those devices, unfiltered and unsorted.

**Requirement coverage**: `SpeechDemo-Devices-LibraryProbeDelegation`.

#### AudioDeviceService_EnumeratePlaybackDevices_ProbeReportsDevices_ReturnsProbeDevices

**Scenario**: The library's playback probe reports devices.

**Expected**: The adapter returns exactly those devices.

**Requirement coverage**: `SpeechDemo-Devices-LibraryProbeDelegation`.

#### AudioDeviceService_Enumerate_ProbesReportNothing_ReturnsEmptyLists

**Scenario**: Both probes report nothing.

**Expected**: Empty lists rather than null, so the panel never has to null-check the library.

**Requirement coverage**: `SpeechDemo-Devices-EmptyStateExplanation`.

#### AudioDeviceService_RefreshDevices_Always_DelegatesToFactoryRefreshDevices

**Scenario**: `RefreshDevices()` is called on the adapter.

**Expected**: The call is forwarded to `AudioDeviceFactory.RefreshDevices()` exactly once.

**Requirement coverage**: `SpeechDemo-Devices-RefreshDelegation`.

#### DeviceSelectionViewModel_Constructor_NullService_ThrowsArgumentNullException

**Scenario**: The panel is constructed with no enumeration seam.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Devices-LibraryProbeDelegation`.

#### DeviceSelectionViewModel_Constructor_DevicesAvailable_PopulatesBothLists

**Scenario**: The seam reports capture and playback devices.

**Expected**: Both lists are populated during construction, so the window shows real device names
the instant it opens.

**Requirement coverage**: `SpeechDemo-Devices-Enumeration`.

#### DeviceSelectionViewModel_Constructor_DevicesAvailable_ReportsCountStatus

**Scenario**: The seam reports devices.

**Expected**: Each status line reports how many devices of that direction were found.

**Requirement coverage**: `SpeechDemo-Devices-Enumeration`.

#### DeviceSelectionViewModel_Constructor_DevicesAvailable_SelectsFirstOfEachDirection

**Scenario**: The seam reports devices.

**Expected**: The first device in each direction is preselected, so the demo is usable without
configuration.

**Requirement coverage**: `SpeechDemo-Devices-DefaultSelection`.

#### DeviceSelectionViewModel_Constructor_NoDevices_ReportsHonestEmptyState

**Scenario**: The seam reports no devices at all.

**Expected**: Both directions report no devices available and carry an explanatory message naming
the possible cause.

**Requirement coverage**: `SpeechDemo-Devices-EmptyStateExplanation`.

#### DeviceSelectionViewModel_Refresh_DeviceAdded_KeepsExistingSelection

**Scenario**: A device is hot-plugged and the panel is refreshed.

**Expected**: The previously chosen device stays selected.

**Requirement coverage**: `SpeechDemo-Devices-Refresh`.

#### DeviceSelectionViewModel_Refresh_SameDeviceReportedWithNewFormat_SelectsMatchingNameAgain

**Scenario**: The selected device is re-reported as a new description carrying a different
sample rate.

**Expected**: It is reselected by name, proving identity is name-based rather than
instance-based.

**Requirement coverage**: `SpeechDemo-Devices-Refresh`.

#### DeviceSelectionViewModel_Refresh_SelectedDeviceRemoved_FallsBackToFirstAvailable

**Scenario**: The selected device is unplugged and the panel is refreshed.

**Expected**: The first remaining device is selected, so the picker is never left blank while
devices exist.

**Requirement coverage**: `SpeechDemo-Devices-Refresh`.

#### DeviceSelectionViewModel_Refresh_AllDevicesRemoved_ClearsSelectionAndExplains

**Scenario**: The last device is removed and the panel is refreshed.

**Expected**: The selection is cleared and the explanatory empty-state message is shown.

**Requirement coverage**: `SpeechDemo-Devices-EmptyStateExplanation`.

#### DeviceSelectionViewModel_RefreshCommand_Executed_ReEnumeratesDevices

**Scenario**: The refresh command is executed the way a bound button does.

**Expected**: Both directions are re-enumerated through the seam.

**Requirement coverage**: `SpeechDemo-Devices-Refresh`.

#### DeviceSelectionViewModel_CaptureSelection_DeviceSelected_ReturnsNameBasedSelection

**Scenario**: A capture device is selected.

**Expected**: The exposed selection is the library's name-based `AudioDeviceSelection` for that
device.

**Requirement coverage**: `SpeechDemo-Devices-NameBasedSelection`.

#### DeviceSelectionViewModel_SelectedPlaybackDevice_Changed_NotifiesPlaybackSelection

**Scenario**: The chosen playback device changes.

**Expected**: The derived playback selection is re-announced, so a bound consumer never reads a
stale selection.

**Requirement coverage**: `SpeechDemo-Devices-NameBasedSelection`.

#### DeviceSelectionViewModel_Selections_NoDevices_ReturnSystemDefault

**Scenario**: No devices exist in either direction.

**Expected**: Both selections report the library's system-default selection, so a caller never
has to special-case the no-device machine.

**Requirement coverage**: `SpeechDemo-Devices-EmptySelectionFallback`.

#### DeviceSelectionViewModel_Refresh_CallsRefreshDevicesBeforeEnumerating

**Scenario**: `Refresh()` is invoked (including via the bound `RefreshCommand`).

**Expected**: `IAudioDeviceService.RefreshDevices()` is called before either direction is
re-enumerated.

**Requirement coverage**: `SpeechDemo-Devices-RefreshDelegation`.

#### DeviceSelectionViewModel_Refresh_ServiceThrowsAudioDeviceInUseException_SurfacesMessageViaStatusAndDoesNotClearLists

**Scenario**: `IAudioDeviceService.RefreshDevices()` throws `AudioDeviceInUseException` because a
device is currently streaming.

**Expected**: Both `CaptureStatus` and `PlaybackStatus` are set to the exception's message, and
neither list nor either selection is touched - a crash-free, honest degrade rather than a
re-enumeration that could not have found anything new anyway.

**Requirement coverage**: `SpeechDemo-Devices-RefreshInUseHandling`.

#### DeviceSelectionViewModel_Refresh_ServiceThrowsGeneralException_SurfacesMessageViaStatusAndDoesNotClearLists

**Scenario**: `IAudioDeviceService.RefreshDevices()` throws a general exception (for example,
`InvalidOperationException` simulating a native `Terminate()` failure) rather than
`AudioDeviceInUseException`.

**Expected**: Both `CaptureStatus` and `PlaybackStatus` are set to the exception's message, and
neither list nor either selection is touched - the same crash-free, honest degrade as the
`AudioDeviceInUseException` case, applied to any other refresh fault.

**Requirement coverage**: `SpeechDemo-Devices-RefreshGeneralFaultHandling`.

#### DeviceSelectionViewModel_Refresh_RegisteredHook_InvokedAndAwaitedBeforeRefreshDevices

**Scenario**: A pre-refresh hook backed by a `TaskCompletionSource` is registered, then `Refresh()`
is called.

**Expected**: `IAudioDeviceService.RefreshDevices()` is not called while the hook's task is
unresolved; once the hook completes, `Refresh()` proceeds and calls `RefreshDevices()` exactly
once - proving the hook is genuinely awaited to completion before the backend call, not just
fired without waiting.

**Requirement coverage**: `SpeechDemo-Devices-RefreshPreHooks`.

#### DeviceSelectionViewModel_Refresh_UnregisteredHook_NotInvoked

**Scenario**: A hook is registered, then unregistered via `UnregisterPreRefreshHook` before
`Refresh()` is called.

**Expected**: The unregistered hook is never invoked.

**Requirement coverage**: `SpeechDemo-Devices-RefreshPreHooks`.

#### DeviceSelectionViewModel_Refresh_HookCannotStopInUseDevice_FallsBackToStatusMessage

**Scenario**: A registered no-op hook cannot help, and `IAudioDeviceService.RefreshDevices()`
unconditionally refuses with `AudioDeviceInUseException`.

**Expected**: The existing fallback status-message behavior still fires - proving the
hook mechanism does not weaken the existing safety net.

**Requirement coverage**: `SpeechDemo-Devices-RefreshInUseHandling`, `SpeechDemo-Devices-RefreshPreHooks`.

### Requirements Coverage

- **`SpeechDemo-Devices-Enumeration`**:
  `DeviceSelectionViewModel_Constructor_DevicesAvailable_PopulatesBothLists`,
  `DeviceSelectionViewModel_Constructor_DevicesAvailable_ReportsCountStatus`
- **`SpeechDemo-Devices-DefaultSelection`**:
  `DeviceSelectionViewModel_Constructor_DevicesAvailable_SelectsFirstOfEachDirection`
- **`SpeechDemo-Devices-NameBasedSelection`**:
  `DeviceSelectionViewModel_CaptureSelection_DeviceSelected_ReturnsNameBasedSelection`,
  `DeviceSelectionViewModel_SelectedPlaybackDevice_Changed_NotifiesPlaybackSelection`
- **`SpeechDemo-Devices-Refresh`**:
  `DeviceSelectionViewModel_Refresh_DeviceAdded_KeepsExistingSelection`,
  `DeviceSelectionViewModel_Refresh_SameDeviceReportedWithNewFormat_SelectsMatchingNameAgain`,
  `DeviceSelectionViewModel_Refresh_SelectedDeviceRemoved_FallsBackToFirstAvailable`,
  `DeviceSelectionViewModel_RefreshCommand_Executed_ReEnumeratesDevices`
- **`SpeechDemo-Devices-LibraryProbeDelegation`**:
  `AudioDeviceService_Constructor_NullFactory_ThrowsArgumentNullException`,
  `AudioDeviceService_EnumerateCaptureDevices_ProbeReportsDevices_ReturnsProbeDevices`,
  `AudioDeviceService_EnumeratePlaybackDevices_ProbeReportsDevices_ReturnsProbeDevices`,
  `DeviceSelectionViewModel_Constructor_NullService_ThrowsArgumentNullException`
- **`SpeechDemo-Devices-EmptyStateExplanation`**:
  `DeviceSelectionViewModel_Constructor_NoDevices_ReportsHonestEmptyState`,
  `DeviceSelectionViewModel_Refresh_AllDevicesRemoved_ClearsSelectionAndExplains`,
  `AudioDeviceService_Enumerate_ProbesReportNothing_ReturnsEmptyLists`
- **`SpeechDemo-Devices-EmptySelectionFallback`**:
  `DeviceSelectionViewModel_Selections_NoDevices_ReturnSystemDefault`
- **`SpeechDemo-Devices-RefreshDelegation`**:
  `AudioDeviceService_RefreshDevices_Always_DelegatesToFactoryRefreshDevices`,
  `DeviceSelectionViewModel_Refresh_CallsRefreshDevicesBeforeEnumerating`
- **`SpeechDemo-Devices-RefreshInUseHandling`**:
  `DeviceSelectionViewModel_Refresh_ServiceThrowsAudioDeviceInUseException_SurfacesMessageViaStatusAndDoesNotClearLists`,
  `DeviceSelectionViewModel_Refresh_HookCannotStopInUseDevice_FallsBackToStatusMessage`
- **`SpeechDemo-Devices-RefreshGeneralFaultHandling`**:
  `DeviceSelectionViewModel_Refresh_ServiceThrowsGeneralException_SurfacesMessageViaStatusAndDoesNotClearLists`
- **`SpeechDemo-Devices-RefreshPreHooks`**:
  `DeviceSelectionViewModel_Refresh_RegisteredHook_InvokedAndAwaitedBeforeRefreshDevices`,
  `DeviceSelectionViewModel_Refresh_UnregisteredHook_NotInvoked`,
  `DeviceSelectionViewModel_Refresh_HookCannotStopInUseDevice_FallsBackToStatusMessage`

### Acceptance Criteria

A DeviceSelectionSubsystem test run passes when: the adapter returns exactly what the library's
probes report; the panel lists and preselects devices on construction; a refresh preserves a
still-present selection by name and falls back correctly when it is gone; an empty direction
carries an explanatory message and reports the library's system-default selection; every derived
selection value is re-announced when the chosen device changes; a refresh first invokes and
awaits every registered pre-refresh hook, in order, before forcing the backend to re-scan its
device table; an unregistered hook is never invoked; a refusal reported as
`AudioDeviceInUseException` - whether or not a hook was registered to try to prevent it - is
surfaced through the existing status-text pattern without clearing either list or selection; and
any other exception a refresh raises (for example, a native `Terminate()` failure) is surfaced
through that same status-text pattern without clearing either list or selection.
