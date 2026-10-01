## SpeechDemo RecognitionPanelSubsystem Verification

### Verification Approach

The RecognitionPanelSubsystem is verified through deterministic unit tests in two layers.
`RecognitionPanelViewModel` is tested against NSubstitute/fake doubles of `IModelCatalogService`,
`IAudioDeviceService`, `IRecognizerSessionFactory`, `ISpeechRecognizerEngine`, and
`IRecognitionSession`, which lets every async Start/Stop lifecycle transition, state derivation
from `IRecognitionSession.StateChanged`, the partial-then-final transcript sequencing, the
two-tier engine/session cache invalidation, and every unavailable-state path (no model, no
device, unavailable engine, a busy/faulted session) be produced on demand with no downloaded
model, no native runtime, and no real microphone. `RecognizerSessionFactory` is tested directly
for argument validation and the honest "wrong role" outcome.

The "correct role composes a working engine" path inside `RecognizerSessionFactory` delegates to
the library's own `SpeechRecognizerFactory`, which requires an `IRecognitionModel` - an interface
only the library's own assemblies can implement (see the design document's remarks). That
composition path is therefore outside this test project's reach and remains covered by the
library's own recognition-subsystem tests; the system-level integration test additionally proves
the panel composes over the real seam without a downloaded model.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: `RecognizerSessionFactoryTests` roots the library's model store in a fresh
  directory under the test output folder
- **Test doubles**: NSubstitute fakes of `IModelCatalogService`, `IAudioDeviceService`, and
  `IRecognizerSessionFactory`; hand-written `FakeSpeechRecognizerEngine` and
  `FakeRecognitionSession` (from `Fakes/`) standing in for `ISpeechRecognizerEngine` and
  `IRecognitionSession`; `FakeSpeechModel`

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

**Scenario**: The session seam loads an engine that honestly reports itself unavailable.

**Expected**: `RecognizerUnavailableMessage` and `Error` state, and the unavailable engine is
disposed.

**Requirement coverage**: `SpeechDemo-Recognition-HonestUnavailableStates`.

#### RecognitionPanelViewModel_Start_RecognizerStartThrows_ReportsErrorStateAndDisposes

**Scenario**: A created session's `StartAsync()` throws `SpeechRecognizerUnavailableException`.

**Expected**: The exception is caught, `Error` state is reported with the exception's message,
and the session is disposed rather than leaked.

**Requirement coverage**: `SpeechDemo-Recognition-HonestUnavailableStates`,
`SpeechDemo-Recognition-ResourceLifetime`.

#### RecognitionPanelViewModel_Start_SuccessfulSession_EntersListeningState

**Scenario**: A successful Start with a working engine and session.

**Expected**: `State` becomes `Listening` and the session's `StartAsync()` is invoked exactly
once.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_ResultReceived_PartialThenFinal_UpdatesTranscriptInOrder

**Scenario**: The session's result stream yields a partial result followed by a final result for
the same utterance.

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

#### RecognitionPanelViewModel_StateChanged_SessionTransitionsToFaulted_ReportsErrorState

**Scenario**: A listening session itself reports an unrecoverable `Faulted` state (for example,
the bound capture device being lost mid-session) via `StateChanged`, not caused by this panel
calling Stop or Start.

**Expected**: `State` becomes `Error` and `StatusMessage` becomes `SessionFaultedMessage`,
driven entirely by the `StateChanged` mapping rather than an ad hoc assignment.

**Requirement coverage**: `SpeechDemo-Recognition-HonestUnavailableStates`.

#### RecognitionPanelViewModel_Stop_DuringListening_StopsWithoutDisposingSession

**Scenario**: Stop is invoked while listening.

**Expected**: The session's `StopAsync()` is invoked and the session is released (disposed as
single-use), but the cached engine itself is **not** disposed and remains cached so a subsequent
Start reuses it instead of reloading its model; `State` returns to `Idle` and `StatusMessage`
becomes `StoppedMessage`.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_Stop_NothingListening_IsSafeNoOp

**Scenario**: Stop is invoked with no active session.

**Expected**: No exception and no state change.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_Stop_CalledTwiceConcurrently_BothCompleteWithoutThrowing

**Scenario**: `StopCommand` (`AllowConcurrentExecutions`) is invoked twice without awaiting the
first before starting the second.

**Expected**: Both complete without throwing, rather than racing to double-dispose the same
session, and the panel settles at `Idle`.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_StartStopStart_SameSelection_ReusesEngine

**Scenario**: Start, Stop, then Start again, with the same model and capture device selected
throughout.

**Expected**: The session seam loads an engine exactly once (`LoadAsync` is invoked once); a
fresh single-use session is created from the same cached engine for each of the two runs.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_DeviceRefresh_InvalidatesCachedSessionButNotEngine

**Scenario**: A session is cached (idle) from a prior Start/Stop cycle, then the shared
`DeviceSelectionViewModel.Refresh()` runs.

**Expected**: The cached session is disposed, but the engine is never reloaded; a subsequent
Start creates a fresh session bound to the post-refresh device table from the same cached engine.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_SelectedModelChanged_InvalidatesCachedEngine

**Scenario**: The selected recognition model is changed while idle, with an engine already
cached (idle) for the previously selected model.

**Expected**: The engine cached for the previous model is disposed, so a subsequent Start loads
one for the newly selected model instead of reusing a stale instance.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_SelectedModelChanged_WhileListening_StopsAndInvalidatesEngine

**Scenario**: The selected recognition model is changed while a session is actively listening -
bypassing the view's disabled model picker, since `SelectedModel`'s setter remains public and is
not guarded at the model level.

**Expected**: The active session is stopped, then the engine is disposed; `State` returns to
`Idle` rather than remaining stuck at `Listening` with no cached session for a later Stop to
find.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_SelectedCaptureDeviceChanged_InvalidatesSessionButNotEngine

**Scenario**: The shared `DeviceSelectionViewModel`'s selected capture device is changed while
idle, with a session already cached (idle) bound to the previously selected device.

**Expected**: The session cached for the previous device is disposed, but the engine is never
reloaded, so a subsequent Start builds a session bound to the newly selected device while reusing
the already-loaded engine.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_SelectedCaptureDeviceChanged_WhileListening_StopsAndInvalidatesSession

**Scenario**: The shared `DeviceSelectionViewModel`'s selected capture device is changed while a
session is actively listening - the capture-device picker, unlike the model picker, is never
disabled while listening, so this is reachable directly from the view.

**Expected**: The active session is stopped and disposed, but the engine persists; `State`
returns to `Idle` rather than remaining stuck at `Listening` bound to an abandoned device.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`.

#### RecognitionPanelViewModel_Dispose_ReleasesActiveSessionWithoutThrowing

**Scenario**: The panel is disposed (`DisposeAsync`) while a session is active.

**Expected**: The session and the cached engine are disposed without throwing.

**Requirement coverage**: `SpeechDemo-Recognition-ResourceLifetime`.

#### RecognizerSessionFactory_Constructor_NullStore_ThrowsArgumentNullException

**Scenario**: The seam is constructed with no model store.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Recognition-SessionSeam`.

#### RecognizerSessionFactory_LoadAsync_NullModel_ThrowsArgumentNullException

**Scenario**: `LoadAsync` is called with a missing model.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Recognition-SessionSeam`.

#### RecognizerSessionFactory_LoadAsync_ModelNotRecognitionRole_ReturnsUnavailableEngine

**Scenario**: `LoadAsync` is called with a model that does not implement the library's
recognition role.

**Expected**: The library's own `UnavailableSpeechRecognizerEngine.Instance`, exactly like a
model that is not installed, rather than an exception.

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
listening" flag is true; the session's `StopAsync()` flips that flag false. The shared
`DeviceSelectionViewModel.Refresh()` is then invoked, as the "Refresh devices" button would.

**Expected**: The panel's registered pre-refresh hook calls `StopAsync()` on the session before
the device refresh is attempted, so the refresh completes without throwing; the panel returns to
`Idle` with `CanStop` false.

**Requirement coverage**: `SpeechDemo-Recognition-StopsBeforeDeviceRefresh`.

#### RecognitionPanelViewModel_PreRefreshHook_WhileIdle_IsNoOpAndDeviceRefreshSucceeds

**Scenario**: The registered pre-refresh hook runs while no listening session was ever started
(no engine, no session created).

**Expected**: The refresh completes without throwing, no session is created, and the panel
remains `Idle`.

**Requirement coverage**: `SpeechDemo-Recognition-StopsBeforeDeviceRefresh`.

### Requirements Coverage

- **`SpeechDemo-Recognition-ModelSelection`**:
  `RecognitionPanelViewModel_Refresh_MixedCatalog_OffersOnlyInstalledRecognitionModels`,
  `RecognitionPanelViewModel_Refresh_ModelStillInstalled_PreservesSelection`
- **`SpeechDemo-Recognition-StartStopLifecycle`**:
  `RecognitionPanelViewModel_Start_SuccessfulSession_EntersListeningState`,
  `RecognitionPanelViewModel_Stop_DuringListening_StopsWithoutDisposingSession`,
  `RecognitionPanelViewModel_Stop_NothingListening_IsSafeNoOp`,
  `RecognitionPanelViewModel_Stop_CalledTwiceConcurrently_BothCompleteWithoutThrowing`,
  `RecognitionPanelViewModel_StartStopStart_SameSelection_ReusesEngine`,
  `RecognitionPanelViewModel_DeviceRefresh_InvalidatesCachedSessionButNotEngine`,
  `RecognitionPanelViewModel_SelectedModelChanged_InvalidatesCachedEngine`,
  `RecognitionPanelViewModel_SelectedModelChanged_WhileListening_StopsAndInvalidatesEngine`,
  `RecognitionPanelViewModel_SelectedCaptureDeviceChanged_InvalidatesSessionButNotEngine`,
  `RecognitionPanelViewModel_SelectedCaptureDeviceChanged_WhileListening_StopsAndInvalidatesSession`
- **`SpeechDemo-Recognition-TranscriptSequencing`**:
  `RecognitionPanelViewModel_ResultReceived_PartialThenFinal_UpdatesTranscriptInOrder`,
  `RecognitionPanelViewModel_BuildTranscriptText_FinalsAndPartial_RendersInOrder`
- **`SpeechDemo-Recognition-HonestUnavailableStates`**:
  `RecognitionPanelViewModel_Start_NoModelSelected_ReportsErrorState`,
  `RecognitionPanelViewModel_Start_NoCaptureDevice_ReportsErrorState`,
  `RecognitionPanelViewModel_Start_RecognizerUnavailable_ReportsErrorStateAndDisposes`,
  `RecognitionPanelViewModel_Start_RecognizerStartThrows_ReportsErrorStateAndDisposes`,
  `RecognitionPanelViewModel_StateChanged_SessionTransitionsToFaulted_ReportsErrorState`
- **`SpeechDemo-Recognition-HonestEmptyCatalogState`**:
  `RecognitionPanelViewModel_Constructor_NoInstalledRecognitionModel_ReportsHonestEmptyState`
- **`SpeechDemo-Recognition-ResourceLifetime`**:
  `RecognitionPanelViewModel_Start_RecognizerStartThrows_ReportsErrorStateAndDisposes`,
  `RecognitionPanelViewModel_Dispose_ReleasesActiveSessionWithoutThrowing`
- **`SpeechDemo-Recognition-SessionSeam`**:
  `RecognitionPanelViewModel_Constructor_NullDependency_ThrowsArgumentNullException`,
  `RecognizerSessionFactory_Constructor_NullStore_ThrowsArgumentNullException`,
  `RecognizerSessionFactory_LoadAsync_NullModel_ThrowsArgumentNullException`,
  `RecognizerSessionFactory_LoadAsync_ModelNotRecognitionRole_ReturnsUnavailableEngine`
- **`SpeechDemo-Recognition-AutoRefreshOnInstall`**:
  `RecognitionPanelViewModel_ModelInstalled_MatchingRole_TriggersRefresh`,
  `RecognitionPanelViewModel_ModelInstalled_NonMatchingRole_DoesNotTriggerRefresh`,
  `RecognitionPanelViewModel_Dispose_UnsubscribesFromModelInstalled_NoRefreshAfterDispose`
- **`SpeechDemo-Recognition-ModelSwitchGuard`**:
  `RecognitionPanelViewModel_CanChangeModel_TogglesAcrossStateTransitions`,
  `RecognitionPanelViewModel_CanChangeModel_ErrorState_IsTrue`
- **`SpeechDemo-Recognition-StopsBeforeDeviceRefresh`**:
  `RecognitionPanelViewModel_PreRefreshHook_WhileListening_StopsSessionBeforeDeviceRefreshSucceeds`,
  `RecognitionPanelViewModel_PreRefreshHook_WhileIdle_IsNoOpAndDeviceRefreshSucceeds`

### Acceptance Criteria

A RecognitionPanelSubsystem test run passes when: only installed recognition models are offered
and the selection survives a refresh; a successful Start enters the listening state and invokes
the session's `StartAsync()` exactly once; partial results replace the trailing transcript line
while finals commit permanently in order; Stop stops an active session without disposing the
cached engine, releasing only the single-use session for reuse of the engine, while two
concurrent Stop calls both complete without racing to double-dispose, and Dispose releases an
active session and the cached engine exactly once; both Stop and Dispose are safe no-ops with
nothing active and neither throws; every unavailable state - no model, no device, an unavailable
engine, a session `StartAsync()` throwing, or a session transitioning to `Faulted` mid-stream via
`StateChanged` - is reported honestly with resources released rather than leaked; a cached engine
is reused across repeated Start/Stop cycles for the same model, with a fresh single-use session
created each time, but the engine is invalidated - stopping an active session first if one is in
progress, never leaving the panel stuck in `Listening` - whenever the selected model requires a
different engine, while only the session (not the engine) is invalidated whenever the selected
capture device changes or a device refresh is pending; the session seam validates its arguments
and reports a role mismatch the same honest way the library reports an uninstalled model; a
matching-role `ModelInstalled` event triggers an automatic refresh while a non-matching-role
event does not; `DisposeAsync()` unsubscribes from `ModelInstalled` so a later event is never
applied; and the panel's registered pre-refresh hook stops an actively listening session before a
shared device refresh is attempted (and is a safe no-op while idle), so the refresh succeeds
deterministically.
