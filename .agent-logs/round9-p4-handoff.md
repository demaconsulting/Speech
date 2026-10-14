# Round 9 P4 Handoff to P9

P4 did not edit `docs/reqstream/speech-cli.yaml` (system), `speech*.yaml`, `requirements.yaml`,
`.reviewmark.yaml` or `docs/sysml2`. P9 should apply the following.

## New requirement IDs to add to `children` lists

Add each to the parent requirement(s) that the sibling ID decomposes, in the system-level
`docs/reqstream/speech-cli.yaml` (and the subsystem yaml `children` where listed).

Synthesis (unit yaml: `docs/reqstream/speech-cli/synthesis-command-subsystem/`):

- `SpeechCli-SynthesisCommands-ValueFlagOptionRejection` (speak-command.yaml)
- `SpeechCli-SynthesisCommands-TextFileUnreadable` (speak-command.yaml)
- `SpeechCli-SynthesisCommands-PlaybackDeviceCreation` (audio-device-factory-playback-device-source.yaml)
- `SpeechCli-SynthesisCommands-PlaybackDeviceUnavailableRejection` (i-cli-playback-device-source.yaml)

Recognition:

- `SpeechCli-RecognitionCommands-RecognizeMicrophoneIdleEnd` (subsystem yaml
  `recognition-command-subsystem.yaml`; split sibling of `...RecognizeMicrophone`, which keeps its
  ID and now covers only streaming from the microphone) - add alongside
  `SpeechCli-RecognitionCommands-RecognizeMicrophone` wherever that is a child
  (e.g. SpeechToTextMicrophone).
- `SpeechCli-RecognitionCommands-ValueFlagOptionRejection` (recognize-command.yaml)
- `SpeechCli-RecognitionCommands-FileInputCancellation` (recognize-command.yaml)
- `SpeechCli-RecognitionCommands-SessionTimeoutDrain` (silence-timeout-recognizer-session.yaml)

## Reworded (ID stable)

- `SpeechCli-RecognitionCommands-SessionDefaultClock`: now real-time default behavior.
- `...PlaybackDeviceSelection`, `...PlaybackDeviceSourceContract`: reworded to observable behavior.
- `...ParamNumberFormatRejection`, `...SilenceTimeoutValidation`, `...StartTimeoutValidation`:
  now also reject non-finite values.

## Visibility note

`ParameterBagParser.Resolve`/`ParseToken` are C# `internal`; design docs state this consistently.
Any sysml2 model of the Synthesis subsystem interfaces should show them as internal.
