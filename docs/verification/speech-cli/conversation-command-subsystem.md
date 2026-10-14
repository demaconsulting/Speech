## SpeechCli ConversationCommandSubsystem Verification

### Verification Approach

The ConversationCommandSubsystem is verified at subsystem level by running a complete speak-then-listen turn
through `AskCommand` over the capture-device seam, using in-memory test doubles for the catalog, the synthesis and
recognition engines, and both device sources. The turn needs no real model catalog, network access, or audio
hardware, so it is deterministic on any machine, including a headless build agent that has no PortAudio hardware.
The production seam implementation is verified against a real `AudioDeviceFactory` composed over a fake probe, so
its unavailable-device results are deterministic on any machine.

Unit-level behavior is verified in the unit verification documents for `AskCommand`, `ICliCaptureDeviceSource`,
and `AudioDeviceFactoryCaptureDeviceSource`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every test builds its own fakes; no shared state exists between tests
- **Test doubles**: The in-memory catalog, engine, session, and device-source doubles described in _AskCommand
  Verification_

### Acceptance Criteria

The subsystem passes when a complete turn runs end to end through a substituted capture-device source, and the
default capture device is reported as unavailable, without a crash, when no capture devices exist.

### Test Scenarios

#### Capture-Device Seam Composition

**Tests**: `AskCommand_Run_Success_SpeaksThenListensAndPrintsFinalResult`

**Scenario/Expected**: With the capture device supplied by a test double, `ask` speaks the prompt, listens, and
prints the final reply, and every object created along the way is disposed.

#### Default Capture Device Unavailable

**Tests**: `AskCommand_Run_NoCaptureDeviceAvailable_ThrowsInvalidOperationException`,
`AudioDeviceFactoryCaptureDeviceSource_CreateCaptureDevice_DefaultWithNoDevices_ReturnsUnavailableDevice`

**Scenario/Expected**: With no capture devices present, the production source returns an unavailable device for the
default selection without throwing, and `ask` reports the unavailable device with a clean error after speaking the
prompt.

## Manual / Build-Time Verification

The automated tests cover `ask` without a downloaded model pair or real audio hardware. A genuine two-model,
real-hardware run is verified manually on a machine with a downloaded synthesis model, a downloaded recognition
model, a microphone, and speakers, as for `recognize --mic` in the RecognitionCommandSubsystem:

- `dotnet run --project src/DemaConsulting.Speech.Cli -- ask --tts-model <tts-id> --stt-model <stt-id> --text "What
  is your name?" --start-timeout 20 --silence-timeout 1.5` speaks the prompt aloud, then listens for a spoken reply,
  waiting up to the start timeout for it to begin and ending capture the silence timeout after it pauses, and prints
  the recognized reply once the turn ends
- The same invocation with `--output-text <path>` writes the recognized reply to the named file instead of stdout
- Pressing `Ctrl+C` during either the prompt or the listening ends the command cleanly with a non-zero exit code
  and no crash
