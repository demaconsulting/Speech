### CleanCommand Verification

#### Verification Approach

The command is verified with a `FakeCliModelCatalog` that tracks `CleanUpLeftovers` calls, with a
capturing `Context` and a controlled `CancellationToken`. One out-of-process test in
`test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs` (outside this subsystem's review set) proves
the nothing-to-clean-up path against the built tool.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- `FakeCliModelCatalog`; no network

#### Acceptance Criteria

The command is verified when cleanup is reported successful whether or not anything was cleaned, a
cancellation is reported with a non-zero exit code, and invalid arguments are an `ArgumentException`.

#### Test Scenarios

##### CleanCommand_Run_KnownModel_CleansUpAndReportsSuccess

**Scenario**: A known model is cleaned.

**Expected**: The catalog's cleanup is called and success is reported.

##### SpeechCli_CleanCommand_Invoked_NothingToCleanUp_ExitsCleanly

**Scenario**: The built tool is run for a model with nothing to clean up.

**Expected**: The exit code is `0`.

##### CleanCommand_Run_Canceled_ReportsErrorExitCode

**Scenario**: The token is canceled.

**Expected**: An error is reported with exit code `1` and no exception escapes.

##### CleanCommand_Run_MissingModelIdArgument_ThrowsArgumentException

##### CleanCommand_Run_ExtraArgument_ThrowsArgumentException

**Scenario**: No id, or more than one argument, is given.

**Expected**: An `ArgumentException` with no parameter name is thrown.

##### CleanCommand_Run_NullContext_ThrowsArgumentNullException

##### CleanCommand_Run_NullCatalog_ThrowsArgumentNullException

**Scenario**: The command is called with a `null` context or catalog.

**Expected**: An `ArgumentNullException` is thrown. These are null guards and carry no requirement.
