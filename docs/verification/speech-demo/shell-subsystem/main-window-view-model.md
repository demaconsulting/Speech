### MainWindowViewModel Verification

#### Verification Approach

`MainWindowViewModel` is verified through deterministic unit tests over a shell composed with
panel view models built over NSubstitute fakes of the demo's service seams. Because the shell
touches neither the library nor Avalonia, every behavior is verifiable with no audio hardware, no
installed model, and no running UI toolkit.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test constructs its own view models; no shared state exists between tests
- **Test doubles**: NSubstitute fakes of `IAudioDeviceService`, `IModelCatalogService`,
  `ISynthesizerSessionFactory`, and `IRecognizerSessionFactory`

#### Acceptance Criteria

The unit passes when: it rejects any missing panel (device selection, model catalog,
text-to-speech, or speech-to-text); it offers exactly one titled entry per injected panel, in
order, and hosts those exact four instances; it opens on the first entry; it announces a selection
change and stays silent for an unchanged selection; and its window title names the four panels.

#### Test Scenarios

##### MainWindowViewModel_Constructor_NullDeviceSelection_ThrowsArgumentNullException

**Scenario**: The shell is constructed with no device panel.

**Expected**: `ArgumentNullException`, so a half-composed window never reaches a user.

##### MainWindowViewModel_Constructor_NullModelCatalog_ThrowsArgumentNullException

**Scenario**: The shell is constructed with no catalog panel.

**Expected**: `ArgumentNullException`.
##### MainWindowViewModel_Constructor_NullSynthesis_ThrowsArgumentNullException

**Scenario**: The shell is constructed with no text-to-speech panel.

**Expected**: `ArgumentNullException`.
##### MainWindowViewModel_Constructor_NullRecognition_ThrowsArgumentNullException

**Scenario**: The shell is constructed with no speech-to-text panel.

**Expected**: `ArgumentNullException`.
##### MainWindowViewModel_Constructor_ValidPanels_OffersAllFourPanels

**Scenario**: The shell is constructed with all four panels.

**Expected**: Exactly four navigable entries, in order, titled "Audio Devices", "Model Catalog",
"Text to Speech", and "Speech to Text".

##### MainWindowViewModel_Constructor_ValidPanels_EveryEntryHasNonBlankTitle

**Scenario**: The shell is constructed with all four panels.

**Expected**: No entry has a null, empty or whitespace title.
##### MainWindowViewModel_Constructor_ValidPanels_HostsTheInjectedPanelInstances

**Scenario**: The shell is constructed with all four panels.

**Expected**: Each entry's content is the exact injected instance, proving the shell hosts panels
rather than constructing its own.
##### MainWindowViewModel_Constructor_ValidPanels_SelectsFirstPanel

**Scenario**: The shell is constructed.

**Expected**: The first entry is selected, so the window never opens with a blank content area.
##### MainWindowViewModel_SelectedPanel_Changed_NotifiesSelectedPanel

**Scenario**: The selected panel is changed.

**Expected**: Change notification is raised, which is what lets the bound view swap panels with
no code-behind.

##### MainWindowViewModel_SelectedPanel_SameValue_DoesNotNotify

**Scenario**: The already-selected panel is assigned again.

**Expected**: No change notification is raised.
##### MainWindowViewModel_Title_Read_NamesTheImplementedCapabilities

**Scenario**: The window title is read.

**Expected**: It names the demo and the four panels the shell presents.