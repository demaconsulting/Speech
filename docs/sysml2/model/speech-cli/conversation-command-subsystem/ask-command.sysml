package SpeechCli {
    part def AskCommand {
        doc /* Implements ask: resolves prompt text from --text/--file/piped stdin,
             * resolves and validates both a synthesis-role --tts-model and a
             * recognition-role --stt-model, validates --tts-param/--stt-param values
             * via the reused ParameterBagParser, speaks the resolved text through a
             * real playback device via an ISpeechSynthesizerEngine/ISynthesisSession,
             * then - once playback finishes - listens through a capture device resolved
             * via ICliCaptureDeviceSource using an ISpeechRecognizerEngine/
             * IRecognitionSession wrapped in a reused SilenceTimeoutRecognizerSession,
             * until the first final recognition result arrives, a start or silence
             * timeout elapses, or Ctrl+C is pressed. The recognizer engine and session
             * are created while the prompt plays, so the recognizer is ready when
             * playback ends; microphone capture starts only in the listen phase. If the
             * prompt is canceled or fails, the recognizer is disposed in the background
             * without delaying the caller. A Ctrl+C during the listen phase is
             * distinguished from a timeout by the shared cancellation token and is
             * reported as a cancellation, printing or writing no text. Otherwise the
             * final recognized text is printed (or written to --output-text), and every
             * engine, session, and device is disposed on every exit path. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ConversationCommandSubsystem/AskCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ConversationCommandSubsystem/AskCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/conversation-command-subsystem/ask-command.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/conversation-command-subsystem/ask-command.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/conversation-command-subsystem/ask-command.yaml */
    }
}
