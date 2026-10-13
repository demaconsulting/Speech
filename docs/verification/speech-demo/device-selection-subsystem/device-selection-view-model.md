### DeviceSelectionViewModel Verification

#### Verification Approach

`DeviceSelectionViewModel` is verified through deterministic unit tests against an NSubstitute
fake of `IAudioDeviceService`, which lets every enumeration outcome - populated, empty, device
added, device removed, device re-reported in a different format, refresh refused, refresh faulted -
be produced on demand with no audio hardware present. Pre-refresh hooks are controlled with
`TaskCompletionSource` instances so ordering and awaiting can be observed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own view model and fakes
- **Test doubles**: An NSubstitute fake of `IAudioDeviceService`

#### Acceptance Criteria

The unit passes when: it rejects a missing service; it lists, counts and preselects devices on
construction without forcing a re-scan; a refresh preserves a still-present selection by name and
falls back to the first remaining device when it is gone; an empty direction carries an
explanatory message and reports the library's system-default selection; every derived selection and
availability flag is re-announced when it changes; a refresh first invokes and awaits every
registered pre-refresh hook, in order, before asking the service to re-scan; an unregistered hook
is never invoked; a null hook is rejected; a refusal or any other fault raised by a refresh is
shown through both status texts without clearing either list or selection, and a later successful
refresh recovers.

#### Test Scenarios

##### DeviceSelectionViewModel_Constructor_NullService_ThrowsArgumentNullException

**Scenario**: The panel is constructed with no device service.

**Expected**: `ArgumentNullException`.


##### DeviceSelectionViewModel_Constructor_DevicesAvailable_PopulatesBothLists

**Scenario**: The service reports capture and playback devices.

**Expected**: Both lists are populated during construction, so the window shows real device names
the instant it opens.


##### DeviceSelectionViewModel_Constructor_DevicesAvailable_ReportsCountStatus

**Scenario**: The service reports devices.

**Expected**: Each status line reports how many devices of that direction were found.


##### DeviceSelectionViewModel_Constructor_DevicesAvailable_SelectsFirstOfEachDirection

**Scenario**: The service reports devices.

**Expected**: The first device in each direction is preselected, so the demo is usable without
configuration.


##### DeviceSelectionViewModel_Constructor_NeverCallsRefreshDevices

**Scenario**: The service would refuse a refresh, and the panel is constructed.

**Expected**: `RefreshDevices()` is never called and both lists are still populated.


##### DeviceSelectionViewModel_Constructor_NoDevices_ReportsHonestEmptyState

**Scenario**: The service reports no devices at all.

**Expected**: Both directions report no devices available and carry an explanatory message naming
the possible cause.


##### DeviceSelectionViewModel_Refresh_AllDevicesRemoved_ClearsSelectionAndExplains

**Scenario**: The last device is removed and the panel is refreshed.

**Expected**: The selection is cleared and the explanatory empty-state message is shown for both
directions.


##### DeviceSelectionViewModel_CaptureSelection_DeviceSelected_ReturnsNameBasedSelection

**Scenario**: A capture device is selected.

**Expected**: The exposed selection is the library's name-based `AudioDeviceSelection` for that
device.


##### DeviceSelectionViewModel_SelectedPlaybackDevice_Changed_NotifiesPlaybackSelection

**Scenario**: The chosen playback device changes.

**Expected**: The derived playback selection is re-announced, so a bound consumer never reads a
stale selection.


##### DeviceSelectionViewModel_Selections_NoDevices_ReturnSystemDefault

**Scenario**: No devices exist in either direction.

**Expected**: Both selections report the library's system-default selection, so a caller never
has to special-case the no-device machine.


##### DeviceSelectionViewModel_RefreshCommand_Executed_ReEnumeratesDevices

**Scenario**: The refresh command is executed the way a bound button does.

**Expected**: Both directions are re-enumerated through the service.


##### DeviceSelectionViewModel_Refresh_CallsRefreshDevicesBeforeEnumerating

**Scenario**: `Refresh()` is invoked.

**Expected**: `IAudioDeviceService.RefreshDevices()` is called exactly once, before either direction
is re-enumerated.


##### DeviceSelectionViewModel_Refresh_DeviceAdded_KeepsExistingSelection

**Scenario**: A device is hot-plugged and the panel is refreshed.

**Expected**: The chosen device stays selected.


##### DeviceSelectionViewModel_Refresh_SameDeviceReportedWithNewFormat_SelectsMatchingNameAgain

**Scenario**: The selected device is re-reported as a new description carrying a different
sample rate.

**Expected**: It is reselected by name, proving identity is name-based rather than
instance-based.

##### DeviceSelectionViewModel_Refresh_SelectedDeviceRemoved_FallsBackToFirstAvailable

**Scenario**: The selected capture device is unplugged and the panel is refreshed.

**Expected**: The first remaining device is selected, so the picker is never left blank while
devices exist.


##### DeviceSelectionViewModel_Refresh_SelectedPlaybackDeviceRemoved_FallsBackToFirstPlayback

**Scenario**: The selected playback device is unplugged and the panel is refreshed.

**Expected**: The first remaining playback device is selected and the capture selection is
unchanged.

##### DeviceSelectionViewModel_Refresh_DevicesAppear_NotifiesAvailabilityFlags

**Scenario**: A panel with no devices is refreshed and the service now reports devices.

**Expected**: Change notifications are raised for both availability flags and both read true.


##### DeviceSelectionViewModel_Refresh_ServiceThrowsAudioDeviceInUseException_SurfacesMessageViaStatusAndDoesNotClearLists

**Scenario**: `IAudioDeviceService.RefreshDevices()` throws `AudioDeviceInUseException` because a
device is currently streaming.

**Expected**: Both `CaptureStatus` and `PlaybackStatus` are set to the exception's message, and
neither list nor either selection is touched.


##### DeviceSelectionViewModel_Refresh_HookCannotStopInUseDevice_FallsBackToStatusMessage

**Scenario**: A registered no-op hook cannot help, and `IAudioDeviceService.RefreshDevices()`
unconditionally refuses with `AudioDeviceInUseException`.

**Expected**: The status-message fallback still fires, proving the hook mechanism does not weaken
the safety net.

##### DeviceSelectionViewModel_Refresh_ServiceThrowsGeneralException_SurfacesMessageViaStatusAndDoesNotClearLists

**Scenario**: `IAudioDeviceService.RefreshDevices()` throws a general exception (for example,
`InvalidOperationException` simulating a native `Terminate()` failure).

**Expected**: Both status texts are set to the exception's message, and neither list nor either
selection is touched.


##### DeviceSelectionViewModel_Refresh_SucceedsAfterRefusal_ReEnumeratesAndClearsMessage

**Scenario**: The first refresh is refused with `AudioDeviceInUseException` and the second
succeeds.

**Expected**: The first shows the refusal message; the second re-enumerates and reports the normal
count status.


##### DeviceSelectionViewModel_Refresh_RegisteredHook_InvokedAndAwaitedBeforeRefreshDevices

**Scenario**: A pre-refresh hook backed by a `TaskCompletionSource` is registered, then `Refresh()`
is called.

**Expected**: `RefreshDevices()` is not called while the hook's task is unresolved; once the hook
completes, `Refresh()` proceeds and calls `RefreshDevices()` exactly once, proving the hook is
awaited to completion rather than fired without waiting.


##### DeviceSelectionViewModel_Refresh_MultipleHooks_InvokedInRegistrationOrder

**Scenario**: Two hooks are registered and `Refresh()` is called.

**Expected**: The hooks run in registration order.


##### DeviceSelectionViewModel_Refresh_UnregisteredHook_NotInvoked

**Scenario**: A hook is registered, then unregistered before `Refresh()` is called.

**Expected**: The unregistered hook is never invoked.


##### DeviceSelectionViewModel_UnregisterPreRefreshHook_NeverRegistered_RefreshStillSucceeds

**Scenario**: A hook that was never registered is unregistered, then `Refresh()` is called.

**Expected**: No exception, and the refresh still reaches the service.


##### DeviceSelectionViewModel_RegisterPreRefreshHook_NullHook_ThrowsArgumentNullException

**Scenario**: A null hook is registered.

**Expected**: `ArgumentNullException`.


##### DeviceSelectionViewModel_UnregisterPreRefreshHook_NullHook_ThrowsArgumentNullException

**Scenario**: A null hook is unregistered.

**Expected**: `ArgumentNullException`.

