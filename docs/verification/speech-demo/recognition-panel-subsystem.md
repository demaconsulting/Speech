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

#### RecognitionPanelViewModel_Stop_DuringListening_StopsAndReleasesSession

**Scenario**: Stop is invoked while listening.

**Expected**: The recognizer's `Stop()` is invoked, the recognizer is disposed, and `State`
returns to `Idle`.

**Requirement coverage**: `SpeechDemo-Recognition-StartStopLifecycle`,
`SpeechDemo-Recognition-ResourceLifetime`.

#### RecognitionPanelViewModel_Stop_NothingListening_IsSafeNoOp

**Scenario**: Stop is invoked with no active session.

**Expected**: No exception and no state change.

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

### Requirements Coverage

- **`SpeechDemo-Recognition-ModelSelection`**:
  `RecognitionPanelViewModel_Refresh_MixedCatalog_OffersOnlyInstalledRecognitionModels`,
  `RecognitionPanelViewModel_Refresh_ModelStillInstalled_PreservesSelection`
- **`SpeechDemo-Recognition-StartStopLifecycle`**:
  `RecognitionPanelViewModel_Start_SuccessfulSession_EntersListeningState`,
  `RecognitionPanelViewModel_Stop_DuringListening_StopsAndReleasesSession`,
  `RecognitionPanelViewModel_Stop_NothingListening_IsSafeNoOp`
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
  `RecognitionPanelViewModel_Stop_DuringListening_StopsAndReleasesSession`,
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

### Acceptance Criteria

A RecognitionPanelSubsystem test run passes when: only installed recognition models are offered
and the selection survives a refresh; a successful Start enters the listening state and invokes
the recognizer exactly once; partial results replace the trailing transcript line while finals
commit permanently in order; Stop and Dispose both stop and release an active recognizer exactly
once without throwing, and are safe no-ops with nothing active; every unavailable state - no
model, no device, an unavailable recognizer, a throwing `Start()` - is reported honestly with the
recognizer released rather than leaked; the session seam validates its arguments and reports a
role mismatch the same honest way the library reports an uninstalled model; a matching-role
`ModelInstalled` event triggers an automatic refresh while a non-matching-role event does not; and
`Dispose()` unsubscribes from `ModelInstalled` so a later event is never applied.
