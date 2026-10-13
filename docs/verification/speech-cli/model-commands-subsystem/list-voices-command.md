### ListVoicesCommand Verification

#### Verification Approach

The command is verified with a `FakeCliModelCatalog` supplying reference voices (including voices with
mood clips) and a capturing `Context`. Three out-of-process tests in
`test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs` (outside this subsystem's review set) prove
the table and JSON forms against the built tool.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- `FakeCliModelCatalog`; no network

#### Acceptance Criteria

The command is verified when the table lists each voice with its source, usage hint and consent note, an
empty list explains how to add voices, JSON is a parseable array, moods are shown, and invalid arguments
are an `ArgumentException`.

#### Test Scenarios

##### ListVoicesCommand_Run_Table_ListsVoicesAndConsentNote

**Scenario**: Voices are available and the default table format is used.

**Expected**: Each voice is listed with its source, followed by how to choose a voice and the consent note.

##### SpeechCli_ListVoicesCommand_Invoked_PrintsConsentNote

**Scenario**: The built tool is run with `list-voices`.

**Expected**: The consent note is printed.

##### ListVoicesCommand_Run_NoVoices_ExplainsHowToAddVoices

**Scenario**: No voices are available.

**Expected**: A message explains how to obtain voices (the pack install command, the per-user folder or
`--voices-dir`), followed by the consent note.

##### ListVoicesCommand_Run_Json_WritesParseableArray

##### SpeechCli_ListVoicesCommandWithJsonFormat_Invoked_EmitsJsonArray

**Scenario**: `--format json` is given, in-process and against the built tool.

**Expected**: A parseable JSON array is written.

##### ListVoicesCommand_Run_MoodClips_ShowsMoodsColumnAndJson

**Scenario**: A voice has mood clips.

**Expected**: The table shows the mood count in a Moods column and the JSON has a `Moods` array.

##### ListVoicesCommand_Run_BadArguments_ThrowsArgumentException

##### ListVoicesCommand_Run_UnknownFormat_ThrowsArgumentException

**Scenario**: An unsupported argument, a `--format` with no value, or an unknown format is given.

**Expected**: An `ArgumentException` is thrown.

##### ListVoicesCommand_Run_NullArguments_Throw

**Scenario**: The command is called with a `null` context or catalog.

**Expected**: An `ArgumentNullException` is thrown. This is a null guard and carries no requirement.
