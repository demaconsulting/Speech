### ListModelsCommand Verification

#### Verification Approach

The command is verified with a `FakeCliModelCatalog` and a capturing `Context`, so every catalog
content and filter combination is deterministic. Two out-of-process tests in
`test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs` (outside this subsystem's review set) prove
the table and JSON forms against the built tool.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- `FakeCliModelCatalog` and `FakeSpeechModel`; no network

#### Acceptance Criteria

The command is verified when an unfiltered list shows every model, each filter selects exactly the
matching subset, JSON is a parseable array, an empty result prints an explanatory line, and every
invalid argument form is an `ArgumentException`.

#### Test Scenarios

##### ListModelsCommand_Run_NoFilters_ListsEveryModelAsTable

**Scenario**: A catalog with several models is listed with no options.

**Expected**: An aligned table with the Id, Display Name, Role, State and License columns lists every model.

##### ListModelsCommand_Run_RoleFilterTts_ListsOnlySynthesisModels

**Scenario**: `--role tts` is given.

**Expected**: Only synthesis models are listed.

##### ListModelsCommand_Run_StateFilterDownloaded_ListsOnlyDownloadedModels

**Scenario**: `--state downloaded` is given.

**Expected**: Only downloaded models are listed.

##### ListModelsCommand_Run_StateFilterMissing_ListsOnlyMissingModels

**Scenario**: `--state missing` is given.

**Expected**: Only models that are not downloaded are listed.

##### ListModelsCommand_Run_FormatJson_EmitsJsonArray

**Scenario**: `--format json` is given.

**Expected**: A JSON array is written whose objects carry `Id`, `DisplayName` and `LicenseName`. The test
does not assert `Role` and `State`, so the claim that the JSON carries the same fields as the table is
only partly evidenced and is not a separate requirement.

##### ListModelsCommand_Run_EmptyCatalog_PrintsNoModelsMessage

**Scenario**: The catalog is empty.

**Expected**: An explanatory message is printed instead of a header-only table.

##### ListModelsCommand_Run_InvalidRoleValue_ThrowsArgumentException

##### ListModelsCommand_Run_RoleFlagMissingValue_ThrowsArgumentException

**Scenario**: `--role` has an unrecognized value, or no value.

**Expected**: An `ArgumentException` is thrown.

##### ListModelsCommand_Run_InvalidStateValue_ThrowsArgumentException

**Scenario**: `--state` has an unrecognized value.

**Expected**: An `ArgumentException` is thrown.

##### ListModelsCommand_Run_ConflictingStateValues_ThrowsArgumentException

**Scenario**: `--state downloaded` and `--state missing` are both given (in either order).

**Expected**: An `ArgumentException` is thrown.

##### ListModelsCommand_Run_InvalidFormatValue_ThrowsArgumentException

**Scenario**: `--format` has an unrecognized value.

**Expected**: An `ArgumentException` is thrown.

##### ListModelsCommand_Run_UnsupportedFlag_ThrowsArgumentException

**Scenario**: An unsupported flag is given.

**Expected**: An `ArgumentException` is thrown.

##### SpeechCli_ListModelsCommand_Invoked_ListsKnownModelsAsTable

##### SpeechCli_ListModelsCommandWithJsonFormat_Invoked_EmitsJsonArray

**Scenario**: The built tool is run out-of-process with `list-models` and with `list-models --format json`.

**Expected**: The table lists the known models; the JSON output is an array whose objects have the `Id`
and `DisplayName` keys.

##### ListModelsCommand_Run_NullContext_ThrowsArgumentNullException

##### ListModelsCommand_Run_NullCatalog_ThrowsArgumentNullException

**Scenario**: `Run` is called with a `null` context or catalog.

**Expected**: An `ArgumentNullException` is thrown. These are null guards and carry no requirement.
