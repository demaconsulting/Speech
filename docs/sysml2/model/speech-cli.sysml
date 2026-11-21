package SpeechCli {
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;
    private import SpeechCli_CommandDispatch::*;
    private import SpeechCli_ConsoleCancellation::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_ConversationCommandSubsystem::*;
    private import SpeechCli_DeviceCommandsSubsystem::*;
    private import SpeechCli_ModelCommandsSubsystem::*;
    private import SpeechCli_PathHelpers::*;
    private import SpeechCli_Program::*;
    private import SpeechCli_RecognitionCommandSubsystem::*;
    private import SpeechCli_SynthesisCommandSubsystem::*;
    private import SpeechCli_Validation::*;

    doc
    /*
     * SpeechCli is the .NET global tool (`speech-cli`) that exposes the Speech library's model management, audio
     * device inspection, text-to-speech, and speech-to-text capabilities from the command line. It comprises five
     * command subsystems that implement all thirteen subcommands: ModelCommandsSubsystem (list-models, model-info,
     * download, uninstall, clean, list-voices, voices, and the shared ICliModelCatalog seam), DeviceCommandsSubsystem
     * (list-devices, devices test, doctor), SynthesisCommandSubsystem (speak), RecognitionCommandSubsystem
     * (recognize), and ConversationCommandSubsystem (ask), which speaks a prompt and then listens for the reply using
     * the existing synthesis and recognition seam members with no new seam member of its own.
     */

    part def SpeechCliSystem {
        doc /* The SpeechCli application as a whole. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/ProgramTests.cs", "test/DemaConsulting.Speech.Cli.Tests/Cli/ContextTests.cs", "test/DemaConsulting.Speech.Cli.Tests/Cli/CommandDispatchTests.cs", "test/DemaConsulting.Speech.Cli.Tests/IntegrationTests.cs");
            designFiles = ("docs/design/speech-cli.md");
            verificationFiles = ("docs/verification/speech-cli.md");
            requirementFiles = ("docs/reqstream/speech-cli.yaml");
        }

        part modelCommandsSubsystem : ModelCommandsSubsystem;
        part deviceCommandsSubsystem : DeviceCommandsSubsystem;
        part synthesisCommandSubsystem : SynthesisCommandSubsystem;
        part recognitionCommandSubsystem : RecognitionCommandSubsystem;
        part conversationCommandSubsystem : ConversationCommandSubsystem;
        part program : Program;
        part commandDispatch : CommandDispatch;
        part context : Context;
        part consoleCancellation : ConsoleCancellation;
        part validation : Validation;
        part pathHelpers : PathHelpers;
        part testResults : TestResults;
    }
}
