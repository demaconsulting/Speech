## SpeechCli ConversationCommandSubsystem Design

![ConversationCommandSubsystem Structure](ConversationCommandSubsystemView.svg)

### Overview

The ConversationCommandSubsystem implements the one voice-conversation subcommand dispatched to by
`CommandDispatch`: `ask`, which speaks a prompt and then listens for the reply. It contains the following units,
each documented in its own file:

- **AskCommand**: parses `ask`'s flags, resolves the text source and both models, speaks the prompt, listens for the
  reply, and delivers the recognized text - see _AskCommand Design_
- **ICliCaptureDeviceSource**: a CLI-owned seam over capture-device resolution, mirroring the playback-side
  `ICliPlaybackDeviceSource` of the SynthesisCommandSubsystem - see _ICliCaptureDeviceSource Design_
- **AudioDeviceFactoryCaptureDeviceSource**: the production implementation of that seam, forwarding every call to
  the real `AudioDeviceFactory` - see _AudioDeviceFactoryCaptureDeviceSource Design_

The subsystem combines the synthesis and recognition capabilities of two sibling subsystems into one turn, built
entirely on the asynchronous engine and session API. It owns no catalog member of its own: `ask` calls the
synthesizer-engine member that the SynthesisCommandSubsystem uses and the recognizer-engine member that the
RecognitionCommandSubsystem uses, each already part of the `ICliModelCatalog` seam of the ModelCommandsSubsystem.

### Interfaces

**Exposed**: `AskCommand.RunAsync(Context)` is the `CommandDispatch` entry point. The `ICliCaptureDeviceSource`
seam, with its production `AudioDeviceFactoryCaptureDeviceSource`, is exposed to the commands that listen through a
capture device, which are `AskCommand` and the RecognitionCommandSubsystem's `RecognizeCommand`. Operator mistakes
are reported as `ArgumentException` or `InvalidOperationException`.

**Consumed**: `ICliModelCatalog` (model enumeration and the synthesis and recognition engine members); the
library's synthesis, recognition, and audio device types; `ICliPlaybackDeviceSource` and `ParameterBagParser` from
the SynthesisCommandSubsystem; `SilenceTimeoutRecognizerSession` from the RecognitionCommandSubsystem; and the
device-selection resolution of `DevicesTestCommand` from the DeviceCommandsSubsystem.

### Design

A turn has two strictly sequential phases. In the first, the prompt is spoken through a playback device and the
command waits for playback to finish. In the second, the reply is listened for through a capture device until the
first final result arrives, a timeout fires, or the operator presses `Ctrl+C`. Listening never begins while the
prompt is still playing.

Cross-unit data flow:

1. `AskCommand` validates its arguments, resolves the prompt text, and resolves and validates both models.
2. `AskCommand` asks `ICliCaptureDeviceSource` for the capture device and creates the recognizer on a background
   task while the prompt plays, so the recognizer is ready the moment playback ends.
3. `AskCommand` resolves the playback device through the SynthesisCommandSubsystem's `ICliPlaybackDeviceSource`,
   speaks the prompt, and, once it finishes, listens through the already-created recognizer wrapped in the
   RecognitionCommandSubsystem's `SilenceTimeoutRecognizerSession`.
4. The recognized text is printed or written to the `--output-text` file, and everything created along the way is
   disposed.

Design constraints:

- **Capture-device seam.** `AskCommand` obtains its capture device only through `ICliCaptureDeviceSource`, never
  directly from the real factory. The real factory resolves to its unavailable fallback on any machine without
  audio hardware whatever probe a test injects, so only a substitutable seam lets the whole turn run
  deterministically in tests. The production adapter is a pure pass-through, so real resolution is unchanged.
- **One synthesis-role model and one recognition-role model.** A single model never serves both roles, so no
  combined catalog member exists; the two existing members are called once each.
- **Sibling units are reused, not duplicated.** Parameter validation, timeout handling, playback-device resolution,
  and device-selection errors are the sibling subsystems' units, used as they are.

### Interactions with Other Units

`AskCommand` depends on the `ICliModelCatalog` seam, the library's public synthesis, recognition, and audio types,
the SynthesisCommandSubsystem's `ICliPlaybackDeviceSource` and `ParameterBagParser`, the
RecognitionCommandSubsystem's `SilenceTimeoutRecognizerSession`, the DeviceCommandsSubsystem's device-selection
resolution, and `ICliCaptureDeviceSource`. The subsystem owns `ICliCaptureDeviceSource` and its production
implementation `AudioDeviceFactoryCaptureDeviceSource`, the capture-side pair of the SynthesisCommandSubsystem's
playback-device pair. `CommandDispatch` calls `AskCommand`, and the RecognitionCommandSubsystem's `RecognizeCommand`
also resolves its capture device through `ICliCaptureDeviceSource`. Neither seam unit adds a library-level API.
