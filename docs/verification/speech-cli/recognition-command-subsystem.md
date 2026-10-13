## SpeechCli RecognitionCommandSubsystem Verification

### Verification Approach

The RecognitionCommandSubsystem is verified through deterministic unit tests over hand-written
test doubles, plus out-of-process integration tests that run the built tool. Each unit has its own
verification document (see _RecognizeCommand Verification_ and _SilenceTimeoutRecognizerSession
Verification_); this document covers only the behavior that spans both units and the tests that
exercise the subsystem as a whole.

The cross-unit flow is verified by driving `RecognizeCommand.RunAsync` in microphone mode through
the `ICliCaptureDeviceSource` seam, with a fake available capture device and a fake recognition
session. The real `SilenceTimeoutRecognizerSession` wraps that session, so the run also proves the
decorator ends the session and the command finishes. The file-input flow is verified through the
unit tests of `RecognizeCommand` and, against the built tool, through an integration test.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every unit test constructs its own fake catalog, engine, session and capture
  device source; integration tests use an isolated, empty `--models-dir` where a model store is
  needed
- **Test doubles**: `FakeCliModelCatalog`, `FakeRecognitionSession`,
  `FakeSpeechRecognizerEngine`, `FakeCaptureDeviceSource` and `FakeAudioCaptureDeviceProbe`; see
  the unit verification documents for how each is used

### Test Scenarios

#### Microphone Run Across Both Units

**Tests**: `RecognizeCommand_Run_MicMode_StreamsResultsFromCaptureDevice`

**Scenario/Expected**: `recognize --mic` runs with a short silence timeout against a fake available
capture device and a fake session that raises one final result. The session is bound to the
capture device (not a WAV file device) and started once, the result is printed to the console,
the run ends on the idle timeout without operator interruption, and the exit code is `0`. The
run uses the real clock with a 0.2-second window, so it also confirms that the decorator and the
command cooperate end to end.

#### File Recognition Against the Built Tool

**Tests**: `SpeechCli_RecognizeCommandWithRealSttModel_Invoked_ProducesNonEmptyRecognizedText`,
`RecognizeCommand_ParseArguments_InputFlag_ParsesInputPath`,
`RecognizeCommand_Run_Success_DisposesEngineAndSessionOnce`

**Scenario/Expected**: `--input` parses to a file source, and a successful file run disposes the
engine and session once each. The out-of-process integration test runs `recognize --input` over a
real WAV fixture against a downloaded recognition model and expects exit code 0, the closing
"Recognition finished." line, and at least one non-blank recognized line.

That integration test skips dynamically: it calls `Assert.Skip` when no downloaded speech-to-text
model exists in the per-user model store. A run without such a model therefore reports the
scenario as skipped, not as passed, and the scenario is then verified manually as described below.
The fixture is located at run time by walking up from the test assembly's directory to the
repository root (identified by `Speech.slnx`), so no binary fixture is duplicated.

### Acceptance Criteria

A RecognitionCommandSubsystem test run passes when the microphone run above succeeds, the file-input
parsing and disposal tests pass, every unit's own acceptance criteria are met, and the
integration test either passes against a real model or is reported as skipped for lack of a
downloaded model. A skip means that scenario is unverified in that environment and must be
covered by the manual verification below.

### Manual / Build-Time Verification

Real-microphone behavior and real-model recognition cannot be verified in a build environment
with no audio hardware or downloaded model. When the integration test is skipped, or whenever
microphone behavior needs confirming, perform these checks on a developer machine:

- `dotnet run --project src/DemaConsulting.Speech.Cli -- recognize --stt-model <id> --input
  test/DemaConsulting.Speech.Tests/TestData/crossing-the-bar-16k-mono.wav`, against a downloaded
  recognition model, prints non-empty recognized text and exits cleanly.
- `recognize --stt-model <id> --mic --silence-timeout <seconds>`, on a machine with a real
  microphone, prints live interim and final results while speaking and ends the session
  automatically after the configured idle window once speech stops.
- `recognize --stt-model <id> --mic --silence-timeout <seconds> --start-timeout <seconds>`, on a
  machine with a real microphone, stays listening past the shorter `--silence-timeout` while no
  speech has begun, up to `--start-timeout`, and, once speech has started, ends the session after
  `--silence-timeout` with no further result.
- `recognize --stt-model <id> --mic --capture-device <name>`, with the name of a listed capture
  device, listens on that device. No automated test proves device selection by a valid name; the
  automated tests cover only the rejection of an unknown name and the no-device case.
