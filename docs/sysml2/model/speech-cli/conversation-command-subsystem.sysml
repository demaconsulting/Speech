package SpeechCli {
    part def ConversationCommandSubsystem {
        doc /* The one voice-conversation subcommand (ask) - an eleventh subcommand added
             * after all ten scaffolded subcommands were implemented - combining speak's
             * synthesis-then-play flow and recognize --mic's mic-listen flow into a
             * single invocation. Introduces no new ICliModelCatalog seam member: it
             * reuses SynthesisCommandSubsystem's CreateSynthesizer and
             * RecognitionCommandSubsystem's CreateRecognizer members exactly as speak
             * and recognize already do individually. */

        comment designRef /* Design: docs/design/speech-cli/conversation-command-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/conversation-command-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/conversation-command-subsystem.yaml */

        part askCommand : AskCommand;
    }

    part def AskCommand {
        doc /* Implements ask: resolves prompt text from --text/--file/piped stdin,
             * resolves and validates both a synthesis-role --tts-model and a
             * recognition-role --stt-model, validates --tts-param/--stt-param values
             * via the reused ParameterBagParser, speaks the resolved text through a
             * real playback device (Phase 1), then - once playback finishes - listens
             * through a real capture device (Phase 2) until the first final
             * recognition result arrives, a reused SilenceTimeoutRecognizerSession
             * times out, or Ctrl+C is pressed, printing (or writing to --output-text)
             * the final recognized text, and disposing the synthesizer, playback
             * device, recognizer, and silence-timeout session (when present) on every
             * exit path. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ConversationCommandSubsystem/AskCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ConversationCommandSubsystem/AskCommandTests.cs */
    }
}
