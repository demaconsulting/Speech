package SpeechCli {
    part def RecognitionCommandSubsystem {
        doc /* The one speech-to-text subcommand (recognize) plus the
             * SilenceTimeoutRecognizerSession idle-timeout decorator, extending the
             * ModelCommandsSubsystem's ICliModelCatalog seam with a further member
             * (CreateRecognizerEngineAsync) rather than introducing a
             * second, competing seam. Consumes the library's async Engine/Session
             * RecognitionSubsystem surface (ISpeechRecognizerEngine/IRecognitionSession)
             * rather than the prior synchronous, event-based ISpeechRecognizer. */

        comment designRef /* Design: docs/design/speech-cli/recognition-command-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/recognition-command-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/recognition-command-subsystem.yaml */

        part recognizeCommand : RecognizeCommand;
        part silenceTimeoutRecognizerSession : SilenceTimeoutRecognizerSession;
    }
}
