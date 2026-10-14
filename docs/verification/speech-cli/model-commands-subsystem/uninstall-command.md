### UninstallCommand Verification

#### Verification Approach

The command is verified with a `FakeCliModelCatalog` that tracks `Uninstall` calls and can throw a
`SpeechModelStoreException`, with a capturing `Context` and a controlled `CancellationToken`. One
out-of-process test in `test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs` (outside this
subsystem's review set) proves the never-installed path against the built tool.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- `FakeCliModelCatalog`; no network

#### Acceptance Criteria

The command is verified when an installed model is uninstalled and reported, a never-installed model is
a successful no-op, a store failure becomes `InvalidOperationException`, a cancellation is reported with
a non-zero exit code, and invalid arguments are an `ArgumentException`.

#### Test Scenarios

##### UninstallCommand_Run_InstalledModel_UninstallsAndReportsSuccess

**Scenario**: A downloaded model is uninstalled.

**Expected**: The catalog's uninstall is called and success is reported.

##### UninstallCommand_Run_NeverInstalledModel_IsSafeNoOp

##### SpeechCli_UninstallCommand_Invoked_NeverInstalledModel_ExitsCleanly

**Scenario**: A model with nothing installed is uninstalled, in-process and against the built tool.

**Expected**: Success is reported; out-of-process, the exit code is `0`.

##### UninstallCommand_Run_StoreThrows_ThrowsInvalidOperationException

**Scenario**: The store throws a `SpeechModelStoreException`.

**Expected**: It is rethrown as `InvalidOperationException`.

##### UninstallCommand_Run_Canceled_ReportsErrorExitCode

**Scenario**: The token is canceled before the removal; the catalog observes the token.

**Expected**: An error is reported with exit code `1` and no exception escapes.

##### UninstallCommand_Run_MissingModelIdArgument_ThrowsArgumentException

##### UninstallCommand_Run_ExtraArgument_ThrowsArgumentException

**Scenario**: No id, or more than one argument, is given.

**Expected**: An `ArgumentException` with no parameter name is thrown.

##### UninstallCommand_Run_NullContext_ThrowsArgumentNullException

##### UninstallCommand_Run_NullCatalog_ThrowsArgumentNullException

**Scenario**: The command is called with a `null` context or catalog.

**Expected**: An `ArgumentNullException` is thrown. These are null guards and carry no requirement.
