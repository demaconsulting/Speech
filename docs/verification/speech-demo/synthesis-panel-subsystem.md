## SpeechDemo SynthesisPanelSubsystem Verification

### Verification Approach

The SynthesisPanelSubsystem is verified at subsystem level by the `SynthesisPanelViewModel`
working with its seams: the catalog service, the device service and the session factory.
Unit-level behavior is verified in the unit verification documents for SynthesisPanelViewModel and
SynthesizerSessionFactory.

The seams are replaced by NSubstitute and hand-written fakes, so every lifecycle transition and
every unavailable-state path is produced on demand with no downloaded model, no native runtime and
no real speakers. The system-level integration test additionally proves the panel composes over the
real seam without a downloaded model.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test builds its own view model; no shared state exists between tests
- **Test doubles**: NSubstitute fakes of `IModelCatalogService`, `IAudioDeviceService` and
  `ISynthesizerSessionFactory`; hand-written `FakeSpeechSynthesizerEngine` and
  `FakeSynthesisSession`

### Acceptance Criteria

A SynthesisPanelSubsystem test run passes when all of the following hold:

- Play speaks the entered text through the selected installed model and device.
- Stop interrupts an in-flight Play and reports it.
- An unavailable playback device and a setup fault each produce an explained error state.
- The panel refreshes its models while keeping the chosen settings when the catalog or the
  reference voices change.
- The model-switch guard and the Chatterbox voice notice behave as specified.
- Playback is stopped and awaited before a shared device refresh.
- A successful Play leaves no status message, and a closed panel frees its playback device and
  voice and no longer reacts to catalog changes.
- Repeated Play calls with nothing changed prepare the voice and open the device once; a changed
  model, parameter value or playback device takes effect on the next Play.
- The consent note, the pack attribution and the absence of a notice for other models are each
  shown as specified.

### Test Scenarios

#### SynthesisPanelViewModel_Play_EnteredText_SpeaksTextThroughSelectedPlaybackDevice

**Scenario**: Play is invoked with entered text and a selected playback device.

**Expected**: The exact text is spoken through a session bound to that device.

#### SynthesisPanelViewModel_Play_SetupFaults_ReportsErrorStateWithMessage

**Scenario**: Engine load or session creation throws.

**Expected**: An error state carrying the exception's message; nothing escapes.

#### SynthesisPanelViewModel_Play_SuccessfulSession_CanChangeModelTogglesAcrossLifecycle

**Scenario**: A successful Play passes through its lifecycle.

**Expected**: Changing the model is allowed only while idle or in error.

#### SynthesisPanelViewModel_VoiceNotice_ChatterboxShowsConsentAndAttribution_OthersShowNothing

**Scenario**: Chatterbox Turbo and another model are selected in turn.

**Expected**: The notice appears only for Chatterbox. This one scenario covers three subsystem
requirements: `VoiceNotice` (the consent note is present), `VoiceAttribution` (the installed
pack's attribution is present) and, for the other model, `VoiceNoticeOtherModels` (no notice).

#### SynthesisPanelViewModel_VoiceNotice_OtherModel_ShowsNothing

**Scenario**: Only a non-Chatterbox synthesis model is offered and a voice pack is installed.

**Expected**: No voice notice is shown (boundary for `VoiceNoticeOtherModels`).

#### SynthesisPanelViewModel_Play_CalledTwiceWithUnchangedModelAndParameters_ReusesSameSessionWithoutReload

**Scenario**: Play is invoked twice with nothing changed.

**Expected**: The voice is prepared and the playback device opened once, and both calls speak
(`EngineSessionReuse`).

#### SynthesisPanelViewModel_Play_ParameterValueChanged_ReloadsEngineAndRecreatesSession

**Scenario**: A parameter value changes between two Play calls (`ChangedVoiceRespected`).

**Expected**: The next Play prepares the voice again and speaks with the changed value.

#### SynthesisPanelViewModel_Play_ModelInstanceChanged_ReloadsEngine

**Scenario**: The selected model is replaced by a different model between two Play calls
(`ChangedVoiceRespected`).

**Expected**: The next Play prepares the voice again.

#### SynthesisPanelViewModel_Play_PlaybackDeviceChanged_RecreatesSessionButNotEngine

**Scenario**: The playback device changes between two Play calls (`DeviceChangeRespected`).

**Expected**: The next Play uses the new device without preparing the voice again.

#### SynthesisPanelViewModel_SelectedModel_Changed_UpdatesEmbeddedSettings

**Scenario**: The selected model changes (`ModelSettings`).

**Expected**: The presented parameters are those of the newly selected model.

#### SynthesisPanelViewModel_Constructor_NullDependency_ThrowsArgumentNullException

**Scenario**: A required dependency is missing (error case for `DependencyValidation`; the
missing-model case, `ModelValidation`, is verified at unit level by
`SynthesizerSessionFactory_LoadAsync_NullModel_ThrowsArgumentNullException`).

**Expected**: An argument-null error is raised.

#### SynthesisPanelViewModel_Play_NoModelSelected_ReportsErrorState

**Scenario**: Play is invoked with no model selected (error case).

**Expected**: An explained error state; no exception.

#### SynthesisPanelViewModel_Play_SpeakThrowsUnexpected_ReportsErrorAndReleasesSession

**Scenario**: An unexpected fault occurs while speaking (error case).

**Expected**: An error state with the fault message and the playback device freed.

#### SynthesisPanelViewModel_ModelInstalled_NonMatchingRole_DoesNotTriggerRefresh

**Scenario**: A recognition-role model is installed (boundary for `ExternalChangeHandling`).

**Expected**: The offered models are unchanged.

#### SynthesisPanelViewModel_DisposeAsync_WhilePlaying_StopsPlayAndReleasesSessionAndEngine

**Scenario**: The panel is closed while a Play is in flight (`ShutdownReleasesPlayback`).

**Expected**: Speech stops, and the playback device and voice are freed once.

#### SynthesisPanelViewModel_DisposeAsync_AfterPlay_ReleasesSessionAndEngineOnce

**Scenario**: The panel is closed twice after a completed Play (`ShutdownReleasesPlayback`).

**Expected**: Resources are freed exactly once; the second close does nothing.

#### SynthesisPanelViewModel_DisposeAsync_NoActiveSession_IsSafeAndIdempotent

**Scenario**: The panel is closed twice with nothing played (boundary).

**Expected**: No exception.

#### SynthesisPanelViewModel_DisposeAsync_UnsubscribesFromModelInstalled_NoRefreshAfterDispose

**Scenario**: A matching model install occurs after close (`ShutdownStopsCatalogReaction`).

**Expected**: The offered models do not change and nothing throws.

#### SynthesisPanelViewModel_DisposeAsync_UnsubscribesFromReferenceVoicesChanged

**Scenario**: A reference voice change occurs after close (`ShutdownStopsCatalogReaction`).

**Expected**: The offered models do not change.

#### SynthesisPanelViewModel_Play_SuccessfulSession_TransitionsThroughLifecycleToIdle

**Scenario**: A full Play with a working engine and session.

**Expected**: `State` passes through `Synthesizing` then `Playing` before settling on `Idle`, no
status message is left, and the exact entered text reaches the session.

#### SynthesisPanelViewModel_Stop_DuringPlayback_CancelsSessionAndReportsStopped

**Scenario**: Stop is invoked while Play is in flight.

**Expected**: The session is stopped, the panel settles on `Idle` and reports the stopped message.

#### SynthesisPanelViewModel_Play_NoPlaybackDevice_ReportsErrorState

**Scenario**: No playback device is available.

**Expected**: The panel reports an error state rather than throwing.

#### SynthesisPanelViewModel_ModelInstalled_MatchingRole_TriggersRefresh

**Scenario**: The catalog raises `ModelInstalled` for a synthesis model.

**Expected**: The panel refreshes and offers the new model.

#### SynthesisPanelViewModel_ReferenceVoicesChanged_RefreshesAndKeepsChosenVoice

**Scenario**: The catalog raises `ReferenceVoicesChanged` with a rebuilt model.

**Expected**: The panel refreshes and keeps the voice the user had chosen.

#### SynthesisPanelViewModel_PreRefreshHook_WhilePlaying_StopsAndAwaitsExecutionTaskBeforeDeviceRefreshSucceeds

**Scenario**: A Play is in flight and the shared device list is refreshed.

**Expected**: Playback is stopped and awaited before the refresh proceeds, which then succeeds.

#### Supporting subsystem scenarios

The following scenarios, detailed in the SynthesisPanelViewModel verification document, are
also cited as subsystem evidence for the requirement named in each line:

- `SynthesisPanelViewModel_Refresh_MixedCatalog_OffersOnlyInstalledSynthesisModels` and
  `SynthesisPanelViewModel_Refresh_SelectedModelStillInstalled_PreservesSelection` - the model
  choice (normal and boundary): only installed synthesis models are offered and a still-installed
  selection is kept (`PlaybackThroughSeams`, model-choice grouping).
- `SynthesisPanelViewModel_ExampleTagHints_ContainsExpectedTags` - example tags are shown.
- `SynthesisPanelViewModel_Constructor_NoInstalledSynthesisModel_ReportsHonestEmptyState` - with
  no synthesis model installed the panel offers nothing, disables Play and shows a message that
  explains how to install one (error case; consistent with the other panels' empty states).
- `SynthesisPanelViewModel_Play_WhilePlaying_IsRefused`,
  `SynthesisPanelViewModel_SessionStateChanged_ForeignSender_IsIgnored` and
  `SynthesisPanelViewModel_Refresh_WhilePlaying_DoesNotDisturbPlayAndSettlesIdle` - a second Play
  is refused, a late report from replaced playback is ignored and a refresh does not interrupt
  speech (`LifecycleThroughSeams`, `ReferenceVoiceChangeHandling`).
- `SynthesisPanelViewModel_Play_SetupCanceled_ReportsStoppedAndReturnsToIdle` - stopping during
  preparation reports stopped and returns to idle (`StopPlayback`).
- `SynthesisPanelViewModel_CanChangeModel_ErrorState_IsTrue` - the model may be changed again
  after a failed Play (`ModelChangeGuard`).
- `SynthesisPanelViewModel_Play_ModelDeclaresChoiceParameter_ForwardsValueBagToSessionFactory` -
  the chosen voice values reach the synthesizer (`PlaybackThroughSeams`).
- `SynthesisPanelViewModel_PreRefreshHook_WhileIdle_IsNoOpAndDeviceRefreshSucceeds` - a device
  refresh with nothing playing is unaffected (`DeviceRefreshCoordination`, boundary).
- `SynthesisPanelViewModel_Play_SynthesizerUnavailable_ReportsErrorStateAndDisposes`,
  `..._UnavailableSession_ReportsErrorStateAndDisposesSession`,
  `..._SpeakThrowsSynthesizerUnavailable_ReportsErrorStateWithMessage`,
  `..._EngineBusy_ReportsErrorStateWithEngineMessage`,
  `..._SessionFaulted_ReportsErrorReleasesSessionAndNextPlayRecovers` - every unavailable, busy
  and faulted synthesizer outcome is an explained error state and the next Play recovers.
