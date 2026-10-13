### ModelInfoCommand Verification

#### Verification Approach

The command is verified with a `FakeCliModelCatalog` holding models that carry numeric, choice and
boolean parameters, and a capturing `Context`. Two out-of-process tests in
`test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs` (outside this subsystem's review set) prove the
clean-error paths against the built tool.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- `FakeCliModelCatalog` and `FakeSpeechModel`; no network

#### Acceptance Criteria

The command is verified when a known model's identity and every parameter kind are printed, numbers use
the invariant culture, and an unknown id, a missing id and an extra argument are each an
`ArgumentException`.

#### Test Scenarios

##### ModelInfoCommand_Run_KnownModel_PrintsIdentityAndState

**Scenario**: A known model id is given.

**Expected**: Id, display name, role, state, license and audio-tag support are printed.

##### ModelInfoCommand_Run_NumericParameter_PrintsBounds

**Scenario**: The model has a numeric parameter.

**Expected**: Its min, max, step and default are printed.

##### ModelInfoCommand_Run_ChoiceParameter_PrintsOptions

**Scenario**: The model has a choice parameter.

**Expected**: Its options and default are printed.

##### ModelInfoCommand_Run_BooleanParameter_PrintsDefault

**Scenario**: The model has a boolean parameter.

**Expected**: Its default is printed.

##### ModelInfoCommand_Run_NumericParameter_CommaDecimalCulture_PrintsInvariantFormat

**Scenario**: The current culture uses a comma decimal separator.

**Expected**: Numeric values are still printed with a period.

##### ModelInfoCommand_Run_UnknownModelId_ThrowsArgumentException

##### SpeechCli_ModelInfoCommandWithUnknownId_Invoked_ReturnsCleanError

**Scenario**: An id that matches no model is given, in-process and against the built tool.

**Expected**: An `ArgumentException` with no parameter name naming the id and suggesting `list-models`;
out-of-process, a one-line error and a non-zero exit code with no stack trace.

##### ModelInfoCommand_Run_MissingModelIdArgument_ThrowsArgumentException

##### SpeechCli_ModelInfoCommandWithMissingArgument_Invoked_ReturnsCleanError

**Scenario**: No id is given, in-process and against the built tool.

**Expected**: An `ArgumentException` with no parameter name; out-of-process, a clean error and a non-zero
exit code.

##### ModelInfoCommand_Run_ExtraArgument_ThrowsArgumentException

**Scenario**: A second positional argument is given.

**Expected**: An `ArgumentException` is thrown.

##### ModelInfoCommand_Run_NullContext_ThrowsArgumentNullException

##### ModelInfoCommand_Run_NullCatalog_ThrowsArgumentNullException

**Scenario**: `Run` is called with a `null` context or catalog.

**Expected**: An `ArgumentNullException` is thrown. These are null guards and carry no requirement.
