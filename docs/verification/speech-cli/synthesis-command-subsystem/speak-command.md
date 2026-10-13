### SpeakCommand Verification

#### Verification Approach

`SpeakCommand` is verified through deterministic in-process unit tests. The model catalog is a
`FakeCliModelCatalog` whose delegate overrides supply the model descriptors, the preferred audio
format and the engine. The engine and session are the hand-written `FakeSpeechSynthesizerEngine` and
`FakeSynthesisSession`, which record the text passed to `SpeakAsync` and the number of disposals and
can be told to throw. Real-device dispatch uses a `FakePlaybackDeviceSource` that resolves purely
from an in-memory device list and never touches PortAudio, so every scenario is deterministic on
every machine, headless or not. Flag parsing is verified directly on `ParseArguments`. The
operator-visible error paths are additionally verified by running the built tool as a child process
(`IntegrationTests.cs`) and through the program entry point (`ProgramTests.cs`).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test builds its own fakes and context; output-file tests use a uniquely named
  temporary path that is deleted afterward; integration tests use an isolated, empty temporary model
  directory
- **Test doubles**: `FakeCliModelCatalog`, `FakeSpeechSynthesizerEngine`, `FakeSynthesisSession`,
  `FakePlaybackDeviceSource`, `FakeAudioPlaybackDevice`, `FakeAudioPlaybackDeviceProbe`

#### Acceptance Criteria

The unit passes when `speak` enforces its flag and text-source rules, reports each model-resolution
failure with its own actionable message, applies `--tts-param`, `--no-tags` and the output and
playback-device rules correctly, reports a cancellation cleanly, always disposes the session, the
engine and the device, and rejects a missing dependency.

#### Known Gaps

No test covers `--file` naming a readable, missing or unreadable file, text read from piped stdin,
or empty and whitespace-only text; `speak` with a missing file surfaces the file-system exception as
an unexpected error. The unknown-model and wrong-role tests assert that the message names the
model, but no test asserts the suggested remedy text (`list-models`, `list-models --role tts`).

#### Test Scenarios

##### SpeakCommand_ParseArguments_MissingModel_ThrowsArgumentException

**Scenario**: The arguments contain no `--tts-model`.

**Expected**: `ArgumentException`.

##### SpeakCommand_ParseArguments_TextFlag_ParsesText

**Scenario**: The arguments contain `--tts-model` and `--text`.

**Expected**: The model id and the text are carried in the parsed options.

##### SpeakCommand_ParseArguments_RepeatedParamFlags_AccumulatesInOrder

**Scenario**: `--tts-param` is given several times.

**Expected**: Every token is accumulated in the order given.

##### SpeakCommand_ParseArguments_NoTagsFlag_ParsesTrue

**Scenario**: `--no-tags` is given.

**Expected**: The parsed options record the switch as set.

##### SpeakCommand_ParseArguments_UnsupportedArgument_ThrowsArgumentException

**Scenario**: An argument that `speak` does not support is given.

**Expected**: `ArgumentException`.

##### SpeakCommand_ParseArguments_FlagMissingValue_ThrowsArgumentException

**Scenario**: A flag that needs a value is last on the command line.

**Expected**: `ArgumentException`.

##### SpeakCommand_RunAsync_TextAndFileBothGiven_ThrowsArgumentException

**Scenario**: Both `--text` and `--file` are given.

**Expected**: `ArgumentException`.

##### SpeakCommand_RunAsync_NoTextSourceAndStdinNotRedirected_ThrowsArgumentException

**Scenario**: No text source is given and stdin is not redirected. The test returns without
asserting when the test host does redirect stdin.

**Expected**: `ArgumentException`.

##### SpeakCommand_RunAsync_UnknownModelId_ThrowsArgumentException

**Scenario**: `--tts-model` names an id the catalog does not list.

**Expected**: `ArgumentException` whose message contains the id.

##### SpeakCommand_RunAsync_WrongRoleModel_ThrowsArgumentException

**Scenario**: `--tts-model` names a recognition-role model.

**Expected**: `ArgumentException` whose message contains the model id.

##### SpeakCommand_RunAsync_NotDownloadedModel_ThrowsArgumentExceptionWithDownloadHint

**Scenario**: `--tts-model` names a synthesis model that is not downloaded.

**Expected**: `ArgumentException` whose message contains `download model-1`.

##### SpeakCommand_RunAsync_ValidParam_ForwardsToCreateSynthesizerEngine

**Scenario**: A valid `--tts-param` for a declared numeric parameter is given.

**Expected**: The catalog receives the engine request with the parameter resolved to its typed
value.

##### SpeakCommand_RunAsync_InvalidParam_ThrowsArgumentException

**Scenario**: A `--tts-param` value outside the declared range is given.

**Expected**: `ArgumentException`, thrown before any engine is created.

##### SpeakCommand_RunAsync_NoTags_StripsRecognizedTagExactly

**Scenario**: `--no-tags` is given with text `Hello [laughs] there`.

**Expected**: The text passed to `SpeakAsync` is `Hello  there`, the plain-text spans kept verbatim
(including the whitespace on each side of the removed tag).

##### SpeakCommand_RunAsync_WithoutNoTags_PassesOriginalTextUnchanged

**Scenario**: Text containing a recognized tag is spoken without `--no-tags`.

**Expected**: The original text reaches `SpeakAsync` unchanged.

##### SpeakCommand_RunAsync_Output_ConstructsWavFileDeviceFromPreferredFormat

**Scenario**: `--output-audio` is given; the catalog reports a 22050 Hz mono preferred format and
the device source's probe throws if it is ever enumerated.

**Expected**: A WAV file device is created at the path, sized from the preferred format, and the
probe is never touched.

##### SpeakCommand_RunAsync_UnknownDevice_ThrowsArgumentException

**Scenario**: `--playback-device` names a device the probe does not list and `--output-audio` is
absent.

**Expected**: `ArgumentException` whose message contains the requested name.

##### SpeakCommand_RunAsync_NoPlaybackDeviceAvailable_ThrowsInvalidOperationException

**Scenario**: The device source reports no playback device and `--output-audio` is absent.

**Expected**: `InvalidOperationException`.

##### SpeakCommand_RunAsync_Canceled_ReportsErrorCleanly

**Scenario**: The fake session throws `OperationCanceledException` from `SpeakAsync`.

**Expected**: `RunAsync` returns without throwing, the exit code is 1, and the session and engine
are each disposed once.

##### SpeakCommand_RunAsync_Success_DisposesEngineAndSessionOnce

**Scenario**: A complete `speak` run over a fake model, device, engine and session.

**Expected**: The session and engine are each disposed exactly once and the exit code is 0.

##### SpeakCommand_RunAsync_NullContext_ThrowsArgumentNullException

**Scenario**: The injectable overload is called with a null context.

**Expected**: `ArgumentNullException`.

##### SpeakCommand_RunAsync_NullCatalog_ThrowsArgumentNullException

**Scenario**: The overload is called with a null catalog.

**Expected**: `ArgumentNullException`.

##### SpeakCommand_RunAsync_NullFactory_ThrowsArgumentNullException

**Scenario**: The overload is called with a null device source.

**Expected**: `ArgumentNullException`.

##### SpeakCommand_RunAsync_NullContextWithDeviceSource_ThrowsArgumentNullException

**Scenario**: The three-argument overload (the one that wires `Ctrl+C`) is called with a null
context and a fake device source.

**Expected**: `ArgumentNullException`.

##### SpeechCli_SpeakCommandWithoutModel_Invoked_ReturnsCleanError

**Scenario**: The built tool is run with `speak` and no `--tts-model`.

**Expected**: A non-zero exit code and an error naming `--tts-model`.

##### SpeechCli_SpeakCommandWithUnknownModel_Invoked_ReturnsCleanError

**Scenario**: The built tool is run with an unknown model id against an empty model directory.

**Expected**: A non-zero exit code and an error naming the id.

##### SpeechCli_SpeakCommandWithWrongRoleModel_Invoked_ReturnsCleanError

**Scenario**: The built tool is run with the id of a recognition model.

**Expected**: A non-zero exit code and an error naming the model.

##### SpeechCli_SpeakCommandWithNotDownloadedModel_Invoked_ReturnsCleanErrorWithDownloadHint

**Scenario**: The built tool is run with a known, not-downloaded synthesis model.

**Expected**: A non-zero exit code and an error that mentions downloading.

##### SpeechCli_SpeakCommandWithConflictingTextSources_Invoked_ReturnsCleanError

**Scenario**: The built tool is run with both `--text` and `--file`.

**Expected**: A non-zero exit code and an error naming both options.

##### Program_Run_WithSpeakCommandAndNoModel_ThrowsArgumentException

**Scenario**: The program entry point runs `speak` with no model.

**Expected**: The missing option is reported as an `ArgumentException`.
