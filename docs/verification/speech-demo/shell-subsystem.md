## SpeechDemo ShellSubsystem Verification

### Verification Approach

The ShellSubsystem is verified at subsystem level by composing the shell over the four panel view
models, each built over NSubstitute fakes of the demo's service seams. Unit-level behavior is
verified in the unit verification documents for MainWindowViewModel and DemoPanelViewModel.

Because the shell touches neither the library nor Avalonia, every navigation behavior is
verifiable with no audio hardware, no installed model, and no running UI toolkit.

The composition root in the `App` unit is verified indirectly, by the system-level tests that
construct the same object graph over the real library entry points (see _SpeechDemo App
Verification_).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own view models; no shared state exists between tests
- **Test doubles**: NSubstitute fakes of `IAudioDeviceService`, `IModelCatalogService`,
  `ISynthesizerSessionFactory`, and `IRecognizerSessionFactory`

### Acceptance Criteria

A ShellSubsystem test run passes when: the shell composes successfully over an environment that
reports nothing at all - no audio devices and no known models - with all four panels still
present (each panel's own empty-state wording is verified by the owning subsystem); the navigation
list offers exactly four panels in the order device selection, model catalog, text-to-speech,
speech-to-text; every entry has a non-blank title and hosts the injected panel; the first panel is
selected initially; a selection change is announced; and re-selecting the current panel is not
announced.

The shell has no failure path of its own: it performs no I/O and raises no errors of its own, so
there is no error scenario to run here. Faults (a missing audio backend, a failed refresh, an
unavailable model) are handled and reported by the panel that owns them, and are verified in the
DeviceSelectionSubsystem, ModelCatalogSubsystem and session-owning panel verification documents.
Invalid shell inputs (a blank title, a missing content object) are covered by the
DemoPanelViewModel unit verification.

### Test Scenarios

#### MainWindowViewModel_Constructor_EmptyEnvironment_ComposesAllPanelsWithEmptyStates

**Scenario**: The shell is composed over seams that report no devices and no models (boundary:
nothing available).

**Expected**: Composition succeeds, all four panels are present, and each panel reports an empty
state.

#### MainWindowViewModel_Constructor_ValidPanels_OffersAllFourPanels

**Scenario**: The shell is composed over the four real panel view models.

**Expected**: The navigation list offers all four panels, in the order device selection, model
catalog, text-to-speech, speech-to-text.

#### MainWindowViewModel_Constructor_ValidPanels_SelectsFirstPanel

**Scenario**: The shell is composed and nothing has been navigated yet.

**Expected**: The first panel is the selected panel.

#### MainWindowViewModel_SelectedPanel_Changed_NotifiesSelectedPanel

**Scenario**: The selected panel is changed to a different panel.

**Expected**: `SelectedPanel` changes and a property-changed notification is raised for it.

#### MainWindowViewModel_SelectedPanel_SameValue_DoesNotNotify

**Scenario**: The already-selected panel is assigned again.

**Expected**: No property-changed notification is raised.
