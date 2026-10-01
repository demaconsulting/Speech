## SpeechDemo RecognitionPanelSubsystem Verification

### Verification Approach

The RecognitionPanelSubsystem is verified through deterministic unit tests in two layers.
`RecognitionPanelViewModel` is tested against NSubstitute fakes of `IModelCatalogService`,
`IAudioDeviceService`, and `IRecognizerSessionFactory`, which lets every Start/Stop lifecycle
transition, the partial-then-final transcript sequencing, and every unavailable-state path (no
model, no device, unavailable recognizer, `Start()` throwing) be produced on demand with no
downloaded model, no native runtime, and no real microphone. `RecognizerSessionFactory` is
tested directly for argument validation and the honest "wrong role" outcome.

The "correct role composes a working recognizer" path inside `RecognizerSessionFactory`
delegates to the library's own `SpeechRecognizerFactory`, which requires an `IRecognitionModel`

- an interface only the library's own assemblies can implement (see the design document's
remarks). That composition path is therefore outside this test project's reach and remains
covered by the library's own recognition-subsystem tests; the system-level integration test
additionally proves the panel composes over the real seam without a downloaded model.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: `RecognizerSessionFactoryTests` roots the library's model store in a fresh
  directory under the test output folder
- **Test doubles**: NSubstitute fakes of `IModelCatalogService`, `IAudioDeviceService`,
  `IRecognizerSessionFactory`, `ISpeechRecognizer`, and `IAudioCaptureDevice`; `FakeSpeechModel`

### Test Scenarios

#### RecognitionPanelViewModel_Constructor_NullDependency_ThrowsArgumentNullException

**Scenario**: The panel is constructed with each dependency missing in turn.

**Expected**: `ArgumentNullException` for every missing dependency.

**Requirement coverage**: `SpeechDemo-Recognition-SessionSeam`.

#### RecognitionPanelViewModel_Constructor_NoInstalledRecognitionModel_ReportsHonestEmptyState

**Scenario**: The catalog reports no installed recognition models.

**Expected**: `HasModels` is false and `AvailableModels`/`SelectedModel` reflect nothing to
choose from.

**Requirement coverage**: `SpeechDemo-Recognition-HonestEmptyCatalogState`.

#### RecognitionPanelViewModel_Refresh_MixedCatalog_OffersOnlyInstalledRecognitionModels

**Scenario**: The catalog reports an installed recognition model, a not-yet-downloaded
recognition model, and an installed synthesis model.

**Expected**: Only the installed recognition model is offered and preselected.

**Requirement coverage**: `SpeechDemo-Recognition-ModelSelection`.

#### RecognitionPanelViewModel_Refresh_ModelStillInstalled_PreservesSelection

**Scenario**: The catalog is refreshed while the previously selected recognition model is still
installed.

**Expected**: The previously selected model remains selected after the refresh.

**Requirement coverage**: `SpeechDemo-Recognition-ModelSelection`.

#### RecognitionPanelViewModel_Start_NoModelSelected_ReportsErrorState

**Scenario**: Start is invoked with no model selected.

**Expected**: `NoModelSelectedMessage` and `Error` state, not an exception.

**Requirement coverage**: `SpeechDemo-Recognition-HonestUnavailableStates`.

#### RecognitionPanelViewModel_Start_NoCaptureDevice_ReportsErrorState

**Scenario**: The device seam reports no available capture device.

**Expected**: `NoCaptureDeviceMessage` and `Error` state.

**Requirement coverage**: `SpeechDemo-Recognition-HonestUnavailableStates`.

#### RecognitionPanelViewModel_Start_RecognizerUnavailable_ReportsErrorStateAndDisposes

**Scenario**: The session seam composes a recognizer that honestly reports itself unavailable.

**Expected**: `RecognizerUnavailableMessage` and `Error` state, and the unavailable recognizer is
disposed.

**Requirement coverage**: `SpeechDemo-Recognition-HonestUnavailableStates`.

#### RecognitionPanelViewModel_Start_RecognizerStartThrows_ReportsErrorStateAndDisposes

**Scenario**: A composed recognizer's `Start()` throws.

**Expected**: The exception is caught, `Error` state is reported with the exception's message,
and the recognizer is disposed rather than leaked.

**Requirement coverage**: `SpeechDemo-Recognition-HonestUnavailableStates`,
`SpeechDemo-Recognition-ResourceLifetime`.

#### RecognitionPanelViewModel_Start_SuccessfulSession_EntersListeningState

**Scenario**: A successful Start with a working recognizer.

**Expected**: `State` becomes `Listening` and the recognizer's `Start()` is invoked exactly once.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_ResultReceived_PartialThenFinal_UpdatesTranscriptInOrder

**Scenario**: The recognizer raises a partial result followed by a final result for the same
utterance.

**Expected**: The partial is shown as the trailing line while provisional, then replaced by the
committed final line once the final result arrives; the final is preserved and a fresh partial
starts empty.

**Requirement coverage**: `SpeechDemo-Recognition-TranscriptSequencing`.

#### RecognitionPanelViewModel_BuildTranscriptText_FinalsAndPartial_RendersInOrder

**Scenario**: `BuildTranscriptText` is exercised directly with a mix of committed finals and a
trailing partial.

**Expected**: All finals render in commit order followed by the partial on its own trailing
line.

**Requirement coverage**: `SpeechDemo-Recognition-TranscriptSequencing`.

#### RecognitionPanelViewModel_Stop_DuringListening_StopsWithoutDisposingSession

**Scenario**: Stop is invoked while listening.

**Expected**: The recognizer's `Stop()` is invoked, but the recognizer is **not** disposed and
remains cached so a subsequent Start can reuse it; `State` returns to `Idle`.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_Stop_NothingListening_IsSafeNoOp

**Scenario**: Stop is invoked with no active session.

**Expected**: No exception and no state change.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_StartStopStart_SameSelection_ReusesRecognizer

**Scenario**: Start, Stop, then Start again, with the same model and capture device selected
throughout.

**Expected**: The session seam composes a recognizer exactly once; the second Start reuses the
cached instance rather than recomposing (and reloading the model) a second time.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_DeviceRefresh_InvalidatesCachedRecognizer

**Scenario**: A recognizer is cached (idle) for the current capture device, then the shared
`DeviceSelectionViewModel.Refresh()` runs.

**Expected**: The cached recognizer is disposed, so a subsequent Start composes a fresh one
bound to the post-refresh device table rather than reusing one for a now-stale device.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_SelectedModelChanged_InvalidatesCachedRecognizer

**Scenario**: The selected recognition model is changed while idle, with a recognizer already
cached for the previously selected model.

**Expected**: The recognizer cached for the previous model is disposed, so a subsequent Start
composes one for the newly selected model instead of reusing a stale instance.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_SelectedModelChanged_WhileListening_StopsAndInvalidatesRecognizer

**Scenario**: The selected recognition model is changed while a session is actively listening -
bypassing the view's disabled model picker, since `SelectedModel`'s setter remains public and is
not guarded at the model level.

**Expected**: The active recognizer is stopped, then disposed; `State` returns to `Idle` rather
than remaining stuck at `Listening` with no cached recognizer for a later Stop to find.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_SelectedCaptureDeviceChanged_InvalidatesCachedRecognizer

**Scenario**: The shared `DeviceSelectionViewModel`'s selected capture device is changed while
idle, with a recognizer already cached for the previously selected device.

**Expected**: The recognizer cached for the previous device is disposed, so a subsequent Start
composes one bound to the newly selected device instead of reusing a stale instance.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_SelectedCaptureDeviceChanged_WhileListening_StopsAndInvalidatesRecognizer

**Scenario**: The shared `DeviceSelectionViewModel`'s selected capture device is changed while a
session is actively listening - the capture-device picker, unlike the model picker, is never
disabled while listening, so this is reachable directly from the view.

**Expected**: The active recognizer is stopped, then disposed; `State` returns to `Idle` rather
than remaining stuck at `Listening` bound to an abandoned device.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_Dispose_ReleasesActiveSessionWithoutThrowing

**Scenario**: The panel is disposed while a recognizer is active.

**Expected**: The recognizer is disposed exactly once and disposal does not throw.

**Requirement coverage**: `SpeechDemo-Recognition-ResourceLifetime`.

#### RecognizerSessionFactory_Constructor_NullStore_ThrowsArgumentNullException

**Scenario**: The seam is constructed with no model store.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Recognition-SessionSeam`.

#### RecognizerSessionFactory_Create_NullModel_ThrowsArgumentNullException

**Scenario**: `Create` is called with a missing model.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Recognition-SessionSeam`.

#### RecognizerSessionFactory_Create_NullDevice_ThrowsArgumentNullException

**Scenario**: `Create` is called with a missing capture device.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Recognition-SessionSeam`.

#### RecognizerSessionFactory_Create_ModelNotRecognitionRole_ReturnsUnavailableRecognizer

**Scenario**: `Create` is called with a model that does not implement the library's recognition
role.

**Expected**: The library's own `UnavailableSpeechRecognizer.Instance`, exactly like a model that
is not installed, rather than an exception.

**Requirement coverage**: `SpeechDemo-Recognition-SessionSeam`.

#### RecognitionPanelViewModel_ModelInstalled_MatchingRole_TriggersRefresh

**Scenario**: `IModelCatalogService.ModelInstalled` is raised for a recognition-role model after
construction.

**Expected**: The panel refreshes itself automatically and the newly installed model appears in
`AvailableModels`, without a manual Refresh click.

**Requirement coverage**: `SpeechDemo-Recognition-AutoRefreshOnInstall`.

#### RecognitionPanelViewModel_ModelInstalled_NonMatchingRole_DoesNotTriggerRefresh

**Scenario**: `IModelCatalogService.ModelInstalled` is raised for a synthesis-role model.

**Expected**: The panel does not refresh; it still reports no models.

**Requirement coverage**: `SpeechDemo-Recognition-AutoRefreshOnInstall`.

#### RecognitionPanelViewModel_Dispose_UnsubscribesFromModelInstalled_NoRefreshAfterDispose

**Scenario**: The panel is disposed, then `IModelCatalogService.ModelInstalled` is raised for a
matching-role model.

**Expected**: No exception, and the panel does not pick up the later install since it had already
unsubscribed.

**Requirement coverage**: `SpeechDemo-Recognition-AutoRefreshOnInstall`.

#### RecognitionPanelViewModel_CanChangeModel_TogglesAcrossStateTransitions

**Scenario**: A successful `Start()` transitions `State` from `Idle` to `Listening`, then `Stop()`
returns it to `Idle`.

**Expected**: `CanChangeModel` is `true` while `Idle`, `false` while `Listening`, and `true` again
after `Stop()`.

**Requirement coverage**: `SpeechDemo-Recognition-ModelSwitchGuard`.

#### RecognitionPanelViewModel_CanChangeModel_ErrorState_IsTrue

**Scenario**: `Start()` fails (no capture device available) and the panel enters `Error`.

**Expected**: `CanChangeModel` is `true`, since `Error` is not `Listening` and a user must be able
to pick a different model after a failed attempt.

**Requirement coverage**: `SpeechDemo-Recognition-ModelSwitchGuard`.

#### RecognitionPanelViewModel_PreRefreshHook_WhileListening_StopsSessionBeforeDeviceRefreshSucceeds

**Scenario**: The panel is actively listening, sharing a `DeviceSelectionViewModel` whose device
service refuses `RefreshDevices()` with `AudioDeviceInUseException` only while a "still
listening" flag is true; the recognizer's `Stop()` flips that flag false. The shared
`DeviceSelectionViewModel.Refresh()` is then invoked, as the "Refresh devices" button would.

**Expected**: The panel's registered pre-refresh hook calls `Stop()` on the recognizer before the
device refresh is attempted, so the refresh completes without throwing; the panel returns to
`Idle` with `CanStop` false.

**Requirement coverage**: `SpeechDemo-Recognition-StopsBeforeDeviceRefresh`.

### Requirements Coverage

- **`SpeechDemo-Recognition-ModelSelection`**:
  `RecognitionPanelViewModel_Refresh_MixedCatalog_OffersOnlyInstalledRecognitionModels`,
  `RecognitionPanelViewModel_Refresh_ModelStillInstalled_PreservesSelection`
- **`SpeechDemo-Recognition-StartStopLifecycle`**:
  `RecognitionPanelViewModel_Start_SuccessfulSession_EntersListeningState`,
  `RecognitionPanelViewModel_Stop_DuringListening_StopsWithoutDisposingSession`,
  `RecognitionPanelViewModel_Stop_NothingListening_IsSafeNoOp`,
  `RecognitionPanelViewModel_StartStopStart_SameSelection_ReusesRecognizer`,
  `RecognitionPanelViewModel_DeviceRefresh_InvalidatesCachedRecognizer`,
  `RecognitionPanelViewModel_SelectedModelChanged_InvalidatesCachedRecognizer`,
  `RecognitionPanelViewModel_SelectedModelChanged_WhileListening_StopsAndInvalidatesRecognizer`,
  `RecognitionPanelViewModel_SelectedCaptureDeviceChanged_InvalidatesCachedRecognizer`,
  `RecognitionPanelViewModel_SelectedCaptureDeviceChanged_WhileListening_StopsAndInvalidatesRecognizer`
- **`SpeechDemo-Recognition-TranscriptSequencing`**:
  `RecognitionPanelViewModel_ResultReceived_PartialThenFinal_UpdatesTranscriptInOrder`,
  `RecognitionPanelViewModel_BuildTranscriptText_FinalsAndPartial_RendersInOrder`
- **`SpeechDemo-Recognition-HonestUnavailableStates`**:
  `RecognitionPanelViewModel_Start_NoModelSelected_ReportsErrorState`,
  `RecognitionPanelViewModel_Start_NoCaptureDevice_ReportsErrorState`,
  `RecognitionPanelViewModel_Start_RecognizerUnavailable_ReportsErrorStateAndDisposes`,
  `RecognitionPanelViewModel_Start_RecognizerStartThrows_ReportsErrorStateAndDisposes`
- **`SpeechDemo-Recognition-HonestEmptyCatalogState`**:
  `RecognitionPanelViewModel_Constructor_NoInstalledRecognitionModel_ReportsHonestEmptyState`
- **`SpeechDemo-Recognition-ResourceLifetime`**:
  `RecognitionPanelViewModel_Start_RecognizerStartThrows_ReportsErrorStateAndDisposes`,
  `RecognitionPanelViewModel_Dispose_ReleasesActiveSessionWithoutThrowing`
- **`SpeechDemo-Recognition-SessionSeam`**:
  `RecognitionPanelViewModel_Constructor_NullDependency_ThrowsArgumentNullException`,
  `RecognizerSessionFactory_Constructor_NullStore_ThrowsArgumentNullException`,
  `RecognizerSessionFactory_Create_NullModel_ThrowsArgumentNullException`,
  `RecognizerSessionFactory_Create_NullDevice_ThrowsArgumentNullException`,
  `RecognizerSessionFactory_Create_ModelNotRecognitionRole_ReturnsUnavailableRecognizer`
- **`SpeechDemo-Recognition-AutoRefreshOnInstall`**:
  `RecognitionPanelViewModel_ModelInstalled_MatchingRole_TriggersRefresh`,
  `RecognitionPanelViewModel_ModelInstalled_NonMatchingRole_DoesNotTriggerRefresh`,
  `RecognitionPanelViewModel_Dispose_UnsubscribesFromModelInstalled_NoRefreshAfterDispose`
- **`SpeechDemo-Recognition-ModelSwitchGuard`**:
  `RecognitionPanelViewModel_CanChangeModel_TogglesAcrossStateTransitions`,
  `RecognitionPanelViewModel_CanChangeModel_ErrorState_IsTrue`
- **`SpeechDemo-Recognition-StopsBeforeDeviceRefresh`**:
  `RecognitionPanelViewModel_PreRefreshHook_WhileListening_StopsSessionBeforeDeviceRefreshSucceeds`

### Acceptance Criteria

A RecognitionPanelSubsystem test run passes when: only installed recognition models are offered
and the selection survives a refresh; a successful Start enters the listening state and invokes
the recognizer exactly once; partial results replace the trailing transcript line while finals
commit permanently in order; Stop stops an active recognizer without disposing it, retaining it
cached for reuse, while Dispose releases an active recognizer exactly once; both are safe no-ops
with nothing active and neither throws; every unavailable state - no
model, no device, an unavailable recognizer, a throwing `Start()` - is reported honestly with the
recognizer released rather than leaked; a cached recognizer is reused across repeated Start/Stop
cycles for the same model and device, but is invalidated - stopping an active session first if
one is in progress, never leaving the panel stuck in `Listening` - whenever the selected model,
the selected capture device, or a pending device refresh requires a different recognizer; the
session seam validates its arguments and reports a
role mismatch the same honest way the library reports an uninstalled model; a matching-role
`ModelInstalled` event triggers an automatic refresh while a non-matching-role event does not;
`Dispose()` unsubscribes from `ModelInstalled` so a later event is never applied; and the panel's
registered pre-refresh hook stops an actively listening session before a shared device refresh is
attempted, so the refresh succeeds deterministically.
