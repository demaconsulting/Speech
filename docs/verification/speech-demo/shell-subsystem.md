## SpeechDemo ShellSubsystem Verification

### Verification Approach

The ShellSubsystem is verified through deterministic unit tests over `MainWindowViewModel` and
`DemoPanelViewModel`, composed with panel view models built over NSubstitute fakes of the demo's
service seams. Because the shell touches neither the library nor Avalonia, every navigation
behavior is verifiable with no audio hardware, no installed model, and no running UI toolkit.

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

A ShellSubsystem test run passes when: the shell rejects any missing panel (device selection,
model catalog, text-to-speech, or speech-to-text); it offers exactly one titled entry per
injected panel and hosts those exact four instances; it opens on the first entry and announces
every selection change; and it composes successfully over an environment that reports nothing at
all.

### Test Scenarios

#### MainWindowViewModel_Constructor_NullDeviceSelection_ThrowsArgumentNullException

**Scenario**: The shell is constructed with no device panel.

**Expected**: `ArgumentNullException`, so a half-composed window never reaches a user.

#### MainWindowViewModel_Constructor_NullModelCatalog_ThrowsArgumentNullException

**Scenario**: The shell is constructed with no catalog panel.

**Expected**: `ArgumentNullException`.

#### MainWindowViewModel_Constructor_NullSynthesis_ThrowsArgumentNullException

**Scenario**: The shell is constructed with no text-to-speech panel.

**Expected**: `ArgumentNullException`.

#### MainWindowViewModel_Constructor_NullRecognition_ThrowsArgumentNullException

**Scenario**: The shell is constructed with no speech-to-text panel.

**Expected**: `ArgumentNullException`.

#### MainWindowViewModel_Constructor_ValidPanels_OffersAllFourPanels

**Scenario**: The shell is constructed with all four panels (device selection, model catalog,
text-to-speech, speech-to-text).

**Expected**: Exactly four navigable entries, in order, titled "Audio Devices", "Model Catalog",
"Text to Speech", and "Speech to Text".

#### MainWindowViewModel_Constructor_ValidPanels_HostsTheInjectedPanelInstances

**Scenario**: The shell is constructed with all four panels.

**Expected**: Each entry's content is the exact injected instance, proving the shell hosts panels
rather than constructing its own.

#### MainWindowViewModel_Constructor_ValidPanels_SelectsFirstPanel

**Scenario**: The shell is constructed.

**Expected**: The first entry is selected, so the window never opens with a blank content area.

#### MainWindowViewModel_SelectedPanel_Changed_NotifiesSelectedPanel

**Scenario**: The selected panel is changed.

**Expected**: Change notification is raised, which is what lets the bound view swap panels with
no code-behind.

#### MainWindowViewModel_Title_Read_NamesTheImplementedCapabilities

**Scenario**: The window title is read.

**Expected**: It names the four panels the shell presents.

#### MainWindowViewModel_Constructor_EmptyEnvironment_PanelsReportHonestEmptyState

**Scenario**: The shell is composed over seams that report no devices and no models.

**Expected**: Composition succeeds and each panel reports its own honest empty state.

#### DemoPanelViewModel_Constructor_ValidArguments_CarriesTitleAndContent

**Scenario**: A navigation entry is built from a title and a panel view model.

**Expected**: Both are carried unchanged.

#### DemoPanelViewModel_Constructor_BlankTitle_ThrowsArgumentException

**Scenario**: A navigation entry is built with an empty or whitespace title.

**Expected**: `ArgumentException`, because an untitled navigation entry is unusable.

#### DemoPanelViewModel_Constructor_NullTitle_ThrowsArgumentNullException

**Scenario**: A navigation entry is built with no title.

**Expected**: `ArgumentNullException`.

#### DemoPanelViewModel_Constructor_NullContent_ThrowsArgumentNullException

**Scenario**: A navigation entry is built with no content.

**Expected**: `ArgumentNullException`.
