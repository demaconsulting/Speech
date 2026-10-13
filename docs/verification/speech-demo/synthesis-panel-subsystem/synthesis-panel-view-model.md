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

The unit passes when all of the following hold:

- A missing collaborator is rejected.
- Only installed synthesis models are offered and the selection survives a refresh.
- The embedded settings panel reflects the selected model and its value bag reaches the session
  factory.
- A successful Play speaks the entered text through the selected device and passes through every
  lifecycle state, with session states mapped to playback states.
- A second Play is refused while one is in flight, and Stop interrupts an in-flight Play.
- Repeated Play with an unchanged selection reuses the cached engine and session; a changed
  parameter or model instance reloads the engine; a changed device recreates only the session.
- Every unavailable, busy, faulted, setup-fault and unexpected-speak-fault state is reported
  honestly, setup cancellation reports stopped, a faulted session is replaced on the next Play,
  and events from a non-current session are ignored.
- The model-switch guard is false while synthesizing or playing and true while idle or in error.
- Matching-role installs and reference voice changes refresh the panel while keeping the chosen
  voice, and non-matching installs do not.
- The voice notice shows for Chatterbox only.
- `DisposeAsync` stops an in-flight Play, releases the session and engine once, is idempotent and
  unsubscribes from catalog events.
- The pre-refresh hook stops an in-flight Play and awaits its completion before a shared device
  refresh.

#### Test Scenarios

##### SynthesisPanelViewModel_Constructor_NullDependency_ThrowsArgumentNullException

**Scenario**: The panel is constructed with each dependency missing in turn.

**Expected**: `ArgumentNullException` for every missing dependency.

##### SynthesisPanelViewModel_Constructor_NoInstalledSynthesisModel_ReportsHonestEmptyState

**Scenario**: The catalog reports no installed synthesis models.

**Expected**: `HasModels` is false, `AvailableModels` and `SelectedModel` reflect nothing to
choose from, Play is unavailable, and `NoModelsMessage` points the user to the Model Catalog panel.
The view's rendering of that message is not under automated UI test.

##### SynthesisPanelViewModel_Refresh_WhilePlaying_DoesNotDisturbPlayAndSettlesIdle

**Scenario**: A Play is in flight on a session that only ends when stopped, and the catalog is
refreshed; Stop is then requested.

**Expected**: The selected model is unchanged and the session is neither stopped nor disposed by
the refresh; after Stop the Play ends and the panel is no longer `Playing`.

##### SynthesisPanelViewModel_Refresh_MixedCatalog_OffersOnlyInstalledSynthesisModels

**Scenario**: The catalog reports an installed synthesis model, a not-yet-downloaded synthesis
model and an installed recognition model.

**Expected**: Only the installed synthesis model is offered and preselected.

##### SynthesisPanelViewModel_SelectedModel_Changed_UpdatesEmbeddedSettings

**Scenario**: `SelectedModel` is assigned.

**Expected**: The embedded `Settings.Model` is updated to the same model.

##### SynthesisPanelViewModel_Refresh_SelectedModelStillInstalled_PreservesSelection

**Scenario**: Two installed synthesis models are offered, the second is selected and the panel is
refreshed.

**Expected**: The second model is still selected.

##### SynthesisPanelViewModel_Play_EnteredText_SpeaksTextThroughSelectedPlaybackDevice

**Scenario**: Text is entered and Play is invoked.

**Expected**: The created session is bound to the device the device service resolved and speaks
exactly the entered text.

##### SynthesisPanelViewModel_Play_WhilePlaying_IsRefused

**Scenario**: Play is in flight with a session that only ends when stopped.

**Expected**: `State` is `Playing`, and `CanPlay` and `PlayCommand.CanExecute` are false.

##### SynthesisPanelViewModel_Play_UnavailableSession_ReportsErrorStateAndDisposesSession

**Scenario**: The engine creates a session that reports itself unavailable.

**Expected**: `SynthesizerUnavailableMessage` and `Error` state; the session is disposed and never
asked to speak.

##### SynthesisPanelViewModel_Play_EngineBusy_ReportsErrorStateWithEngineMessage

**Scenario**: The engine throws `SynthesisEngineBusyException` while creating the session.

**Expected**: `Error` state with the exception's message, and no exception escapes.

##### SynthesisPanelViewModel_Play_SessionFaulted_ReportsErrorReleasesSessionAndNextPlayRecovers

**Scenario**: The first session faults while speaking; a second Play follows.

**Expected**: `Error` state with the fault's own message ("Session faulted.") and the faulted
session disposed; the second Play
creates a fresh session from the same engine and settles on `Idle`.

##### SynthesisPanelViewModel_Play_SpeakThrowsSynthesizerUnavailable_ReportsErrorStateWithMessage

**Scenario**: `SpeakAsync` throws `SpeechSynthesizerUnavailableException`.

**Expected**: `Error` state carrying the exception's message.

##### SynthesisPanelViewModel_SessionStateChanged_StoppingCreatedFaulted_MapsToPlayingIdleError

**Scenario**: The session raises `Stopping`, `Created` and `Faulted` while speaking.

**Expected**: `State` becomes `Playing`, `Idle` and `Error` respectively, and `Faulted` sets
`SessionFaultedMessage`.

##### SynthesisPanelViewModel_DisposeAsync_WhilePlaying_StopsPlayAndReleasesSessionAndEngine

**Scenario**: The panel is disposed while a Play is in flight.

**Expected**: The session is stopped once, and the session and engine are each disposed once.

##### SynthesisPanelViewModel_DisposeAsync_AfterPlay_ReleasesSessionAndEngineOnce

**Scenario**: The panel is disposed twice after a completed Play.

**Expected**: The session and the engine are each disposed exactly once.

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

##### SynthesisPanelViewModel_Play_SetupFaults_ReportsErrorStateWithMessage

**Scenario**: Engine load, or session creation, throws an unexpected exception (Theory over both
stages).

**Expected**: Nothing escapes; the panel is in `Error` with the exception's message, and no session
is left cached.

##### SynthesisPanelViewModel_Play_SetupCanceled_ReportsStoppedAndReturnsToIdle

**Scenario**: Engine load, or session creation, is canceled (Theory over both stages).

**Expected**: The panel reports `StoppedMessage` and returns to `Idle`.

##### SynthesisPanelViewModel_Play_SpeakThrowsUnexpected_ReportsErrorAndReleasesSession

**Scenario**: `SpeakAsync` throws an exception that is neither cancellation, an unavailable
synthesizer nor a session fault.

**Expected**: The panel is in `Error` with the exception's message and the session is released.

##### SynthesisPanelViewModel_SessionStateChanged_ForeignSender_IsIgnored

**Scenario**: A state change is raised by a session other than the panel's current one.

**Expected**: The panel's state is unchanged.

##### SynthesisPanelViewModel_Play_ModelInstanceChanged_ReloadsEngine

**Scenario**: The selected model is replaced by a different instance between two Play calls.

**Expected**: The engine is reloaded.

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

##### SynthesisPanelViewModel_VoiceNotice_OtherModel_ShowsNothing

**Scenario**: A catalog offering only a non-Chatterbox synthesis model is composed, with a voice pack
reported installed.

**Expected**: `HasVoiceNotice` is `false` and `VoiceNotice` is `null`.

#### Accepted Gaps

The behavior listed as a _Known limitation_ in the unit design - Stop requested before any session
exists, which only cancels the command's token - is intentionally not covered by a dedicated
test, because its effect depends on whether the seam honors the token. Setup faults, setup
cancellation, unexpected speak faults and stale session events are now handled and tested above.
