## App Verification

### Verification Approach

`App` is verified by the system integration tests that compose the shell over the real library
entry points, and by an Avalonia inspection test that confirms the class derives from the Avalonia
application type. The deferred shutdown sequence requires a live desktop lifetime and is not
exercised by an automated test; this is an accepted gap, covered by manual use of the demo.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Integration tests root the model store in a fresh directory under the test output
  folder

### Acceptance Criteria

The unit passes when the shell composes over the real library entry points without a downloaded
model, the real catalog reports its known models, and `App` derives from the Avalonia application
type.

### Test Scenarios

#### SpeechDemo_SystemIntegration_RealLibraryEntryPoints_ComposesWorkingShell

**Scenario**: The shell is composed over the real library entry points.

**Expected**: A working shell with all panels is produced without a downloaded model.

#### SpeechDemo_SystemIntegration_RealModelCatalog_ReportsKnownModels

**Scenario**: The real catalog is enumerated through the demo seam.

**Expected**: The library's known models are reported.

#### Avalonia_DemoApplication_Inspected_DerivesFromAvaloniaApplication

**Scenario**: The `App` type is inspected.

**Expected**: It derives from the Avalonia application type.
