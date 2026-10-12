### RecognitionPanelViewModel Verification

#### Verification Approach

`RecognitionPanelViewModel` is tested against NSubstitute/fake doubles of `IModelCatalogService`,
`IAudioDeviceService` and `IRecognizerSessionFactory`, with hand-written `FakeSpeechRecognizerEngine`
and `FakeRecognitionSession` standing in for the library's engine and session. The fakes let every
lifecycle transition, state derivation, transcript result, cache invalidation and unavailable state
be produced on demand with no downloaded model, no native runtime, and no real microphone. A fault
that completes through the session's result stream is asynchronous, so that test polls the status
message up to a bounded timeout.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own view model and fakes
- **Test doubles**: NSubstitute fakes of `IModelCatalogService`, `IAudioDeviceService` and
  `IRecognizerSessionFactory`; `FakeSpeechRecognizerEngine`, `FakeRecognitionSession`,
  `FakeSpeechModel`

#### Acceptance Criteria

The unit passes when: only installed recognition models are offered, the selection survives a
refresh, and an empty catalog is explained; partial results replace the trailing transcript line
while finals commit in order and a new run clears the previous transcript; Stop releases only the
single-use session, is a safe no-op when idle, and two concurrent Stop calls both complete; a cached
engine is reused across Start/Stop cycles, invalidated (stopping an active session first) when the
selected model changes, while only the session is invalidated when the capture device changes or a
device refresh occurs; every unavailable state is reported honestly with resources released;
Dispose releases the session and engine and unsubscribes from `ModelInstalled`; a matching-role
`ModelInstalled` event refreshes while others do not; and `CanChangeModel` is false only while
listening.

#### Test Scenarios

##### RecognitionPanelViewModel_Constructor_NullDependency_ThrowsArgumentNullException

**Scenario**: The panel is constructed with each dependency missing in turn.

**Expected**: `ArgumentNullException` for every missing dependency.

##### RecognitionPanelViewModel_Constructor_NoInstalledRecognitionModel_ReportsHonestEmptyState

**Scenario**: The catalog reports no installed recognition models.

**Expected**: `HasModels` is false and `AvailableModels`/`SelectedModel` reflect nothing to
choose from.

##### RecognitionPanelViewModel_Refresh_MixedCatalog_OffersOnlyInstalledRecognitionModels

**Scenario**: The catalog reports an installed recognition model, a not-yet-downloaded
recognition model, and an installed synthesis model.

**Expected**: Only the installed recognition model is offered and preselected.

##### RecognitionPanelViewModel_Refresh_ModelStillInstalled_PreservesSelection

**Scenario**: The catalog is refreshed while the previously selected recognition model is still
installed.

**Expected**: The previously selected model remains selected after the refresh.

##### RecognitionPanelViewModel_Refresh_SelectedModelNoLongerInstalled_FallsBackToFirstModel

**Scenario**: The catalog is refreshed after the previously selected model is no longer installed.

**Expected**: The first offered model becomes the selection.

##### RecognitionPanelViewModel_Start_NoModelSelected_ReportsErrorState

**Scenario**: Start is invoked with no model selected.

**Expected**: `NoModelSelectedMessage` and `Error` state, not an exception.

##### RecognitionPanelViewModel_Start_NoCaptureDevice_ReportsErrorState

**Scenario**: The device seam reports no available capture device.

**Expected**: `NoCaptureDeviceMessage` and `Error` state.

##### RecognitionPanelViewModel_Start_RecognizerStartThrows_ReportsErrorStateAndDisposes

**Scenario**: A created session's `StartAsync()` throws `SpeechRecognizerUnavailableException`.

**Expected**: The exception is caught, `Error` state is reported with the exception's message,
and the session is disposed rather than leaked.

##### RecognitionPanelViewModel_Start_EngineBusy_ReportsErrorState

**Scenario**: Creating a session throws `RecognitionEngineBusyException`.

**Expected**: `Error` state with the exception's message, not an exception.

##### RecognitionPanelViewModel_StateChanged_SessionTransitionsToFaulted_ReportsErrorState

**Scenario**: A listening session itself reports an unrecoverable `Faulted` state (for example,
the bound capture device being lost mid-session) via `StateChanged`, not caused by this panel
calling Stop or Start.

**Expected**: `State` becomes `Error` and `StatusMessage` becomes `SessionFaultedMessage`,
driven entirely by the `StateChanged` mapping rather than an ad hoc assignment.

##### RecognitionPanelViewModel_ResultStream_SessionFaulted_ReportsFaultMessage

**Scenario**: The session's result stream completes with a `RecognitionSessionFaultedException`.

**Expected**: `StatusMessage` reports the fault's message (polled for up to a bounded timeout
because the pump completes asynchronously).

##### RecognitionPanelViewModel_ResultReceived_PartialThenFinal_UpdatesTranscriptInOrder

**Scenario**: The session's result stream yields a partial result followed by a final result for
the same utterance.

**Expected**: The partial is shown as the trailing line while provisional, then replaced by the
committed final line once the final result arrives; the final is preserved and a fresh partial
starts empty.

##### RecognitionPanelViewModel_BuildTranscriptText_FinalsAndPartial_RendersInOrder

**Scenario**: `BuildTranscriptText` is exercised directly with a mix of committed finals and a
trailing partial.

**Expected**: All finals render in commit order followed by the partial on its own trailing
line.

##### RecognitionPanelViewModel_Start_SecondRun_ClearsPreviousTranscriptAndStatus

**Scenario**: A run produces transcript lines and is stopped, then Start is invoked again.

**Expected**: `Finals`, `Partial` and the previous `StatusMessage` are cleared for the new run.

##### RecognitionPanelViewModel_Stop_DuringListening_StopsWithoutDisposingSession

**Scenario**: Stop is invoked while listening.

**Expected**: The session's `StopAsync()` is invoked, but the cached engine itself is **not**
disposed and remains cached so a subsequent Start reuses it instead of reloading its model;
`State` returns to `Idle` and `StatusMessage` becomes `StoppedMessage`.

##### RecognitionPanelViewModel_Stop_NothingListening_IsSafeNoOp

**Scenario**: Stop is invoked with no active session.

**Expected**: No exception and no state change.

##### RecognitionPanelViewModel_Stop_CalledTwiceConcurrently_BothCompleteWithoutThrowing

**Scenario**: `StopCommand` (`AllowConcurrentExecutions`) is invoked twice without awaiting the
first before starting the second.

**Expected**: Both complete without throwing, rather than racing to double-dispose the same
session, and the panel settles at `Idle`.

##### RecognitionPanelViewModel_StartStopStart_SameSelection_ReusesEngine

**Scenario**: Start, Stop, then Start again, with the same model and capture device selected
throughout.

**Expected**: The session seam loads an engine exactly once (`LoadAsync` is invoked once); a
fresh single-use session is created from the same cached engine for each of the two runs.

##### RecognitionPanelViewModel_DeviceRefresh_InvalidatesCachedSessionButNotEngine

**Scenario**: A session is cached (idle) from a prior Start/Stop cycle, then the shared
`DeviceSelectionViewModel.Refresh()` runs.

**Expected**: The cached session is disposed, but the engine is never reloaded; a subsequent
Start creates a fresh session bound to the post-refresh device table from the same cached engine.

##### RecognitionPanelViewModel_SelectedModelChanged_InvalidatesCachedEngine

**Scenario**: The selected recognition model is changed while idle, with an engine already
cached (idle) for the previously selected model.

**Expected**: The engine cached for the previous model is disposed, so a subsequent Start loads
one for the newly selected model instead of reusing a stale instance.

##### RecognitionPanelViewModel_SelectedModelChanged_WhileListening_StopsAndInvalidatesEngine

**Scenario**: The selected recognition model is changed while a session is actively listening -
bypassing the view's disabled model picker, since `SelectedModel`'s setter remains public.

**Expected**: The active session is stopped, then the engine is disposed; `State` returns to
`Idle` rather than remaining stuck at `Listening`.

##### RecognitionPanelViewModel_SelectedCaptureDeviceChanged_InvalidatesSessionButNotEngine

**Scenario**: The shared `DeviceSelectionViewModel`'s selected capture device is changed while
idle, with a session already cached (idle) bound to the previously selected device.

**Expected**: The session cached for the previous device is disposed, but the engine is never
reloaded, so a subsequent Start builds a session bound to the newly selected device while reusing
the already-loaded engine.

##### RecognitionPanelViewModel_SelectedCaptureDeviceChanged_WhileListening_StopsAndInvalidatesSession

**Scenario**: The shared `DeviceSelectionViewModel`'s selected capture device is changed while a
session is actively listening - the capture-device picker, unlike the model picker, is never
disabled while listening.

**Expected**: The active session is stopped and disposed, but the engine persists; `State`
returns to `Idle` rather than remaining stuck at `Listening` bound to an abandoned device.

##### RecognitionPanelViewModel_Dispose_ReleasesActiveSessionWithoutThrowing

**Scenario**: The panel is disposed (`DisposeAsync`) while a session is active.

**Expected**: The session and the cached engine are disposed without throwing.

##### RecognitionPanelViewModel_ModelInstalled_MatchingRole_TriggersRefresh

**Scenario**: `IModelCatalogService.ModelInstalled` is raised for a recognition-role model after
construction.

**Expected**: The panel refreshes itself automatically and the newly installed model appears in
`AvailableModels`, without a manual Refresh click.

##### RecognitionPanelViewModel_ModelInstalled_NonMatchingRole_DoesNotTriggerRefresh

**Scenario**: `IModelCatalogService.ModelInstalled` is raised for a synthesis-role model.

**Expected**: The panel does not refresh; it still reports no models.

##### RecognitionPanelViewModel_Dispose_UnsubscribesFromModelInstalled_NoRefreshAfterDispose

**Scenario**: The panel is disposed, then `IModelCatalogService.ModelInstalled` is raised for a
matching-role model.

**Expected**: No exception, and the panel does not pick up the later install since it had already
unsubscribed.

##### RecognitionPanelViewModel_CanChangeModel_TogglesAcrossStateTransitions

**Scenario**: A successful `Start()` transitions `State` from `Idle` to `Listening`, then `Stop()`
returns it to `Idle`.

**Expected**: `CanChangeModel` is `true` while `Idle`, `false` while `Listening`, and `true` again
after `Stop()`.

##### RecognitionPanelViewModel_CanChangeModel_ErrorState_IsTrue

**Scenario**: `Start()` fails (no capture device available) and the panel enters `Error`.

**Expected**: `CanChangeModel` is `true`, since `Error` is not `Listening` and a user must be
able to pick a different model after a failed attempt.

### Accepted Gaps

The pre-refresh hook's removal on disposal and the unsubscription from the shared
device-selection panel's property changes on disposal are not observable through the existing
public surface; they are confirmed by inspection of `DisposeAsync`. The
`RecognitionPanelView.axaml` binding of the model picker's `IsEnabled` to `CanChangeModel` is not
exercised by an automated UI test and is confirmed structurally.
