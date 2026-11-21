package SpeechCli_CommandDispatch {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_AskCommand::*;
    private import SpeechCli_CleanCommand::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_DevicesTestCommand::*;
    private import SpeechCli_DoctorCommand::*;
    private import SpeechCli_DownloadCommand::*;
    private import SpeechCli_ListDevicesCommand::*;
    private import SpeechCli_ListModelsCommand::*;
    private import SpeechCli_ListVoicesCommand::*;
    private import SpeechCli_ModelInfoCommand::*;
    private import SpeechCli_RecognizeCommand::*;
    private import SpeechCli_SpeakCommand::*;
    private import SpeechCli_UninstallCommand::*;
    private import SpeechCli_VoicesCommand::*;

    part def CommandDispatch {
        doc /* Supporting record describing one subcommand in the CLI dispatch table: its canonical kebab-case name,
             * its one-line --help flag summary and the asynchronous handler (Func<Context, Task>) that runs it. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Cli/CommandDispatch.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Cli/CommandDispatchTests.cs", "test/DemaConsulting.Speech.Cli.Tests/ProgramTests.cs");
            designFiles = ("docs/design/speech-cli/command-dispatch.md");
            verificationFiles = ("docs/verification/speech-cli/command-dispatch.md");
            requirementFiles = ("docs/reqstream/speech-cli/command-dispatch.yaml");
        }

        // Binds each subcommand name and help summary to its command handler, which runs against the
        // shared Context.
        dependency CommandDispatch to AskCommand;
        dependency CommandDispatch to CleanCommand;
        dependency CommandDispatch to Context;
        dependency CommandDispatch to DevicesTestCommand;
        dependency CommandDispatch to DoctorCommand;
        dependency CommandDispatch to DownloadCommand;
        dependency CommandDispatch to ListDevicesCommand;
        dependency CommandDispatch to ListModelsCommand;
        dependency CommandDispatch to ListVoicesCommand;
        dependency CommandDispatch to ModelInfoCommand;
        dependency CommandDispatch to RecognizeCommand;
        dependency CommandDispatch to SpeakCommand;
        dependency CommandDispatch to UninstallCommand;
        dependency CommandDispatch to VoicesCommand;
    }
}
