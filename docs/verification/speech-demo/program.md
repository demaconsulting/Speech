## Program Verification

### Verification Approach

`Program.BuildAvaloniaApp` is verified by a test that invokes it and inspects the returned builder.
`Program.Main` starts a live desktop lifetime and cannot run inside the test host, so its fail-fast
and `--help` paths are not exercised by automated tests; this is an accepted gap. The option
parsing and mirror validation that `Main` relies on are verified in the AppLaunchOptions unit.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: The test builds a fresh builder and holds no shared state

### Acceptance Criteria

The unit passes when `BuildAvaloniaApp` returns a configured Avalonia application builder.

### Test Scenarios

#### Avalonia_BuildAvaloniaApp_Invoked_ReturnsConfiguredApplicationBuilder

**Scenario**: `BuildAvaloniaApp` is invoked.

**Expected**: A configured application builder is returned.
