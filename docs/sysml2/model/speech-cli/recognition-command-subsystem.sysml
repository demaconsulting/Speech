package SpeechCli_RecognitionCommandSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_RecognizeCommand::*;
    private import SpeechCli_SilenceTimeoutRecognizerSession::*;

    part def RecognitionCommandSubsystem {
        doc /* The one speech-to-text subcommand (recognize) plus the SilenceTimeoutRecognizerSession idle-timeout
             * decorator, extending the ModelCommandsSubsystem's ICliModelCatalog seam with a further member
             * (CreateRecognizerEngineAsync) rather than introducing a second, competing seam. Consumes the library's
             * async Engine/Session RecognitionSubsystem surface (ISpeechRecognizerEngine/IRecognitionSession) rather
             * than the prior synchronous, event-based ISpeechRecognizer. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-cli/recognition-command-subsystem.md");
            verificationFiles = ("docs/verification/speech-cli/recognition-command-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-cli/recognition-command-subsystem.yaml");
        }

        part recognizeCommand : RecognizeCommand;
        part silenceTimeoutRecognizerSession : SilenceTimeoutRecognizerSession;
    }
}
