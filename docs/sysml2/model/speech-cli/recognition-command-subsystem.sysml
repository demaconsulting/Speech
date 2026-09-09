package SpeechCli {
    part def RecognitionCommandSubsystem {
        doc /* The one speech-to-text subcommand (recognize) - the last of all 10
             * subcommands to be implemented - plus the SilenceTimeoutRecognizerSession
             * idle-timeout utility, extending the ModelCommandsSubsystem's
             * ICliModelCatalog seam with two further members (GetAudioFormat,
             * CreateRecognizer) rather than introducing a second, competing seam. */

        comment designRef /* Design: docs/design/speech-cli/recognition-command-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/recognition-command-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/recognition-command-subsystem.yaml */

        part recognizeCommand : RecognizeCommand;
        part silenceTimeoutRecognizerSession : SilenceTimeoutRecognizerSession;
    }

    part def RecognizeCommand {
        doc /* Implements recognize: resolves exactly one of --input/--mic, resolves and
             * validates the requested recognition model, validates --param values via
             * the reused ParameterBagParser, drives file-input mode to completion via
             * the capture device's own EndOfFileReached event (reentrant Stop()), wires
             * mic-input mode to Ctrl+C and an optional SilenceTimeoutRecognizerSession,
             * filters console output per --interim/--final-only, writes only final
             * results to --output, and disposes the recognizer (and silence-timeout
             * session, when present) on every exit path. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/RecognitionCommandSubsystem/RecognizeCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/RecognitionCommandSubsystem/RecognizeCommandTests.cs */
    }

    part def SilenceTimeoutRecognizerSession {
        doc /* Observes an ISpeechRecognizer's ResultReceived event and calls Stop(),
             * then raises TimedOut, when no result (partial or final) arrives within a
             * configured idle window. Enforces two sequential idle windows via a
             * TimeProvider-based timer: a start-timeout grace period (defaulting to the
             * silence-timeout value) armed once at construction, before any result has
             * arrived, then re-armed with the silence-timeout value on every subsequent
             * result from the first onward - deterministic and real-wall-clock-free for
             * unit testing. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/RecognitionCommandSubsystem/SilenceTimeoutRecognizerSession.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/RecognitionCommandSubsystem/SilenceTimeoutRecognizerSessionTests.cs */
    }
}
