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

A ShellSubsystem test run passes when the shell composes successfully over an environment that
reports nothing at all - no audio devices and no known models - with every panel reporting its own
honest empty state.

### Test Scenarios

#### MainWindowViewModel_Constructor_EmptyEnvironment_PanelsReportHonestEmptyState

**Scenario**: The shell is composed over seams that report no devices and no models.

**Expected**: Composition succeeds and each panel reports its own honest empty state.

