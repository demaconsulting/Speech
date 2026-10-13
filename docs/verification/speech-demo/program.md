## Program Verification

### Verification Approach

`Program.Run` is verified through the internal seam with in-memory writers, a fake environment
reader and a recording start action, so the fail-fast, help and start paths are exercised without a
desktop lifetime. `Program.BuildAvaloniaApp` is verified by a test that invokes it, inspects the
returned builder and asserts that it is bound to the demo `App`. `Main` itself is a thin wrapper over
`Run` that starts the desktop lifetime and is not run in the test host. Option parsing and mirror
validation are verified in the AppLaunchOptions unit.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test uses its own writers, fake environment and start action, and holds no
  shared state

### Acceptance Criteria

The unit passes when `BuildAvaloniaApp` returns a configured application builder; `Run` prints usage
and returns 0 for a help flag; reports an error plus usage on the error writer and returns 1 for an
invalid option or mirror URL; never starts the application in those cases; and starts it with the
parsed options for valid arguments.

### Test Scenarios

#### Avalonia_BuildAvaloniaApp_Invoked_ReturnsConfiguredApplicationBuilder

**Scenario**: `BuildAvaloniaApp` is invoked.

**Expected**: A configured application builder bound to the demo `App` is returned.

#### Program_Run_HelpFlag_PrintsUsageAndDoesNotStart

**Scenario**: `Run` receives `--help`.

**Expected**: Usage text is written to the output writer, 0 is returned and the start action is not
called.

#### Program_Run_UnrecognizedOption_ReportsErrorAndDoesNotStart

**Scenario**: `Run` receives an unknown option.

**Expected**: An error message and the usage text go to the error writer, 1 is returned and the
start action is not called.

#### Program_Run_InvalidMirrorUrl_ReportsErrorAndDoesNotStart

**Scenario**: `Run` receives an invalid `--mirror-url`.

**Expected**: An error message goes to the error writer without echoing the value, 1 is returned and
the start action is not called.

#### Program_Run_ValidOptions_StartsWithParsedOptions

**Scenario**: `Run` receives valid options.

**Expected**: 0 is returned and the start action is called once with the parsed options.

#### Program_Run_UnrecognizedSecretArgument_ErrorDoesNotEchoArgument

**Scenario**: `Run` receives a stray argument that looks like a secret.

**Expected**: The error output reports the argument position but not its text, and 1 is returned.

#### Program_Run_HelpWithInvalidMirror_PrintsUsageAndReturnsZero

**Scenario**: `Run` receives `--help` together with an invalid `--mirror-url`.

**Expected**: Usage text is printed, 0 is returned and no mirror error is reported.
