## SpeechDemo SynthesisPanelSubsystem Verification

### Verification Approach

The SynthesisPanelSubsystem is verified through deterministic unit tests in two layers.
`SynthesisPanelViewModel` is tested against NSubstitute/fake doubles of `IModelCatalogService`,
`IAudioDeviceService`, `ISynthesizerSessionFactory`, `ISpeechSynthesizerEngine`, and
`ISynthesisSession`, which lets every async Play/Stop lifecycle transition, state derivation from
`ISynthesisSession.StateChanged`, the two-tier engine/session lazy-reload cache (the central
bugfix this redesign exists for), and every unavailable-state path (no model, no device,
unavailable engine) be produced on demand with no downloaded model, no native runtime, and no
real speakers. `SynthesizerSessionFactory` is tested directly for argument validation and the
honest "wrong role" outcome.

The "correct role composes a working engine" path inside `SynthesizerSessionFactory` delegates to
the library's own `SpeechSynthesizerFactory`, which requires an `ISynthesisModel` - an interface
only the library's own assemblies can implement (see the design document's remarks). That
composition path is therefore outside this test project's reach and remains covered by the
library's own synthesis-subsystem tests; the system-level integration test additionally proves
the panel composes over the real seam without a downloaded model.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: `SynthesizerSessionFactoryTests` roots the library's model store in a fresh
  directory under the test output folder
- **Test doubles**: NSubstitute fakes of `IModelCatalogService`, `IAudioDeviceService`, and
  `ISynthesizerSessionFactory`; hand-written `FakeSpeechSynthesizerEngine` and
  `FakeSynthesisSession` (from `Fakes/`) standing in for `ISpeechSynthesizerEngine` and
  `ISynthesisSession`; `FakeSpeechModel`

### Test Scenarios

#### SynthesisPanelViewModel_Constructor_NullDependency_ThrowsArgumentNullException

**Scenario**: The panel is constructed with each dependency missing in turn.

**Expected**: `ArgumentNullException` for every missing dependency.

**Requirement coverage**: `SpeechDemo-Synthesis-SessionSeam`.

#### SynthesisPanelViewModel_Constructor_NoInstalledSynthesisModel_ReportsHonestEmptyState

**Scenario**: The catalog reports no installed synthesis models.

**Expected**: `HasModels` is false and `AvailableModels`/`SelectedModel` reflect nothing to
choose from.

**Requirement coverage**: `SpeechDemo-Synthesis-HonestEmptyCatalogState`.

#### SynthesisPanelViewModel_Refresh_MixedCatalog_OffersOnlyInstalledSynthesisModels

**Scenario**: The catalog reports an installed synthesis model, a not-yet-downloaded synthesis
model, and an installed recognition model.

**Expected**: Only the installed synthesis model is offered and preselected.

**Requirement coverage**: `SpeechDemo-Synthesis-ModelSelection`.

#### SynthesisPanelViewModel_SelectedModel_Changed_UpdatesEmbeddedSettings

**Scenario**: `SelectedModel` is assigned.

**Expected**: The embedded `Settings.Model` is updated to the same model.

**Requirement coverage**: `SpeechDemo-Synthesis-EmbeddedSettings`.

#### SynthesisPanelViewModel_Play_NoModelSelected_ReportsErrorState

**Scenario**: Play is invoked with no model selected.

**Expected**: `NoModelSelectedMessage` and `Error` state, not an exception.

**Requirement coverage**: `SpeechDemo-Synthesis-HonestUnavailableStates`.

#### SynthesisPanelViewModel_Play_NoPlaybackDevice_ReportsErrorState

**Scenario**: The device seam reports no available playback device.

**Expected**: `NoPlaybackDeviceMessage` and `Error` state.

**Requirement coverage**: `SpeechDemo-Synthesis-HonestUnavailableStates`.

#### SynthesisPanelViewModel_Play_SynthesizerUnavailable_ReportsErrorStateAndDisposes

**Scenario**: The session seam loads an engine that honestly reports itself unavailable.

**Expected**: `SynthesizerUnavailableMessage` and `Error` state, and the unavailable engine is
disposed.

**Requirement coverage**: `SpeechDemo-Synthesis-HonestUnavailableStates`.

#### SynthesisPanelViewModel_Play_ModelDeclaresChoiceParameter_ForwardsValueBagToSessionFactory

**Scenario**: The selected model declares a `voice` `ChoiceParameter` with a non-default current
selection, and Play is invoked.

**Expected**: `ISynthesizerSessionFactory.LoadAsync` is called with a `parameterValues` bag whose
content exactly matches `Settings.BuildValueBag()` (one entry, `"voice"` -> the selected value),
proving the embedded settings panel's selection genuinely reaches the session factory rather
than being built and discarded.

**Requirement coverage**: `SpeechDemo-Synthesis-VoiceSelectionForwarding`.

#### SynthesisPanelViewModel_Play_SuccessfulSession_TransitionsThroughLifecycleToIdle

**Scenario**: A full Play with a working engine and session.

**Expected**: `State` transitions through `Synthesizing` then `Playing` before settling on
`Idle`, driven by `ISynthesisSession.StateChanged`, and the exact text is forwarded to
`SpeakAsync`.

**Requirement coverage**: `SpeechDemo-Synthesis-PlayLifecycle`.

#### SynthesisPanelViewModel_Play_SuccessfulSession_CanChangeModelTogglesAcrossLifecycle

**Scenario**: A successful `PlayAsync` transitions `State` through `Synthesizing`, `Playing`, and
back to `Idle`.

**Expected**: `CanChangeModel` is `true` while `Idle`, `false` while `Synthesizing`/`Playing`, and
`true` again once settled back at `Idle`.

**Requirement coverage**: `SpeechDemo-Synthesis-ModelSwitchGuard`.

#### SynthesisPanelViewModel_CanChangeModel_ErrorState_IsTrue

**Scenario**: `PlayAsync` fails (no playback device available) and the panel enters `Error`.

**Expected**: `CanChangeModel` is `true`, matching `CanPlay`'s own `Idle`-or-`Error` condition, so
a user can pick a different model after a failed attempt.

**Requirement coverage**: `SpeechDemo-Synthesis-ModelSwitchGuard`.

#### SynthesisPanelViewModel_Stop_DuringPlayback_CancelsSessionAndReportsStopped

**Scenario**: Stop is invoked while Play is in flight (an indefinitely long `SpeakAsync` awaiting
cancellation).

**Expected**: The session's `StopAsync()` is invoked, cancellation unwinds the task cleanly,
`State` settles on `Idle`, and `StatusMessage` reports `StoppedMessage`.

**Requirement coverage**: `SpeechDemo-Synthesis-StopControl`.

#### SynthesisPanelViewModel_PreRefreshHook_WhilePlaying_StopsAndAwaitsExecutionTaskBeforeDeviceRefreshSucceeds

**Scenario**: A `PlayAsync` execution is in flight (an indefinitely long `SpeakAsync` awaiting
cancellation), sharing the panel's own `DeviceSelectionViewModel`. The shared
`DeviceSelectionViewModel.Refresh()` is then invoked, as the "Refresh devices" button would.

**Expected**: The panel's registered pre-refresh hook calls `StopAsync()` on the session and
awaits the in-flight `PlayCommand.ExecutionTask` to its actual completion (not merely its
cancellation request) before the device refresh proceeds, so the refresh completes without
throwing, the session is released, and the panel settles on `Idle`.

**Requirement coverage**: `SpeechDemo-Synthesis-StopsBeforeDeviceRefresh`.

#### SynthesisPanelViewModel_PreRefreshHook_WhileIdle_IsNoOpAndDeviceRefreshSucceeds

**Scenario**: The registered pre-refresh hook runs while no Play session was ever started (no
engine, no session created).

**Expected**: The refresh completes without throwing, no session is created, and the panel
remains `Idle`.

**Requirement coverage**: `SpeechDemo-Synthesis-StopsBeforeDeviceRefresh`.

#### SynthesisPanelViewModel_Play_CalledTwiceWithUnchangedModelAndParameters_ReusesSameSessionWithoutReload

**Scenario**: Play is invoked twice in a row with the selected model and settings parameter
values unchanged between calls - the central bugfix this redesign exists for.

**Expected**: The engine is loaded exactly once (`LoadAsync` invoked once), exactly one session
is created, and both Play calls speak through that same cached session rather than reloading the
model and recreating the session on every click.

**Requirement coverage**: `SpeechDemo-Synthesis-EngineSessionReuse`.

#### SynthesisPanelViewModel_Play_ParameterValueChanged_ReloadsEngineAndRecreatesSession

**Scenario**: A declared settings parameter's value is changed between two Play calls.

**Expected**: The stale cached engine is disposed and a fresh one is loaded for the second Play
(`LoadAsync` invoked twice), and - as a direct consequence of the engine reload - a fresh session
is created from the new engine rather than reusing the previous one.

**Requirement coverage**: `SpeechDemo-Synthesis-EngineSessionReuse`.

#### SynthesisPanelViewModel_Play_PlaybackDeviceChanged_RecreatesSessionButNotEngine

**Scenario**: The shared `DeviceSelectionViewModel`'s selected playback device is changed between
two Play calls, with the same model and parameter values throughout.

**Expected**: The engine is loaded only once (`LoadAsync` invoked once), but a second, distinct
session is created bound to the newly selected device, and the stale first session is disposed.

**Requirement coverage**: `SpeechDemo-Synthesis-EngineSessionReuse`.

#### SynthesisPanelViewModel_ExampleTagHints_ContainsExpectedTags

**Scenario**: `ExampleTagHints` is read.

**Expected**: It contains bracketed tags drawn from the library's `AudioTagCatalog`, including
`[whispers]`.

**Requirement coverage**: `SpeechDemo-Synthesis-AudioTagHints`.

#### SynthesizerSessionFactory_Constructor_NullStore_ThrowsArgumentNullException

**Scenario**: The seam is constructed with no model store.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Synthesis-SessionSeam`.

#### SynthesizerSessionFactory_LoadAsync_NullModel_ThrowsArgumentNullException

**Scenario**: `LoadAsync` is called with a missing model.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Synthesis-SessionSeam`.

#### SynthesizerSessionFactory_LoadAsync_ModelNotSynthesisRole_ReturnsUnavailableEngine

**Scenario**: `LoadAsync` is called with a model that does not implement the library's synthesis
role.

**Expected**: The library's own `UnavailableSpeechSynthesizerEngine.Instance`, exactly like a
model that is not installed, rather than an exception.

**Requirement coverage**: `SpeechDemo-Synthesis-SessionSeam`.

#### SynthesizerSessionFactory_LoadAsync_ModelNotSynthesisRoleWithParameterValues_ReturnsUnavailableEngine

**Scenario**: `LoadAsync` is called with a non-null `parameterValues` bag alongside a model that
does not implement the library's synthesis role.

**Expected**: The library's own `UnavailableSpeechSynthesizerEngine.Instance`, proving the
`parameterValues` argument does not disturb the existing wrong-role fallback.

**Requirement coverage**: `SpeechDemo-Synthesis-VoiceSelectionForwarding`.

#### SynthesisPanelViewModel_ModelInstalled_MatchingRole_TriggersRefresh

**Scenario**: `IModelCatalogService.ModelInstalled` is raised for a synthesis-role model after
construction.

**Expected**: The panel refreshes itself automatically and the newly installed model appears in
`AvailableModels`, without a manual Refresh click.

**Requirement coverage**: `SpeechDemo-Synthesis-AutoRefreshOnInstall`.

#### SynthesisPanelViewModel_ModelInstalled_NonMatchingRole_DoesNotTriggerRefresh

**Scenario**: `IModelCatalogService.ModelInstalled` is raised for a recognition-role model.

**Expected**: The panel does not refresh; it still reports no models.

**Requirement coverage**: `SpeechDemo-Synthesis-AutoRefreshOnInstall`.

#### SynthesisPanelViewModel_DisposeAsync_UnsubscribesFromModelInstalled_NoRefreshAfterDispose

**Scenario**: The panel is disposed (`DisposeAsync`), then `IModelCatalogService.ModelInstalled`
is raised for a matching-role model.

**Expected**: No exception, and the panel does not pick up the later install since it had already
unsubscribed.

**Requirement coverage**: `SpeechDemo-Synthesis-ResourceLifetime`.

#### SynthesisPanelViewModel_DisposeAsync_NoActiveSession_IsSafeAndIdempotent

**Scenario**: The panel is disposed twice with no active session.

**Expected**: No exception either time - this is a new capability on this class (it did not
previously implement any disposable contract), so unlike `RecognitionPanelViewModel` it has no
prior coverage to rely on.

**Requirement coverage**: `SpeechDemo-Synthesis-ResourceLifetime`.

### Requirements Coverage

- **`SpeechDemo-Synthesis-ModelSelection`**:
  `SynthesisPanelViewModel_Refresh_MixedCatalog_OffersOnlyInstalledSynthesisModels`
- **`SpeechDemo-Synthesis-EmbeddedSettings`**:
  `SynthesisPanelViewModel_SelectedModel_Changed_UpdatesEmbeddedSettings`
- **`SpeechDemo-Synthesis-AudioTagHints`**:
  `SynthesisPanelViewModel_ExampleTagHints_ContainsExpectedTags`
- **`SpeechDemo-Synthesis-PlayLifecycle`**:
  `SynthesisPanelViewModel_Play_SuccessfulSession_TransitionsThroughLifecycleToIdle`
- **`SpeechDemo-Synthesis-StopControl`**:
  `SynthesisPanelViewModel_Stop_DuringPlayback_CancelsSessionAndReportsStopped`
- **`SpeechDemo-Synthesis-EngineSessionReuse`**:
  `SynthesisPanelViewModel_Play_CalledTwiceWithUnchangedModelAndParameters_ReusesSameSessionWithoutReload`,
  `SynthesisPanelViewModel_Play_ParameterValueChanged_ReloadsEngineAndRecreatesSession`,
  `SynthesisPanelViewModel_Play_PlaybackDeviceChanged_RecreatesSessionButNotEngine`
- **`SpeechDemo-Synthesis-HonestUnavailableStates`**:
  `SynthesisPanelViewModel_Play_NoModelSelected_ReportsErrorState`,
  `SynthesisPanelViewModel_Play_NoPlaybackDevice_ReportsErrorState`,
  `SynthesisPanelViewModel_Play_SynthesizerUnavailable_ReportsErrorStateAndDisposes`
- **`SpeechDemo-Synthesis-HonestEmptyCatalogState`**:
  `SynthesisPanelViewModel_Constructor_NoInstalledSynthesisModel_ReportsHonestEmptyState`
- **`SpeechDemo-Synthesis-SessionSeam`**:
  `SynthesisPanelViewModel_Constructor_NullDependency_ThrowsArgumentNullException`,
  `SynthesizerSessionFactory_Constructor_NullStore_ThrowsArgumentNullException`,
  `SynthesizerSessionFactory_LoadAsync_NullModel_ThrowsArgumentNullException`,
  `SynthesizerSessionFactory_LoadAsync_ModelNotSynthesisRole_ReturnsUnavailableEngine`
- **`SpeechDemo-Synthesis-VoiceSelectionForwarding`**:
  `SynthesisPanelViewModel_Play_ModelDeclaresChoiceParameter_ForwardsValueBagToSessionFactory`,
  `SynthesizerSessionFactory_LoadAsync_ModelNotSynthesisRoleWithParameterValues_ReturnsUnavailableEngine`
- **`SpeechDemo-Synthesis-AutoRefreshOnInstall`**:
  `SynthesisPanelViewModel_ModelInstalled_MatchingRole_TriggersRefresh`,
  `SynthesisPanelViewModel_ModelInstalled_NonMatchingRole_DoesNotTriggerRefresh`
- **`SpeechDemo-Synthesis-ResourceLifetime`**:
  `SynthesisPanelViewModel_DisposeAsync_UnsubscribesFromModelInstalled_NoRefreshAfterDispose`,
  `SynthesisPanelViewModel_DisposeAsync_NoActiveSession_IsSafeAndIdempotent`
- **`SpeechDemo-Synthesis-ModelSwitchGuard`**:
  `SynthesisPanelViewModel_Play_SuccessfulSession_CanChangeModelTogglesAcrossLifecycle`,
  `SynthesisPanelViewModel_CanChangeModel_ErrorState_IsTrue`
- **`SpeechDemo-Synthesis-StopsBeforeDeviceRefresh`**:
  `SynthesisPanelViewModel_PreRefreshHook_WhilePlaying_StopsAndAwaitsExecutionTaskBeforeDeviceRefreshSucceeds`,
  `SynthesisPanelViewModel_PreRefreshHook_WhileIdle_IsNoOpAndDeviceRefreshSucceeds`

### Acceptance Criteria

A SynthesisPanelSubsystem test run passes when: only installed synthesis models are offered and
the selection survives a refresh; the embedded settings panel always reflects the selected
model; the example tag hints are drawn from the library's own vocabulary; a successful Play
transitions through every documented lifecycle state (driven by `ISynthesisSession.StateChanged`)
and releases its session; Stop interrupts an in-flight Play deterministically; calling Play
repeatedly with the same model/parameter/device selection reuses the same cached engine and
session rather than reloading the model and recreating the session on every click, while a
changed parameter value reloads the engine (and, as a consequence, recreates the session) and a
changed playback device alone recreates only the session; every unavailable state - no model, no
device, an unavailable engine - is reported honestly rather than crashing; the session seam
validates its arguments and reports a role mismatch the same honest way the library reports an
uninstalled model, regardless of whether a `parameterValues` bag is supplied; the settings
panel's current value bag genuinely reaches the session factory when Play is invoked; a
matching-role `ModelInstalled` event triggers an automatic refresh while a non-matching-role
event does not; `DisposeAsync()` - now implemented on this class for the first time - is safe and
idempotent, releases the cached engine/session, and unsubscribes from `ModelInstalled` so a later
event is never applied; and the panel's registered pre-refresh hook stops an in-flight Play and
genuinely awaits its completion before a shared device refresh is attempted (and is a safe no-op
while idle), so the refresh succeeds deterministically.
