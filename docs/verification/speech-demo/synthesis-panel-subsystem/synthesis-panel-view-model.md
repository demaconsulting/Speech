### SynthesisPanelViewModel Verification

#### Verification Approach

`SynthesisPanelViewModel` is tested against NSubstitute and hand-written fakes of
`IModelCatalogService`, `IAudioDeviceService`, `ISynthesizerSessionFactory`,
`ISpeechSynthesizerEngine` and `ISynthesisSession`. This lets every async Play and Stop lifecycle
transition, state derivation from `ISynthesisSession.StateChanged`, the two-tier engine and session
lazy-reload cache, and every unavailable-state path (no model, no device, unavailable engine) be
produced on demand with no downloaded model, no native runtime and no real speakers.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test builds its own view model; no shared state exists between tests
- **Test doubles**: NSubstitute fakes of `IModelCatalogService`, `IAudioDeviceService` and
  `ISynthesizerSessionFactory`; hand-written `FakeSpeechSynthesizerEngine` and
  `FakeSynthesisSession` (from `Fakes/`); `FakeSpeechModel`

#### Acceptance Criteria

The unit passes when: only installed synthesis models are offered and the selection survives a
refresh; the embedded settings panel reflects the selected model and its value bag reaches the
session factory; a successful Play passes through every lifecycle state and releases its session;
Stop interrupts an in-flight Play; repeated Play with an unchanged selection reuses the cached
engine and session, while a changed parameter reloads the engine and a changed device recreates
only the session; every unavailable state is reported honestly; matching-role installs and
reference voice changes refresh the panel while keeping the chosen voice; the voice notice shows
for Chatterbox only; `DisposeAsync` is safe, idempotent and unsubscribes from catalog events; and
the pre-refresh hook stops an in-flight Play and awaits its completion before a shared device
refresh.

#### Test Scenarios

##### SynthesisPanelViewModel_Constructor_NullDependency_ThrowsArgumentNullException

**Scenario**: The panel is constructed with each dependency missing in turn.

**Expected**: `ArgumentNullException` for every missing dependency.

##### SynthesisPanelViewModel_Constructor_NoInstalledSynthesisModel_ReportsHonestEmptyState

**Scenario**: The catalog reports no installed synthesis models.

**Expected**: `HasModels` is false and `AvailableModels` and `SelectedModel` reflect nothing to
choose from.

##### SynthesisPanelViewModel_Refresh_MixedCatalog_OffersOnlyInstalledSynthesisModels

**Scenario**: The catalog reports an installed synthesis model, a not-yet-downloaded synthesis
model and an installed recognition model.

**Expected**: Only the installed synthesis model is offered and preselected.

##### SynthesisPanelViewModel_SelectedModel_Changed_UpdatesEmbeddedSettings

**Scenario**: `SelectedModel` is assigned.

**Expected**: The embedded `Settings.Model` is updated to the same model.

##### SynthesisPanelViewModel_Play_NoModelSelected_ReportsErrorState

**Scenario**: Play is invoked with no model selected.

**Expected**: `NoModelSelectedMessage` and `Error` state, not an exception.

##### SynthesisPanelViewModel_Play_NoPlaybackDevice_ReportsErrorState

**Scenario**: The device seam reports no available playback device.

**Expected**: `NoPlaybackDeviceMessage` and `Error` state.

##### SynthesisPanelViewModel_Play_SynthesizerUnavailable_ReportsErrorStateAndDisposes

**Scenario**: The session seam loads an engine that reports itself unavailable.

**Expected**: `SynthesizerUnavailableMessage` and `Error` state, and the unavailable engine is
disposed.

##### SynthesisPanelViewModel_Play_ModelDeclaresChoiceParameter_ForwardsValueBagToSessionFactory

**Scenario**: The selected model declares a `voice` `ChoiceParameter` with a non-default selection
and Play is invoked.

**Expected**: `ISynthesizerSessionFactory.LoadAsync` receives a value bag exactly matching
`Settings.BuildValueBag()`, proving the settings selection reaches the session factory.

##### SynthesisPanelViewModel_Play_SuccessfulSession_TransitionsThroughLifecycleToIdle

**Scenario**: A full Play with a working engine and session.

**Expected**: `State` passes through `Synthesizing` then `Playing` before settling on `Idle`,
driven by `ISynthesisSession.StateChanged`, and the exact text is forwarded to `SpeakAsync`.

##### SynthesisPanelViewModel_Play_SuccessfulSession_CanChangeModelTogglesAcrossLifecycle

**Scenario**: A successful Play passes through its lifecycle.

**Expected**: `CanChangeModel` is true while `Idle`, false while `Synthesizing` or `Playing`, and
true again once settled.

##### SynthesisPanelViewModel_CanChangeModel_ErrorState_IsTrue

**Scenario**: Play fails with no playback device and the panel enters `Error`.

**Expected**: `CanChangeModel` is true, so a user can pick another model after a failed attempt.

##### SynthesisPanelViewModel_Stop_DuringPlayback_CancelsSessionAndReportsStopped

**Scenario**: Stop is invoked while Play is in flight.

**Expected**: The session's `StopAsync()` is invoked, the task unwinds cleanly, `State` settles on
`Idle` and `StatusMessage` reports `StoppedMessage`.

##### SynthesisPanelViewModel_PreRefreshHook_WhilePlaying_StopsAndAwaitsExecutionTaskBeforeDeviceRefreshSucceeds

**Scenario**: A Play is in flight and the shared device selection is refreshed.

**Expected**: The pre-refresh hook stops the session and awaits the in-flight execution task before
the refresh proceeds, so the refresh completes without throwing and the panel settles on `Idle`.

##### SynthesisPanelViewModel_PreRefreshHook_WhileIdle_IsNoOpAndDeviceRefreshSucceeds

**Scenario**: The pre-refresh hook runs while no Play was ever started.

**Expected**: The refresh completes without throwing, no session is created and the panel remains
`Idle`.

##### SynthesisPanelViewModel_Play_CalledTwiceWithUnchangedModelAndParameters_ReusesSameSessionWithoutReload

**Scenario**: Play is invoked twice with the model and parameter values unchanged.

**Expected**: The engine is loaded once, one session is created, and both calls speak through that
cached session.

##### SynthesisPanelViewModel_Play_ParameterValueChanged_ReloadsEngineAndRecreatesSession

**Scenario**: A declared settings parameter value changes between two Play calls.

**Expected**: The stale engine is disposed, a fresh one is loaded, and a fresh session is created.

##### SynthesisPanelViewModel_Play_PlaybackDeviceChanged_RecreatesSessionButNotEngine

**Scenario**: The selected playback device changes between two Play calls.

**Expected**: The engine is loaded once, a second session is created for the new device, and the
stale session is disposed.

##### SynthesisPanelViewModel_ExampleTagHints_ContainsExpectedTags

**Scenario**: `ExampleTagHints` is read.

**Expected**: It contains bracketed tags drawn from the library's `AudioTagCatalog`, including
`[whispers]`.

##### SynthesisPanelViewModel_ModelInstalled_MatchingRole_TriggersRefresh

**Scenario**: `ModelInstalled` is raised for a synthesis-role model after construction.

**Expected**: The panel refreshes and the new model appears in `AvailableModels`.

##### SynthesisPanelViewModel_ModelInstalled_NonMatchingRole_DoesNotTriggerRefresh

**Scenario**: `ModelInstalled` is raised for a recognition-role model.

**Expected**: The panel does not refresh.

##### SynthesisPanelViewModel_DisposeAsync_UnsubscribesFromModelInstalled_NoRefreshAfterDispose

**Scenario**: The panel is disposed, then `ModelInstalled` is raised for a matching-role model.

**Expected**: No exception and no refresh.

##### SynthesisPanelViewModel_DisposeAsync_NoActiveSession_IsSafeAndIdempotent

**Scenario**: The panel is disposed twice with no active session.

**Expected**: No exception either time.

##### SynthesisPanelViewModel_ReferenceVoicesChanged_RefreshesAndKeepsChosenVoice

**Scenario**: A 10-voice model is selected and voice 5 chosen; the catalog then reports a rebuilt
150-voice model and raises `ReferenceVoicesChanged`.

**Expected**: A new model instance with 150 options, still set to the previously chosen voice.

##### SynthesisPanelViewModel_DisposeAsync_UnsubscribesFromReferenceVoicesChanged

**Scenario**: The panel is disposed, then `ReferenceVoicesChanged` is raised.

**Expected**: The panel does not refresh.

##### SynthesisPanelViewModel_VoiceNotice_ChatterboxShowsConsentAndAttribution_OthersShowNothing

**Scenario**: The real Chatterbox Turbo model is selected with the pack reported installed, then a
different synthesis model.

**Expected**: The notice contains the consent note and "OwenTyme" for Chatterbox and is absent for
the other model.
