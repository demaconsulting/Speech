## App Verification

### Verification Approach

`App` is verified through its internal seams, so no desktop lifetime is needed. `Compose` is called
with isolated launch options (a fresh model store root under the test output folder) and the
resulting shell is inspected. `RunTeardownAsync` is called with fake steps to prove that each
shutdown step runs and that a failure in one step is contained without skipping the others. The
system integration tests also compose the shell over the real library entry points, and an Avalonia
inspection test confirms the class derives from the Avalonia application type. The test
`Avalonia_DemoApplication_Inspected_DerivesFromAvaloniaApplication` lives in the Avalonia OTS
verification (`Ots/AvaloniaTests.cs`) and is linked here as evidence for the `App` requirement.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Composition tests root the model store in a fresh directory under the test output
  folder and never read `App.LaunchOptions`; teardown tests use in-memory fake steps

### Acceptance Criteria

The unit passes when the shell composes over the real library entry points without a downloaded
model, every panel is wired and the catalog lists every registered model, every teardown step runs
in order even when earlier steps fail, each failure is contained, and `App` derives from the Avalonia
application type.

### Test Scenarios

#### App_Compose_IsolatedLaunchOptions_ComposesShellWithAllPanels

**Scenario**: `Compose` is called with isolated launch options.

**Expected**: A main window view model is returned with the device, synthesis, recognition and
model catalog panels all present.

#### App_Compose_IsolatedLaunchOptions_CatalogListsEveryRegisteredModel

**Scenario**: The composed catalog is enumerated.

**Expected**: Every registered model family appears in the catalog.

#### App_Compose_NullLaunchOptions_ComposesShell

**Scenario**: `Compose` is called with null launch options.

**Expected**: A shell is still composed using the library defaults.

#### App_RunTeardownAsync_AllStepsSucceed_RunsEveryStepInOrder

**Scenario**: Every teardown step succeeds.

**Expected**: The steps run once each, in the supplied order.

#### App_RunTeardownAsync_StepsFail_ContainsEachFailureAndRunsRemainingSteps

**Scenario**: Some teardown steps throw.

**Expected**: No exception escapes, and every remaining step still runs.

#### SpeechDemo_SystemIntegration_RealLibraryEntryPoints_ComposesWorkingShell

**Scenario**: The shell is composed over the real library entry points.

**Expected**: A working shell with all panels is produced without a downloaded model, composed
through `App.Compose` with an isolated models directory.

#### SpeechDemo_SystemIntegration_RealModelCatalog_ReportsKnownModels

**Scenario**: The shell is composed through `App.Compose` and the catalog panel is inspected.

**Expected**: The panel lists exactly the models the shared service enumerates, covering both
recognition and synthesis, and is not empty.

#### App_Compose_MirrorLaunchOptions_PrePopulatesCatalogPanelMirrorSettings

**Scenario**: The shell is composed with launch options that carry a mirror.

**Expected**: The catalog panel's mirror fields show the launch-time values.

#### App_HandleShutdownRequested_FirstRequest_DefersThenResumesShutdownOnce

**Scenario**: The first shutdown request arrives.

**Expected**: Shutdown is deferred (cancelled), cleanup runs, then shutdown is resumed exactly once.

#### App_HandleShutdownRequested_RepeatedRequest_StartsNoSecondCleanup

**Scenario**: A second shutdown request arrives while cleanup may still be pending.

**Expected**: The request is cancelled while cleanup is pending, and no second cleanup starts.

#### App_ShutdownTask_BeforeAnyRequest_IsNull

**Scenario**: No shutdown has been requested.

**Expected**: There is no shutdown task.

#### Avalonia_DemoApplication_Inspected_DerivesFromAvaloniaApplication

**Scenario**: The `App` type is inspected.

**Expected**: It derives from the Avalonia application type.
