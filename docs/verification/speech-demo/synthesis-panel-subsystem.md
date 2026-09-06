## SpeechDemo SynthesisPanelSubsystem Verification

### Verification Approach

The SynthesisPanelSubsystem is verified through deterministic unit tests in two layers.
`SynthesisPanelViewModel` is tested against NSubstitute fakes of `IModelCatalogService`,
`IAudioDeviceService`, and `ISynthesizerSessionFactory`, which lets every Play/Stop lifecycle
transition and every unavailable-state path (no model, no device, unavailable synthesizer) be
produced on demand with no downloaded model, no native runtime, and no real speakers.
`SynthesizerSessionFactory` is tested directly for argument validation and the honest "wrong
role" outcome.

The "correct role composes a working synthesizer" path inside `SynthesizerSessionFactory`
delegates to the library's own `SpeechSynthesizerFactory`, which requires an `ISynthesisModel` -
an interface only the library's own assemblies can implement (see the design document's
remarks). That composition path is therefore outside this test project's reach and remains
covered by the library's own synthesis-subsystem tests; the system-level integration test
additionally proves the panel composes over the real seam without a downloaded model.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: `SynthesizerSessionFactoryTests` roots the library's model store in a fresh
  directory under the test output folder
- **Test doubles**: NSubstitute fakes of `IModelCatalogService`, `IAudioDeviceService`,
  `ISynthesizerSessionFactory`, `ISpeechSynthesizer`, and `IAudioPlaybackDevice`; `FakeSpeechModel`

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

**Scenario**: The session seam composes a synthesizer that honestly reports itself unavailable.

**Expected**: `SynthesizerUnavailableMessage` and `Error` state, and the unavailable synthesizer
is disposed.

**Requirement coverage**: `SpeechDemo-Synthesis-HonestUnavailableStates`.

#### SynthesisPanelViewModel_Play_SuccessfulSession_TransitionsThroughLifecycleToIdle

**Scenario**: A full Play with a working synthesizer.

**Expected**: `State` transitions through `Synthesizing` then `Playing` before settling on
`Idle`, and the synthesizer is disposed afterward.

**Requirement coverage**: `SpeechDemo-Synthesis-PlayLifecycle`.

#### SynthesisPanelViewModel_Stop_DuringPlayback_CancelsSessionAndReportsStopped

**Scenario**: Stop is invoked while Play is in flight.

**Expected**: The session is canceled, `State` settles on `Idle`, and `StatusMessage` reports
`StoppedMessage`.

**Requirement coverage**: `SpeechDemo-Synthesis-StopControl`.

#### SynthesisPanelViewModel_ExampleTagHints_ContainsExpectedTags

**Scenario**: `ExampleTagHints` is read.

**Expected**: It contains bracketed tags drawn from the library's `AudioTagCatalog`, including
`[whispers]`.

**Requirement coverage**: `SpeechDemo-Synthesis-AudioTagHints`.

#### SynthesizerSessionFactory_Constructor_NullStore_ThrowsArgumentNullException

**Scenario**: The seam is constructed with no model store.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Synthesis-SessionSeam`.

#### SynthesizerSessionFactory_Create_NullModel_ThrowsArgumentNullException

**Scenario**: `Create` is called with a missing model.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Synthesis-SessionSeam`.

#### SynthesizerSessionFactory_Create_NullDevice_ThrowsArgumentNullException

**Scenario**: `Create` is called with a missing playback device.

**Expected**: `ArgumentNullException`.

**Requirement coverage**: `SpeechDemo-Synthesis-SessionSeam`.

#### SynthesizerSessionFactory_Create_ModelNotSynthesisRole_ReturnsUnavailableSynthesizer

**Scenario**: `Create` is called with a model that does not implement the library's synthesis
role.

**Expected**: The library's own `UnavailableSpeechSynthesizer.Instance`, exactly like a model
that is not installed, rather than an exception.

**Requirement coverage**: `SpeechDemo-Synthesis-SessionSeam`.

#### SynthesizerSessionFactory_Create_ModelNotSynthesisRoleWithParameterValues_ReturnsUnavailableSynthesizer

**Scenario**: `Create` is called with a non-null `parameterValues` bag alongside a model that
does not implement the library's synthesis role.

**Expected**: The library's own `UnavailableSpeechSynthesizer.Instance`, proving the new
`parameterValues` argument does not disturb the existing wrong-role fallback.

**Requirement coverage**: `SpeechDemo-Synthesis-VoiceSelectionForwarding`.

#### SynthesisPanelViewModel_Play_ModelDeclaresChoiceParameter_ForwardsValueBagToSessionFactory

**Scenario**: The selected model declares a `voice` `ChoiceParameter` with a non-default current
selection, and Play is invoked.

**Expected**: `ISynthesizerSessionFactory.Create` is called with a `parameterValues` bag whose
content exactly matches `Settings.BuildValueBag()` (one entry, `"voice"` -> the selected value),
proving the embedded settings panel's selection genuinely reaches the session factory rather
than being built and discarded.

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

#### SynthesisPanelViewModel_Dispose_UnsubscribesFromModelInstalled_NoRefreshAfterDispose

**Scenario**: The panel is disposed, then `IModelCatalogService.ModelInstalled` is raised for a
matching-role model.

**Expected**: No exception, and the panel does not pick up the later install since it had already
unsubscribed.

**Requirement coverage**: `SpeechDemo-Synthesis-ResourceLifetime`.

#### SynthesisPanelViewModel_Dispose_NoActiveSynthesizer_IsSafeAndIdempotent

**Scenario**: The panel is disposed twice with no active synthesizer.

**Expected**: No exception either time - this is a new capability on this class (it did not
previously implement `IDisposable`), so unlike `RecognitionPanelViewModel` it has no prior
coverage to rely on.

**Requirement coverage**: `SpeechDemo-Synthesis-ResourceLifetime`.

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
- **`SpeechDemo-Synthesis-HonestUnavailableStates`**:
  `SynthesisPanelViewModel_Play_NoModelSelected_ReportsErrorState`,
  `SynthesisPanelViewModel_Play_NoPlaybackDevice_ReportsErrorState`,
  `SynthesisPanelViewModel_Play_SynthesizerUnavailable_ReportsErrorStateAndDisposes`
- **`SpeechDemo-Synthesis-HonestEmptyCatalogState`**:
  `SynthesisPanelViewModel_Constructor_NoInstalledSynthesisModel_ReportsHonestEmptyState`
- **`SpeechDemo-Synthesis-SessionSeam`**:
  `SynthesisPanelViewModel_Constructor_NullDependency_ThrowsArgumentNullException`,
  `SynthesizerSessionFactory_Constructor_NullStore_ThrowsArgumentNullException`,
  `SynthesizerSessionFactory_Create_NullModel_ThrowsArgumentNullException`,
  `SynthesizerSessionFactory_Create_NullDevice_ThrowsArgumentNullException`,
  `SynthesizerSessionFactory_Create_ModelNotSynthesisRole_ReturnsUnavailableSynthesizer`
- **`SpeechDemo-Synthesis-VoiceSelectionForwarding`**:
  `SynthesisPanelViewModel_Play_ModelDeclaresChoiceParameter_ForwardsValueBagToSessionFactory`,
  `SynthesizerSessionFactory_Create_ModelNotSynthesisRoleWithParameterValues_ReturnsUnavailableSynthesizer`
- **`SpeechDemo-Synthesis-AutoRefreshOnInstall`**:
  `SynthesisPanelViewModel_ModelInstalled_MatchingRole_TriggersRefresh`,
  `SynthesisPanelViewModel_ModelInstalled_NonMatchingRole_DoesNotTriggerRefresh`
- **`SpeechDemo-Synthesis-ResourceLifetime`**:
  `SynthesisPanelViewModel_Dispose_UnsubscribesFromModelInstalled_NoRefreshAfterDispose`,
  `SynthesisPanelViewModel_Dispose_NoActiveSynthesizer_IsSafeAndIdempotent`
- **`SpeechDemo-Synthesis-ModelSwitchGuard`**:
  `SynthesisPanelViewModel_Play_SuccessfulSession_CanChangeModelTogglesAcrossLifecycle`,
  `SynthesisPanelViewModel_CanChangeModel_ErrorState_IsTrue`

### Acceptance Criteria

A SynthesisPanelSubsystem test run passes when: only installed synthesis models are offered and
the selection survives a refresh; the embedded settings panel always reflects the selected
model; the example tag hints are drawn from the library's own vocabulary; a successful Play
transitions through every documented lifecycle state and releases its synthesizer; Stop
interrupts an in-flight Play deterministically; every unavailable state - no model, no device,
an unavailable synthesizer - is reported honestly rather than crashing; the session seam
validates its arguments and reports a role mismatch the same honest way the library reports an
uninstalled model, regardless of whether a `parameterValues` bag is supplied; the settings
panel's current value bag genuinely reaches the session factory when Play is invoked; a
matching-role `ModelInstalled` event triggers an automatic refresh while a non-matching-role
event does not; and `Dispose()` - now implemented on this class for the first time - is safe and
idempotent, and unsubscribes from `ModelInstalled` so a later event is never applied.
