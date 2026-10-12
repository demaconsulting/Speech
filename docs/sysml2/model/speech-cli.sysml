package SpeechCli {
    doc
    /*
     * SpeechCli is the .NET global tool (`speech-cli`) that exposes the Speech library's
     * model management, audio device inspection, text-to-speech, and speech-to-text
     * capabilities from the command line. It comprises five command subsystems that
     * implement all thirteen subcommands: ModelCommandsSubsystem (list-models,
     * model-info, download, uninstall, clean, list-voices, voices, and the shared
     * ICliModelCatalog seam), DeviceCommandsSubsystem (list-devices, devices test,
     * doctor), SynthesisCommandSubsystem (speak), RecognitionCommandSubsystem
     * (recognize), and ConversationCommandSubsystem (ask), which speaks a prompt and
     * then listens for the reply using the existing synthesis and recognition seam
     * members with no new seam member of its own.
     */

    part def SpeechCliSystem {
        doc /* The SpeechCli application as a whole. */

        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/ProgramTests.cs, test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs */
        comment designRef /* Design: docs/design/speech-cli.md */
        comment verificationRef /* Verification: docs/verification/speech-cli.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli.yaml */

        part modelCommandsSubsystem : ModelCommandsSubsystem;
        part deviceCommandsSubsystem : DeviceCommandsSubsystem;
        part synthesisCommandSubsystem : SynthesisCommandSubsystem;
        part recognitionCommandSubsystem : RecognitionCommandSubsystem;
        part conversationCommandSubsystem : ConversationCommandSubsystem;
        part commandDispatch : CommandDispatch;
        part pathHelpers : PathHelpers;
    }
}
