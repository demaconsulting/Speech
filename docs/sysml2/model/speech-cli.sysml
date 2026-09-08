package SpeechCli {
    doc
    /*
     * SpeechCli is the .NET global tool (`speech-cli`) that exposes the Speech library's
     * model management, audio device inspection, text-to-speech, and speech-to-text
     * capabilities from the command line. Pass 2 modeled only the scaffold: process
     * entry point, global-option and subcommand dispatch, and self-validation, with all
     * ten recognized subcommands dispatching to a stub. Pass 3 added the
     * ModelCommandsSubsystem, replacing five of those stubs (list-models, model-info,
     * download, uninstall, clean) with real behavior. Pass 4 added the
     * DeviceCommandsSubsystem, replacing three more stubs (list-devices, devices test,
     * doctor) with real behavior. Pass 5 added the SynthesisCommandSubsystem, replacing
     * the speak stub with real behavior, and extended ModelCommandsSubsystem's
     * ICliModelCatalog seam with two further members (GetPreferredAudioFormat,
     * CreateSynthesizer). Pass 6 adds the RecognitionCommandSubsystem, replacing the
     * last remaining stub (recognize) with real behavior, and extends
     * ModelCommandsSubsystem's ICliModelCatalog seam with two further members
     * (GetAudioFormat, CreateRecognizer) symmetric to the synthesis pair - all ten
     * subcommands are now implemented.
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
    }
}
